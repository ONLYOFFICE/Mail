namespace ASC.Mail.Core.Log;

internal static partial class TagEngineLogger
{
    [LoggerMessage(Level = LogLevel.Information, Message = "TagEngine -> GetOrCreateTags(): new tag '{name}' with id = {id} has been created")]
    public static partial void InfoTagEngineTagCreated(this ILogger<TagEngine> logger, string name, int id);

    [LoggerMessage(Level = LogLevel.Information, Message = "TagEngine -> SetMessagesTag(): tag with id = {idTag} has bee added to messages [{ids}]")]
    public static partial void InfoTagEngineTagAdded(this ILogger<TagEngine> logger, int idTag, string ids);
}
