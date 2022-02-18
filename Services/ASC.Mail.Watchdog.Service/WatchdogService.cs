
using ASC.Common;
using ASC.Common.Logging;
using ASC.Mail.Configuration;
using ASC.Mail.Core.Engine;
using ASC.Mail.Core.Utils;

using Microsoft.Extensions.Options;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ASC.Mail.Watchdog.Service
{
    [Singletone]
    public class WatchdogService
    {
        private ILog Log { get; }

        private Timer WorkTimer;
        private MailboxEngine MailboxEngine { get; }

        readonly TimeSpan Interval;
        readonly TimeSpan TasksTimeoutInterval;

        public WatchdogService(
            IOptionsMonitor<ILog> options,
            MailboxEngine mailboxEngine,
            MailSettings settings,
            NlogCongigure mailLogCongigure)
        {
            mailLogCongigure.Configure();

            Log = options.Get("ASC.Mail.WatchdogService");
            MailboxEngine = mailboxEngine;

            Interval = TimeSpan.FromMinutes(Convert.ToInt32(settings.Watchdog.TimerIntervalInMinutes));
            TasksTimeoutInterval = TimeSpan.FromMinutes(Convert.ToInt32(settings.Watchdog.TasksTimeoutInMinutes));

            Log.InfoFormat("\r\nConfiguration:\r\n" +
                      "\t- check locked mailboxes in every {0} minutes;\r\n" +
                      "\t- locked mailboxes timeout {1} minutes;\r\n",
                      Interval.TotalMinutes,
                      TasksTimeoutInterval.TotalMinutes);
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

            Log.Info("Try stop service...");

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

                Log.Info($"ReleaseLockedMailboxes(timeout is {TasksTimeoutInterval.TotalMinutes} minutes)");

                var freeMailboxIds = MailboxEngine.ReleaseMailboxes((int)TasksTimeoutInterval.TotalMinutes);

                if (freeMailboxIds.Any())
                    Log.Info($"Released next locked mailbox's ids: {string.Join(",", freeMailboxIds)}");
                else
                    Log.Info("Nothing to do!");

            }
            catch (Exception ex)
            {
                Log.Error($"IntervalTimer_Elapsed() Exception:\r\n{ex}");
            }
            finally
            {
                Log.Info($"Waiting for {Interval.TotalMinutes} minutes for next check...");
                WorkTimer.Change(Interval, Interval);
            }
        }
    }
}
