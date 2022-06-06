namespace ASC.Mail.Core.Log
{
    internal static partial class BaseOAuth2AuthorizationLogger
    {
        [LoggerMessage(Level = LogLevel.Error, Message = "RequestAccessToken() Exception:\r\n{ex}\r\n")]
        public static partial void ErrorBaseOAuth2Authorization(this ILogger logger, Exception ex);
    }
}
