namespace ASC.Mail.Core.Log;

internal static partial class MailOperationLogger
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Mail operation error -> Remove user folder: {error}")]
    public static partial void ErrorMailOperationRemoveUserFolder(this ILogger<MailOperation> logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error processing: {progressState}. Exception: {error}")]
    public static partial void ErrorMailOperationProcessing(this ILogger<MailOperation> logger, string progressState, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Mail operation error -> Domain '{domainName}' dns check failed. Error: {error}")]
    public static partial void ErrorMailOperationDomainDnsCheckFailed(this ILogger<MailOperation> logger, string domainName, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "{error}")]
    public static partial void ErrorMailOperation(this ILogger<MailOperation> logger, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Zipped archive has been stored to {path}")]
    public static partial void DebugMailOperationArchiveStored(this ILogger<MailOperation> logger, Uri path);

    [LoggerMessage(Level = LogLevel.Error, Message = "Mail operation error -> Download all attachments: {error}")]
    public static partial void ErrorMailOperationDownloadAttachments(this ILogger<MailOperation> logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Mail operation error -> Recalculate folders: {error}")]
    public static partial void ErrorMailOperationRecalculateFolders(this ILogger<MailOperation> logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Mail operation error -> Remove mailbox: {error}")]
    public static partial void ErrorMailOperationRemoveMailbox(this ILogger<MailOperation> logger, string error);
}
