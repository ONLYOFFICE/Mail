namespace ASC.Mail.Core.Loggers;

internal static partial class ApiHelperLogger
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "ApiHelper -> Setup: Tenant={tenantId} User='{userId}' IsAuthenticated={isAuthenticated} Scheme='{scheme}' HttpContext is {httpCon}")]
    public static partial void DebugApiHelperSetup(this ILogger logger, int tenantId, Guid userId, bool isAuthenticated, string scheme, string httpCon);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ApiHelper -> Execute: request url: {uri}/{resource}")]
    public static partial void DebugApiHelperExecuteRequest(this ILogger logger, Uri uri, string resource);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ApiHelper -> Response status code {statusCode}")]
    public static partial void DebugApiHelperResponseCode(this ILogger logger, HttpStatusCode statusCode);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ApiHelper -> Create tariff request...")]
    public static partial void DebugApiHelperCreateTariffRequest(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ApiHelper -> Execute tariff request...")]
    public static partial void DebugApiHelperExecuteTariffRequest(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ApiHelper -> HttpStatusCode: PaymentRequired. TariffType: LongDead")]
    public static partial void DebugApiHelperPaymentRequired(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ApiHelper -> Cannot get tariff by request. Status code: statusCode")]
    public static partial void DebugApiHelperCannotGetTariff(this ILogger logger, HttpStatusCode statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Upload ics-file to calendar failed. No count number.")]
    public static partial void WarningUploadIcsFileToCalendar(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{response}")]
    public static partial void DebugApiHelperResponse(this ILogger logger, string response);
}
