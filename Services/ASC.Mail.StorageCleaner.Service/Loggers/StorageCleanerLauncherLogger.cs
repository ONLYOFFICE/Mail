using Microsoft.Extensions.Logging;

namespace ASC.Mail.StorageCleaner.Loggers;

internal static partial class StorageCleanerLauncherLogger
{
    [LoggerMessage(Level = LogLevel.Critical, Message = "Unhandled exception: {error}")]
    public static partial void CritStorageCleanerLauncher(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Service starts...")]
    public static partial void InfoStorageCleanerLauncherStart(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Trying to stop the service.")]
    public static partial void InfoStorageCleanerLauncherTryingToStop(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to terminate the service correctly. The details:\r\n{error}\r\n")]
    public static partial void ErrorStorageCleanerLauncherStop(this ILogger logger, string error);
}
