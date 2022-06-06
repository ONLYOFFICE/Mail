namespace ASC.Mail.Core.Log
{
    internal static partial class FolderEngineLogger
    {
        [LoggerMessage(Level = LogLevel.Error, Message = "ChangeFolderCounters() Exception: {errMsg}")]
        public static partial void ErrorFolderEngineChangeFolderCounters(this ILogger logger, string errMsg);
    }
}
