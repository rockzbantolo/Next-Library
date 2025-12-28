using Dapper;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Next.Clients.Core
{
    public abstract class Base : IDatabase
    {
        // Core properties
        protected readonly DbProviderFactory Factory;
        protected readonly string ConnectionString;
        public readonly Action<string> Logger;

        private const int MaxRetries = 5;
        private static readonly TimeSpan BaseDelay = TimeSpan.FromMilliseconds(250);
        private static readonly ThreadLocal<Random> Rng = new(() =>
            new Random(unchecked((int)(Environment.TickCount ^ Thread.CurrentThread.ManagedThreadId))));

        private readonly CancellationTokenSource _cts = new();
        private Task _healthTask;
        private volatile bool _isHealthy = true;
        private DateTime _lastSuccessUtc = DateTime.UtcNow;

        public string Name { get; }
        public bool IsHealthy => _isHealthy && (DateTime.UtcNow - _lastSuccessUtc) < TimeSpan.FromMinutes(10);
        public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Initializes the base client with provider, connection string, and logger.
        /// </summary>
        protected Base(DbProviderFactory factory, string connectionString, string name, Action<string> logger)
        {
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
            ConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            Name = string.IsNullOrWhiteSpace(name) ? "DB" : name.Trim();
            Logger = logger ?? (msg => Debug.WriteLine(msg));

            // Log initialization
            Logs.Write(Logger, "Initialized", Name, $"Created {GetType().Name}");

            // Start background health monitor
            _healthTask = Task.Run(HealthMonitorAsync);
        }

        /// <summary>
        /// Creates a new connection instance from the provider factory.
        /// </summary>
        private DbConnection CreateConnection()
        {
            var conn = Factory.CreateConnection();
            if (conn == null) throw new InvalidOperationException("Factory failed to create connection");
            conn.ConnectionString = ConnectionString;
            return conn;
        }

        /// <summary>
        /// Executes a database operation with retries, transient detection, and logging.
        /// </summary>
        protected async Task<T> ExecuteWithRetryAsync<T>(Func<DbConnection, Task<T>> action, [CallerMemberName] string op = "")
        {
            Exception lastEx = null;

            if (!await HasInternetAsync().ConfigureAwait(false))
                throw new DbException("No internet connection – aborting database operation");

            for (int i = 0; i <= MaxRetries; i++)
            {
                DbConnection conn = null;
                Stopwatch sw = null;

                try
                {
                    conn = CreateConnection();
                    await conn.OpenAsync(_cts.Token).ConfigureAwait(false);

                    // Only start stopwatch around actual DB operation
                    sw = Stopwatch.StartNew();
                    var result = await action(conn).ConfigureAwait(false);
                    sw.Stop();

                    _lastSuccessUtc = DateTime.UtcNow;
                    _isHealthy = true;

                    // Lightweight logging
                    Logs.Write(Logger, "OK", Name, $"{op} '{sw.ElapsedMilliseconds}ms'");
                    return result;
                }
                catch (Exception ex) when (i < MaxRetries && IsTransient(ex))
                {
                    lastEx = ex;
                    var delay = TimeSpan.FromMilliseconds(Rng.Value.NextDouble() * Math.Pow(2, i) * BaseDelay.TotalMilliseconds);
                    Logs.Write(Logger, "Retry", Name, $"Attempt {i + 1}/{MaxRetries} → {ex.GetType().Name}: {ex.Message} (wait '{delay.TotalMilliseconds:F0}ms')");
                    await Task.Delay(delay, _cts.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (sw != null) sw.Stop();
                    _isHealthy = false;
                    Logs.Write(Logger, "Fail", Name, $"{op} '{sw?.ElapsedMilliseconds ?? 0}ms' → {ex}");
                    throw;
                }
                finally
                {
                    if (conn != null)
                    {
                        try { conn.Close(); } catch { }
                        try { conn.Dispose(); } catch { }
                    }
                }
            }

            _isHealthy = false;
            throw new DbException($"Database '{Name}' failed after {MaxRetries} retries", lastEx);
        }


        /// <summary>
        /// Determines whether an exception is transient and should be retried.
        /// Improvement: override in provider-specific derived classes for better detection.
        /// </summary>
        protected virtual bool IsTransient(Exception ex) =>
            ex is TimeoutException ||
            ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("transient", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Checks internet availability before attempting a DB operation.
        /// </summary>
        private async Task<bool> HasInternetAsync()
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                var resp = await client.GetAsync("https://1.1.1.1").ConfigureAwait(false);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        /// <summary>
        /// Background health monitor to periodically verify DB connectivity.
        /// Improvement: reduces risk of silent failures in long-running apps.
        /// </summary>
        private async Task HealthMonitorAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(HealthCheckInterval, _cts.Token);

                    if (DateTime.UtcNow - _lastSuccessUtc > HealthCheckInterval + TimeSpan.FromMinutes(1))
                        await ExecuteWithRetryAsync(c => c.ExecuteAsync("SELECT 1"), "HEALTH_CHECK");
                }
                catch { /* ignore */ }
            }
        }

        // --------------------------------------------------------------------
        // Dapper API
        // --------------------------------------------------------------------
        public Task<IEnumerable<T>> QueryAsync<T>(string sql, object param = null, int? timeoutSec = null)
            => ExecuteWithRetryAsync(c => c.QueryAsync<T>(sql, param, commandTimeout: timeoutSec), "Query");

        public Task<T> QueryFirstOrDefaultAsync<T>(string sql, object param = null, int? timeoutSec = null)
            => ExecuteWithRetryAsync(c => c.QueryFirstOrDefaultAsync<T>(sql, param, commandTimeout: timeoutSec), "Query-First");

        public Task<int> ExecuteAsync(string sql, object param = null, int? timeoutSec = null)
            => ExecuteWithRetryAsync(c => c.ExecuteAsync(sql, param, commandTimeout: timeoutSec), "Execute");

        public Task<IEnumerable<T>> QuerySpAsync<T>(string proc, object param = null, int? timeoutSec = null)
            => ExecuteWithRetryAsync(c => c.QueryAsync<T>(proc, param, commandType: System.Data.CommandType.StoredProcedure, commandTimeout: timeoutSec), "Sp-Query");

        public Task<T> QuerySpFirstAsync<T>(string proc, object param = null, int? timeoutSec = null)
            => ExecuteWithRetryAsync(c => c.QueryFirstOrDefaultAsync<T>(proc, param, commandType: System.Data.CommandType.StoredProcedure, commandTimeout: timeoutSec), "Sp-First");

        public Task<int> ExecuteSpAsync(string proc, object param = null, int? timeoutSec = null)
            => ExecuteWithRetryAsync(c => c.ExecuteAsync(proc, param, commandType: System.Data.CommandType.StoredProcedure, commandTimeout: timeoutSec), "Sp-Execute");

        /// <summary>
        /// Abstract method for full DB backup. Must be implemented per provider.
        /// </summary>
        public abstract Task<bool> CreateFullBackupAsync(string filePath);

        /// <summary>
        /// Dispose of health monitor and cancellation token.
        /// </summary>
        public void Dispose()
        {
            _cts.Cancel();
            try { _healthTask?.Wait(3000); } catch { }
            _cts.Dispose();
            Logs.Write(Logger, "Stop", Name, "Disposed");
        }
    }
}



