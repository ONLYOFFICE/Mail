using static ASC.Mail.DefineConstants;

namespace ASC.Mail.Aggregator.Service.Log;

internal static partial class QueueManagerLogger
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Dump file path: {path}")]
    public static partial void DebugQueueManagerDumpFilePath(this ILogger<QueueManager> logger, string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "GetLockedMailbox() Stored dublicate with id = {id}, address = {address}. Mailbox not added to the queue.")]
    public static partial void ErrorQueueManagerStoredDublicateMailbox(this ILogger<QueueManager> logger, int id, string address);

    [LoggerMessage(Level = LogLevel.Information, Message = "QueueManager -> ReleaseAllProcessingMailboxes()")]
    public static partial void InfoQueueManagerReleaseAllMailboxes(this ILogger<QueueManager> logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "QueueManager -> ReleaseMailbox(Tenant = {tenantId} MailboxId = {mailboxId}, Address = '{address}') mailbox not found")]
    public static partial void WarnQueueManagerReleaseMailboxNotFound(this ILogger<QueueManager> logger, int tenantId, int mailboxId, string address);

    [LoggerMessage(Level = LogLevel.Information, Message = "QueueManager -> ReleaseMailbox(MailboxId = {mailboxId} Address '{address}')")]
    public static partial void InfoQueueManagerReleaseMailbox(this ILogger<QueueManager> logger, int mailboxId, string address);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Mailbox {mailBoxData.MailBoxId} will be realesed...Now remove from locked queue by Id.")]
    public static partial void DebugQueueManagerReleaseMailboxOk(this ILogger<QueueManager> logger, int mailboxId);

    [LoggerMessage(Level = LogLevel.Error, Message = "QueueManager -> ReleaseMailbox(Tenant = {tenantId} MailboxId = {mailboxId}, Address = '{address}')\r\nException: {error} \r\n")]
    public static partial void ErrorQueueManagerReleaseMailbox(this ILogger<QueueManager> logger, int tenantId, int mailboxId, string address, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "QueueManager -> LoadMailboxesFromDump()")]
    public static partial void DebugQueueManagerLoadMailboxesFromDump(this ILogger<QueueManager> logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "QueueManager -> LoadMailboxesFromDump: {error}")]
    public static partial void ErrorQueueManagerLoadMailboxesFromDump(this ILogger<QueueManager> logger, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "QueueManager -> LoadTenantsFromDump()")]
    public static partial void DebugQueueManagerLoadTenantsFromDump(this ILogger<QueueManager> logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "QueueManager -> LoadTenantsFromDump: {error}")]
    public static partial void ErrorQueueManagerLoadTenantsFromDump(this ILogger<QueueManager> logger, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Dump file '{file}' exists, trying delete")]
    public static partial void DebugQueueManagerDumpFileExists(this ILogger<QueueManager> logger, string file);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Dump file '{file}' deleted")]
    public static partial void DebugQueueManagerDumpFileDeleted(this ILogger<QueueManager> logger, string file);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Dump journal file '{file}' exists, trying delete")]
    public static partial void DebugQueueManagerDumpJournalFileExists(this ILogger<QueueManager> logger, string file);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Dump journal file '{file}' deleted")]
    public static partial void DebugQueueManagerDumpFileJournalDeleted(this ILogger<QueueManager> logger, string file);

    [LoggerMessage(Level = LogLevel.Error, Message = "QueueManager -> ReCreateDump() failed Exception: {error}")]
    public static partial void ErrorQueueManagerReCreateDumpFailed(this ILogger<QueueManager> logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "QueueManager -> AddMailboxToDumpDb(Id = {mailboxId}) Exception: {error}")]
    public static partial void ErrorQueueManagerAddMailboxToDumpDb(this ILogger<QueueManager> logger, int mailboxId, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "QueueManager -> DeleteMailboxFromDumpDb(MailboxId = {mailboxId}) Exception: {error}")]
    public static partial void ErrorQueueManagerDeleteMailboxFromDumpDb(this ILogger<QueueManager> logger, int mailboxId, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "QueueManager -> LoadDump() failed Exception: {error}")]
    public static partial void ErrorQueueManagerLoadDumpFailed(this ILogger<QueueManager> logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "QueueManager -> AddTenantToDumpDb(TenantId = {tenantId}) Exception: {error}")]
    public static partial void ErrorQueueManagerAddTenantToDumpDb(this ILogger<QueueManager> logger, int tenantId, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "QueueManager -> DeleteTenantFromDumpDb(TenantId = {tenantId}) Exception: {error}")]
    public static partial void ErrorQueueManagerDeleteTenantFromDump(this ILogger<QueueManager> logger, int tenantId, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "QueueManager -> LoadQueue()\r\nException: \r\n {error}")]
    public static partial void ErrorQueueManagerLoadQueue(this ILogger<QueueManager> logger, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Queue is {queueStr}. Load new queue.")]
    public static partial void DebugQueueManagerLoadQueue(this ILogger<QueueManager> logger, string queueStr);

    [LoggerMessage(Level = LogLevel.Debug, Message = "RemoveFromQueue()")]
    public static partial void DebugQueueManagerRemoveFromQueue(this ILogger<QueueManager> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Next mailbox will be removed from queue: {mailboxId}")]
    public static partial void DebugQueueManagerMailboxWillBeRemoved(this ILogger<QueueManager> logger, int mailboxId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Mailbox with id |{mailboxId}| for user {userId} from tenant {tenantId} was removed from queue")]
    public static partial void DebugQueueManagermailboxwasRemovedFromQueue(this ILogger<QueueManager> logger, int mailboxId, string userId, int tenantId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Tenant {tenantId} isn't in cache")]
    public static partial void DebugQueueManagerTenantIsntInCache(this ILogger<QueueManager> logger, int tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "TryLockMailbox -> Returned tenant {tenantId} status: {type}.")]
    public static partial void InfoQueueManagerReturnedTenantStatus(this ILogger<QueueManager> logger, int tenantId, TariffType type);

    [LoggerMessage(Level = LogLevel.Information, Message = "Tenant {tenantId} is not paid. Disable mailboxes.")]
    public static partial void InfoQueueManagerReturnedTenantLongDead(this ILogger<QueueManager> logger, int tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Tenant {tenantId} is not paid. Stop processing mailboxes.")]
    public static partial void InfoQueueManagerReturnedTenantOverdue(this ILogger<QueueManager> logger, int tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Tenant {0} is paid.")]
    public static partial void InfoQueueManagerReturnedTenantPaid(this ILogger<QueueManager> logger, int tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Cannot get tariff type for {mailboxId} mailbox")]
    public static partial void InfoQueueManagerCannotGetTariffType(this ILogger<QueueManager> logger, int mailboxId);

    [LoggerMessage(Level = LogLevel.Error, Message = "QueueManager -> TryLockMailbox(): GetTariffType \r\nException:{error}\r\n")]
    public static partial void ErrorQueueManagerGetTariffType(this ILogger<QueueManager> logger, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Tenant {tenantId} is in cache")]
    public static partial void DebugQueueManagerTenantIsInCache(this ILogger<QueueManager> logger, int tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User '{userId}' was {userStatus}. Tenant = {tenantId}. Disable mailboxes for user.")]
    public static partial void InfoQueueManagerDisableMailboxesForUser(this ILogger<QueueManager> logger, string userId, string userStatus, int tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Tenant = {tenantId} User = {userId}. Quota is ended.")]
    public static partial void InfoQueueManagerQuotaIsEnded(this ILogger<QueueManager> logger, int tenantId, string userId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "TryLockMailbox {address} (MailboxId: {mailboxId} is {status})")]
    public static partial void DebugQueueManagerTryLockMailbox(this ILogger<QueueManager> logger, string address, int mailboxId, string status);

    [LoggerMessage(Level = LogLevel.Error, Message = "QueueManager -> TryLockMailbox(MailboxId={mailboxId} is {status})\r\nException:{error}\r\n")]
    public static partial void ErrorQueueManagerTryLockMailbox(this ILogger<QueueManager> logger, int mailboxId, string status, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Tenant {tenantId} payment cache is expired.")]
    public static partial void InfoQueueManagerPaymentCacheIsExpired(this ILogger<QueueManager> logger, int tenantId);
}
