using MySql.Data.MySqlClient;
using Next.Clients.Core;
using System;
using System.Threading.Tasks;

namespace Next.Clients.Providers
{
    public sealed class MySqlDb : Base
    {
        public MySqlDb(string connectionString, Action<string> logger = null)
            : base(MySqlClientFactory.Instance, connectionString, "MySql", logger) {}

        /// <summary>
        /// Creates a full database backup asynchronously.
        /// </summary>
        /// <param name="filePath">Path where the backup will be saved.</param>
        /// <returns>True if successful.</returns>
        public override async Task<bool> CreateFullBackupAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Backup path cannot be null or empty.", nameof(filePath));

            Logger?.Invoke($"Starting MySQL backup → {filePath}");
            return await Backup.MySqlBackup.DumpAsync(this, filePath).ConfigureAwait(false);
        }
    }
}
