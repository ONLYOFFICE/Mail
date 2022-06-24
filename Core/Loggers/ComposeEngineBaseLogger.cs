namespace ASC.Mail.Core.Loggers;

internal static partial class ComposeEngineBaseLogger
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Clearing temp storage failed with exception: {error}")]
    public static partial void ErrorComposeEngineClearingTempStorage(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "ChangeEmbededAttachmentLinksForStoring() failed with exception: {error}")]
    public static partial void ErrorComposeEngineChangeLinks(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected Error in Send() Id = {messageId}\r\nException: {error}")]
    public static partial void ErrorDraftEngineSend(this ILogger logger, int messageId, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Mail -> Send failed: Exception: {error}")]
    public static partial void ErrorDraftEngineSendFailed(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Problem with attach ICAL to message. mailId={id} Exception:\r\n{error}\r\n")]
    public static partial void WarnDraftEngineAttachICALToMessage(this ILogger logger, int id, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error with wcf signalrServiceClient: {message}, {errorTrace}")]
    public static partial void ErrorDraftEngineWcfSignalr(this ILogger logger, string message, string errorTrace);

    [LoggerMessage(Level = LogLevel.Error, Message = "AddNotificationAlertToMailbox() in MailboxId={mailboxId} failed with exception:\r\n{error}")]
    public static partial void ErrorDraftEngineAlertToMailbox(this ILogger logger, int mailboxId, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "LoadLinkData(url = '{link}')")]
    public static partial void ErrorStorageManagerLoadLinkData(this ILogger logger, string link);

    [LoggerMessage(Level = LogLevel.Error, Message = "ChangeUrlProxyLinks(): Exception: {error}")]
    public static partial void ErrorMailboxExtensionsChangeUrlProxyLinks(this ILogger logger, string error);
}
