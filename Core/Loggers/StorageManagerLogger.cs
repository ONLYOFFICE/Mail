namespace ASC.Mail.Core.Loggers;

internal static partial class StorageManagerLogger
{
    [LoggerMessage(Level = LogLevel.Information, Message = "ChangeSignatureEditorImagesLinks() Original image link: {link}")]
    public static partial void InfoStorageManagerOriginalImageLink(this ILogger logger, string link);

    [LoggerMessage(Level = LogLevel.Error, Message = "ChangeSignatureEditorImagesLinks() failed with exception: {error}")]
    public static partial void ErrorStorageManagerChangeSignature(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "StoreCKeditorImageWithoutQuota(). filename: {filename} Exception:\r\n{error}\r\n")]
    public static partial void ErrorStorageManagerStoreCKeditor(this ILogger logger, string filename, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "StoreAttachmentWithoutQuota(). filename: {filename}, ctype: {contentType} Exception:\r\n{error}\r\n")]
    public static partial void ErrorStorageManagerStoreAttachment(this ILogger logger, string filename, string contentType, string error);
}
