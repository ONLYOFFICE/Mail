using ASC.Common.Log;

namespace ASC.Mail.Core;

[Singletone]
public class SocketIoNotifier : IDisposable
{
    public bool StartImmediately { get; set; } = true;

    private Task _workerTask;
    private volatile bool _workerTerminateSignal;

    private readonly Queue<MailBoxData> _processingQueue;
    private readonly EventWaitHandle _waitHandle;
    private readonly TimeSpan _timeSpan;
    private readonly ILogger _log;
    private readonly IServiceProvider _serviceProvider;
    private readonly CancellationTokenSource _cancellationTokenSource;

    public SocketIoNotifier(
        ILoggerProvider logProvider,
        IServiceProvider serviceProvider)
    {
        _log = logProvider.CreateLogger("ASC.Mail.SignalrWorker");
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

    private async void ProcessQueue()
    {
        while (!_workerTerminateSignal)
        {
            if (!HasQueuedMailbox)
            {
                _log.DebugSocketIoNotifierNoItems();
                _waitHandle.WaitOne();
                _log.DebugSocketIoNotifierWakingUp();
            }

            var mailbox = NextQueuedMailBoxData;
            if (mailbox == null)
                continue;

            try
            {
                var result = await SendUnreadUser(mailbox.TenantId, mailbox.UserId);

                _log.DebugSocketIoNotifierSendUnreadUser(mailbox.UserId, mailbox.TenantId, result);
            }
            catch (Exception ex)
            {
                _log.ErrorSocketIoNotifierSendUnreadUser(mailbox.UserId, mailbox.TenantId, ex.ToString());
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

        if (_workerTask.Status == System.Threading.Tasks.TaskStatus.Running)
        {
            _log.InfoSocketIoNotifierStop();

            if (!_workerTask.Wait(_timeSpan))
            {
                _log.InfoSocketIoNotifierBusy();
                _cancellationTokenSource.Cancel();
            }
        }

        _workerTask = null;
    }

    private async Task<bool> SendUnreadUser(int tenant, string userId)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();

            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var folderEngine = scope.ServiceProvider.GetService<FolderEngine>();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var socketServiceClient = scope.ServiceProvider.GetService<SocketServiceClient>();

            _log.DebugSocketIoNotifierTrySetTenant(tenant, userId);

            tenantManager.SetCurrentTenant(tenant);

            _log.DebugSocketIoNotifierCurrentTenant(tenantManager.GetCurrentTenant().Id);

            var userInfo = userManager.GetUsers(Guid.Parse(userId));

            if (userInfo.Id != ASC.Core.Users.Constants.LostUser.Id)
            {
                _log.DebugSocketIoNotifierSendStart();

                var count = folderEngine.GetUserUnreadMessageCount(userId);

                var responce = await socketServiceClient.MakeRequest("updateFolders", new { tenant, userId, count });

                _log.Debug($"SendUnreadUser responce {responce}");

                return true;

            }
            else
            {
                _log.DebugSocketIoNotifierLostUser();
            }
        }
        catch (Exception e)
        {
            _log.ErrorSocketIoNotifier(e.ToString(), e.InnerException != null ? e.InnerException.Message : string.Empty);
        }

        return false;
    }
}
