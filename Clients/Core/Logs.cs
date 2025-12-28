using System;

namespace Next.Clients.Core
{
    internal static class Logs
    {
        public static void Write(Action<string> logger, string level, string dbName, string message)
        {
            //{DateTime.Now:MM/dd/yyyy hh:mm:ss.fff tt}
            logger?.Invoke($"'Database:{dbName}' ({level}): {message}");
        }
    }
}
