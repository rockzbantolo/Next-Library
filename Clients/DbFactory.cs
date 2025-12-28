using System;

namespace Next.Clients
{
    /// <summary>
    /// Factory to create database instances safely — never throws FileNotFoundException on startup.
    /// Only fails when you actually USE a provider that is missing.
    /// </summary>
    public static class DbFactory
    {
        public static IDatabase CreateSqlServer(string connectionString, Action<string> logger = null)
            => new Providers.SqlServerDb(connectionString, logger);

        public static IDatabase CreateMySql(string connectionString, Action<string> logger = null)
            => new Providers.MySqlDb(connectionString, logger);

        public static IDatabase CreatePostgreSql(string connectionString, Action<string> logger = null)
            => new Providers.PostgreSqlDb(connectionString, logger);
    }
}
