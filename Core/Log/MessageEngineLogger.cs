using FolderType = ASC.Mail.Enums.FolderType;

namespace ASC.Mail.Core.Log;

internal static partial class MessageEngineLogger
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Exception when SetFolder: {userFolderId}, type {folderType}\n{error}")]
    public static partial void ErrorMessageEngineSetFolder(this ILogger<MessageEngine> logger, int? userFolderId, FolderType folderType, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Exception when commit SetFolder: {userFolderId}, type {folderType}\n{error}")]
    public static partial void ErrorMessageEngineCommitSetFolder(this ILogger<MessageEngine> logger, int? userFolderId, FolderType folderType, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "MailSave() tenant='{tenantId}', user_id='{userId}', email='{email}', from='{from}', id_mail='{mailId}'")]
    public static partial void DebugMessageEngineMailSave(this ILogger<MessageEngine> logger, int tenantId, string userId, string email, string from, int mailId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "DetectChainId() params tenant={tenantId}, user_id='{userId}', mailbox_id={mailboxId}, mime_message_id='{mimeMessageId}' Exception:\r\n{error}")]
    public static partial void WarnMessageEngineDetectChain(this ILogger<MessageEngine> logger, int tenantId, string userId, int mailboxId, string mimeMessageId, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "DetectChainId() tenant='{tenantId}', user_id='{userId}', mailbox_id='{mailboxId}', mime_message_id='{mimeMessageId}' Result: {chainId}")]
    public static partial void DebugMessageEngineDetectChain(this ILogger<MessageEngine> logger, int tenantId, string userId, int mailboxId, string mimeMessageId, string chainId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "UpdateChain() row deleted from chain table tenant='{tenantId}', user_id='{userId}', id_mailbox='{mailboxId}', folder='{folder}', chain_id='{chainId}' result={result}")]
    public static partial void DebugMessageEngineUpdateChainRowDeleted(this ILogger<MessageEngine> logger, int tenantId, string userId, int mailboxId, FolderType folder, string chainId, int result);

    [LoggerMessage(Level = LogLevel.Debug, Message = "UpdateChain() row inserted to chain table tenant='{tenantId}', user_id='{userId}', id_mailbox='{mailboxId}', folder='{folder}', chain_id='{chainId}'")]
    public static partial void DebugMessageEngineUpdateChainRowInserted(this ILogger<MessageEngine> logger, int tenantId, string userId, int mailboxId, FolderType folder, string chainId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Mail -> GetConversation(id={id})->Elapsed {milliseconds}ms (NeedProxyHttp={needProxy}, NeedSanitizer={needSanitize})")]
    public static partial void DebugMessageEngineGetConversation(this ILogger<MessageEngine> logger, int id, double milliseconds, bool needProxy, bool? needSanitize);

    [LoggerMessage(Level = LogLevel.Information, Message = "Original file id: {fileId}")]
    public static partial void InfoMessageEngineOriginalFileId(this ILogger<MessageEngine> logger, string fileId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Original file name: {title}")]
    public static partial void InfoMessageEngineOriginalFileName(this ILogger<MessageEngine> logger, string title);

    [LoggerMessage(Level = LogLevel.Information, Message = "File converted type: {type}")]
    public static partial void InfoMessageEngineFileConvertedType(this ILogger<MessageEngine> logger, string type);

    [LoggerMessage(Level = LogLevel.Information, Message = "Changed file name - {fileName} for file {fileId}:")]
    public static partial void InfoMessageEngineChangeFileName(this ILogger<MessageEngine> logger, string fileName, string fileId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Attached attachment: ID - {fileId}, Name - {fileName}, StoredUrl - {fileUrl}")]
    public static partial void InfoMessageEngineAttachedAttachment(this ILogger<MessageEngine> logger, int fileId, string fileName, string fileUrl);

    [LoggerMessage(Level = LogLevel.Debug, Message = "StoreAttachmentCopy() tenant='{tenantId}', user_id='{userId}', stream_id='{streamId}', new_s3_key='{newS3Key}', copy_s3_url='{copyS3Url}', storedFileUrl='{storedFileUrl}', filename='{fileName}'")]
    public static partial void DebugMessageEngineStoreAttachmentCopy(this ILogger<MessageEngine> logger, int tenantId, string userId, string streamId, string newS3Key, Uri copyS3Url, string storedFileUrl, string fileName);

    [LoggerMessage(Level = LogLevel.Error, Message = "CopyAttachment(). filename='{fileName}', ctype='{contentType}' Exception:\r\n{error}\r\n")]
    public static partial void ErrorMessageEngineCopyAttachment(this ILogger<MessageEngine> logger, string fileName, string contentType, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "[Failed] StoreAttachments(mailboxId={mailboxId}). All message attachments were deleted.")]
    public static partial void InfoMessageEngineStoreAttachmentsFailed(this ILogger<MessageEngine> logger, int mailboxId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Mail -> GetMailInfo(id={mailId}) -> Start Body Load. tenant: {tenantId}, user: '{user}', key='{key}'")]
    public static partial void DebugMessageEngineStartBodyLoad(this ILogger<MessageEngine> logger, int mailId, int tenantId, string user, string key);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Mail -> GetMailInfo(id={mailId}) -> Start Sanitize Body. tenant: {tenantId}, user: '{user}', BodyLength: {length} bytes")]
    public static partial void DebugMessageEngineStartSanitizeBody(this ILogger<MessageEngine> logger, int mailId, int tenantId, string user, int length);

    [LoggerMessage(Level = LogLevel.Error, Message = "Mail -> GetMailInfo(tenant={tenantId} user=\"{user}\" messageId={mailId} key=\"{key}\"). Ex = {error}")]
    public static partial void ErrorMessageEngineGetMailInfo(this ILogger<MessageEngine> logger, int tenantId, string user, int mailId, string key, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Mail -> GetMailInfo(id={mailId}) -> Elapsed: BodyLoad={swtGetBodyMilliseconds}ms, Sanitaze={swtSanitazeilliseconds}ms (NeedSanitizer={needSanitizer}, NeedProxyHttp={needProxyHttp})")]
    public static partial void DebugMessageEngineGetMailInfoElapsed(this ILogger<MessageEngine> logger, int mailId, double swtGetBodyMilliseconds, double swtSanitazeilliseconds, bool needSanitizer, bool needProxyHttp);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Mail -> GetMailInfo(id={mailId}) -> Elapsed [BodyLoadFailed]: BodyLoad={swtGetBodyMilliseconds}ms, Sanitaze={swtSanitazeilliseconds}ms (NeedSanitizer={needSanitizer}, NeedProxyHttp={needProxyHttp})")]
    public static partial void DebugMessageEngineGetMailInfoElapsedBodyLoadFailed(this ILogger<MessageEngine> logger, int mailId, double swtGetBodyMilliseconds, double swtSanitazeilliseconds, bool needSanitizer, bool needProxyHttp);

}
