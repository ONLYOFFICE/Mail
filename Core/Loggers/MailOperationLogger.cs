namespace ASC.Mail.Core.Loggers;

internal static partial class MailOperationLogger
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Mail operation error -> Remove user folder: {error}")]
    public static partial void ErrorMailOperationRemoveUserFolder(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error processing: {progressState}. Exception: {error}")]
    public static partial void ErrorMailOperationProcessing(this ILogger logger, string progressState, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Mail operation error -> Domain '{domainName}' dns check failed. Error: {error}")]
    public static partial void ErrorMailOperationDomainDnsCheckFailed(this ILogger logger, string domainName, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "{error}")]
    public static partial void ErrorMailOperation(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Zipped archive has been stored to {path}")]
    public static partial void DebugMailOperationArchiveStored(this ILogger logger, Uri path);

    [LoggerMessage(Level = LogLevel.Error, Message = "Mail operation error -> Download all attachments: {error}")]
    public static partial void ErrorMailOperationDownloadAttachments(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Mail operation error -> Recalculate folders: {error}")]
    public static partial void ErrorMailOperationRecalculateFolders(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Mail operation error -> Remove mailbox: {error}")]
    public static partial void ErrorMailOperationRemoveMailbox(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "{error}")]
    public static partial void ErrorMailOperationAuthorizing(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "TenantQuotaException. {error}")]
    public static partial void ErrorMailOperationTenantQuota(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "FormatException error. {error}")]
    public static partial void ErrorMailOperationFormat(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Internal server error. {error}")]
    public static partial void ErrorMailOperationServer(this ILogger logger, string error);
}
