namespace ASC.Mail.Core.Loggers;

internal static partial class MailBoxSettingEngineLogger
{
    [LoggerMessage(Level = LogLevel.Error, Message = "MxToDomainBusinessVendorsList failed. Ex = {error}")]
    public static partial void ErrorMailBoxSettingEngineMxToDomain(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "SetMailBoxSettings failed. Ex = {error}")]
    public static partial void ErrorMailBoxSettingEngineSetMailBoxSettings(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "SearchBusinessVendorsSettings failed. Ex = {error}")]
    public static partial void ErrorMailBoxSettingEngineSearchBusinessVendorsSettings(this ILogger logger, string error);
}
