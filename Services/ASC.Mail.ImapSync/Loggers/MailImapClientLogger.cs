namespace ASC.Mail.ImapSync.Loggers;

internal static partial class MailImapClientLogger
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "|{threadId}|{duration}|{method}|{status}|{mailboxId}|{address}")]
    public static partial void DebugStatistic(this ILogger logger, int threadId, double duration, string method, bool status, int mailboxId, string address);

    [LoggerMessage(Level = LogLevel.Error, Message = "CheckRedis error: {error}.")]
    public static partial void ErrorMailImapClientCheckRedis(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "CheckRedis: {iterationCount} keys readed. User have {count} clients")]
    public static partial void DebugMailImapClientCheckRedis(this ILogger logger, int iterationCount, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "GetUserMailBoxes exception: {error}")]
    public static partial void ErrorMailImapClientGetUserMailBoxes(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "CreateSimpleImapClients: MailboxId={mailboxId} created and {isLockerd}.")]
    public static partial void DebugMailImapClientCreateSimpleImapClients(this ILogger logger, int mailboxId, string isLockerd);

    [LoggerMessage(Level = LogLevel.Error, Message = "CreateSimpleImapClients exception: {error}")]
    public static partial void ErrorMailImapClientCreateSimpleImapClients(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "CreateSimpleImapClient {mailboxName}.{folderName} exception: {error}")]
    public static partial void ErrorMailImapClientCreateSimpleImapClient(this ILogger logger, string mailboxName, string folderName, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "DeleteSimpleImapClients: {count} clients with MailboxId={mailboxId} removed and {isLocked}.")]
    public static partial void DebugMailImapClientDeleteSimpleImapClients(this ILogger logger, int count, int mailboxId, string isLocked);

    [LoggerMessage(Level = LogLevel.Error, Message = "DeleteSimpleImapClient exception: {error}")]
    public static partial void ErrorMailImapClientDeleteSimpleImapClients(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ProcessActionFromImapTimer_Elapsed Action {folderAction} complete with result {result} for {count} messages.")]
    public static partial void DebugMailImapClientProcessAction(this ILogger logger, string folderAction, string result, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ProcessActionFromImapTimer_Elapsed ids: {ids}")]
    public static partial void DebugMailImapClientProcessActionIds(this ILogger logger, string ids);

    [LoggerMessage(Level = LogLevel.Error, Message = "ProcessActionFromImap exception: {error}")]
    public static partial void ErrorMailImapClientProcessAction(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "IAliveTimer. No user online.")]
    public static partial void DebugMailImapClientNoUserOnline(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ImapClient_NewActionFromImap: imapActionsQueue.Count={count}. Action={action}")]
    public static partial void DebugMailImapClientNewActionFromImap(this ILogger logger, int count, string action);

    [LoggerMessage(Level = LogLevel.Error, Message = "SetMailboxAuthError(Tenant = {tenantId}, MailboxId = {mailboxId}, Address = '{address}') Exception: {error}")]
    public static partial void ErrorMailImapClientMailboxAuth(this ILogger logger, int tenantId, int mailboxId, string address, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "UpdateDbFolder: ImapMessagesList==null.")]
    public static partial void DebugMailImapClientUpdateDbFolder(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "UpdateDbFolder: simpleImapClient.WorkFolderMails.Count={count}.")]
    public static partial void DebugMailImapClientUpdateDbFolderMailsCount(this ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "UpdateDbFolder: imap_message_Uidl={id}.")]
    public static partial void DebugMailImapClientUpdateDbFolderMessageUidl(this ILogger logger, uint id);

    [LoggerMessage(Level = LogLevel.Debug, Message = "UpdateDbFolder: imap_message_Uidl={uidl} not found in DB.")]
    public static partial void DebugMailImapClientUpdateDbFolderMessageUidlNotFound(this ILogger logger, string uidl);

    [LoggerMessage(Level = LogLevel.Error, Message = "UpdateDbFolder {folderName} exception {error}.")]
    public static partial void ErrorMailImapClientUpdateDbFolder(this ILogger logger, string folderName, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "SetMessageFlagsFromImap: imap_message_Uidl={id}, flag={messageFlag}." +
        "\nSetMessageFlagsFromImap: db_message={uidl}, folder={folder}, IsRemoved={isRemoved}.")]
    public static partial void DebugMailImapClientSetMessageFlagsFromImap(this ILogger logger, uint id, string messageFlag, string uidl, string folder, bool isRemoved);

    [LoggerMessage(Level = LogLevel.Error, Message = "SetMessageFlagsFromImap: {error}")]
    public static partial void ErrorMailImapClientSetMessageFlagsFromImap(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "NewMessage: Folder={folderName} Uidl={uniqueId}.")]
    public static partial void DebugMailImapClientNewMessage(this ILogger logger, string folderName, string uniqueId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Get message (UIDL: '{uidl}', MailboxId = {mailboxId}, Address = '{address}')")]
    public static partial void InfoMailImapClientGetMessage(this ILogger logger, string uidl, int mailboxId, string address);

    [LoggerMessage(Level = LogLevel.Debug, Message = "CreateMessageInDB: failed.")]
    public static partial void DebugMailImapClientCreateMessageInDBFailed(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Message saved (id: {messageId}, From: '{from}', Subject: '{subject}', Unread: {isNew})")]
    public static partial void InfoMailImapClientMessageSaved(this ILogger logger, int messageId, string from, string subject, bool isNew);

    [LoggerMessage(Level = LogLevel.Information, Message = "Message updated (id: {messageId}, Folder: '{from}'), Subject: '{subject}'")]
    public static partial void InfoMailImapClientMessageUpdated(this ILogger logger, int messageId, string from, string subject);

    [LoggerMessage(Level = LogLevel.Error, Message = "CreateMessageInDB:{error}")]
    public static partial void ErrorMailImapClientCreateMessageInDB(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "CreateMessageInDB time={milliseconds} ms.")]
    public static partial void DebugMailImapClientCreateMessageInDB(this ILogger logger, double milliseconds);

    [LoggerMessage(Level = LogLevel.Error, Message = "SendUnreadUser error {error}. Inner error: {innerError}.")]
    public static partial void ErrorMailImapClientSendUnreadUser(this ILogger logger, string error, string innerError);

    [LoggerMessage(Level = LogLevel.Debug, Message = "DoOptionalOperations -> GetOrCreateTags()")]
    public static partial void DebugMailImapClientGetOrCreateTags(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "DoOptionalOperations -> IsCrmAvailable()")]
    public static partial void DebugMailImapClientIsCrmAvailable(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "DoOptionalOperations -> GetCrmTags()")]
    public static partial void DebugMailImapClientGetCrmTags(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "DoOptionalOperations -> AddMessageToIndex()")]
    public static partial void DebugMailImapClientAddMessageToIndex(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "DoOptionalOperations -> SetMessagesTag(tagId: {tagId})")]
    public static partial void DebugMailImapClientSetMessagesTag(this ILogger logger, int tagId);

    [LoggerMessage(Level = LogLevel.Error, Message = "SetMessagesTag(tenant={tenantId}, userId='{userName}', messageId={messageId}, tagid = {tagIds})\r\nException:{error}\r\n")]
    public static partial void ErrorMailImapClientSetMessagesTag(this ILogger logger, int tenantId, string userName, int messageId, string tagIds, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "DoOptionalOperations -> AddRelationshipEventForLinkedAccounts()")]
    public static partial void DebugMailImapClientAddRelationshipEvent(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "DoOptionalOperations -> SaveEmailInData()")]
    public static partial void DebugMailImapClientSaveEmailInData(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "DoOptionalOperations -> SendAutoreply()")]
    public static partial void DebugMailImapClientSendAutoreply(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "DoOptionalOperations -> UploadIcsToCalendar()")]
    public static partial void DebugMailImapClientUploadIcsToCalendar(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "DoOptionalOperations -> StoreMailEml()")]
    public static partial void DebugMailImapClientStoreMailEml(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "DoOptionalOperations -> ApplyFilters()")]
    public static partial void DebugMailImapClientApplyFilters(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "DoOptionalOperations() ->\r\nException:{error}\r\n")]
    public static partial void ErrorMailImapClientDoOptionalOperations(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "StoreMailEml: Tenant = {tenantId}, UserId = {userId}, SaveEmlPath = {path}. Result: {result}")]
    public static partial void InfoMailImapClientStoreMailEml(this ILogger logger, int tenantId, string userId, string path, string result);

    [LoggerMessage(Level = LogLevel.Error, Message = "StoreMailEml exception: {error}")]
    public static partial void ErrorMailImapClientStoreMailEml(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Stop exception: {error}")]
    public static partial void ErrorMailImapClientStop(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Dispose")]
    public static partial void InfoMailImapClientDispose(this ILogger logger);
}
