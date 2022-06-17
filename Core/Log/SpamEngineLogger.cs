namespace ASC.Mail.Core.Log;

internal static partial class SpamEngineLogger
{
    [LoggerMessage(Level = LogLevel.Error, Message = "SendConversationsToSpamTrainer() failed with exception:\r\n{error}")]
    public static partial void ErrorSpamEngineSendConversationsToSpam(this ILogger<SpamEngine> logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "SendEmlUrlsToSpamTrainer: Can't sent task to spam trainer. Empty server api info.")]
    public static partial void ErrorSpamEngineSendEmlUrlsToSpam(this ILogger<SpamEngine> logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "SendEmlUrlsToSpamTrainer() Exception: \r\n {error}")]
    public static partial void ErrorSpamEngineSendEmlUrlsToSpam(this ILogger<SpamEngine> logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "GetMailEmlUrl() tenant='{tenant}', user_id='{user}', save_eml_path='{emlPath}' Exception: {error}")]
    public static partial void ErrorSpamEngineGetMailEmlUrl(this ILogger<SpamEngine> logger, int tenant, string user, string emlPath, string error);
}
