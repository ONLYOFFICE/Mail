namespace ASC.Mail.Core.Loggers;

internal static partial class QuotaEngineLogger
{
    [LoggerMessage(Level = LogLevel.Error, Message = "QuotaUsedAdd with params: tenant={tenantId}, used_quota={usedQuota}. Ex = {error}")]
    public static partial void ErrorQuotaEngineQuotaUsedAdd(this ILogger logger, int tenantId, long usedQuota, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "QuotaUsedDelete with params: tenant={tenantId}, used_quota={usedQuota}. Ex = {error}")]
    public static partial void ErrorQuotaEngineQuotaUsedDelete(this ILogger logger, int tenantId, long usedQuota, string error);
}
