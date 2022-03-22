namespace ASC.Mail.StorageCleaner.Service;

[Singletone]
public class StorageCleanerService
{
    private readonly ILog _log;
    internal Timer WorkTimer { get; private set; }
    readonly TimeSpan TsInterval;
    private MailGarbageEngine Eraser { get; set; }

    public StorageCleanerService(
        IOptionsMonitor<ILog> options,
        MailGarbageEngine mailGarbageEngine,
        MailSettings settings,
        NlogCongigure mailLogCongigure)
    {
        mailLogCongigure.Configure();

        _log = options.Get("ASC.Mail.Cleaner");
        Eraser = mailGarbageEngine;
        TsInterval = TimeSpan.FromMinutes(Convert.ToInt32(settings.Cleaner.TimerWaitMinutes));

        _log.InfoFormat("Service will clear mail storage every {0} minutes\r\n", TsInterval.TotalMinutes);
    }

    internal Task StartTimer(CancellationToken token, bool immediately = false)
    {
        if (WorkTimer == null)
            WorkTimer = new Timer(WorkTimerElapsed, token, Timeout.Infinite, Timeout.Infinite);

        if (immediately)
        {
            _log.Debug("Setup WorkTimer to start immediately");

            WorkTimer.Change(0, Timeout.Infinite);
        }
        else
        {
            _log.Debug($"Setup WorkTimer to {TsInterval.TotalMinutes} minutes");

            WorkTimer.Change(TsInterval, TsInterval);
        }

        return Task.CompletedTask;
    }

    private void StopTimer()
    {
        if (WorkTimer == null)
            return;

        _log.Debug("Setup WorkTimer to Timeout.Infinite");

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
        _log.Debug("Timer -> WorkTimerElapsed");

        var cancelToken = state as CancellationToken? ?? new CancellationToken();

        try
        {
            Eraser.ClearMailGarbage(cancelToken);

            _log.InfoFormat("All mailboxes were processed. Go back to timer. Next start after {0} minutes.\r\n",
                TsInterval.TotalMinutes);

        }
        catch (Exception ex)
        {
            if (ex is AggregateException)
            {
                ex = ((AggregateException)ex).GetBaseException();
            }

            if (ex is TaskCanceledException || ex is OperationCanceledException)
            {
                _log.Info("Execution was canceled.");

                return;
            }

            _log.ErrorFormat("Timer -> WorkTimerElapsed. Exception:\r\n{0}\r\n", ex.ToString());
        }

        StartTimer(cancelToken);
    }
}
