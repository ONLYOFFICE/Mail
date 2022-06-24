namespace ASC.Mail.Aggregator.Loggers;

public static partial class MailboxHandlerLogger
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Client -> Could not connect: {IsConnected} | Not authenticated: {IsAuthenticated} | Was disposed: {IsDisposed}")]
    public static partial void InfoMailboxHandlerCreateClient(this ILogger logger, string IsConnected, string IsAuthenticated, string IsDisposed);

    [LoggerMessage(Level = LogLevel.Information, Message = "Client was null")]
    public static partial void InfoMailboxHandlerNullClient(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Release mailbox (Tenant: {tenantId} MailboxId: {mailboxId}, Address: '{address}')")]
    public static partial void InfoMailboxHandlerReleaseMailbox(this ILogger logger, int tenantId, int mailboxId, string address);

    [LoggerMessage(Level = LogLevel.Information, Message = "Process mailbox(Tenant: {tenantId}, MailboxId: {mailboxId}, Address: \"{address}\") Is {active}. | Task №: {taskId}")]
    public static partial void InfoMailboxHandlerProcessMailbox(this ILogger logger, int tenantId, int mailboxId, string address, string active, int? taskId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Operation cancel: ProcessMailbox. Tenant: {tenantId}, MailboxId: {mailboxId}, Address: {address}")]
    public static partial void InfoMailboxHandlerOperationCancel(this ILogger logger, int tenantId, int mailboxId, string address);

    [LoggerMessage(Level = LogLevel.Error, Message = "ProcessMailbox exception. Tenant: {tenantId}, MailboxId: {mailboxId}, Address = {address})\r\n{error}")]
    public static partial void ErrorMailboxHandlerProcessMailbox(this ILogger logger, int tenantId, int mailboxId, string address, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Mailbox {mailboxId} {address} has been processed.")]
    public static partial void InfoMailboxHandlerHasBeenProcessed(this ILogger logger, int mailboxId, string address);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Get mailbox state")]
    public static partial void DebugMailboxHandlerGetState(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "MailBox {mailboxId}. STATUS: Begin date was changed.")]
    public static partial void InfoMailboxHandlerBeginDateWasChanged(this ILogger logger, int mailboxId);

    [LoggerMessage(Level = LogLevel.Information, Message = "MailBox {mailboxId}. STATUS: Was removed.")]
    public static partial void InfoMailboxHandlerWasRemoved(this ILogger logger, int mailboxId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Remove mailbox {mailboxId} exception.\r\n{error}")]
    public static partial void ErrorMailboxHandlerRemoveMailbox(this ILogger logger, int mailboxId, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Mailbox {mailboxId}. STATUS: Deactivated")]
    public static partial void InfoMailboxHandlerMailboxDeactivated(this ILogger logger, int mailboxId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Mailbox {mailboxId}. STATUS: Not changed")]
    public static partial void InfoMailboxHandlerMailboxNotChanged(this ILogger logger, int mailboxId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Check mailbox state exception.\r\n{error}")]
    public static partial void ErrorMailboxHandlerCheckState(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AT LOGIN IMAP/POP3 [TIMEOUT]\r\n{boxInfo}\r\n{error}")]
    public static partial void WarnMailboxHandlerLoginTimeout(this ILogger logger, string boxInfo, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "AT LOGIN IMAP/POP3 [IMAP PROTOCOL]\r\n{boxInfo}\r\n{error}")]
    public static partial void ErrorMailboxHandlerAtLogin(this ILogger logger, string boxInfo, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "AT LOGIN IMAP/POP3 [OPERATION CANCEL]\r\n{boxInfo}\r\n{error}")]
    public static partial void InfoMailboxHandlerOperationCancelAtLogin(this ILogger logger, string boxInfo, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "AT LOGIN IMAP/POP3 [AUTHENTICATION]\r\n{boxInfo}\r\n{error}")]
    public static partial void ErrorMailboxHandlerAuthentication(this ILogger logger, string boxInfo, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "AT LOGIN IMAP/POP3 [WEB]\r\n{boxInfo}\r\n{error}")]
    public static partial void ErrorMailboxHandlerWeb(this ILogger logger, string boxInfo, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "AT LOGIN IMAP/POP3 [Certificate has expired EXCEPTION]\r\n{boxInfo}\r\n{error}")]
    public static partial void ErrorMailboxHandlerCertificateExpired(this ILogger logger, string boxInfo, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "AT LOGIN IMAP/POP3 [SSL EXCEPTION]\r\n{boxInfo}\r\n{error}")]
    public static partial void ErrorMailboxHandlerSSL(this ILogger logger, string boxInfo, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "AT LOGIN IMAP/POP3 [Could not resolve host EXCEPTION]\r\n{boxInfo}\r\n{error}")]
    public static partial void ErrorMailboxHandlerCouldNotResolveHost(this ILogger logger, string boxInfo, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "AT LOGIN IMAP/POP3 [UNREGISTERED EXCEPTION]\r\n{boxInfo}\r\n{error}")]
    public static partial void ErrorMailboxHandlerUnknownEx(this ILogger logger, string boxInfo, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Found message uidl: {uidl}, {boxInfo}")]
    public static partial void InfoMailboxHandlerFoundMessage(this ILogger logger, string uidl, string boxInfo);

    [LoggerMessage(Level = LogLevel.Error, Message = "Client on get message exception.\r\n{error}")]
    public static partial void ErrorMailboxHandlerOnGetMessage(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Message {messageId} has been saved to mailbox {mailboxId} ({address})")]
    public static partial void InfoMailboxHandlerMessageSaved(this ILogger logger, int messageId, int mailboxId, string address);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Optional operations: GetOrCreateTags")]
    public static partial void DebugMailboxHandlerGetOrCreateTags(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Optional operations: GetCrmTags")]
    public static partial void DebugMailboxHandlerGetCrmTags(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Optional operations: AddMessageToIndex")]
    public static partial void DebugMailboxHandlerAddMessageToIndex(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Optional operations: SetMessagesTags")]
    public static partial void DebugMailboxHandlerSetMessagesTags(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Set message tags exception.\r\nTags: {tags} | Message: {messageId}, Tenant: {tenantId}, User: {userdId}\r\n{error}")]
    public static partial void ErrorMailboxHandlerSetMessagesTags(this ILogger logger, string tags, int messageId, int tenantId, string userdId, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Optional operations: AddRelationshipEventForLinkedAccounts")]
    public static partial void DebugMailboxHandlerAddRelationshipEvent(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Optional operations: SaveEmailInData")]
    public static partial void DebugMailboxHandlerSaveEmailInData(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Optional operations: SendAutoreply")]
    public static partial void DebugMailboxHandlerSendAutoreply(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Optional operations: UploadIcsToCalendar")]
    public static partial void DebugMailboxHandlerUploadIcs(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Optional operations: StoreMailEml")]
    public static partial void DebugMailboxHandlerStoreMailEml(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Optional operations: ApplyFilters")]
    public static partial void DebugMailboxHandlerApplyFilters(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Optional operations: NotifySocketIO")]
    public static partial void DebugMailboxHandlerNotifySocketIO(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skip notify socketIO... Enable: false")]
    public static partial void DebugMailboxHandlerSkipNotifySocketIO(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Do optional operation exception:\r\n{error}")]
    public static partial void ErrorMailboxHandlerOptionalOperations(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Get filters exception:\r\n{error}")]
    public static partial void ErrorMailboxHandlerGetFilters(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Store mail eml. Tenant: {tenantId}, user: {userId}, result: {result}, path {path}")]
    public static partial void DebugMailboxHandlerStoreMailEmlResult(this ILogger logger, int tenantId, string userId, string result, string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "Store mail eml exception:\r\n{error}")]
    public static partial void ErrorMailboxHandlerStoreMailEml(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Set mailbox auth error exception\r\n{boxInfo}\r\n{error}")]
    public static partial void ErrorMailboxHandlerSetAuthError(this ILogger logger, string boxInfo, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Try close client exception.\r\n{boxInfo}\r\n{error}")]
    public static partial void ErrorMailboxHandlerTryCloseClient(this ILogger logger, string boxInfo, string error);
}
