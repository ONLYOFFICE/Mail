namespace ASC.Mail.ImapSync.Loggers;

internal static partial class MailImapClientLogger
{
    [LoggerMessage(EventId = 10, Level = LogLevel.Debug, Message = "{duration}|{method}|{status}|{mailboxId}|{address}")]
    public static partial void DebugStatistic(this ILogger logger, double duration, string method, bool status, int mailboxId, string address);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "{message}: {error}.")]
    public static partial void ErrorMailImapClient(this ILogger logger, string message, string error);

    [LoggerMessage(EventId = 11, Level = LogLevel.Debug, Message = "{message}")]
    public static partial void DebugMailImapClient(this ILogger logger, string message);

    [LoggerMessage(Level = LogLevel.Debug, Message = "SetMessageFlagsFromImap: imap_message_Uidl={id}, flag={messageFlag}." +
        "\nSetMessageFlagsFromImap: db_message={uidl}, folder={folder}, IsRemoved={isRemoved}.")]
    public static partial void DebugMailImapClientSetMessageFlagsFromImap(this ILogger logger, uint id, string messageFlag, string uidl, string folder, bool isRemoved);

    [LoggerMessage(EventId = 100, Level = LogLevel.Information, Message = "{message}")]
    public static partial void InfoMailImapClient(this ILogger logger, string message);

    [LoggerMessage(Level = LogLevel.Information, Message = "Message updated (id: {messageId}, Folder: '{from}'), Subject: '{subject}'")]
    public static partial void InfoMailImapClientMessageUpdated(this ILogger logger, int messageId, string from, string subject);

    [LoggerMessage(Level = LogLevel.Error, Message = "SendUnreadUser error {error}. Inner error: {innerError}.")]
    public static partial void ErrorMailImapClientSendUnreadUser(this ILogger logger, string error, string innerError);

    [LoggerMessage(Level = LogLevel.Debug, Message = "DoOptionalOperations -> GetOrCreateTags()")]
    public static partial void DebugMailImapClientGetOrCreateTags(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "SetMessagesTag(tenant={tenantId}, userId='{userName}', messageId={messageId}, tagid = {tagIds})\r\nException:{error}\r\n")]
    public static partial void ErrorMailImapClientSetMessagesTag(this ILogger logger, int tenantId, string userName, int messageId, string tagIds, string error);
}
