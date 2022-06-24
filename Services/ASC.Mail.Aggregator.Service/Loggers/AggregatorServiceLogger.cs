namespace ASC.Mail.Aggregator.Loggers;

internal static partial class AggregatorServiceLogger
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Service is ready.")]
    public static partial void InfoAggServReady(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Found {count} tasks to release")]
    public static partial void InfoAggServTasksToRelease(this ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Aggregator work: IsCancellationRequested. Quit.")]
    public static partial void DebugAggServCancellationRequested(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Task.WaitAny timeout. Tasks count = {count}\r\nTasks:\r\n{tasks}")]
    public static partial void InfoAggServTaskWaitAnyTimeout(this ILogger logger, int count, string tasks);

    [LoggerMessage(Level = LogLevel.Information, Message = "Need free next tasks = {count}: ({tasks})")]
    public static partial void InfoAggServNeedFreeNextTasks(this ILogger logger, int count, string tasks);

    [LoggerMessage(Level = LogLevel.Information, Message = "Total tasks count = {count} ({tasks}).")]
    public static partial void InfoAggServTotalTasks(this ILogger logger, int count, string tasks);

    [LoggerMessage(Level = LogLevel.Information, Message = "All mailboxes were processed. Go back to timer.")]
    public static partial void InfoAggServAllMailboxesWereProcessed(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Execution was canceled.")]
    public static partial void InfoAggServExecutionWasCanceled(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Aggregator work exception:\r\n{error}\r\n")]
    public static partial void ErrorAggServWorkException(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Setup Work timer to {seconds} seconds")]
    public static partial void DebugAggServSetupWorkTimer(this ILogger logger, double seconds);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Setup Work timer to Timeout.Infinite")]
    public static partial void DebugAggServSetupWorkTimerInfinite(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Create tasks (need {count}).")]
    public static partial void InfoAggServNeedCreateTasks(this ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created {count} tasks.")]
    public static partial void InfoAggServCreatedTasks(this ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "No more mailboxes for processing.")]
    public static partial void InfoAggServNoMailboxes(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "End Task {taskId} with status = '{status}'.")]
    public static partial void DebugAggServEndTask(this ILogger logger, int taskId, string status);

    [LoggerMessage(Level = LogLevel.Error, Message = "Task not exists in tasks array.")]
    public static partial void ErrorAggServTaskNotExists(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "FreeTask(Id: {mailboxId}, Email: {address}):\r\nException:{error}\r\n")]
    public static partial void ErrorAggServFreeTask(this ILogger logger, int mailboxId, string address, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Try forget Filters for user failed")]
    public static partial void ErrorAggServForgetFilters(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "|{threadId}|{duration}|{method}|{status}|{mailboxId}|{address}")]
    public static partial void DebugStatistic(this ILogger logger, int threadId, double duration, string method, string status, int mailboxId, string address);
}
