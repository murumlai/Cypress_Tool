namespace Fx3I2cProgrammer.Core.Logging
{
    /// <summary>
    /// Sink for human-readable operation log messages. Implemented by the UI (bound list) and by
    /// tests (in-memory capture). The hardware layer writes progress and diagnostic detail here.
    /// </summary>
    public interface IOperationLog
    {
        void Log(LogLevel level, string message);
    }

    /// <summary>
    /// Convenience helpers over <see cref="IOperationLog"/>.
    /// </summary>
    public static class OperationLogExtensions
    {
        public static void Info(this IOperationLog log, string message) => log?.Log(LogLevel.Info, message);

        public static void Success(this IOperationLog log, string message) => log?.Log(LogLevel.Success, message);

        public static void Warning(this IOperationLog log, string message) => log?.Log(LogLevel.Warning, message);

        public static void Error(this IOperationLog log, string message) => log?.Log(LogLevel.Error, message);
    }
}
