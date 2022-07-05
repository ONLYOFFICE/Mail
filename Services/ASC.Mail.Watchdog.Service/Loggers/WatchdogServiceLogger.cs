namespace ASC.Mail.Watchdog.Loggers;

internal static partial class WatchdogServiceLogger
{
    [LoggerMessage(Level = LogLevel.Information, Message = "\r\nConfiguration:\r\n\t- check locked mailboxes in every {chekInterval} minutes;\r\n" +
                  "\t- locked mailboxes timeout {mailboxTimeout} minutes;\r\n")]
    public static partial void InfoWatchdogServiceConfiguration(this ILogger logger, double chekInterval, double mailboxTimeout);

    [LoggerMessage(Level = LogLevel.Information, Message = "Try stop service...")]
    public static partial void InfoWatchdogServiceTryStopService(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "ReleaseLockedMailboxes(timeout is {interval} minutes)")]
    public static partial void InfoWatchdogServiceReleaseLockedMailboxes(this ILogger logger, double interval);

    [LoggerMessage(Level = LogLevel.Information, Message = "Released next locked mailbox's ids: {ids}")]
    public static partial void InfoWatchdogServiceReleasedMailboxes(this ILogger logger, string ids);

    [LoggerMessage(Level = LogLevel.Information, Message = "Nothing to do!")]
    public static partial void InfoWatchdogServiceNothingToDo(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "IntervalTimer_Elapsed() Exception:\r\n{error}")]
    public static partial void ErrorWatchdogServiceIntervalTimer(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Waiting for {minutes} minutes for next check...")]
    public static partial void InfoWatchdogServiceWaiting(this ILogger logger, double minutes);
}
