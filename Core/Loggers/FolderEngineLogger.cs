namespace ASC.Mail.Core.Loggers;

internal static partial class FolderEngineLogger
{
    [LoggerMessage(Level = LogLevel.Error, Message = "ChangeFolderCounters() Exception: {error}")]
    public static partial void ErrorFolderEngineChangeFolderCounters(this ILogger logger, string error);
}
