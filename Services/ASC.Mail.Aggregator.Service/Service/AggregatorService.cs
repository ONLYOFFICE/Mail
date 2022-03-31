namespace ASC.Mail.Aggregator.Service.Service;

[Singletone]
public class AggregatorService
{
    public const string ASC_MAIL_COLLECTION_SERVICE_NAME = "ASC Mail Collection Service";
    private const string S_FAIL = "error";
    private const string S_OK = "success";
    private const int SIGNALR_WAIT_SECONDS = 30;

    private readonly TimeSpan _taskStateCheck;
    private readonly TimeSpan _taskSecondsLifetime;

    private bool _isFirstTime = true;
    private Timer _aggregatorTimer;

    private readonly ILog _log;
    private readonly List<ServerFolderAccessInfo> _serverFolderAccessInfo;
    private readonly IOptionsMonitor<ILog> _logOptions;
    private readonly MailSettings _settings;
    private readonly ConsoleParameters _consoleParameters;
    private readonly IServiceProvider _serviceProvider;
    private readonly QueueManager _queueManager;

    internal static SocketIoNotifier SignalrWorker { get; private set; }
    private static ILog _logStat;

    public static ConcurrentDictionary<string, bool> UserCrmAvailabeDictionary { get; set; } = new ConcurrentDictionary<string, bool>();
    public static ConcurrentDictionary<string, List<MailSieveFilterData>> Filters { get; set; } = new ConcurrentDictionary<string, List<MailSieveFilterData>>();

    internal static readonly object crmAvailabeLocker = new object();
    internal static readonly object filtersLocker = new object();

    public AggregatorService(
        QueueManager queueManager,
        ConsoleParser consoleParser,
        IOptionsMonitor<ILog> optionsMonitor,
        MailSettings mailSettings,
        IServiceProvider serviceProvider,
        SocketIoNotifier signalrWorker,
        MailQueueItemSettings mailQueueItemSettings,
        IMailDaoFactory mailDaoFactory,
        NlogCongigure mailLogCongigure)
    {
        mailLogCongigure.Configure();

        _serviceProvider = serviceProvider;
        _consoleParameters = consoleParser.GetParsedParameters();
        _queueManager = queueManager;

        _logOptions = optionsMonitor;

        _log = optionsMonitor.Get("ASC.Mail.MainThread");
        _logStat = optionsMonitor.Get("ASC.Mail.Stat");

        _settings = mailSettings;

        _settings.DefaultFolders = mailQueueItemSettings.DefaultFolders;
        _settings.ImapFlags = mailQueueItemSettings.ImapFlags;
        _settings.SkipImapFlags = mailQueueItemSettings.SkipImapFlags;
        _settings.SpecialDomainFolders = mailQueueItemSettings.SpecialDomainFolders;

        _taskStateCheck = _settings.Aggregator.TaskCheckState;

        if (_consoleParameters.OnlyUsers != null) _settings.Defines.WorkOnUsersOnlyList.AddRange(_consoleParameters.OnlyUsers.ToList());

        if (_consoleParameters.NoMessagesLimit) _settings.Aggregator.MaxMessagesPerSession = -1;

        _taskSecondsLifetime = _settings.Aggregator.TaskLifetime;

        if (_settings.Aggregator.EnableSignalr)
            SignalrWorker = signalrWorker;

        _serverFolderAccessInfo = mailDaoFactory
                .GetImapSpecialMailboxDao()
                .GetServerFolderAccessInfoList();

        _log.Info("Service is ready.");
    }

    #region methods

