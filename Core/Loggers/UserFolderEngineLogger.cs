namespace ASC.Mail.Core.Loggers;

internal static partial class UserFolderEngineLogger
{
    [LoggerMessage(Level = LogLevel.Error, Message = "UserFolderEngine -> SetFolderCounters() Exception: {error}\nStack trace:\n{trace}")]
    public static partial void ErrorUserFolderEngineSetFolderCounters(this ILogger logger, string error, string trace);

    [LoggerMessage(Level = LogLevel.Error, Message = "UserFolderEngine -> ChangeFolderCounters() Exception: {error}")]
    public static partial void ErrorUserFolderEngineChangeFolderCounters(this ILogger logger, string error);
}
