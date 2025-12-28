using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Next.Common

{
    public static class Logger
    {
        public static bool IsDevelopmentMode { get; set; } = true;

        public static void Trace(string message, bool? isDev = null,
            [CallerMemberName] string caller = null,
            [CallerFilePath] string file = null,
            [CallerLineNumber] int line = 0)
        {
            if (!(isDev ?? IsDevelopmentMode)) return;
            WriteLogs("TRACE", message, caller, file, line);
        }

        public static void Info(string message, bool? isDev = null,
            [CallerMemberName] string caller = null,
            [CallerFilePath] string file = null,
            [CallerLineNumber] int line = 0)
        {
            if (!(isDev ?? IsDevelopmentMode)) return;
            WriteLogs("INFO", message, caller, file, line);
        }

        public static void Warn(string message, bool? isDev = null,
            [CallerMemberName] string caller = null,
            [CallerFilePath] string file = null,
            [CallerLineNumber] int line = 0)
        {
            if (!(isDev ?? IsDevelopmentMode)) return;
            WriteLogs("WARN", message, caller, file, line);
        }

        public static void Error(string message, Exception ex = null, bool? isDev = null,
            [CallerMemberName] string caller = null,
            [CallerFilePath] string file = null,
            [CallerLineNumber] int line = 0)
        {
            if (!(isDev ?? IsDevelopmentMode)) return;

            var sb = new StringBuilder();
            sb.AppendLine(message);

            if (ex != null)
            {
                sb.AppendLine($">[Reason]   : {ex.InnerException?.InnerException?.Message ?? ex.InnerException?.Message ?? ex.Message}");
                sb.AppendLine($">[Exception]: {ex.GetType().Name} - {ex.Message}");

                int depth = 1;
                var inner = ex.InnerException;
                while (inner != null)
                {
                    sb.AppendLine($" - [Inner#{depth}]    : {inner.GetType().Name} - {inner.Message}");
                    inner = inner.InnerException;
                    depth++;
                }

                sb.AppendLine(" - [StackTrace] :");
                sb.AppendLine(ex.StackTrace);
            }

            WriteErrorLog("ERROR", sb.ToString(), caller, file, line);
        }

        public static void Debug(string message, bool? isDev = null,
            [CallerMemberName] string caller = null,
            [CallerFilePath] string file = null,
            [CallerLineNumber] int line = 0)
        {
            if (!(isDev ?? IsDevelopmentMode)) return;
            WriteLogs("DEBUG", message, caller, file, line);
        }

        private static void WriteLogs(string level, string message, string caller, string file, int line)
        {
            string fileName = Path.GetFileName(file);
            string folderName = Path.GetFileName(Path.GetDirectoryName(file));
            Console.WriteLine($"[{level}].[{DateTime.Now:MM-dd-yyyy HH:mm:ss}].[{caller}].[{folderName}/{fileName}:{line}] - {message}");
        }

        private static void WriteErrorLog(string level, string message, string caller, string file, int line)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string fileName = Path.GetFileName(file);
            string folderName = Path.GetFileName(Path.GetDirectoryName(file));

            Console.WriteLine($"\n>[{level}]-[Timestamp] : {timestamp} --------------------------------------------------");
            Console.WriteLine($">[Method]   : {caller}");
            Console.WriteLine($">[Location] : {folderName}/{fileName}:{line}");
            Console.WriteLine($">[Message]  : {message}");
            Console.WriteLine($">-------------------------------------------------------------------------------------------");
        }
    }
}
