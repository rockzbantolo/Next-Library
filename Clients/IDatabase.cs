using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Next.Clients
{
    public interface IDatabase : IDisposable
    {
        string Name { get; }
        bool IsHealthy { get; }
        TimeSpan HealthCheckInterval { get; set; }
        Task<IEnumerable<T>> QueryAsync<T>(string sql, object param = null, int? timeoutSec = null);
        Task<T> QueryFirstOrDefaultAsync<T>(string sql, object param = null, int? timeoutSec = null);
        Task<int> ExecuteAsync(string sql, object param = null, int? timeoutSec = null);
        Task<IEnumerable<T>> QuerySpAsync<T>(string proc, object param = null, int? timeoutSec = null);
        Task<T> QuerySpFirstAsync<T>(string proc, object param = null, int? timeoutSec = null);
        Task<int> ExecuteSpAsync(string proc, object param = null, int? timeoutSec = null);
        Task<bool> CreateFullBackupAsync(string filePath);
        Task TransactAsync(Func<DbConnection, DbTransaction, Task> action, [CallerMemberName] string op = "");
    }
}
