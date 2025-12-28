using Next.Clients.Core;
using System;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace Next.Clients.Providers
{
    public sealed class SqlServerDb : Base
    {
        public SqlServerDb(string connectionString, Action<string> logger = null)
            : base(SqlClientFactory.Instance, connectionString, "SQLServer", logger) {}

        public override async Task<bool> CreateFullBackupAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Backup path cannot be null or empty.", nameof(filePath));

            Logger?.Invoke($"Starting SQL Server backup → {filePath}");
            return await Backup.SqlBackup.DumpAsync(this, filePath).ConfigureAwait(false);
        }
    }
}
