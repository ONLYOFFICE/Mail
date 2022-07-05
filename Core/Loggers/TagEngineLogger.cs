namespace ASC.Mail.Core.Loggers;

internal static partial class TagEngineLogger
{
    [LoggerMessage(Level = LogLevel.Information, Message = "TagEngine -> GetOrCreateTags(): new tag '{name}' with id = {id} has been created")]
    public static partial void InfoTagEngineTagCreated(this ILogger logger, string name, int id);

    [LoggerMessage(Level = LogLevel.Information, Message = "TagEngine -> SetMessagesTag(): tag with id = {idTag} has bee added to messages [{ids}]")]
    public static partial void InfoTagEngineTagAdded(this ILogger logger, int idTag, string ids);
}
