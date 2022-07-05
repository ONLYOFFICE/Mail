namespace ASC.Mail.Core.Loggers;

internal static partial class MessageEngineLogger
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Exception when SetFolder: {userFolderId}, type {folderType}\n{error}")]
    public static partial void ErrorMessageEngineSetFolder(this ILogger logger, int? userFolderId, string folderType, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Exception when commit SetFolder: {userFolderId}, type {folderType}\n{error}")]
    public static partial void ErrorMessageEngineCommitSetFolder(this ILogger logger, int? userFolderId, string folderType, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "MailSave() tenant='{tenantId}', user_id='{userId}', email='{email}', from='{from}', id_mail='{mailId}'")]
    public static partial void DebugMessageEngineMailSave(this ILogger logger, int tenantId, string userId, string email, string from, int mailId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "DetectChainId() params tenant={tenantId}, user_id='{userId}', mailbox_id={mailboxId}, mime_message_id='{mimeMessageId}' Exception:\r\n{error}")]
    public static partial void WarnMessageEngineDetectChain(this ILogger logger, int tenantId, string userId, int mailboxId, string mimeMessageId, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "DetectChainId() tenant='{tenantId}', user_id='{userId}', mailbox_id='{mailboxId}', mime_message_id='{mimeMessageId}' Result: {chainId}")]
    public static partial void DebugMessageEngineDetectChain(this ILogger logger, int tenantId, string userId, int mailboxId, string mimeMessageId, string chainId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "UpdateChain() row deleted from chain table tenant='{tenantId}', user_id='{userId}', id_mailbox='{mailboxId}', folder='{folder}', chain_id='{chainId}' result={result}")]
    public static partial void DebugMessageEngineUpdateChainRowDeleted(this ILogger logger, int tenantId, string userId, int mailboxId, string folder, string chainId, int result);

    [LoggerMessage(Level = LogLevel.Debug, Message = "UpdateChain() row inserted to chain table tenant='{tenantId}', user_id='{userId}', id_mailbox='{mailboxId}', folder='{folder}', chain_id='{chainId}'")]
    public static partial void DebugMessageEngineUpdateChainRowInserted(this ILogger logger, int tenantId, string userId, int mailboxId, string folder, string chainId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Mail -> GetConversation(id={id})->Elapsed {milliseconds}ms (NeedProxyHttp={needProxy}, NeedSanitizer={needSanitize})")]
    public static partial void DebugMessageEngineGetConversation(this ILogger logger, int id, double milliseconds, bool needProxy, bool? needSanitize);

    [LoggerMessage(Level = LogLevel.Information, Message = "Original file id: {fileId}")]
    public static partial void InfoMessageEngineOriginalFileId(this ILogger logger, string fileId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Original file name: {title}")]
    public static partial void InfoMessageEngineOriginalFileName(this ILogger logger, string title);

    [LoggerMessage(Level = LogLevel.Information, Message = "File converted type: {type}")]
    public static partial void InfoMessageEngineFileConvertedType(this ILogger logger, string type);

    [LoggerMessage(Level = LogLevel.Information, Message = "Changed file name - {fileName} for file {fileId}:")]
    public static partial void InfoMessageEngineChangeFileName(this ILogger logger, string fileName, string fileId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Attached attachment: ID - {fileId}, Name - {fileName}, StoredUrl - {fileUrl}")]
    public static partial void InfoMessageEngineAttachedAttachment(this ILogger logger, int fileId, string fileName, string fileUrl);

    [LoggerMessage(Level = LogLevel.Debug, Message = "StoreAttachmentCopy() tenant='{tenantId}', user_id='{userId}', stream_id='{streamId}', new_s3_key='{newS3Key}', copy_s3_url='{copyS3Url}', storedFileUrl='{storedFileUrl}', filename='{fileName}'")]
    public static partial void DebugMessageEngineStoreAttachmentCopy(this ILogger logger, int tenantId, string userId, string streamId, string newS3Key, Uri copyS3Url, string storedFileUrl, string fileName);

    [LoggerMessage(Level = LogLevel.Error, Message = "CopyAttachment(). filename='{fileName}', ctype='{contentType}' Exception:\r\n{error}\r\n")]
    public static partial void ErrorMessageEngineCopyAttachment(this ILogger logger, string fileName, string contentType, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "[Failed] StoreAttachments(mailboxId={mailboxId}). All message attachments were deleted.")]
    public static partial void InfoMessageEngineStoreAttachmentsFailed(this ILogger logger, int mailboxId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Mail -> GetMailInfo(id={mailId}) -> Start Body Load. tenant: {tenantId}, user: '{user}', key='{key}'")]
    public static partial void DebugMessageEngineStartBodyLoad(this ILogger logger, int mailId, int tenantId, string user, string key);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Mail -> GetMailInfo(id={mailId}) -> Start Sanitize Body. tenant: {tenantId}, user: '{user}', BodyLength: {length} bytes")]
    public static partial void DebugMessageEngineStartSanitizeBody(this ILogger logger, int mailId, int tenantId, string user, int length);

    [LoggerMessage(Level = LogLevel.Error, Message = "Mail -> GetMailInfo(tenant={tenantId} user=\"{user}\" messageId={mailId} key=\"{key}\"). Ex = {error}")]
    public static partial void ErrorMessageEngineGetMailInfo(this ILogger logger, int tenantId, string user, int mailId, string key, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Mail -> GetMailInfo(id={mailId}) -> Elapsed: BodyLoad={swtGetBodyMilliseconds}ms, Sanitaze={swtSanitazeilliseconds}ms (NeedSanitizer={needSanitizer}, NeedProxyHttp={needProxyHttp})")]
    public static partial void DebugMessageEngineGetMailInfoElapsed(this ILogger logger, int mailId, double swtGetBodyMilliseconds, double swtSanitazeilliseconds, bool needSanitizer, bool needProxyHttp);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Mail -> GetMailInfo(id={mailId}) -> Elapsed [BodyLoadFailed]: BodyLoad={swtGetBodyMilliseconds}ms, Sanitaze={swtSanitazeilliseconds}ms (NeedSanitizer={needSanitizer}, NeedProxyHttp={needProxyHttp})")]
    public static partial void DebugMessageEngineGetMailInfoElapsedBodyLoadFailed(this ILogger logger, int mailId, double swtGetBodyMilliseconds, double swtSanitazeilliseconds, bool needSanitizer, bool needProxyHttp);

    [LoggerMessage(Level = LogLevel.Debug, Message = "GetOrCreateTags()")]
    public static partial void DebugMessageEngineGetOrCreateTags(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "UpdateExistingMessages(md5 = {md5})")]
    public static partial void DebugMessageEngineUpdateExistingMessages(this ILogger logger, string md5);

    [LoggerMessage(Level = LogLevel.Debug, Message = "DetectChainId(md5 = {md5}))")]
    public static partial void DebugMessageEngineDetectChainId(this ILogger logger, string md5);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Convert MimeMessage -> MailMessage (md5 = {md5})")]
    public static partial void DebugMessageEngineConvertMimeMessage(this ILogger logger, string md5);

    [LoggerMessage(Level = LogLevel.Debug, Message = "TryStoreMailData(md5 = {md5})")]
    public static partial void DebugMessageEngineTryStoreMailData(this ILogger logger, string md5);

    [LoggerMessage(Level = LogLevel.Debug, Message = "MailSave(md5 = {md5})")]
    public static partial void DebugMessageEngineMailSaveMd(this ILogger logger, string md5);

    [LoggerMessage(Level = LogLevel.Information, Message = "Problem with mail proccessing(Account: {address}). Body and attachment have been deleted")]
    public static partial void InfoMessageEngineProblemWithMailProccessing(this ILogger logger, string address);

    [LoggerMessage(Level = LogLevel.Debug, Message = "StoreMailBody() tenant='{tenantId}', user_id='{userId}', save_body_path='{savePath}' Result: {response}")]
    public static partial void DebugMessageEngineStoreMailBody(this ILogger logger, int tenantId, string userId, string savePath, string response);

    [LoggerMessage(Level = LogLevel.Error, Message = "StoreMailBody() Problems with message saving in messageId={messageId}. \r\n Exception: \r\n {error}\r\n")]
    public static partial void ErrorMessageEngineStoreMailBody(this ILogger logger, string messageId, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[DEADLOCK] MailSave() try again (attempt {attempt}/2)")]
    public static partial void WarnMessageEngineDeadlockSave(this ILogger logger, int attempt);

    [LoggerMessage(Level = LogLevel.Error, Message = "TrySaveMail Exception:\r\n{error}\r\n")]
    public static partial void ErrorMessageEngineTrySaveMail(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "StoreAttachments()")]
    public static partial void DebugMessageEngineStoreAttachments(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "MailMessage.ReplaceEmbeddedImages()")]
    public static partial void DebugMessageEngineReplaceEmbeddedImages(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "StoreMailBody()")]
    public static partial void DebugMessageEngineStoreBody(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "TryStoreMailData(Account:{address}): Exception:\r\n{error}\r\n")]
    public static partial void ErrorMessageEngineStoreMailData(this ILogger logger, string address, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Problems with mail_directory deleting. Account: {address}. Folder: {tenantId}/{userId}/{streamId}. Exception: {error}")]
    public static partial void ErrorMessageEngineMailDirectoryDeleting(this ILogger logger, string address, int tenantId, string userId, string streamId, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Message already exists and it was removed from portal. (md5 = {md5})")]
    public static partial void InfoMessageEngineMessageAlreadyExists(this ILogger logger, string md5);

    [LoggerMessage(Level = LogLevel.Information, Message = "Message already exists: mailId = {mailId}. Clone")]
    public static partial void InfoMessageEngineMessageClone(this ILogger logger, int mailId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Message already exists by MD5|MimeMessageId|Subject|DateSent")]
    public static partial void InfoMessageEngineMessageExists(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Message already exists: mailId = {mailId} (md5 = {md5}). Outbox clone")]
    public static partial void InfoMessageEngineMessageOutboxClone(this ILogger logger, int mailId, string md5);

    [LoggerMessage(Level = LogLevel.Information, Message = "Message already exists: mailId = {mailId} (md5 = {md5}). It was moved to spam on server")]
    public static partial void InfoMessageEngineMessageMovedToSpam(this ILogger logger, int mailId, string md5);

    [LoggerMessage(Level = LogLevel.Information, Message = "Message already exists: mailId = {mailId} (md5 = {md5}). Full clone")]
    public static partial void InfoMessageEngineMessageFullClone(this ILogger logger, int mailId, string md5);

    [LoggerMessage(Level = LogLevel.Error, Message = "Convert MimeMessage -> MailMessage: Exception: {error}")]
    public static partial void ErrorMessageEngineConvertMimeMessage(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Creating fake message with original MimeMessage in attachments")]
    public static partial void DebugMessageEngineCreatingFakeMessage(this ILogger logger);
}
