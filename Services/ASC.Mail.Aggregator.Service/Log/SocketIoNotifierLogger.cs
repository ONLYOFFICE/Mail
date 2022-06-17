namespace ASC.Mail.Aggregator.Service.Log;

internal static partial class SocketIoNotifierLogger
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "No items, waiting.")]
    public static partial void DebugSocketIoNotifierNoItems(this ILogger<SocketIoNotifier> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Waking up...")]
    public static partial void DebugSocketIoNotifierWakingUp(this ILogger<SocketIoNotifier> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "SignalrWorker -> SendUnreadUser(UserId = {userId} TenantId = {tenantId})")]
    public static partial void DebugSocketIoNotifierSendUnreadUser(this ILogger<SocketIoNotifier> logger, string userId, int tenantId);

    [LoggerMessage(Level = LogLevel.Error, Message = "SignalrWorker -> SendUnreadUser(UserId = {userId} TenantId = {tenantId})\r\nException: \r\n{error}")]
    public static partial void ErrorSocketIoNotifierSendUnreadUser(this ILogger<SocketIoNotifier> logger, string userId, int tenantId, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stop SignalrWorker.")]
    public static partial void InfoSocketIoNotifierStop(this ILogger<SocketIoNotifier> logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "SignalrWorker busy, cancellation of the task.")]
    public static partial void InfoSocketIoNotifierBusy(this ILogger<SocketIoNotifier> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "SignalrWorker -> SendUnreadUser(). Try set tenant |{tenantId}| for user |{userId}|...")]
    public static partial void DebugSocketIoNotifierTrySetTenant(this ILogger<SocketIoNotifier> logger, int tenantId, string userId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "SignalrWorker -> SendUnreadUser(). Now current tennant = {tenantId}")]
    public static partial void DebugSocketIoNotifierCurrentTenant(this ILogger<SocketIoNotifier> logger, int tenantId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "SignalrWorker -> SendUnreadUser(). SendUnreadUser start")]
    public static partial void DebugSocketIoNotifierSendStart(this ILogger<SocketIoNotifier> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "SignalrWorker -> userID == LostUserID")]
    public static partial void DebugSocketIoNotifierLostUser(this ILogger<SocketIoNotifier> logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "SignalrWorker -> Unknown Error. {0}, {1}")]
    public static partial void ErrorSocketIoNotifier(this ILogger<SocketIoNotifier> logger, string error, string innerError);
}
