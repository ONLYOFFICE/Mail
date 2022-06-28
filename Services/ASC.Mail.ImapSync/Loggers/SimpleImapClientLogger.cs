namespace ASC.Mail.ImapSync.Loggers;

internal static partial class SimpleImapClientLogger
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "CertificateValidationCallback(). Certificate callback: {subject}.")]
    public static partial void DebugSimpleImapClientCertificateCallback(this ILogger logger, string subject);

    [LoggerMessage(Level = LogLevel.Debug, Message = "CertificateValidationCallback(). No Ssl policy errors...")]
    public static partial void DebugSimpleImapClientNoSslErrors(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "ExecuteUserAction: Destination ({destination}) didn't found.")]
    public static partial void ErrorSimpleImapClientExecuteUserActionDest(this ILogger logger, int destination);

    [LoggerMessage(Level = LogLevel.Error, Message = "ExecuteUserAction exception: {error}")]
    public static partial void ErrorSimpleImapClientExecuteUserAction(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Try reconnect to IMAP...")]
    public static partial void InfoSimpleImapClientReconnectToIMAP(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Authenticate: {name}")]
    public static partial void InfoSimpleImapClientAuth(this ILogger logger, string name);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Connect to {server}:{port}, {secureSocketOptions})")]
    public static partial void DebugSimpleImapConnectTo(this ILogger logger, string server, int port, string secureSocketOptions);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Imap.EnableUTF8")]
    public static partial void DebugSimpleImapEnableUTF8(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Imap.Authentication({account})")]
    public static partial void DebugSimpleImapAuth(this ILogger logger, string account);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Imap.AuthenticationByOAuth({account})")]
    public static partial void DebugSimpleImapAuthByOAuth(this ILogger logger, string account);

    [LoggerMessage(Level = LogLevel.Debug, Message = "IMAP: logged in.")]
    public static partial void DebugSimpleImapLoggedIn(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{message}")]
    public static partial void WarnSimpleImap(this ILogger logger, string message);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Load folders from IMAP.")]
    public static partial void DebugSimpleImapLoadFolders(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Find {count} folders in IMAP.")]
    public static partial void DebugSimpleImapLoadFoldersCount(this ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "GetImapSubFolders: {folderName} Exception: {error}")]
    public static partial void ErrorSimpleImapGetSubFolders(this ILogger logger, string folderName, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "SetNewImapWorkFolder {folderName}: {error}")]
    public static partial void ErrorSimpleImapSetNewWorkFolder(this ILogger logger, string folderName, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "SetNewImapWorkFolder: Work folder changed to {folderName}.")]
    public static partial void DebugSimpleImapSetNewWorkFolder(this ILogger logger, string folderName);

    [LoggerMessage(Level = LogLevel.Error, Message = "UpdateMessagesList: Try fetch messages from IMAP folder={folderName}: {error}.")]
    public static partial void ErrorSimpleImapUpdateMessagesList(this ILogger logger, string folderName, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "UpdateMessagesList: Load {count} messages from IMAP.")]
    public static partial void DebugSimpleImapLoadCountMessages(this ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "UpdateMessagesList: Delete message detect. Uid= {uniqueId} DBid={messageInDb} IMAPIndex={index}.")]
    public static partial void DebugSimpleImapDeleteMessageDetect(this ILogger logger, string uniqueId, int messageInDb, int index);

    [LoggerMessage(Level = LogLevel.Debug, Message = "UpdateMessagesList: Change IMAP index. Uid= {uniqueId} DBid={messageInDb} IMAPIndex={oldIndex}->{newIndex}.")]
    public static partial void DebugSimpleImapChangeIMAPIndex(this ILogger logger, string uniqueId, int messageInDb, int oldIndex, int newIndex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "GetNewMessage task run: UniqueId={uniqueId}.")]
    public static partial void DebugSimpleImapGetNewMessageTaskRun(this ILogger logger, string uniqueId);

    [LoggerMessage(Level = LogLevel.Error, Message = "GetNewMessage: Try fetch one mimeMessage from imap with UniqueId={uniqueId}: {error}.")]
    public static partial void ErrorSimpleImapGetNewMessageTryFetchMime(this ILogger logger, string uniqueId, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "MoveMessageInImap: Bad parametrs. Source={sourceFolder}, Destination={destinationFolder}.")]
    public static partial void DebugSimpleImapBadParametrs(this ILogger logger, string sourceFolder, string destinationFolder);

    [LoggerMessage(Level = LogLevel.Debug, Message = "MoveMessageInImap: Source={sourceFolder}, Destination={destinationFolder}, Count={count}.")]
    public static partial void DebugSimpleImapMoveMessageInImap(this ILogger logger, string sourceFolder, string destinationFolder, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "MoveMessageInImap: {error}")]
    public static partial void ErrorSimpleImapMoveMessageInImap(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "SetFlagsInImap task run: In {folder} set {action} for {count} messages.")]
    public static partial void DebugSimpleImapSetFlags(this ILogger logger, string folder, string action, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "SetMessageFlagIMAP -> {folder}, {action} -> {error}")]
    public static partial void ErrorSimpleImapSetFlags(this ILogger logger, string folder, string action, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "CompareImapFlags: No flags in MessageDescriptor.")]
    public static partial void ErrorSimpleImapCompareImapFlagsNoFlags(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "CompareImapFlags: flag is equal.")]
    public static partial void DebugSimpleImapCompareImapFlagsEqual(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "CompareImapFlags: Old flags=({oldFlags}). New flags {newFlags}.")]
    public static partial void DebugSimpleImapCompareImapFlagsNewOld(this ILogger logger, string oldFlags, string newFlags);

    [LoggerMessage(Level = LogLevel.Error, Message = "CompareImapFlags Uidl={uniqueId} exception: {error}")]
    public static partial void ErrorSimpleImapCompareImapFlags(this ILogger logger, string uniqueId, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Task manager: {error}")]
    public static partial void ErrorSimpleImapTaskManager(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "TaskManager Cancellation Requested, folder {fullName}.")]
    public static partial void DebugSimpleImapTaskManagerCancellationRequested(this ILogger logger, string fullName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AddTask exception: {error}")]
    public static partial void WarnSimpleImapAddTask(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dispose exception: {error}")]
    public static partial void WarnSimpleImapDispose(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Stop exception: {error}")]
    public static partial void WarnSimpleImapStop(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "LoadFoldersFromIMAP -> Detect folder {folderName}.")]
    public static partial void DebugSimpleImapDetectFolder(this ILogger logger, string folderName);
}
