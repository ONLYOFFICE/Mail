namespace ASC.Mail.ImapSync.Loggers;

internal static partial class MailImapClientLogger
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "{message}")]
    public static partial void InfoMailImapClient(this ILogger logger, string message);

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Message updated (id: {messageId}, Folder: '{from}'), Subject: '{subject}'")]
    public static partial void InfoMailImapClientMessageUpdated(this ILogger logger, int messageId, string from, string subject);

    [LoggerMessage(EventId = 100, Level = LogLevel.Debug, Message = "{duration}|{method}|{status}|{mailboxId}|{address}")]
    public static partial void DebugStatistic(this ILogger logger, double duration, string method, bool status, int mailboxId, string address);

    [LoggerMessage(EventId = 101, Level = LogLevel.Debug, Message = "{message}.")]
    public static partial void DebugMailImapClientFromRedisPipeline(this ILogger logger, string message);

    [LoggerMessage(EventId = 102, Level = LogLevel.Debug, Message = "{message}.")]
    public static partial void DebugMailImapClientFromIMAPPipeline(this ILogger logger, string message);
    
    [LoggerMessage(EventId = 103, Level = LogLevel.Debug, Message = "{message}.")]
    public static partial void DebugMailImapClientDBPipeline(this ILogger logger, string message);

    [LoggerMessage(EventId = 200, Level = LogLevel.Error, Message = "{message}: {error}.")]
    public static partial void ErrorMailImapClientFromRedisPipeline(this ILogger logger, string message, string error);

    [LoggerMessage(EventId = 201, Level = LogLevel.Error, Message = "{message}: {error}.")]
    public static partial void ErrorMailImapClientFromIMAPPipeline(this ILogger logger, string message, string error);

    [LoggerMessage(EventId = 203, Level = LogLevel.Error, Message = "{message}: {error}.")]
    public static partial void ErrorMailImapClientDBPipeline(this ILogger logger, string message, string error);

    [LoggerMessage(EventId = 204, Level = LogLevel.Error, Message = "SendUnreadUser error {error}. Inner error: {innerError}.")]
    public static partial void ErrorMailImapClientSendUnreadUser(this ILogger logger, string error, string innerError);

    [LoggerMessage(EventId = 205, Level = LogLevel.Error, Message = "SetMessagesTag(tenant={tenantId}, userId='{userName}', messageId={messageId}, tagid = {tagIds})\r\nException:{error}\r\n")]
    public static partial void ErrorMailImapClientSetMessagesTag(this ILogger logger, int tenantId, string userName, int messageId, string tagIds, string error);
}
