using ASC.Mail.Watchdog.Loggers;

namespace ASC.Mail.Watchdog.Service;

[Singletone]
public class WatchdogService
{
    private readonly ILogger _log;
    private readonly MailboxEngine _mailboxEngine;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _tasksTimeoutInterval;

    private Timer WorkTimer;

    public WatchdogService(
        ILoggerProvider logProvider,
        MailboxEngine mailboxEngine,
        MailSettings settings)
    {

        _log = logProvider.CreateLogger("ASC.Mail.WatchdogService");
        _mailboxEngine = mailboxEngine;

        _interval = TimeSpan.FromMinutes(Convert.ToInt32(settings.Watchdog.TimerIntervalInMinutes));
        _tasksTimeoutInterval = TimeSpan.FromMinutes(Convert.ToInt32(settings.Watchdog.TasksTimeoutInMinutes));

        _log.InfoWatchdogServiceConfiguration(_interval.TotalMinutes, _tasksTimeoutInterval.TotalMinutes);
    }

    internal Task StarService(CancellationToken token)
    {
        if (WorkTimer == null)
            WorkTimer = new Timer(WorkTimerElapsed, token, 0, Timeout.Infinite);

        return Task.CompletedTask;
    }

    internal void StopService(CancellationTokenSource tokenSource)
    {
        if (tokenSource != null) tokenSource.Cancel();

        _log.InfoWatchdogServiceTryStopService();

        if (WorkTimer == null) return;

        WorkTimer.Change(Timeout.Infinite, Timeout.Infinite);
        WorkTimer.Dispose();
        WorkTimer = null;
    }

    private void WorkTimerElapsed(object state)
    {
        try
        {
            WorkTimer.Change(Timeout.Infinite, Timeout.Infinite);

            _log.InfoWatchdogServiceReleaseLockedMailboxes(_tasksTimeoutInterval.TotalMinutes);

            var freeMailboxIds = _mailboxEngine.ReleaseMailboxes((int)_tasksTimeoutInterval.TotalMinutes);

            if (freeMailboxIds.Any())
                _log.InfoWatchdogServiceReleasedMailboxes(string.Join(",", freeMailboxIds));
            else
                _log.InfoWatchdogServiceNothingToDo();

        }
        catch (Exception ex)
        {
            _log.ErrorWatchdogServiceIntervalTimer(ex.ToString());
        }
        finally
        {
            _log.InfoWatchdogServiceWaiting(_interval.TotalMinutes);
            WorkTimer.Change(_interval, _interval);
        }
    }
}
