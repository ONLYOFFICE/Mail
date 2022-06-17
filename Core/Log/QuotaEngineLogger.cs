namespace ASC.Mail.Core.Log;

internal static partial class QuotaEngineLogger
{
    [LoggerMessage(Level = LogLevel.Error, Message = "QuotaUsedAdd with params: tenant={tenantId}, used_quota={usedQuota}. Ex = {error}")]
    public static partial void ErrorQuotaEngineQuotaUsedAdd(this ILogger<QuotaEngine> logger, int tenantId, long usedQuota, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "QuotaUsedDelete with params: tenant={tenantId}, used_quota={usedQuota}. Ex = {error}")]
    public static partial void ErrorQuotaEngineQuotaUsedDelete(this ILogger<QuotaEngine> logger, int tenantId, long usedQuota, string error);
}
