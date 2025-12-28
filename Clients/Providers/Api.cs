using Newtonsoft.Json;
using Next.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Next.Clients.Providers
{
    public sealed class Api : IDisposable
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private readonly Action<string> _logger;
        private readonly Random _random = new();
        private readonly CancellationTokenSource _cts = new();

        public string Name { get; }
        public bool InternetRequired { get; set; } = true;
        public TimeSpan InternetCheckTimeout { get; set; } = TimeSpan.FromSeconds(5);
        public string InternetTestHost { get; set; } = "https://www.google.com";

        public Api(
            string baseUrl,
            string name = "API",
            string apiKey = null,
            string bearerToken = null,
            string basicAuthUser = null,
            string basicAuthPass = null,
            Dictionary<string, string> defaultHeaders = null,
            TimeSpan? timeout = null,
            Action<string> logger = null,
            int maxRetries = 5)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "API" : name.Trim();
            _baseUrl = (baseUrl ?? "").TrimEnd('/');
            _logger = logger ?? (msg => Debug.WriteLine(msg));

            var handler = new HttpClientHandler();
            if (handler.SupportsAutomaticDecompression)
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            _http = new HttpClient(handler)
            {
                Timeout = timeout ?? TimeSpan.FromSeconds(30)
            };

            _http.DefaultRequestHeaders.UserAgent.ParseAdd("NextClient/2.0 (+https://yoursite.com)");

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                _http.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", apiKey);
                _http.DefaultRequestHeaders.TryAddWithoutValidation("ApiKey", apiKey);
            }

            if (!string.IsNullOrWhiteSpace(bearerToken))
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

            if (!string.IsNullOrWhiteSpace(basicAuthUser))
            {
                var cred = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{basicAuthUser}:{basicAuthPass ?? ""}"));
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", cred);
            }

            if (defaultHeaders != null)
                foreach (var h in defaultHeaders)
                    _http.DefaultRequestHeaders.TryAddWithoutValidation(h.Key, h.Value);

            Logs("Info", "Initialized", $"BaseUrl={_baseUrl} Retries={maxRetries} Timeout={_http.Timeout}");
        }

        public async Task<IResponse> RequestAsync(
            string endpoint,
            string method,
            object jsonBody = null,
            Dictionary<string, string> headers = null,
            Dictionary<string, string> query = null,
            int maxRetries = 5)
        {
            if (InternetRequired && !await CheckInternetAsync())
            {
                var msg = $"No internet connectivity detected for host {InternetTestHost}";
                Logs("Error", "Internet", msg);
                return ApiResponses.Error(503, msg);
            }

            var url = BuildUrl(endpoint, query);
            Exception lastError = null;

            for (int attempt = 1; attempt <= maxRetries + 1; attempt++)
            {
                using var req = new HttpRequestMessage(new HttpMethod(method), url);
                var sw = Stopwatch.StartNew();

                try
                {
                    if (headers != null)
                        foreach (var h in headers)
                            req.Headers.TryAddWithoutValidation(h.Key, h.Value);

                    if (jsonBody != null && (method == "POST" || method == "PUT" || method == "PATCH"))
                    {
                        var json = JsonConvert.SerializeObject(jsonBody);
                        req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    }

                    var response = await _http.SendAsync(req, _cts.Token).ConfigureAwait(false);
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    sw.Stop();

                    if (response.IsSuccessStatusCode)
                    {
                        Logs("Info", $"{method} {endpoint}",
                            $"Success • {(int)response.StatusCode} • {sw.ElapsedMilliseconds}ms • {body.Length} chars");
                        return ApiResponses.Ok((int)response.StatusCode, body);
                    }

                    if (attempt <= maxRetries && IsTransient(response))
                    {
                        var delay = Backoff(attempt - 1);
                        Logs("Retry", $"{method} {endpoint}",
                            $"Transient • {(int)response.StatusCode} {response.ReasonPhrase} • attempt {attempt}/{maxRetries + 1}");
                        await Task.Delay(delay, _cts.Token).ConfigureAwait(false);
                        continue;
                    }

                    Logs("Fail", $"{method} {endpoint}",
                        $"Failed • {(int)response.StatusCode} {response.ReasonPhrase} • {sw.ElapsedMilliseconds}ms");

                    return ApiResponses.Error(
                        statusCode: (int)response.StatusCode,
                        errorMessage: $"{(int)response.StatusCode} {response.ReasonPhrase}",
                        body: body);
                }
                catch (Exception ex) when (attempt <= maxRetries && IsTransient(ex))
                {
                    lastError = ex;
                    var delay = Backoff(attempt - 1);
                    Logs("Retry", $"{method} {endpoint}",
                        $"Network error • {ex.GetType().Name}: {ex.Message} • retry {attempt}/{maxRetries + 1}");
                    await Task.Delay(delay, _cts.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logs("Error", $"{method} {endpoint}", $"Fatal • {ex.GetType().Name}: {ex.Message}");
                    throw;
                }
            }

            var failMsg = $"Request failed after {maxRetries + 1} attempts • {lastError?.Message}";
            Logs("Error", $"{method} {endpoint}", failMsg);
            return ApiResponses.Error(503, failMsg);
        }

        public async Task<IResponse<T>> GetAsync<T>(string endpoint, Dictionary<string, string> query = null, int retries = 5)
            => await RequestAndDeserialize<T>(endpoint, "GET", null, null, query, retries);

        public async Task<IResponse<T>> PostAsync<T>(string endpoint, object body, int retries = 5)
            => await RequestAndDeserialize<T>(endpoint, "POST", body, null, null, retries);

        public async Task<IResponse<T>> PutAsync<T>(string endpoint, object body, int retries = 5)
            => await RequestAndDeserialize<T>(endpoint, "PUT", body, null, null, retries);

        public async Task<IResponse<T>> PatchAsync<T>(string endpoint, object body, int retries = 5)
            => await RequestAndDeserialize<T>(endpoint, "PATCH", body, null, null, retries);

        public Task<IResponse> DeleteAsync(string endpoint, int retries = 5)
            => RequestAsync(endpoint, "DELETE", null, null, null, retries);

        private async Task<IResponse<T>> RequestAndDeserialize<T>(
            string endpoint,
            string method,
            object body = null,
            Dictionary<string, string> headers = null,
            Dictionary<string, string> query = null,
            int retries = 5)
        {
            var raw = await RequestAsync(endpoint, method, body, headers, query, retries);

            if (!raw.Success)
            {
                return ApiResponses.Error<T>(raw.ErrorMessage, raw.StatusCode, raw.Body);
            }

            try
            {
                var data = JsonConvert.DeserializeObject<T>(raw.Body ?? string.Empty);
                return ApiResponses.Ok(data, raw.StatusCode, raw.Body);
            }
            catch (Exception ex)
            {
                Logs("Error", "Deserialize", $"JSON parse failed • {ex.Message}");
                return ApiResponses.Error<T>($"JSON deserialization failed: {ex.Message}", 500, raw.Body);
            }
        }

        public async Task<bool> CheckInternetAsync()
        {
            // unchanged - your original implementation
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(new Uri(InternetTestHost).Host, (int)InternetCheckTimeout.TotalMilliseconds);
                if (reply.Status == IPStatus.Success) return true;
            }
            catch { }

            try
            {
                using var client = new HttpClient { Timeout = InternetCheckTimeout };
                using var resp = await client.GetAsync(InternetTestHost);
                return resp.IsSuccessStatusCode;
            }
            catch { }

            return false;
        }

        private bool IsTransient(HttpResponseMessage resp) => IsTransientStatus(resp.StatusCode);

        private static bool IsTransientStatus(HttpStatusCode code)
        {
            int c = (int)code;
            return c == 408 || c == 429 || c >= 500;
        }

        private bool IsTransient(Exception ex) =>
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is TimeoutException;

        private TimeSpan Backoff(int attempt)
        {
            var delay = TimeSpan.FromMilliseconds(250 * Math.Pow(2, attempt));
            var jitter = _random.NextDouble() * delay.TotalMilliseconds;
            return TimeSpan.FromMilliseconds(jitter);
        }

        private string BuildUrl(string endpoint, Dictionary<string, string> query)
        {
            var url = _baseUrl + endpoint;
            if (query == null || query.Count == 0) return url;

            var qs = string.Join("&", query.Select(kvp =>
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value ?? "")}"));
            return url + (url.Contains('?') ? "&" : "?") + qs;
        }

        private void Logs(string level, string operation, string message)
        {
            var line = $"[API:{Name}] [{level,-5}] {operation,-25} → {message}";
            _logger(line);
        }

        public void Dispose()
        {
            _cts.Cancel();
            _http?.Dispose();
            Logs("Info", "Disposed", "Client shut down");
        }
    }
}