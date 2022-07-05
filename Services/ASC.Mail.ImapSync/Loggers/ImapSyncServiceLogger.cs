namespace ASC.Mail.ImapSync.Loggers;

internal static partial class ImapSyncServiceLogger
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Service is ready")]
    public static partial void InfoImapSyncServiceReady(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Critical, Message = "ImapSyncService error under construct: {error}")]
    public static partial void CritImapSyncServiceConstruct(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Didn`t subscribe to redis. Message: {error}")]
    public static partial void ErrorImapSyncServiceSubscribeRedis(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Try to subscribe redis...")]
    public static partial void InfoImapSyncServiceTrySubscribeRedis(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Online Users count: {count}")]
    public static partial void DebugImapSyncServiceOnlineUsersCount(this ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "User Activity -> {username}, folder={folder}. Wait for client start...")]
    public static partial void DebugImapSyncServiceWaitForClient(this ILogger logger, string username, int folder);

    [LoggerMessage(Level = LogLevel.Debug, Message = "User Activity -> {username}, folder = {folder}. Try to create client ...")]
    public static partial void DebugImapSyncServiceTryСreateСlient(this ILogger logger, string username, int folder);

    [LoggerMessage(Level = LogLevel.Information, Message = "Can`t create Mail client for user {username}.")]
    public static partial void InfoImapSyncServiceCantCreateMailClient(this ILogger logger, string username);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[TIMEOUT] Create mail client for user {username}. {error}")]
    public static partial void WarnImapSyncServiceCreateClientTimeout(this ILogger logger, string username, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "[CANCEL] Create mail client for user {userName}.")]
    public static partial void InfoImapSyncServiceCreateClientCancel(this ILogger logger, string username);

    [LoggerMessage(Level = LogLevel.Error, Message = "[AuthenticationException] Create mail client for user {username}. {error}")]
    public static partial void ErrorImapSyncServiceCreateClientAuth(this ILogger logger, string username, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "[WebException] Create mail client for user {username}. {error}")]
    public static partial void ErrorImapSyncServiceCreateClientWeb(this ILogger logger, string username, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Create mail client for user {username}. {error}")]
    public static partial void ErrorImapSyncServiceCreateClientException(this ILogger logger, string username, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "ImapSyncService. MailImapClient {clientKey} died and was remove.")]
    public static partial void InfoImapSyncServiceClientDiedAndWasRemove(this ILogger logger, string clientKey);

    [LoggerMessage(Level = LogLevel.Information, Message = "ImapSyncService. MailImapClient {clientKey} died, bud wasn`t remove.")]
    public static partial void InfoImapSyncServiceClientDiedAndWasntRemove(this ILogger logger, string clientKey);

    [LoggerMessage(Level = LogLevel.Information, Message = "Start service\r\n")]
    public static partial void InfoImapSyncServiceStart(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "{error}")]
    public static partial void ErrorImapSyncService(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stoping service\r\n")]
    public static partial void InfoImapSyncServiceStoping(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Stop service Error: {error}\r\n")]
    public static partial void ErrorImapSyncServiceStop(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stop service\r\n")]
    public static partial void InfoImapSyncServiceStop(this ILogger logger);
}
