namespace ASC.Mail.Core.Loggers;

internal static partial class SpamEngineLogger
{
    [LoggerMessage(Level = LogLevel.Error, Message = "SendConversationsToSpamTrainer() failed with exception:\r\n{error}")]
    public static partial void ErrorSpamEngineSendConversationsToSpam(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "SendEmlUrlsToSpamTrainer: Can't sent task to spam trainer. Empty server api info.")]
    public static partial void ErrorSpamEngineSendEmlUrlsToSpamEmptyApi(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "SendEmlUrlsToSpamTrainer() Exception: \r\n {error}")]
    public static partial void ErrorSpamEngineSendEmlUrlsToSpam(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "GetMailEmlUrl() tenant='{tenant}', user_id='{user}', save_eml_path='{emlPath}' Exception: {error}")]
    public static partial void ErrorSpamEngineGetMailEmlUrl(this ILogger logger, int tenant, string user, string emlPath, string error);
}
