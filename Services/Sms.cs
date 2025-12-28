using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace YourProject.Services
{
    /// <summary>
    /// Super simple, fast, cheap SMS service for Philippines using Semaphore
    /// Price: ~₱0.80 per SMS | 50 free credits on signup
    /// Sign up: https://semaphore.co → Get API Key in 1 minute
    /// </summary>
    public sealed class Sms
    {
        private readonly string _apiKey;
        private readonly string _senderName;  // Your approved sender ID (max 11 chars)
        private static readonly HttpClient _client = new HttpClient();

        public Sms(string apiKey, string senderName = "MyApp")
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _senderName = senderName.Length > 11 ? senderName.Substring(0, 11) : senderName;
        }

        /// <summary>
        /// Send SMS to Philippine number
        /// </summary>
        /// <param name="number">09xxxxxxxxx or 639xxxxxxxxx</param>
        /// <param name="message">Max 160 chars (or 70 for Unicode)</param>
        public async Task<SmsResult> SendAsync(string number, string message)
        {
            if (string.IsNullOrWhiteSpace(number) || string.IsNullOrWhiteSpace(message))
                return new SmsResult(false, "Number or message empty");

            // Auto-fix number format
            var cleanNumber = number.TrimStart('+').Replace(" ", "");
            if (cleanNumber.StartsWith("09") && cleanNumber.Length == 11)
                cleanNumber = "63" + cleanNumber.Substring(1);  // 09xx → 63xxx
            else if (cleanNumber.StartsWith("9") && cleanNumber.Length == 10)
                cleanNumber = "63" + cleanNumber;

            if (!cleanNumber.StartsWith("63") || cleanNumber.Length != 12)
                return new SmsResult(false, "Invalid PH mobile number");

            var url = "https://api.semaphore.co/api/v4/messages";
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("apikey", _apiKey),
                new KeyValuePair<string, string>("number", cleanNumber),
                new KeyValuePair<string, string>("message", message),
                new KeyValuePair<string, string>("sendername", _senderName)
            });

            try
            {
                var response = await _client.PostAsync(url, form);
                var json = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode && json.Contains("\"status\":\"success\""))
                    return new SmsResult(true, "SMS sent successfully!");

                return new SmsResult(false, json.Length > 200 ? "Failed to send SMS" : json);
            }
            catch (Exception ex)
            {
                return new SmsResult(false, ex.Message);
            }
        }
    }

    public class SmsResult
    {
        public bool Success { get; }
        public string Message { get; }
        public SmsResult(bool success, string message) { Success = success; Message = message; }
        public override string ToString() => Success ? "SMS Sent!" : $"SMS Failed: {Message}";
    }
}