namespace ASC.Mail.Core.Log;

internal static partial class AccountEngineLogger
{
    [LoggerMessage(Level = LogLevel.Error, Message = "GetMailServerMxDomain() failed.")]
    public static partial void ErrorGetMailServerMxDomain(this ILogger<AccountEngine> logger, Exception exception);
}
