namespace ASC.Mail.ImapSync.Loggers;

internal static partial class ImapSyncServiceLogger
{
    [LoggerMessage(EventId = 100, Level = LogLevel.Information, Message = "Service is {serviceText}.")]
    public static partial void InfoImapSyncService(this ILogger logger, string serviceText);

    [LoggerMessage(EventId = 1, Level = LogLevel.Critical, Message = "ImapSyncService error under construct: {error}")]
    public static partial void CritImapSyncServiceConstruct(this ILogger logger, string error);

    [LoggerMessage(EventId = 11, Level = LogLevel.Debug, Message = "Online Users count: {count}")]
    public static partial void DebugImapSyncServiceOnlineUsersCount(this ILogger logger, int count);

    [LoggerMessage(EventId = 12, Level = LogLevel.Debug, Message = "User Activity -> {username}, folder={folder}. Wait for client start...")]
    public static partial void DebugImapSyncServiceWaitForClient(this ILogger logger, string username, int folder);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Stop service Error: {error}\r\n")]
    public static partial void ErrorImapSyncServiceStop(this ILogger logger, string error);
    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Create client Error: {error}\r\n")]
    public static partial void ErrorImapSyncCreateClient(this ILogger logger, string error);
}
