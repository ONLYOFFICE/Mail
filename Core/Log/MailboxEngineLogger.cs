namespace ASC.Mail.Core.Log
{
    internal static partial class MailboxEngineLogger
    {
        [LoggerMessage(Level = LogLevel.Warning, Message = "Mailbox id = {mailboxId} is not well-formated.")]
        public static partial void WarnMailboxEngineNotWellFormated(this ILogger logger, int mailboxId);

        [LoggerMessage(Level = LogLevel.Error, Message = "TryGetNextMailboxData failed. {errMsg}")]
        public static partial void ErrorMailboxEngineGetMailboxData(this ILogger logger, string errMsg);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Address: {address} | Id: {id} | IsEnabled: {enabled} | IsRemoved: {removed} | Tenant: {tenantId} | Id: {userId}")]
        public static partial void DebugMailboxEngineGetMailboxes(this ILogger logger, string address, int id, bool enabled, bool removed, int tenantId, string userId);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Free quota size: {size}")]
        public static partial void DebugMailboxEngineFreeQuota(this ILogger logger, long size);

        [LoggerMessage(Level = LogLevel.Debug, Message = "RemoveMailboxInfo. Set current tenant: {id}")]
        public static partial void DebugMailboxEngineRemoveMailboxTenant(this ILogger logger, int id);

        [LoggerMessage(Level = LogLevel.Debug, Message = "GetActiveMailboxForProcessing()")]
        public static partial void DebugMailboxEngineGetActiveMailbox(this ILogger logger);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Found {count} active tasks")]
        public static partial void DebugMailboxEngineFoundedTasks(this ILogger logger, int count);

        [LoggerMessage(Level = LogLevel.Debug, Message = "GetInactiveMailboxForProcessing()")]
        public static partial void DebugMailboxEngineGetInactiveMailbox(this ILogger logger);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Found {count} inactive tasks")]
        public static partial void DebugMailboxEngineFoundedInactiveTasks(this ILogger logger, int count);
    }
}
