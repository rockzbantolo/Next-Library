using Dapper;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using Npgsql;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Next.Clients.Core
{
    public abstract class Base : IDatabase, IDisposable
    {
        // --------------------------------------------------------------------
        // Core fields
        // --------------------------------------------------------------------
        protected readonly DbProviderFactory Factory;
        protected readonly string ConnectionString;
        public readonly Action<string> Logger;
        private readonly ILogger _structuredLogger;
        private readonly ResiliencePipeline _pipeline;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _healthTask;
        private volatile bool _isHealthy = true;
        private DateTime _lastSuccessUtc = DateTime.UtcNow;

        // --------------------------------------------------------------------
        // Monitoring counters (public for external health checks / dashboards)
        // --------------------------------------------------------------------
        public long SuccessfulOperations { get; private set; }
        public long FailedOperations { get; private set; }
        public long TotalRetries { get; private set; }

        public string Name { get; }
        public bool IsHealthy => _isHealthy && (DateTime.UtcNow - _lastSuccessUtc) < TimeSpan.FromMinutes(10);
        public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(2);
        public string HealthCheckSql { get; }

        // --------------------------------------------------------------------
        // Constructor with full configurability
        // --------------------------------------------------------------------
        protected Base(
            DbProviderFactory factory,
            string connectionString,
            string name,
            Action<string> logger = null,
            ILogger structuredLogger = null,
            int? maxRetryAttempts = null,
            TimeSpan? baseRetryDelay = null,
            TimeSpan? operationTimeout = null,
            TimeSpan? circuitBreakDuration = null,
            double? circuitBreakerFailureRatio = null,
            int? circuitBreakerMinimumThroughput = null,
            string healthCheckSql = null)
        {
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
            ConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            Name = string.IsNullOrWhiteSpace(name) ? "DB" : name.Trim();

            Logger = logger ?? (m => Debug.WriteLine($"[{Name}] {m}"));
            _structuredLogger = structuredLogger;

            // --------------------------------------------------------------------
            // Default Values
            // --------------------------------------------------------------------
            int retries = maxRetryAttempts ?? 5;
            TimeSpan retryDelay = baseRetryDelay ?? TimeSpan.FromMilliseconds(250);
            TimeSpan timeout = operationTimeout ?? TimeSpan.FromSeconds(45);
            TimeSpan breakDuration = circuitBreakDuration ?? TimeSpan.FromMinutes(2);
            double failureRatio = circuitBreakerFailureRatio ?? 0.5;
            int minThroughput = circuitBreakerMinimumThroughput ?? 10;

            HealthCheckSql = healthCheckSql ?? "SELECT 1";

            _pipeline = new ResiliencePipelineBuilder()
                .AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = retries,
                    Delay = retryDelay,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(IsTransientException),
                    OnRetry = args =>
                    {
                        TotalRetries++;
                        LogOperation("Retry", 0, false, $"Attempt {args.AttemptNumber}");
                        return default;
                    }
                })
                .AddTimeout(timeout)
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    FailureRatio = failureRatio,
                    MinimumThroughput = minThroughput,
                    BreakDuration = breakDuration,
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(IsTransientException),
                    OnOpened = args =>
                    {
                        LogOperation("Circuit", 0, false, $"Opened for {args.BreakDuration.TotalSeconds}s");
                        return default;
                    },
                    OnClosed = _ =>
                    {
                        LogOperation("Circuit", 0, true, "Reset");
                        return default;
                    },
                    OnHalfOpened = _ =>
                    {
                        LogOperation("Circuit", 0, true, "Half-open");
                        return default;
                    }
                })
                .Build();

            LogOperation("Initialized", 0, true, $"Retries({retries}), Timeout({timeout.TotalSeconds})s, Break({breakDuration.TotalMinutes})min");

            _healthTask = Task.Run(HealthMonitorAsync);
        }

        // --------------------------------------------------------------------
        // Connection creation
        // --------------------------------------------------------------------
        private DbConnection CreateConnection()
        {
            var conn = Factory.CreateConnection() ?? throw new InvalidOperationException("Failed to create connection");
            conn.ConnectionString = ConnectionString;
            return conn;
        }

        // --------------------------------------------------------------------
        // Transient exception detection (extensible)
        // --------------------------------------------------------------------
        protected virtual bool IsTransientException(Exception ex)
        {
            if (ex is TimeoutException or TaskCanceledException)
                return true;
            string msg = ex.Message;
            if (msg.Contains("TIMEOUT", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("TRANSIENT", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("DEADLOCK", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("CONNECTION", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("GONE AWAY", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("TOO MANY CONNECTIONS", StringComparison.OrdinalIgnoreCase))
                return true;
            if (ex.InnerException is { } inner)
            {
                if (inner is System.Data.SqlClient.SqlException sqlEx &&
                new[] { 4060, 40197, 40501, 49918, 49919, 49920 }.Contains(sqlEx.Number))
                    return true;
                if (inner is MySql.Data.MySqlClient.MySqlException mysqlEx &&
                new[] { 2006, 2013, 2055 }.Contains(mysqlEx.Number))
                    return true;
                if (inner is Npgsql.PostgresException pgEx &&
                new[] { "08006", "57P01", "57P02", "57P03", "53300" }.Contains(pgEx.SqlState))
                    return true;
            }
            return false;
        }

        // --------------------------------------------------------------------
        // Core execution with resilience
        // --------------------------------------------------------------------
        protected async Task<T> ExecuteWithRetryAsync<T>(
            Func<DbConnection, CancellationToken, Task<T>> action,
            [CallerMemberName] string operationName = "",
            Func<T, string> logDetail = null)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                var result = await _pipeline.ExecuteAsync(async token =>
                {
                    using var conn = CreateConnection();
                    await conn.OpenAsync(token).ConfigureAwait(false);
                    return await action(conn, token).ConfigureAwait(false);
                }, _cts.Token);

                sw.Stop();
                _lastSuccessUtc = DateTime.UtcNow;
                _isHealthy = true;
                SuccessfulOperations++;

                string detail = logDetail != null ? logDetail(result) : null;
                LogOperation(operationName, sw.ElapsedMilliseconds, true, detail);

                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _isHealthy = false;
                FailedOperations++;

                LogOperation(operationName, sw.ElapsedMilliseconds, false, ex.Message);

                if (IsFatalConnectionError(ex))
                    ClearPools();

                throw new DbException($"Database '{Name}' operation '{operationName}' failed", ex);
            }
        }


        // --------------------------------------------------------------------
        // Connection pool clearing on fatal errors
        // --------------------------------------------------------------------
        private void ClearPools()
        {
            try
            {
                if (Factory is System.Data.SqlClient.SqlClientFactory)
                    System.Data.SqlClient.SqlConnection.ClearAllPools();
                else if (Factory is Microsoft.Data.SqlClient.SqlClientFactory)
                    Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
                else if (Factory is MySqlClientFactory)
                    MySqlConnection.ClearAllPools();
                else if (Factory is NpgsqlFactory)
                    NpgsqlConnection.ClearAllPools();

                LogOperation("Pool", 0, false, "Cleared connection pools");
            }
            catch { }
        }

        private static bool IsFatalConnectionError(Exception ex)
        {
            string msg = ex.Message.ToUpperInvariant();
            return msg.Contains("SSL") ||
                   msg.Contains("ENCRYPTION") ||
                   msg.Contains("PRELOGIN") ||
                   msg.Contains("PROVIDER") ||
                   msg.Contains("NETWORK");
        }

        // --------------------------------------------------------------------
        // Background health monitoring
        // --------------------------------------------------------------------
        private async Task HealthMonitorAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                await Task.Delay(HealthCheckInterval, _cts.Token).ConfigureAwait(false);

                if (DateTime.UtcNow - _lastSuccessUtc < HealthCheckInterval + TimeSpan.FromMinutes(1))
                    continue;

                try
                {
                    await ExecuteWithRetryAsync(async (c, ct) =>
                    {
                        await c.ExecuteAsync(HealthCheckSql);
                        return true;
                    }, "HEALTH_CHECK");
                }
                catch { }
            }
        }

        // --------------------------------------------------------------------
        // Manual health check (public API)
        // --------------------------------------------------------------------
        public async Task<bool> CheckHealthAsync()
        {
            try
            {
                await ExecuteWithRetryAsync(async (c, ct) =>
                {
                    await c.ExecuteAsync(HealthCheckSql);
                    return true;
                }, "MANUAL_HEALTH_CHECK");
                return true;
            }
            catch
            {
                return false;
            }
        }

        // --------------------------------------------------------------------
        // Dapper API - all methods use ExecuteWithRetryAsync
        // --------------------------------------------------------------------
        public Task<IEnumerable<T>> QueryAsync<T>(string sql, object param = null, int? timeoutSec = null)
        {
            return ExecuteWithRetryAsync(
                (c, ct) => c.QueryAsync<T>(sql, param, commandTimeout: timeoutSec),
                "QueryAsync",
                result =>
                {
                    int count = result is ICollection<T> col ? col.Count : result.Count();
                    return $"Loaded {count} rows";
                });
        }

        public Task<T> QueryFirstOrDefaultAsync<T>(string sql, object param = null, int? timeoutSec = null)
        {
            return ExecuteWithRetryAsync(
                (c, ct) => c.QueryFirstOrDefaultAsync<T>(sql, param, commandTimeout: timeoutSec),
                "QueryFirstOrDefaultAsync",
                result => result != null ? "Loaded 1 row" : "Loaded 0 rows");
        }

        public Task<int> ExecuteAsync(string sql, object param = null, int? timeoutSec = null)
        {
            return ExecuteWithRetryAsync(
                (c, ct) => c.ExecuteAsync(sql, param, commandTimeout: timeoutSec),
                "ExecuteAsync",
                rowsAffected => $"Affected {rowsAffected} rows");
        }

        public Task<IEnumerable<T>> QuerySpAsync<T>(string proc, object param = null, int? timeoutSec = null)
        {
            return ExecuteWithRetryAsync(
                (c, ct) => c.QueryAsync<T>(proc, param, commandType: System.Data.CommandType.StoredProcedure, commandTimeout: timeoutSec),
                "QuerySpAsync",
                result =>
                {
                    int count = result is ICollection<T> col ? col.Count : result.Count();
                    return $"Loaded {count} rows";
                });
        }

        public Task<T> QuerySpFirstAsync<T>(string proc, object param = null, int? timeoutSec = null)
        {
            return ExecuteWithRetryAsync(
                (c, ct) => c.QueryFirstOrDefaultAsync<T>(proc, param, commandType: System.Data.CommandType.StoredProcedure, commandTimeout: timeoutSec),
                "QuerySpFirstAsync",
                result => result != null ? "Loaded 1 row" : "Loaded 0 rows");
        }

        public Task<int> ExecuteSpAsync(string proc, object param = null, int? timeoutSec = null)
        {
            return ExecuteWithRetryAsync(
                (c, ct) => c.ExecuteAsync(proc, param, commandType: System.Data.CommandType.StoredProcedure, commandTimeout: timeoutSec),
                "ExecuteSpAsync",
                rowsAffected => $"Affected {rowsAffected} rows");
        }

        public Task TransactAsync(Func<DbConnection, DbTransaction, Task> action, [CallerMemberName] string operationName = "")
        {
            return ExecuteWithRetryAsync(
                async (conn, ct) =>
                {
                    using var tx = conn.BeginTransaction();
                    try
                    {
                        await action(conn, tx).ConfigureAwait(false);
                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                    return true; // no row count for transactions
                },
                operationName,
                _ => "Transaction executed");
        }


        // --------------------------------------------------------------------
        // Abstract backup method
        // --------------------------------------------------------------------
        public abstract Task<bool> CreateFullBackupAsync(string filePath);

        // --------------------------------------------------------------------
        // Dispose
        // --------------------------------------------------------------------
        public void Dispose()
        {
            _cts.Cancel();
            try { _healthTask?.Wait(3000); } catch { }
            _cts.Dispose();
            LogOperation("Dispose", 0, true);
        }

        // --------------------------------------------------------------------
        // Centralized logging (simple + structured)
        // --------------------------------------------------------------------
        private void LogOperation(string operation, long elapsedMs, bool success, string detail = null)
        {
            string ts = DateTime.Now.ToString("hh:mm:ss tt");
            string status = success ? "Ok" : "Fail";
            string msg = $"'Next-Provider' ({Name}: {operation}):";

            if (!string.IsNullOrWhiteSpace(detail))
                msg += $" {detail}";

            msg += $" ({ts}) '{elapsedMs} ms' → {status}";

            Logger?.Invoke(msg);

            if (_structuredLogger == null) return;

            if (success)
                _structuredLogger.LogInformation("{Name} {Operation} {Detail} in {ElapsedMs} ms", Name, operation, detail, elapsedMs);
            else
                _structuredLogger.LogError("{Name} {Operation} failed after {ElapsedMs} ms: {Detail}", Name, operation, elapsedMs, detail);
        }

    }
}