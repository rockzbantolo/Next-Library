using MySql.Data.MySqlClient;
using Next.Clients.Core;
using Npgsql;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Next.Clients.Providers
{
    public sealed class MySqlDbBuilder
    {
        private string _host = "localhost";
        private uint _port = 3306;
        private string _database;
        private string _username;
        private string _password;
        private bool _pooling = true;
        private uint? _connectTimeout;
        private MySqlSslMode _sslMode = MySqlSslMode.Disabled;
        private Action<string> _logger;


        public static MySqlDbBuilder Create() => new();

        public MySqlDbBuilder Host(string host) { _host = host; return this; }
        public MySqlDbBuilder Port(uint port)   { _port = port; return this; }
        public MySqlDbBuilder Database(string db) { _database = db; return this; }
        public MySqlDbBuilder Username(string user) { _username = user; return this; }
        public MySqlDbBuilder Password(string pwd) { _password = pwd; return this; }
        public MySqlDbBuilder WithCredentials(string user, string pwd) => Username(user).Password(pwd);
        public MySqlDbBuilder Pooling(bool enable) { _pooling = enable; return this; }
        public MySqlDbBuilder ConnectTimeout(uint seconds) { _connectTimeout = seconds; return this; }
        public MySqlDbBuilder SslMode(MySqlSslMode mode) { _sslMode = mode; return this; }
        public MySqlDbBuilder Logger(Action<string> log) { _logger = log; return this; }

        public static MySqlDbBuilder For(string host, string database)
            => Create().Host(host).Database(database);

        public MySqlDb Build()
        {
            if (string.IsNullOrEmpty(_database))
                throw new InvalidOperationException("Database is required");

            if (string.IsNullOrEmpty(_username) || string.IsNullOrEmpty(_password))
                throw new InvalidOperationException("Credentials are required");

            var cs = $"Server={_host};Port={_port};Database={_database};" +
                     $"Uid={_username};Pwd={_password};Pooling={_pooling};";

            if (_connectTimeout.HasValue)
                cs += $"Connect Timeout={_connectTimeout};";

            return new MySqlDb(cs, _logger ?? (m => Debug.WriteLine(m)));
        }
    }

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
