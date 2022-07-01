namespace ASC.Mail.Core.Loggers;

internal static partial class BaseOAuth2AuthorizationLogger
{
    [LoggerMessage(Level = LogLevel.Error, Message = "RequestAccessToken() Exception:\r\n{error}\r\n")]
    public static partial void ErrorBaseOAuth2Authorization(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "GoogleOAuth2Authorization() Exception:\r\n{error}\r\n")]
    public static partial void ErrorBaseOAuth2AuthorizationGoogleOAuth(this ILogger logger, string error);
}
