namespace ASC.Mail.Watchdog.Loggers;

internal static partial class WatchdogLauncherLogger
{
    [LoggerMessage(Level = LogLevel.Critical, Message = "Unhandled exception: {error}")]
    public static partial void CritWatchdogLauncher(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Service starts...")]
    public static partial void InfoWatchdogLauncherStart(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to terminate the service correctly. The details:\r\n{error}\r\n")]
    public static partial void ErrorWatchdogLauncherStop(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "The service was successfully stopped.")]
    public static partial void InfoWatchdogLauncherStop(this ILogger logger);
}
