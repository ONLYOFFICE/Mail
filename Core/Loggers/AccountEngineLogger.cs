namespace ASC.Mail.Core.Loggers;

internal static partial class AccountEngineLogger
{
    [LoggerMessage(Level = LogLevel.Error, Message = "GetMailServerMxDomain() failed. Exception: {error}")]
    public static partial void ErrorGetMailServerMxDomain(this ILogger logger, string error);
}
