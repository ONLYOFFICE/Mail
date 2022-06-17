namespace ASC.Mail.Core.Log;

internal static partial class ContactEngineLogger
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "IndexEngine -> SaveContactCard")]
    public static partial void DebugContactEngineSaveContact(this ILogger<ContactEngine> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "IndexEngine -> UpdateContactCard()")]
    public static partial void DebugContactEngineUpdateContact(this ILogger<ContactEngine> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "IndexEngine -> RemoveContacts()")]
    public static partial void DebugContactEngineRemoveContacts(this ILogger<ContactEngine> logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "{error}")]
    public static partial void ErrorContactEngineError(this ILogger<ContactEngine> logger, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "SearchEmails (term = '{term}'): {seconds} sec / {count} items")]
    public static partial void DebugContactEngineSearchEmails(this ILogger<ContactEngine> logger, string term, double seconds, int count);
}
