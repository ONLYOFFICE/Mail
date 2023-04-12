namespace ASC.Mail.Core.Loggers;

internal static partial class SocketIoNotifierLogger
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "No items, waiting.")]
    public static partial void DebugSocketIoNotifierNoItems(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Waking up...")]
    public static partial void DebugSocketIoNotifierWakingUp(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "SignalrWorker -> SendUnreadUser(UserId = {userId} TenantId = {tenantId}) Result={result}")]
    public static partial void DebugSocketIoNotifierSendUnreadUser(this ILogger logger, string userId, int tenantId, bool result);

    [LoggerMessage(Level = LogLevel.Error, Message = "SignalrWorker -> SendUnreadUser(UserId = {userId} TenantId = {tenantId})\r\nException: \r\n{error}")]
    public static partial void ErrorSocketIoNotifierSendUnreadUser(this ILogger logger, string userId, int tenantId, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stop SignalrWorker.")]
    public static partial void InfoSocketIoNotifierStop(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "SignalrWorker busy, cancellation of the task.")]
    public static partial void InfoSocketIoNotifierBusy(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "SignalrWorker -> SendUnreadUser(). Try set tenant |{tenantId}| for user |{userId}|...")]
    public static partial void DebugSocketIoNotifierTrySetTenant(this ILogger logger, int tenantId, string userId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "SignalrWorker -> SendUnreadUser(). Now current tennant = {tenantId}")]
    public static partial void DebugSocketIoNotifierCurrentTenant(this ILogger logger, int tenantId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "SignalrWorker -> SendUnreadUser(). SendUnreadUser start")]
    public static partial void DebugSocketIoNotifierSendStart(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "SignalrWorker -> userID == LostUserID")]
    public static partial void DebugSocketIoNotifierLostUser(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "SignalrWorker -> Unknown Error. {error}, {innerError}")]
    public static partial void ErrorSocketIoNotifier(this ILogger logger, string error, string innerError);
}
