namespace ASC.Mail.Core.Log;

internal static partial class ApiHelperLogger
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "ApiHelper -> Setup: Tenant={tenantId} User='{userId}' IsAuthenticated={isAuthenticated} Scheme='{scheme}' HttpContext is {httpCon}")]
    public static partial void DebugApiHelperSetup(this ILogger<ApiHelper> logger, int tenantId, Guid userId, bool isAuthenticated, string scheme, string httpCon);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ApiHelper -> Execute: request url: {uri}/{resource}")]
    public static partial void DebugApiHelperExecuteRequest(this ILogger<ApiHelper> logger, Uri uri, string resource);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ApiHelper -> Response status code {statusCode}")]
    public static partial void DebugApiHelperResponseCode(this ILogger<ApiHelper> logger, HttpStatusCode statusCode);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ApiHelper -> Create tariff request...")]
    public static partial void DebugApiHelperCreateTariffRequest(this ILogger<ApiHelper> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ApiHelper -> Execute tariff request...")]
    public static partial void DebugApiHelperExecuteTariffRequest(this ILogger<ApiHelper> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ApiHelper -> HttpStatusCode: PaymentRequired. TariffType: LongDead")]
    public static partial void DebugApiHelperPaymentRequired(this ILogger<ApiHelper> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ApiHelper -> Cannot get tariff by request. Status code: statusCode")]
    public static partial void DebugApiHelperCannotGetTariff(this ILogger<ApiHelper> logger, HttpStatusCode statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Upload ics-file to calendar failed. No count number.")]
    public static partial void WarningUploadIcsFileToCalendar(this ILogger<ApiHelper> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{response}")]
    public static partial void DebugApiHelperResponse(this ILogger<ApiHelper> logger, string response);
}
