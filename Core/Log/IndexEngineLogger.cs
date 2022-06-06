namespace ASC.Mail.Core.Log
{
    internal static partial class IndexEngineLogger
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "[SKIP INDEX] \"${indexName}\". Support == false")]
        public static partial void InfoIndexEngineSupportFalse(this ILogger logger, string indexName);

        [LoggerMessage(Level = LogLevel.Information, Message = "[SKIP INDEX] IsIndexAvailable -> FactoryIndexer.CheckState(false) == false")]
        public static partial void InfoIndexEngineCheckStateFalse(this ILogger logger);

        [LoggerMessage(Level = LogLevel.Information, Message = "IndexEngine -> Add <{type}> (mail Id = {dataId}) success")]
        public static partial void InfoIndexEngineAddSuccess(this ILogger logger, Type type, int dataId);

        [LoggerMessage(Level = LogLevel.Error, Message = "IndexEngine -> Add <{type}> (mail Id = {dataId}) error: {errMsg}")]
        public static partial void ErrorIndexEngineAdd(this ILogger logger, Type type, int dataId, string errMsg);

        [LoggerMessage(Level = LogLevel.Error, Message = "InitDocument FileNotFoundException. {errMsg}")]
        public static partial void ErrorIndexEngineFileNotFound(this ILogger logger, string errMsg);

        [LoggerMessage(Level = LogLevel.Error, Message = "InitDocument. {errMsg}")]
        public static partial void ErrorIndexEngine(this ILogger logger, string errMsg);

        [LoggerMessage(Level = LogLevel.Error, Message = "IndexEngine -> Update(count = {count}) error: {errMsg}")]
        public static partial void ErrorIndexEngineUpdate(this ILogger logger, int count, string errMsg);

        [LoggerMessage(Level = LogLevel.Error, Message = "IndexEngine -> Update() error: {errMsg}")]
        public static partial void ErrorIndexEngineUpdate(this ILogger logger, string errMsg);

        [LoggerMessage(Level = LogLevel.Error, Message = "IndexEngine -> Update <{type}> (mail Id = {count}) error: {errMsg}")]
        public static partial void ErrorIndexEngineUpdate(this ILogger logger, Type type, int count, string errMsg);

        [LoggerMessage(Level = LogLevel.Error, Message = "IndexEngine -> Remove(count = {count}) error: {errMsg}")]
        public static partial void ErrorIndexEngineRemoveIds(this ILogger logger, int count, string errMsg);

        [LoggerMessage(Level = LogLevel.Error, Message = "IndexEngine -> Remove(mailboxId = {mailBoxId}) error: {errMsg}")]
        public static partial void ErrorIndexEngineRemoveId(this ILogger logger, int mailBoxId, string errMsg);

        [LoggerMessage(Level = LogLevel.Error, Message = "IndexEngine -> RemoveContacts(count = {count}) error: {errMsg}")]
        public static partial void ErrorIndexEngineRemoveContacts(this ILogger logger, int count, string errMsg);
    }
}
