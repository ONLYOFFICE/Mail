namespace ASC.Mail.Watchdog.Service;

[Singletone]
public class WatchdogService
{
    private readonly ILog _log;
    private readonly MailboxEngine _mailboxEngine;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _tasksTimeoutInterval;

    private Timer WorkTimer;

    public WatchdogService(
        IOptionsMonitor<ILog> options,
        MailboxEngine mailboxEngine,
        MailSettings settings)
    {

        _log = options.Get("ASC.Mail.WatchdogService");
        _mailboxEngine = mailboxEngine;

        _interval = TimeSpan.FromMinutes(Convert.ToInt32(settings.Watchdog.TimerIntervalInMinutes));
        _tasksTimeoutInterval = TimeSpan.FromMinutes(Convert.ToInt32(settings.Watchdog.TasksTimeoutInMinutes));

        _log.InfoFormat("\r\nConfiguration:\r\n" +
                  "\t- check locked mailboxes in every {0} minutes;\r\n" +
                  "\t- locked mailboxes timeout {1} minutes;\r\n",
                  _interval.TotalMinutes,
                  _tasksTimeoutInterval.TotalMinutes);
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

        _log.Info("Try stop service...");

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

            _log.Info($"ReleaseLockedMailboxes(timeout is {_tasksTimeoutInterval.TotalMinutes} minutes)");

            var freeMailboxIds = _mailboxEngine.ReleaseMailboxes((int)_tasksTimeoutInterval.TotalMinutes);

            if (freeMailboxIds.Any())
                _log.Info($"Released next locked mailbox's ids: {string.Join(",", freeMailboxIds)}");
            else
                _log.Info("Nothing to do!");

        }
        catch (Exception ex)
        {
            _log.Error($"IntervalTimer_Elapsed() Exception:\r\n{ex}");
        }
        finally
        {
            _log.Info($"Waiting for {_interval.TotalMinutes} minutes for next check...");
            WorkTimer.Change(_interval, _interval);
        }
    }
}
