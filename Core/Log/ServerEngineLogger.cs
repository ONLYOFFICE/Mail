namespace ASC.Mail.Core.Log;

internal static partial class ServerEngineLogger
{
    [LoggerMessage(Level = LogLevel.Error, Message = "{error}\n{stackTrace}")]
    public static partial void ErrorServerEngine(this ILogger<ServerEngine> logger, string error, string stackTrace);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ServerEngine -> ClearDomainStorageSpace: Get client URL: {baseUrl}: OK")]
    public static partial void DebugServerEngineGetClientURL(this ILogger<ServerEngine> logger, Uri baseUrl);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ServerEngine -> ClearDomainStorageSpace: Get request: OK")]
    public static partial void DebugServerEngineGetRequest(this ILogger<ServerEngine> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ServerEngine -> ClearDomainStorageSpace: Add Url Segment (domain name: {domain}): OK")]
    public static partial void DebugServerEngineAddUrlSegment(this ILogger<ServerEngine> logger, string domain);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Request resource: {resource}, method: {method}")]
    public static partial void DebugServerEngineRequestResource(this ILogger<ServerEngine> logger, string resource, Method method);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ServerEngine -> ClearDomainStorageSpace: Response was executing. Status code: {code}")]
    public static partial void DebugServerEngineResponseWasExecuting(this ILogger<ServerEngine> logger, HttpStatusCode code);
}
