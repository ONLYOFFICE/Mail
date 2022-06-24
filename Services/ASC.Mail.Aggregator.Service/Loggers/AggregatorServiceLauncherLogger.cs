namespace ASC.Mail.Aggregator.Service.Loggers;

internal static partial class AggregatorServiceLauncherLogger
{
    [LoggerMessage(Level = LogLevel.Critical, Message = "Unhandled exception: {error}")]
    public static partial void CritAggregatorServiceLauncher(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Service starts...")]
    public static partial void InfoAggregatorServiceLauncherStart(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Trying to stop the service. Await task...")]
    public static partial void InfoAggregatorServiceLauncherAwaitTask(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "AggregatorServiceTask was canceled.")]
    public static partial void ErrorAggregatorServiceLauncherCancel(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to terminate the service correctly. The details:\r\n{error}\r\n")]
    public static partial void ErrorAggregatorServiceLauncherStop(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "The service was successfully stopped.")]
    public static partial void InfoAggregatorServiceLauncherStop(this ILogger logger);
}
