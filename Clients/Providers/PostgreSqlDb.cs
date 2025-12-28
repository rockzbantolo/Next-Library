using Next.Clients.Core;
using Npgsql;
using System;
using System.Threading.Tasks;

namespace Next.Clients.Providers
{
    public sealed class PostgreSqlDb : Base
    {
        public PostgreSqlDb(string connectionString, Action<string> logger = null)
             : base(NpgsqlFactory.Instance, connectionString, "PostgreSQL", logger) {}

        public override async Task<bool> CreateFullBackupAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Backup path cannot be null or empty.", nameof(filePath));

            Logger?.Invoke($"Starting PostgreSQL backup → {filePath}");
            return await Backup.PostgreSqlBackupStrategy.DumpAsync(this, filePath).ConfigureAwait(false);
        }
    }
}
