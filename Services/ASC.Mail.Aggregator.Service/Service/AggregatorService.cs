using ASC.Common;
using ASC.Common.Logging;
using ASC.Common.Utils;
using ASC.Mail.Aggregator.Service.Console;
using ASC.Mail.Aggregator.Service.Queue;
using ASC.Mail.Aggregator.Service.Queue.Data;
using ASC.Mail.Configuration;
using ASC.Mail.Core;
using ASC.Mail.Models;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

using NLog;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace ASC.Mail.Aggregator.Service.Service
{
    [Singletone]
    public class AggregatorService
    {
        public const string ASC_MAIL_COLLECTION_SERVICE_NAME = "ASC Mail Collection Service";
        private const string S_FAIL = "error";
        private const string S_OK = "success";
        private const int SIGNALR_WAIT_SECONDS = 30;

        private readonly TimeSpan TaskStateCheck;
        private readonly TimeSpan TaskSecondsLifetime;

        private bool IsFirstTime = true;
        private Timer AggregatorTimer;

        private ILog Log { get; }
        internal static ILog LogStat { get; private set; }
        private List<ServerFolderAccessInfo> ServerFolderAccessInfo { get; }
        private IOptionsMonitor<ILog> LogOptions { get; }
        private MailSettings Settings { get; }
        private ConsoleParameters ConsoleParameters { get; }
        private IServiceProvider ServiceProvider { get; }
        private QueueManager QueueManager { get; }
        internal static SignalrWorker SignalrWorker { get; private set; }

        public static ConcurrentDictionary<string, bool> UserCrmAvailabeDictionary { get; set; } = new ConcurrentDictionary<string, bool>();
        public static ConcurrentDictionary<string, List<MailSieveFilterData>> Filters { get; set; } = new ConcurrentDictionary<string, List<MailSieveFilterData>>();

        internal static readonly object CrmAvailabeLocker = new object();
        internal static readonly object FiltersLocker = new object();

        public AggregatorService(
            QueueManager queueManager,
            ConsoleParser consoleParser,
            IOptionsMonitor<ILog> optionsMonitor,
            MailSettings mailSettings,
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ConfigurationExtension configurationExtension,
            SignalrWorker signalrWorker,
            MailQueueItemSettings mailQueueItemSettings,
            IMailDaoFactory mailDaoFactory)
        {

            ServiceProvider = serviceProvider;
            ConsoleParameters = consoleParser.GetParsedParameters();
            QueueManager = queueManager;

            ConfigureLogNLog(configuration, configurationExtension);
            LogOptions = optionsMonitor;


            Log = optionsMonitor.Get("ASC.Mail.MainThread");
            LogStat = optionsMonitor.Get("ASC.Mail.Stat");

            Settings = mailSettings;

            Settings.DefaultFolders = mailQueueItemSettings.DefaultFolders;
            Settings.ImapFlags = mailQueueItemSettings.ImapFlags;
            Settings.SkipImapFlags = mailQueueItemSettings.SkipImapFlags;
            Settings.SpecialDomainFolders = mailQueueItemSettings.SpecialDomainFolders;

            TaskStateCheck = Settings.Aggregator.TaskCheckState;

            if (ConsoleParameters.OnlyUsers != null) Settings.Defines.WorkOnUsersOnlyList.AddRange(ConsoleParameters.OnlyUsers.ToList());

            if (ConsoleParameters.NoMessagesLimit) Settings.Aggregator.MaxMessagesPerSession = -1;

            TaskSecondsLifetime = Settings.Aggregator.TaskLifetime;

            if (Settings.Aggregator.EnableSignalr)
                SignalrWorker = signalrWorker;

            ServerFolderAccessInfo = mailDaoFactory
                    .GetImapSpecialMailboxDao()
                    .GetServerFolderAccessInfoList();

            Log.Info("Service is ready.");
        }

        #region methods

        private void AggregatorWork(object state)
        {
            var cancelToken = state as CancellationToken? ?? new CancellationToken();

            try
            {
                if (IsFirstTime)
                {
                    QueueManager.LoadMailboxesFromDump();

                    if (QueueManager.ProcessingCount > 0)
                    {
                        Log.InfoFormat("Found {0} tasks to release", QueueManager.ProcessingCount);

                        QueueManager.ReleaseAllProcessingMailboxes(true);
                    }

                    QueueManager.LoadTenantsFromDump();

                    IsFirstTime = false;
                }

                if (cancelToken.IsCancellationRequested)
                {
                    Log.Debug("Aggregator work: IsCancellationRequested. Quit.");
                    return;
                }

                StopTimer();

                var tasks = CreateTasks(Settings.Aggregator.MaxTasksAtOnce, cancelToken);

                while (tasks.Any())
                {
                    var indexTask = Task.WaitAny(tasks.Select(t => t.Task).ToArray(), (int)TaskStateCheck.TotalMilliseconds, cancelToken);

                    if (indexTask > -1)
                    {
                        var outTask = tasks[indexTask];
                        FreeTask(outTask, tasks);
                    }
                    else
                    {
                        Log.InfoFormat("Task.WaitAny timeout. Tasks count = {0}\r\nTasks:\r\n{1}", tasks.Count,
                            string.Join("\r\n", tasks.Select(t =>
                                        $"Id: {t.Task.Id} Status: {t.Task.Status}, MailboxId: {t.Mailbox.MailBoxId} Address: '{t.Mailbox.EMail}'")));
                    }

                    var tasks2Free =
                        tasks.Where(t =>
                            t.Task.Status == TaskStatus.Canceled ||
                            t.Task.Status == TaskStatus.Faulted ||
                            t.Task.Status == TaskStatus.RanToCompletion)
                        .ToList();

                    if (tasks2Free.Any())
                    {
                        Log.InfoFormat("Need free next tasks = {0}: ({1})", tasks2Free.Count,
                                  string.Join(",",
                                              tasks2Free.Select(t => t.Task.Id.ToString(CultureInfo.InvariantCulture))));

                        tasks2Free.ForEach(task => FreeTask(task, tasks));
                    }

                    var difference = Settings.Aggregator.MaxTasksAtOnce - tasks.Count;

                    if (difference <= 0) continue;

                    var newTasks = CreateTasks(difference, cancelToken);

                    tasks.AddRange(newTasks);

                    Log.InfoFormat("Total tasks count = {0} ({1}).", tasks.Count,
                              string.Join(",", tasks.Select(t => t.Task.Id)));
                }

                Log.Info("All mailboxes were processed. Go back to timer.");
            }
            catch (Exception ex) //Exceptions while boxes in process
            {
                if (ex is AggregateException)
                {
                    ex = ((AggregateException)ex).GetBaseException();
                }

                if (ex is TaskCanceledException || ex is OperationCanceledException)
                {
                    Log.Info("Execution was canceled.");

                    QueueManager.ReleaseAllProcessingMailboxes();

                    QueueManager.CancelHandler.Set();

                    return;
                }

                Log.ErrorFormat("Aggregator work exception:\r\n{0}\r\n", ex.ToString());

                if (QueueManager.ProcessingCount != 0)
                {
                    QueueManager.ReleaseAllProcessingMailboxes();
                }
            }

            QueueManager.CancelHandler.Set();

            StartTimer(cancelToken);
        }

        internal Task StartTimer(CancellationToken token, bool immediately = false)
        {
            if (AggregatorTimer == null)
                AggregatorTimer = new Timer(AggregatorWork, token, Timeout.Infinite, Timeout.Infinite);

            Log.Debug($"Setup Work timer to {Settings.Defines.CheckTimerInterval.TotalSeconds} seconds");

            if (immediately)
            {
                AggregatorTimer.Change(0, Timeout.Infinite);
            }
            else
            {
                AggregatorTimer.Change(Settings.Defines.CheckTimerInterval, Settings.Defines.CheckTimerInterval);
            }

            return Task.CompletedTask;
        }

        private void StopTimer()
        {
            if (AggregatorTimer == null)
                return;

            Log.Debug("Setup Work timer to Timeout.Infinite");
            AggregatorTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        internal void StopService(CancellationTokenSource tokenSource)
        {
            if (tokenSource != null)
                tokenSource.Cancel();

            if (QueueManager != null)
            {
                QueueManager.CancelHandler.WaitOne();
            }

            StopTimer();
            DisposeWorkers();
        }

        private void DisposeWorkers()
        {
            if (AggregatorTimer != null)
            {
                AggregatorTimer.Dispose();
                AggregatorTimer = null;
            }

            if (QueueManager != null)
                QueueManager.Dispose();

            if (SignalrWorker != null)
                SignalrWorker.Dispose();
        }

        public List<TaskData> CreateTasks(int needCount, CancellationToken cancelToken)
        {
            Log.InfoFormat($"Create tasks (need {needCount}).");

            var mailboxes = QueueManager.GetLockedMailboxes(needCount).ToList();

            var tasks = new List<TaskData>();

            foreach (var mailbox in mailboxes)
            {
                var linkedTokenSource =
                    CancellationTokenSource.CreateLinkedTokenSource(cancelToken, new CancellationTokenSource(TaskSecondsLifetime).Token);

                var log = LogOptions.Get($"ASC.Mail Mbox_{mailbox.MailBoxId}");

                var task = Task.Run(() => ProcessMailbox(mailbox, log, linkedTokenSource), linkedTokenSource.Token);

                tasks.Add(new TaskData(mailbox, task));
            }

            if (tasks.Any()) Log.InfoFormat("Created {0} tasks.", tasks.Count);
            else Log.Info("No more mailboxes for processing.");

            return tasks;
        }

        private void ProcessMailbox(MailBoxData mailBox, ILog log, CancellationTokenSource cTSource)
        {
            using var handler = new MailboxHandler(ServiceProvider, mailBox, Settings, log, cTSource, ServerFolderAccessInfo);

            handler.DoProcess();
        }

        private void NotifySignalrIfNeed(MailBoxData mailbox, ILog log)
        {
            if (!Settings.Aggregator.EnableSignalr)
            {
                log.Debug("Skip NotifySignalrIfNeed: EnableSignalr == false");

                return;
            }

            var now = DateTime.UtcNow;

            try
            {
                if (mailbox.LastSignalrNotify.HasValue &&
                    !((now - mailbox.LastSignalrNotify.Value).TotalSeconds > SIGNALR_WAIT_SECONDS))
                {
                    mailbox.LastSignalrNotifySkipped = true;
                    log.InfoFormat($"Skip NotifySignalrIfNeed: last notification has occurend less then {SIGNALR_WAIT_SECONDS} seconds ago");
                    return;
                }

                if (SignalrWorker == null)
                    throw new NullReferenceException("SignalrWorker");

                SignalrWorker.AddMailbox(mailbox);

                log.InfoFormat("NotifySignalrIfNeed(UserId = {0} TenantId = {1}) has been succeeded",
                    mailbox.UserId, mailbox.TenantId);
            }
            catch (Exception ex)
            {
                log.ErrorFormat("NotifySignalrIfNeed(UserId = {0} TenantId = {1}) Exception: {2}", mailbox.UserId,
                    mailbox.TenantId, ex.ToString());
            }

            mailbox.LastSignalrNotify = now;
            mailbox.LastSignalrNotifySkipped = false;
        }

        public void FreeTask(TaskData taskData, ICollection<TaskData> tasks)
        {
            try
            {
                //make dispose in TaskData
                Log.Debug($"End Task {taskData.Task.Id} with status = '{taskData.Task.Status}'.");

                if (!tasks.Remove(taskData))
                    Log.Error("Task not exists in tasks array.");

                ReleaseMailbox(taskData.Mailbox);

                taskData.Task.Dispose();  //=null

                GC.Collect();
            }
            catch (Exception ex)
            {
                Log.Error($"FreeTask(Id: {taskData.Mailbox.MailBoxId}, Email: {taskData.Mailbox.EMail}):\r\nException:{ex}\r\n");
            }
        }

        private void ReleaseMailbox(MailBoxData mailbox)
        {
            if (mailbox == null)
                return;

            if (mailbox.LastSignalrNotifySkipped)
                NotifySignalrIfNeed(mailbox, Log); //TODO: remove dublicate

            QueueManager.ReleaseMailbox(mailbox);

            if (!Filters.ContainsKey(mailbox.UserId))
                return;

            List<MailSieveFilterData> filters;
            if (!Filters.TryRemove(mailbox.UserId, out filters))
            {
                Log.Error("Try forget Filters for user failed");
            }
        }

        private void ConfigureLogNLog(IConfiguration configuration, ConfigurationExtension configurationExtension)
        {
            var fileName = CrossPlatform.PathCombine(configuration["pathToNlogConf"], "nlog.config");

            LogManager.Configuration = new NLog.Config.XmlLoggingConfiguration(fileName);
            LogManager.ThrowConfigExceptions = false;

            var settings = configurationExtension.GetSetting<NLogSettings>("log");
            if (!string.IsNullOrEmpty(settings.Name))
            {
                LogManager.Configuration.Variables["name"] = settings.Name;
            }

            if (!string.IsNullOrEmpty(settings.Dir))
            {
                LogManager.Configuration.Variables["dir"] = settings.Dir.TrimEnd('/').TrimEnd('\\') + Path.DirectorySeparatorChar;
            }

            NLog.Targets.Target.Register<SelfCleaningTarget>("SelfCleaning");
        }

        internal static void LogStatistic(string method, MailBoxData mailBoxData, double duration, bool failed)
        {
            var pairs = new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>("duration", duration),
                new KeyValuePair<string, object>("mailboxId", mailBoxData.MailBoxId),
                new KeyValuePair<string, object>("address", mailBoxData.EMail.ToString()),
                new KeyValuePair<string, object>("status", failed ? S_FAIL : S_OK)
            };

            LogStat.DebugWithProps(method, pairs);
        }
        #endregion
    }

}
