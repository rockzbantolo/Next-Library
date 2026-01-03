using Serilog;
using System;
using System.Runtime.CompilerServices;


namespace Next.Common
{
    public static class Logger
    {
        private static readonly ILogger _log;

        static Logger()
        {
            _log = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: "[{Level:u3}] {Timestamp:HH:mm:ss.fff} {SourceContext} → {Message:lj}{NewLine}{Exception}")
                .WriteTo.Debug()
                .CreateLogger();
        }

        public static void Trace(string msg, [CallerMemberName] string member = "")
            => _log.Verbose("{Member} → {Msg}", member, msg);

        public static void Debug(string msg, [CallerMemberName] string member = "")
            => _log.Debug("{Member} → {Msg}", member, msg);

        public static void Info(string msg, [CallerMemberName] string member = "")
            => _log.Information("{Member} → {Msg}", member, msg);

        public static void Warn(string msg, [CallerMemberName] string member = "")
            => _log.Warning("{Member} → {Msg}", member, msg);

        public static void Error(string msg, Exception ex = null, [CallerMemberName] string member = "")
            => _log.Error(ex, "{Member} → {Msg}", member, msg);

    }
}