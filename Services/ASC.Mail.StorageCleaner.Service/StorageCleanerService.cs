using ASC.Mail.StorageCleaner.Loggers;

using Microsoft.Extensions.Logging;

namespace ASC.Mail.StorageCleaner.Service;

[Singletone]
public class StorageCleanerService
{
    private readonly ILogger _log;
    internal Timer WorkTimer { get; private set; }
    readonly TimeSpan TsInterval;
    private MailGarbageEngine Eraser { get; set; }

    public StorageCleanerService(
        ILoggerProvider loggerProvider,
        MailGarbageEngine mailGarbageEngine,
        MailSettings settings)
    {

        _log = loggerProvider.CreateLogger("ASC.Mail.Cleaner");
        Eraser = mailGarbageEngine;
        TsInterval = TimeSpan.FromMinutes(Convert.ToInt32(settings.Cleaner.TimerWaitMinutes));

        _log.InfoStorageCleanerServiceCleaningInterval(TsInterval.TotalMinutes);
    }

    internal Task StartTimer(CancellationToken token, bool immediately = false)
    {
        if (WorkTimer == null)
            WorkTimer = new Timer(WorkTimerElapsed, token, Timeout.Infinite, Timeout.Infinite);

        if (immediately)
        {
            _log.DebugStorageCleanerServiceStartImmediately();

            WorkTimer.Change(0, Timeout.Infinite);
        }
        else
        {
            _log.DebugStorageCleanerServiceWorkTimer(TsInterval.TotalMinutes);

            WorkTimer.Change(TsInterval, TsInterval);
        }

        return Task.CompletedTask;
    }

    private void StopTimer()
    {
        if (WorkTimer == null)
            return;

        _log.DebugStorageCleanerServiceWorkTimerInfinite();

        WorkTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    internal void StopService(CancellationTokenSource tokenSource, ManualResetEvent resetEvent)
    {
        if (tokenSource != null)
            tokenSource.Cancel();

        StopTimer();

        if (WorkTimer != null)
        {
            WorkTimer.Dispose();
            WorkTimer = null;
        }

        if (resetEvent != null)
            resetEvent.Set();

        if (Eraser != null)
        {
            Eraser.Dispose();
            Eraser = null;
        }
    }

    private void WorkTimerElapsed(object state)
    {
        _log.DebugStorageCleanerServiceWorkTimerElapsed();

        var cancelToken = state as CancellationToken? ?? new CancellationToken();

        try
        {
            Eraser.ClearMailGarbage(cancelToken);

            _log.InfoStorageCleanerServiceNextStart(TsInterval.TotalMinutes);

        }
        catch (Exception ex)
        {
            if (ex is AggregateException)
            {
                ex = ((AggregateException)ex).GetBaseException();
            }

            if (ex is TaskCanceledException || ex is OperationCanceledException)
            {
                _log.InfoStorageCleanerServiceWasCanceled();

                return;
            }

            _log.ErrorStorageCleanerServiceWorkTimer(ex.ToString());
        }

        StartTimer(cancelToken);
    }
}
