namespace ASC.Mail.Aggregator.Service.Queue;

[Singletone]
public class SocketIoNotifier : IDisposable
{
    public bool StartImmediately { get; set; } = true;

    private Task _workerTask;
    private volatile bool _workerTerminateSignal;

    private readonly Queue<MailBoxData> _processingQueue;
    private readonly EventWaitHandle _waitHandle;
    private readonly TimeSpan _timeSpan;
    private readonly ILog _log;
    private readonly SignalrServiceClient _signalrServiceClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly CancellationTokenSource _cancellationTokenSource;

    public SocketIoNotifier(
        IOptionsMonitor<ILog> optionsMonitor,
        IOptionsSnapshot<SignalrServiceClient> optionsSnapshot,
        IServiceProvider serviceProvider)
    {
        _log = optionsMonitor.Get("ASC.Mail.SignalrWorker");
        _signalrServiceClient = optionsSnapshot.Get("mail");
        _serviceProvider = serviceProvider;
        _cancellationTokenSource = new CancellationTokenSource();

        _workerTerminateSignal = false;
        _processingQueue = new Queue<MailBoxData>();
        _waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
        _timeSpan = TimeSpan.FromSeconds(15);

        _workerTask = new Task(ProcessQueue, _cancellationTokenSource.Token);

        if (StartImmediately)
            _workerTask.Start();
    }

    private void ProcessQueue()
    {
        while (!_workerTerminateSignal)
        {
            if (!HasQueuedMailbox)
            {
                _log.Debug("No items, waiting.");
                _waitHandle.WaitOne();
                _log.Debug("Waking up...");
            }

            var mailbox = NextQueuedMailBoxData;
            if (mailbox == null)
                continue;

            try
            {
                _log.Debug($"SignalrWorker -> SendUnreadUser(UserId = {mailbox.UserId} TenantId = {mailbox.TenantId})");

                SendUnreadUser(mailbox.TenantId, mailbox.UserId);
            }
            catch (Exception ex)
            {
                _log.Error($"SignalrWorker -> SendUnreadUser(UserId = {mailbox.UserId} TenantId = {mailbox.TenantId})\r\nException: \r\n{ex}");
            }

            _waitHandle.Reset();
        }
    }

    public int QueueCount
    {
        get
        {
            lock (_processingQueue)
            {
                return _processingQueue.Count;
            }
        }
    }

    public bool HasQueuedMailbox
    {
        get
        {
            lock (_processingQueue)
            {
                return _processingQueue.Any();
            }
        }
    }

    public MailBoxData NextQueuedMailBoxData
    {
        get
        {
            if (!HasQueuedMailbox)
                return null;

            lock (_processingQueue)
            {
                return _processingQueue.Dequeue();
            }
        }
    }

    public void AddMailbox(MailBoxData item)
    {
        lock (_processingQueue)
        {
            if (!_processingQueue.Contains(item))
                _processingQueue.Enqueue(item);
        }
        _waitHandle.Set();
    }

    public void Dispose()
    {
        if (_workerTask == null)
            return;

        _workerTerminateSignal = true;
        _waitHandle.Set();

        if (_workerTask.Status == TaskStatus.Running)
        {
            _log.Info("Stop SignalrWorker.");

            if (!_workerTask.Wait(_timeSpan))
            {
                _log.Info("SignalrWorker busy, cancellation of the task.");
                _cancellationTokenSource.Cancel();
            }
        }

        _workerTask = null;
    }

    private void SendUnreadUser(int tenant, string userId)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();

            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var folderEngine = scope.ServiceProvider.GetService<FolderEngine>();
            var userManager = scope.ServiceProvider.GetService<UserManager>();

            _log.Debug($"SignalrWorker -> SendUnreadUser(). Try set tenant |{tenant}| for user |{userId}|...");

            tenantManager.SetCurrentTenant(tenant);

            _log.Debug($"SignalrWorker -> SendUnreadUser(). Now current tennant = {tenantManager.GetCurrentTenant().TenantId}");

            var userInfo = userManager.GetUsers(Guid.Parse(userId));

            if (userInfo.ID != Constants.LostUser.ID)
            {
                _log.Debug($"SignalrWorker -> SendUnreadUser(). SendUnreadUser start");

                var mailFolderInfos = folderEngine.GetFolders(userId);
                var count = (from mailFolderInfo in mailFolderInfos
                             where mailFolderInfo.id == FolderType.Inbox
                             select mailFolderInfo.unreadMessages)
                    .FirstOrDefault();

                _signalrServiceClient.SendUnreadUser(tenant, userId, count);
            }
            else
            {
                _log.Debug($"SignalrWorker -> userID == LostUserID");
            }
        }
        catch (Exception e)
        {
            _log.ErrorFormat("SignalrWorker -> Unknown Error. {0}, {1}", e.ToString(),
                e.InnerException != null ? e.InnerException.Message : string.Empty);
        }
    }
}
