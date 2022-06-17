namespace ASC.Mail.Core.Log;

internal static partial class IndexEngineLogger
{
    [LoggerMessage(Level = LogLevel.Information, Message = "[SKIP INDEX] \"${indexName}\". Support == false")]
    public static partial void InfoIndexEngineSupportFalse(this ILogger<IndexEngine> logger, string indexName);

    [LoggerMessage(Level = LogLevel.Information, Message = "[SKIP INDEX] IsIndexAvailable -> FactoryIndexer.CheckState(false) == false")]
    public static partial void InfoIndexEngineCheckStateFalse(this ILogger<IndexEngine> logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "IndexEngine -> Add <{type}> (mail Id = {dataId}) success")]
    public static partial void InfoIndexEngineAddSuccess(this ILogger<IndexEngine> logger, Type type, int dataId);

    [LoggerMessage(Level = LogLevel.Error, Message = "IndexEngine -> Add <{type}> (mail Id = {dataId}) error: {error}")]
    public static partial void ErrorIndexEngineAdd(this ILogger<IndexEngine> logger, Type type, int dataId, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "InitDocument FileNotFoundException. {error}")]
    public static partial void ErrorIndexEngineFileNotFound(this ILogger<IndexEngine> logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "InitDocument. {error}")]
    public static partial void ErrorIndexEngine(this ILogger<IndexEngine> logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "IndexEngine -> Update(count = {count}) error: {error}")]
    public static partial void ErrorIndexEngineUpdate(this ILogger<IndexEngine> logger, int count, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "IndexEngine -> Update() error: {error}")]
    public static partial void ErrorIndexEngineUpdate(this ILogger<IndexEngine> logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "IndexEngine -> Update <{type}> (mail Id = {count}) error: {error}")]
    public static partial void ErrorIndexEngineUpdate(this ILogger<IndexEngine> logger, Type type, int count, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "IndexEngine -> Remove(count = {count}) error: {error}")]
    public static partial void ErrorIndexEngineRemoveIds(this ILogger<IndexEngine> logger, int count, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "IndexEngine -> Remove(mailboxId = {mailBoxId}) error: {error}")]
    public static partial void ErrorIndexEngineRemoveId(this ILogger<IndexEngine> logger, int mailBoxId, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "IndexEngine -> RemoveContacts(count = {count}) error: {error}")]
    public static partial void ErrorIndexEngineRemoveContacts(this ILogger<IndexEngine> logger, int count, string error);
}
