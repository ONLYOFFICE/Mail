using Microsoft.Extensions.Logging;

namespace ASC.Mail.StorageCleaner.Loggers;

internal static partial class StorageCleanerServiceLogger
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Service will clear mail storage every {minutes} minutes\r\n")]
    public static partial void InfoStorageCleanerServiceCleaningInterval(this ILogger logger, double minutes);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Setup WorkTimer to start immediately")]
    public static partial void DebugStorageCleanerServiceStartImmediately(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Setup WorkTimer to {minutes} minutes")]
    public static partial void DebugStorageCleanerServiceWorkTimer(this ILogger logger, double minutes);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Setup WorkTimer to Timeout.Infinite")]
    public static partial void DebugStorageCleanerServiceWorkTimerInfinite(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Timer -> WorkTimerElapsed")]
    public static partial void DebugStorageCleanerServiceWorkTimerElapsed(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "All mailboxes were processed. Go back to timer. Next start after {minutes} minutes.\r\n")]
    public static partial void InfoStorageCleanerServiceNextStart(this ILogger logger, double minutes);

    [LoggerMessage(Level = LogLevel.Information, Message = "Execution was canceled.")]
    public static partial void InfoStorageCleanerServiceWasCanceled(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Timer -> WorkTimerElapsed. Exception:\r\n{error}\r\n")]
    public static partial void ErrorStorageCleanerServiceWorkTimer(this ILogger logger, string error);
}
