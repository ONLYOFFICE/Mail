namespace ASC.Mail.Core.Log;

internal static partial class MailboxEngineLogger
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Mailbox id = {mailboxId} is not well-formated.")]
    public static partial void WarnMailboxEngineNotWellFormated(this ILogger<MailboxEngine> logger, int mailboxId);

    [LoggerMessage(Level = LogLevel.Error, Message = "TryGetNextMailboxData failed. {error}")]
    public static partial void ErrorMailboxEngineGetMailboxData(this ILogger<MailboxEngine> logger, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Address: {address} | Id: {id} | IsEnabled: {enabled} | IsRemoved: {removed} | Tenant: {tenantId} | Id: {userId}")]
    public static partial void DebugMailboxEngineGetMailboxes(this ILogger<MailboxEngine> logger, string address, int id, bool enabled, bool removed, int tenantId, string userId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Free quota size: {size}")]
    public static partial void DebugMailboxEngineFreeQuota(this ILogger<MailboxEngine> logger, long size);

    [LoggerMessage(Level = LogLevel.Debug, Message = "RemoveMailboxInfo. Set current tenant: {id}")]
    public static partial void DebugMailboxEngineRemoveMailboxTenant(this ILogger<MailboxEngine> logger, int id);

    [LoggerMessage(Level = LogLevel.Debug, Message = "GetActiveMailboxForProcessing()")]
    public static partial void DebugMailboxEngineGetActiveMailbox(this ILogger<MailboxEngine> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Found {count} active tasks")]
    public static partial void DebugMailboxEngineFoundedTasks(this ILogger<MailboxEngine> logger, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "GetInactiveMailboxForProcessing()")]
    public static partial void DebugMailboxEngineGetInactiveMailbox(this ILogger<MailboxEngine> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Found {count} inactive tasks")]
    public static partial void DebugMailboxEngineFoundedInactiveTasks(this ILogger<MailboxEngine> logger, int count);
}
