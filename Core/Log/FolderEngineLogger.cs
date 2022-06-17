namespace ASC.Mail.Core.Log;

internal static partial class FolderEngineLogger
{
    [LoggerMessage(Level = LogLevel.Error, Message = "ChangeFolderCounters() Exception: {error}")]
    public static partial void ErrorFolderEngineChangeFolderCounters(this ILogger<FolderEngine> logger, string error);
}
