namespace ASC.Mail.Core.Loggers;

internal static partial class BaseOAuth2AuthorizationLogger
{
    [LoggerMessage(Level = LogLevel.Error, Message = "RequestAccessToken() Exception:\r\n{error}\r\n")]
    public static partial void ErrorBaseOAuth2Authorization(this ILogger logger, string error);
}
