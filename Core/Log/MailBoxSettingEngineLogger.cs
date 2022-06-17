namespace ASC.Mail.Core.Log;

internal static partial class MailBoxSettingEngineLogger
{
    [LoggerMessage(Level = LogLevel.Error, Message = "MxToDomainBusinessVendorsList failed. Ex = {error}")]
    public static partial void ErrorMailBoxSettingEngineMxToDomain(this ILogger<MailBoxSettingEngine> logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "SetMailBoxSettings failed. Ex = {error}")]
    public static partial void ErrorMailBoxSettingEngineSetMailBoxSettings(this ILogger<MailBoxSettingEngine> logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "SearchBusinessVendorsSettings failed. Ex = {error}")]
    public static partial void ErrorMailBoxSettingEngineSearchBusinessVendorsSettings(this ILogger<MailBoxSettingEngine> logger, string error);
}
