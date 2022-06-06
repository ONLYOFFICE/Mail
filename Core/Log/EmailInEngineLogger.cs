namespace ASC.Mail.Core.Log
{
    internal static partial class EmailInEngineLogger
    {
        [LoggerMessage(Level = LogLevel.Debug, Message = "SaveEmailInData -> ApiHelper.UploadToDocuments(fileName: '{fileName}', folderId: {eMailInFolder})")]
        public static partial void DebugEmailInEngineUploadToDocuments(this ILogger logger, string fileName, string eMailInFolder);

        [LoggerMessage(Level = LogLevel.Error, Message = "SaveEmailInData(tenant={tenantId}, userId='{userId}', messageId={messageId}) Exception:\r\n{errMsg}\r\n")]
        public static partial void ErrorEmailInEngineSaveEmailInData(this ILogger logger, int tenantId, string userId, int messageId, string errMsg);

        [LoggerMessage(Level = LogLevel.Information, Message = "EmailInEngine -> UploadToDocuments(): file '{fileName}' has been uploaded to document folder '{eMailInFolder}' uploadedFileId = {uploadedFileId}")]
        public static partial void InfoEmailInEngineFileUploaded(this ILogger logger, string fileName, string eMailInFolder, object uploadedFileId);

        [LoggerMessage(Level = LogLevel.Information, Message = "EmailInEngine -> UploadToDocuments() EMailIN folder '{eMailInFolder}' is unreachable. Try to unlink EMailIN...")]
        public static partial void InfoEmailInEngineEmailInIsUnreachable(this ILogger logger, string eMailInFolder);

        [LoggerMessage(Level = LogLevel.Error, Message = "EmailInEngine -> UploadToDocuments(fileName: '{fileName}', folderId: {eMailInFolder}) Exception:\r\n{errMsg}\r\n")]
        public static partial void ErrorEmailInEngineUploadToDocuments(this ILogger logger, string fileName, string eMailInFolder, string errMsg);
    }
}
