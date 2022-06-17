namespace ASC.Mail.Core.Log;

internal static partial class UserFolderEngineLogger
{
    [LoggerMessage(Level = LogLevel.Error, Message = "UserFolderEngine -> SetFolderCounters() Exception: {error}\nStack trace:\n{trace}")]
    public static partial void ErrorUserFolderEngineSetFolderCounters(this ILogger<UserFolderEngine> logger, string error, string trace);

    [LoggerMessage(Level = LogLevel.Error, Message = "UserFolderEngine -> ChangeFolderCounters() Exception: {error}")]
    public static partial void ErrorUserFolderEngineChangeFolderCounters(this ILogger<UserFolderEngine> logger, string error);
}
