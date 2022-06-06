namespace ASC.Mail.Core.Log
{
    internal static partial class MailGarbageEngineLogger
    {
        [LoggerMessage(Level = LogLevel.Debug, Message = "Begin ClearMailGarbage()")]
        public static partial void DebugMailGarbageBegin(this ILogger logger);

        [LoggerMessage(Level = LogLevel.Information, Message = "Wait all tasks to complete")]
        public static partial void InfoMailGarbageWaitTasks(this ILogger logger);

        [LoggerMessage(Level = LogLevel.Error, Message = "{errMsg}")]
        public static partial void ErrorMailGarbage(this ILogger logger, string errMsg);

        [LoggerMessage(Level = LogLevel.Debug, Message = "ClearMailGarbage: IsCancellationRequested. Quit.")]
        public static partial void DebugMailGarbageQuit(this ILogger logger);

        [LoggerMessage(Level = LogLevel.Debug, Message = "End ClearMailGarbage()")]
        public static partial void DebugMailGarbageEnd(this ILogger logger);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Start RemoveUselessMsDomains()")]
        public static partial void DebugMailGarbageStartRemoveDomains(this ILogger logger);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Domain's '{name}' Tenant={tenant} is removed, but it has unremoved server mailboxes (count={count}). Skip it.")]
        public static partial void WarnMailGarbageDomainHasUnremovedMailboxes(this ILogger logger, string name, int tenant, int count);

        [LoggerMessage(Level = LogLevel.Information, Message = "Domain's '{name}' Tenant = {tenant} is removed. Lets remove domain.")]
        public static partial void InfoMailGarbageDomainLetsRemove(this ILogger logger, string name, int tenant);

        [LoggerMessage(Level = LogLevel.Information, Message = "Domain's '{name}' has duplicated entry for another tenant. Remove only current entry.")]
        public static partial void InfoMailGarbageDomainDuplicated(this ILogger logger, string name);

        [LoggerMessage(Level = LogLevel.Error, Message = "RemoveUselessMsDomains failed. Exception: {errMsg}")]
        public static partial void ErrorMailGarbageRemoveDomainFailed(this ILogger logger, string errMsg);

        [LoggerMessage(Level = LogLevel.Debug, Message = "End RemoveUselessMsDomains()")]
        public static partial void DebugMailGarbageEndRemoveDomains(this ILogger logger);

        [LoggerMessage(Level = LogLevel.Error, Message = "GetTenantStatus(tenant='{tenant}') failed. Exception: {errMsg}")]
        public static partial void ErrorMailGarbageGetTenantStatusFailed(this ILogger logger, int tenant, string errMsg);

        [LoggerMessage(Level = LogLevel.Debug, Message = "RemoveDomain. Set current tenant: {tenant}")]
        public static partial void DebugMailGarbageRemoveDomainSetTenant(this ILogger logger, int tenant);

        [LoggerMessage(Level = LogLevel.Debug, Message = "MailGarbageEngine -> RemoveDomain: 1) Delete domain by id {id}...")]
        public static partial void DebugMailGarbageStartDeleteDomain(this ILogger logger, int id);

        [LoggerMessage(Level = LogLevel.Debug, Message = "MailGarbageEngine -> RemoveDomain: 2) Try get server by tenant {tenantId}...")]
        public static partial void DebugMailGarbageTryGetServer(this ILogger logger, int tenantId);

        [LoggerMessage(Level = LogLevel.Debug, Message = "MailGarbageEngine -> RemoveDomain: 3) Successfull init server. " +
                        "\nServer Api |\nPort: {port}\nProtocol: {protocol}\nIP: {ip}\nToken: {token}\nVersion: {version}")]
        public static partial void DebugMailGarbageSuccessfullInitServer(this ILogger logger, int port, string protocol, string ip, string token, string version);

        [LoggerMessage(Level = LogLevel.Error, Message = "RemoveDomainIfUseless(Domain: '{name}', ID='{id}') failed. Exception: {errMsg}")]
        public static partial void ErrorMailGarbageRemoveDomainIfUseless(this ILogger logger, string name, int id, string errMsg);

        [LoggerMessage(Level = LogLevel.Information, Message = "ClearUserMail(userId: '{userId}' tenant: {tenantId})")]
        public static partial void InfoMailGarbageClearUserMail(this ILogger logger, Guid userId, int tenantId);

        [LoggerMessage(Level = LogLevel.Debug, Message = "End Task {taskId} with status = '{taskStatus}'.")]
        public static partial void DebugMailGarbageEndTask(this ILogger logger, int taskId, System.Threading.Tasks.TaskStatus taskStatus);

        [LoggerMessage(Level = LogLevel.Information, Message = "Processing MailboxId = {mailboxId}, email = '{address}', tenant = '{tenantId}', user = '{userId}'")]
        public static partial void InfoMailGarbageProcessingMailbox(this ILogger logger, int mailboxId, string address, int tenantId, string userId);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Mailbox {mailboxId} need remove. Removal started...")]
        public static partial void DebugMailGarbageMailboxNeedRemove(this ILogger logger, int mailboxId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Mailbox {mailboxId} has been marked for deletion. Removal started...")]
        public static partial void InfoMailGarbageMailboxMarkedForDeletion(this ILogger logger, int mailboxId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Mailbox {mailboxId} processing complete.")]
        public static partial void InfoMailGarbageMailboxProcessingComplete(this ILogger logger, int mailboxId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Mailbox {mailboxId} processed with error : {errMsg}")]
        public static partial void InfoMailGarbageMailboxProcessedWithError(this ILogger logger, int mailboxId, string errMsg);

        [LoggerMessage(Level = LogLevel.Information, Message = "Tenant {tenant} isn't in cache")]
        public static partial void InfoMailGarbageTenantIsntInCache(this ILogger logger, int tenant);

        [LoggerMessage(Level = LogLevel.Information, Message = "Tenant {tenant} is in cache")]
        public static partial void InfoMailGarbageTenantIsInCache(this ILogger logger, int tenant);

        [LoggerMessage(Level = LogLevel.Debug, Message = "GetTenantStatus(OverdueDays={overdueDays})")]
        public static partial void DebugMailGarbageGetTenantStatus(this ILogger logger, int? overdueDays);

        [LoggerMessage(Level = LogLevel.Information, Message = "Tenant {tenant} has status '{status}'")]
        public static partial void InfoMailGarbageTenantStatus(this ILogger logger, int tenant, string status);

        [LoggerMessage(Level = LogLevel.Information, Message = "The mailbox {mailboxId} will be deleted")]
        public static partial void InfoMailGarbageMailboxWillBeDeleted(this ILogger logger, int mailboxId);

        [LoggerMessage(Level = LogLevel.Information, Message = "User '{userId}' status is '{status}'")]
        public static partial void InfoMailGarbageUserStatus(this ILogger logger, string userId, string status);

        [LoggerMessage(Level = LogLevel.Information, Message = "RemoveMailboxData(id: {mailboxId} address: {address})")]
        public static partial void InfoMailGarbageRemoveMailboxData(this ILogger logger, int mailboxId, string address);

        [LoggerMessage(Level = LogLevel.Debug, Message = "RemoveMailboxData. Set current tenant: {tenantId}")]
        public static partial void DebugMailGarbageRemoveMailboxDataSetTenant(this ILogger logger, int tenantId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Mailbox is't removed.")]
        public static partial void InfoMailGarbageMaiboxIsntRemove(this ILogger logger);

        [LoggerMessage(Level = LogLevel.Information, Message = "RemoveTeamlabMailbox()")]
        public static partial void InfoMailGarbageRemoveTeamlabMailbox(this ILogger logger);

        [LoggerMessage(Level = LogLevel.Information, Message = "SetMailboxRemoved()")]
        public static partial void InfoMailGarbageSetMailboxRemoved(this ILogger logger);

        [LoggerMessage(Level = LogLevel.Debug, Message = "MailDataStore.GetDataStore(Tenant = {tenantId})")]
        public static partial void DebugMailGarbageGetDataStore(this ILogger logger, int tenantId);

        [LoggerMessage(Level = LogLevel.Debug, Message = "GetMailboxAttachsCount()")]
        public static partial void DebugMailGarbageGetMailboxAttachsCount(this ILogger logger);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Found {count} garbage attachments")]
        public static partial void DebugMailGarbageCountAttachs(this ILogger logger, int count);

        [LoggerMessage(Level = LogLevel.Debug, Message = "GetMailboxAttachsGarbage(limit = {limit})")]
        public static partial void DebugMailGarbageGetAttachsGarbage(this ILogger logger, int? limit);

        [LoggerMessage(Level = LogLevel.Information, Message = "Clearing {count} garbage attachments ({sumCount}/{countAttachs})")]
        public static partial void InfoMailGarbageClearingAttachments(this ILogger logger, int count, int sumCount, int countAttachs);

        [LoggerMessage(Level = LogLevel.Debug, Message = "CleanupMailboxAttachs()")]
        public static partial void DebugMailGarbageCleanupMailboxAttachs(this ILogger logger);

        [LoggerMessage(Level = LogLevel.Debug, Message = "GetMailboxAttachs()")]
        public static partial void DebugMailGarbageGetMailboxAttachs(this ILogger logger);

        [LoggerMessage(Level = LogLevel.Information, Message = "Found {count} garbage attachments ({sumCount}/{countAttachs})")]
        public static partial void InfoMailGarbageFoundAttachments(this ILogger logger, int count, int sumCount, int countAttachs);

        [LoggerMessage(Level = LogLevel.Debug, Message = "GetMailboxMessagesCount()")]
        public static partial void DebugMailGarbageGetMessagesCount(this ILogger logger);

        [LoggerMessage(Level = LogLevel.Information, Message = "Found {count} garbage messages")]
        public static partial void InfoMailGarbageFountCountMsg(this ILogger logger, int count);

        [LoggerMessage(Level = LogLevel.Debug, Message = "GetMailboxMessagesGarbage(limit = {limit})")]
        public static partial void DebugMailGarbageGetMessages(this ILogger logger, int? limit);

        [LoggerMessage(Level = LogLevel.Information, Message = "Clearing {count} garbage messages ({sumCount}/{countMessages})")]
        public static partial void InfoMailGarbageClearingMessages(this ILogger logger, int count, int sumCount, int countMessages);

        [LoggerMessage(Level = LogLevel.Debug, Message = "CleanupMailboxMessages()")]
        public static partial void DebugMailGarbageCleanupMessages(this ILogger logger);

        [LoggerMessage(Level = LogLevel.Debug, Message = "GetMailboxMessages()")]
        public static partial void DebugMailGarbageGetMessages(this ILogger logger);

        [LoggerMessage(Level = LogLevel.Information, Message = "Found {count} garbage messages ({sumCount}/{countMessages})")]
        public static partial void InfoMailGarbageFountMessages(this ILogger logger, int count, int sumCount, int countMessages);

        [LoggerMessage(Level = LogLevel.Debug, Message = "ClearMailboxData()")]
        public static partial void DebugMailGarbageClearMailboxData(this ILogger logger);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Garbage mailbox '{address}' was totaly removed.")]
        public static partial void DebugMailGarbageMailboxWasRemoved(this ILogger logger, string address);

        [LoggerMessage(Level = LogLevel.Error, Message = "Error(mailboxId = {mailboxId}) Failure\r\nException: {errMsg}")]
        public static partial void ErrorMailGarbage(this ILogger logger, int mailboxId, string errMsg);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Removing file: {path}")]
        public static partial void DebugMailGarbageRemovingFile(this ILogger logger, string path);

        [LoggerMessage(Level = LogLevel.Information, Message = "File: '{path}' removed successfully")]
        public static partial void InfoMailGarbageFileRemoved(this ILogger logger, string path);

        [LoggerMessage(Level = LogLevel.Warning, Message = "File: {path} not found")]
        public static partial void WarnMailGarbageFileNotFound(this ILogger logger, string path);

        [LoggerMessage(Level = LogLevel.Error, Message = "RemoveFile(path: {path}) failed. Error: {errMsg}")]
        public static partial void ErrorMailGarbageRemoveFile(this ILogger logger, string path, string errMsg);

        [LoggerMessage(Level = LogLevel.Information, Message = "RemoveUserMailDirectory(Path: {userMailDir}, Tenant = {tenant} User = '{userId}')")]
        public static partial void InfoMailGarbageRemoveUserMailDirectory(this ILogger logger, string userMailDir, int tenant, string userId);

        [LoggerMessage(Level = LogLevel.Error, Message = "MailDataStore.DeleteDirectory(path: {userMailDir}) failed. Error: {errMsg}")]
        public static partial void ErrorMailGarbageDeleteDirectory(this ILogger logger, string userMailDir, string errMsg);

        [LoggerMessage(Level = LogLevel.Debug, Message = "RemoveTeamlabMailbox. Set current tenant: {tenantId}")]
        public static partial void DebugMailGarbageRemoveTLMailboxDataSetTenant(this ILogger logger, int tenantId);

        [LoggerMessage(Level = LogLevel.Error, Message = "RemoveTeamlabMailbox(mailboxId = {mailboxId}) Failure\r\nException: {errMsg}")]
        public static partial void ErrorMailGarbageRemoveTLMailbox(this ILogger logger, int mailboxId, string errMsg);

        [LoggerMessage(Level = LogLevel.Error, Message = "RemoveUserFolders() Failure\r\nException: {errMsg}")]
        public static partial void ErrorMailGarbageRemoveUserFolders(this ILogger logger, string errMsg);

        [LoggerMessage(Level = LogLevel.Information, Message = "There are no user's mailboxes for deletion")]
        public static partial void InfoMailGarbageNoUsersForDeletion(this ILogger logger);

        [LoggerMessage(Level = LogLevel.Error, Message = "RemoveMailboxData(MailboxId: {mailboxId}) failed. Error: {errMsg}")]
        public static partial void ErrorMailGarbageRemoveMailboxData(this ILogger logger, int mailboxId, string errMsg);
    }
}
