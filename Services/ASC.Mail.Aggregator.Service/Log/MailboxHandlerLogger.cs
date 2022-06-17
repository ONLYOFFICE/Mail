namespace ASC.Mail.Aggregator.Service.Log;

internal static partial class MailboxHandlerLogger
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Client -> Could not connect: {IsConnected} | Not authenticated: {IsAuthenticated} | Was disposed: {IsDisposed}")]
    public static partial void InfoMailboxHandlerCreateClient(this ILogger<MailboxHandler> logger, string IsConnected, string IsAuthenticated, string IsDisposed);

    [LoggerMessage(Level = LogLevel.Information, Message = "Client was null")]
    public static partial void InfoMailboxHandlerNullClient(this ILogger<MailboxHandler> logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Release mailbox (Tenant: {tenantId} MailboxId: {mailboxId}, Address: '{address}')")]
    public static partial void InfoMailboxHandlerReleaseMailbox(this ILogger<MailboxHandler> logger, int tenantId, int mailboxId, string address);

    [LoggerMessage(Level = LogLevel.Information, Message = "Process mailbox(Tenant: {tenantId}, MailboxId: {mailboxId}, Address: \"{address}\") Is {active}. | Task №: {taskId}")]
    public static partial void InfoMailboxHandlerProcessMailbox(this ILogger<MailboxHandler> logger, int tenantId, int mailboxId, string address, string active, int? taskId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Operation cancel: ProcessMailbox. Tenant: {tenantId}, MailboxId: {mailboxId}, Address: {address}")]
    public static partial void InfoMailboxHandlerOperationCancel(this ILogger<MailboxHandler> logger, int tenantId, int mailboxId, string address);

    [LoggerMessage(Level = LogLevel.Error, Message = "ProcessMailbox exception. Tenant: {tenantId}, MailboxId: {mailboxId}, Address = {address})\r\n{error}")]
    public static partial void ErrorMailboxHandlerProcessMailbox(this ILogger<MailboxHandler> logger, int tenantId, int mailboxId, string address, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Mailbox {mailboxId} {address} has been processed.")]
    public static partial void InfoMailboxHandlerHasBeenProcessed(this ILogger<MailboxHandler> logger, int mailboxId, string address);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Get mailbox state")]
    public static partial void DebugMailboxHandlerGetState(this ILogger<MailboxHandler> logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "MailBox {mailboxId}. STATUS: Begin date was changed.")]
    public static partial void InfoMailboxHandlerBeginDateWasChanged(this ILogger<MailboxHandler> logger, int mailboxId);

    [LoggerMessage(Level = LogLevel.Information, Message = "MailBox {mailboxId}. STATUS: Was removed.")]
    public static partial void InfoMailboxHandlerWasRemoved(this ILogger<MailboxHandler> logger, int mailboxId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Remove mailbox {mailboxId} exception.\r\n{error}")]
    public static partial void ErrorMailboxHandlerRemoveMailbox(this ILogger<MailboxHandler> logger, int mailboxId, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Mailbox {mailboxId}. STATUS: Deactivated")]
    public static partial void InfoMailboxHandlerMailboxDeactivated(this ILogger<MailboxHandler> logger, int mailboxId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Mailbox {mailboxId}. STATUS: Not changed")]
    public static partial void InfoMailboxHandlerMailboxNotChanged(this ILogger<MailboxHandler> logger, int mailboxId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Check mailbox state exception.\r\n{error}")]
    public static partial void ErrorMailboxHandlerCheckState(this ILogger<MailboxHandler> logger, string error);
}