    private void AggregatorWork(object state)
    {
        var cancelToken = state as CancellationToken? ?? new CancellationToken();

        try
        {
            if (_isFirstTime)
            {
                _queueManager.LoadMailboxesFromDump();

                if (_queueManager.ProcessingCount > 0)
                {
                    _log.InfoFormat("Found {0} tasks to release", _queueManager.ProcessingCount);

                    _queueManager.ReleaseAllProcessingMailboxes(true);
                }

                _queueManager.LoadTenantsFromDump();

                _isFirstTime = false;
            }

            if (cancelToken.IsCancellationRequested)
            {
                _log.Debug("Aggregator work: IsCancellationRequested. Quit.");
                return;
            }

            StopTimer();

            var tasks = CreateTasks(_settings.Aggregator.MaxTasksAtOnce, cancelToken);

            while (tasks.Any())
            {
                var indexTask = Task.WaitAny(tasks.Select(t => t.Task).ToArray(), (int)_taskStateCheck.TotalMilliseconds, cancelToken);

                if (indexTask > -1)
                {
                    var outTask = tasks[indexTask];
                    FreeTask(outTask, tasks);
                }
                else
                {
                    _log.InfoFormat("Task.WaitAny timeout. Tasks count = {0}\r\nTasks:\r\n{1}", tasks.Count,
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
                    _log.InfoFormat("Need free next tasks = {0}: ({1})", tasks2Free.Count,
                              string.Join(",",
                                          tasks2Free.Select(t => t.Task.Id.ToString(CultureInfo.InvariantCulture))));

                    tasks2Free.ForEach(task => FreeTask(task, tasks));
                }

                var difference = _settings.Aggregator.MaxTasksAtOnce - tasks.Count;

                if (difference <= 0) continue;

                var newTasks = CreateTasks(difference, cancelToken);

                tasks.AddRange(newTasks);

                _log.InfoFormat("Total tasks count = {0} ({1}).", tasks.Count,
                          string.Join(",", tasks.Select(t => t.Task.Id)));
            }

            _log.Info("All mailboxes were processed. Go back to timer.");
        }
        catch (Exception ex) //Exceptions while boxes in process
        {
            if (ex is AggregateException)
            {
                ex = ((AggregateException)ex).GetBaseException();
            }

            if (ex is TaskCanceledException || ex is OperationCanceledException)
            {
                _log.Info("Execution was canceled.");

                _queueManager.ReleaseAllProcessingMailboxes();

                _queueManager.CancelHandler.Set();

                return;
            }

            _log.ErrorFormat("Aggregator work exception:\r\n{0}\r\n", ex.ToString());

            if (_queueManager.ProcessingCount != 0)
            {
                _queueManager.ReleaseAllProcessingMailboxes();
            }
        }

        _queueManager.CancelHandler.Set();

        StartTimer(cancelToken);
    }

    internal Task StartTimer(CancellationToken token, bool immediately = false)
    {
        if (_aggregatorTimer == null)
            _aggregatorTimer = new Timer(AggregatorWork, token, Timeout.Infinite, Timeout.Infinite);

        _log.Debug($"Setup Work timer to {_settings.Defines.CheckTimerInterval.TotalSeconds} seconds");

        if (immediately)
        {
            _aggregatorTimer.Change(0, Timeout.Infinite);
        }
        else
        {
            _aggregatorTimer.Change(_settings.Defines.CheckTimerInterval, _settings.Defines.CheckTimerInterval);
        }

        return Task.CompletedTask;
    }

    private void StopTimer()
    {
        if (_aggregatorTimer == null)
            return;

        _log.Debug("Setup Work timer to Timeout.Infinite");
        _aggregatorTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    internal void StopService(CancellationTokenSource tokenSource)
    {
        if (tokenSource != null)
            tokenSource.Cancel();

        if (_queueManager != null)
        {
            _queueManager.CancelHandler.WaitOne();
        }

        StopTimer();
        DisposeWorkers();
    }

    private void DisposeWorkers()
    {
        if (_aggregatorTimer != null)
        {
            _aggregatorTimer.Dispose();
            _aggregatorTimer = null;
        }

        if (_queueManager != null)
            _queueManager.Dispose();

        if (SignalrWorker != null)
            SignalrWorker.Dispose();
    }

    public List<TaskData> CreateTasks(int needCount, CancellationToken cancelToken)
    {
        _log.InfoFormat($"Create tasks (need {needCount}).");

        var mailboxes = _queueManager.GetLockedMailboxes(needCount).ToList();

        var tasks = new List<TaskData>();

        foreach (var mailbox in mailboxes)
        {
            var linkedTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancelToken, new CancellationTokenSource(_taskSecondsLifetime).Token);

            var log = _logOptions.Get($"ASC.Mail Mbox_{mailbox.MailBoxId}");

            var task = Task.Run(() => ProcessMailbox(mailbox, log, linkedTokenSource), linkedTokenSource.Token);

            tasks.Add(new TaskData(mailbox, task));
        }

        if (tasks.Any()) _log.InfoFormat("Created {0} tasks.", tasks.Count);
        else _log.Info("No more mailboxes for processing.");

        return tasks;
    }

    private void ProcessMailbox(MailBoxData mailBox, ILog log, CancellationTokenSource cTSource)
    {
        using var handler = new MailboxHandler(_serviceProvider, mailBox, _settings, log, cTSource, _serverFolderAccessInfo);

        handler.DoProcess();
    }

    internal static void NotifySocketIO(MailBoxData mailbox, ILog log)
    {
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
            _log.Debug($"End Task {taskData.Task.Id} with status = '{taskData.Task.Status}'.");

            if (!tasks.Remove(taskData))
                _log.Error("Task not exists in tasks array.");

            ReleaseMailbox(taskData.Mailbox);

            taskData.Dispose();
        }
        catch (Exception ex)
        {
            _log.Error($"FreeTask(Id: {taskData.Mailbox.MailBoxId}, Email: {taskData.Mailbox.EMail}):\r\nException:{ex}\r\n");
        }
    }

    private void ReleaseMailbox(MailBoxData mailbox)
    {
        if (mailbox == null)
            return;

        if (mailbox.LastSignalrNotifySkipped && _settings.Aggregator.EnableSignalr)
            NotifySocketIO(mailbox, _log);

        _queueManager.ReleaseMailbox(mailbox);

        if (!Filters.ContainsKey(mailbox.UserId))
            return;

        List<MailSieveFilterData> filters;
        if (!Filters.TryRemove(mailbox.UserId, out filters))
        {
            _log.Error("Try forget Filters for user failed");
        }
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

        _logStat.DebugWithProps(method, pairs);
    }
    #endregion
}

