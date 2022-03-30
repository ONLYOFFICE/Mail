namespace ASC.Mail.Aggregator.Service.Queue;

[Singletone]
public class QueueManager : IDisposable
{
    private readonly int _maxItemsLimit;
    private readonly Queue<MailBoxData> _mailBoxQueue;
    private List<MailBoxData> _lockedMailBoxList;

    private DateTime _loadQueueTime;
    private MemoryCache _tenantMemCache;
    private const string DBC_MAILBOXES = "mailboxes";
    private const string DBC_TENANTS = "tenants";
    private static string _dbcFile;
    private static string _dbcJournalFile;
    private readonly object _locker = new object();
    private LiteDatabase _db;
    private ILiteCollection<MailboxData> _mailboxes;
    private ILiteCollection<TenantData> _tenants;

    private readonly ILog _log;
    private readonly MailSettings _mailSettings;
    private readonly IServiceProvider _serviceProvider;

    public ManualResetEvent CancelHandler { get; set; }

    public QueueManager(
        MailSettings mailSettings,
        IOptionsMonitor<ILog> optionsMonitor,
        IServiceProvider serviceProvider)
    {
        _maxItemsLimit = mailSettings.Aggregator.MaxTasksAtOnce;
        _mailBoxQueue = new Queue<MailBoxData>();
        _lockedMailBoxList = new List<MailBoxData>();

        _mailSettings = mailSettings;
        _serviceProvider = serviceProvider;

        _log = optionsMonitor.Get("ASC.Mail.MainThread");
        _loadQueueTime = DateTime.UtcNow;
        _tenantMemCache = new MemoryCache("QueueManagerTenantCache");

        CancelHandler = new ManualResetEvent(false);

        if (_mailSettings.Aggregator.UseDump)
        {
            _dbcFile = Path.Combine(Environment.CurrentDirectory, "dump.db");
            _dbcJournalFile = Path.Combine(Environment.CurrentDirectory, "dump-journal.db");

            _log.Debug($"Dump file path: {_dbcFile}");

            LoadDump();
        }
    }

    #region - public methods -

    public IEnumerable<MailBoxData> GetLockedMailboxes(int needTasks)
    {
        var mbList = new List<MailBoxData>();
        do
        {
            var mailBox = GetLockedMailbox();
            if (mailBox == null)
                break;

            mbList.Add(mailBox);

        } while (mbList.Count < needTasks);

        return mbList;
    }

    public MailBoxData GetLockedMailbox()
    {
        MailBoxData mailBoxData;

        do
        {
            mailBoxData = GetQueuedMailbox();

        }
        while (mailBoxData != null && !TryLockMailbox(mailBoxData));

        if (mailBoxData == null)
            return null;

        if (_lockedMailBoxList.Any(m => m.MailBoxId == mailBoxData.MailBoxId))
        {
            _log.Error($"GetLockedMailbox() Stored dublicate with id = {mailBoxData.MailBoxId}, address = {mailBoxData.EMail.Address}. Mailbox not added to the queue.");
            return null;
        }

        _lockedMailBoxList.Add(mailBoxData);

        CancelHandler.Reset();

        AddMailboxToDumpDb(mailBoxData.ToMailboxData());

        return mailBoxData;
    }

    public void ReleaseAllProcessingMailboxes(bool firstTime = false)
    {
        if (!_lockedMailBoxList.Any())
            return;

        var cloneCollection = new List<MailBoxData>(_lockedMailBoxList);

        _log.Info("QueueManager -> ReleaseAllProcessingMailboxes()");

        using var scope = _serviceProvider.CreateScope();

        var mailboxEngine = scope.ServiceProvider.GetService<MailboxEngine>();

        foreach (var mailbox in cloneCollection)
        {
            ReleaseMailbox(mailbox);
        }
    }

    public void ReleaseMailbox(MailBoxData mailBoxData)
    {
        try
        {
            if (!_lockedMailBoxList.Any(m => m.MailBoxId == mailBoxData.MailBoxId))
            {
                _log.WarnFormat($"QueueManager -> ReleaseMailbox(Tenant = {mailBoxData.TenantId} " +
                    $"MailboxId = {mailBoxData.MailBoxId}, Address = '{mailBoxData.EMail}') mailbox not found");

                return;
            }

            _log.InfoFormat($"QueueManager -> ReleaseMailbox(MailboxId = {mailBoxData.MailBoxId} Address '{mailBoxData.EMail}')");

            using var scope = _serviceProvider.CreateScope();

            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();

            tenantManager.SetCurrentTenant(mailBoxData.TenantId);

            var mailboxEngine = scope.ServiceProvider.GetService<MailboxEngine>();

            mailboxEngine.ReleaseMailbox(mailBoxData, _mailSettings);

            _log.Debug($"Mailbox {mailBoxData.MailBoxId} will be realesed...Now remove from locked queue by Id.");

            _lockedMailBoxList.RemoveAll(m => m.MailBoxId == mailBoxData.MailBoxId);

            DeleteMailboxFromDumpDb(mailBoxData.MailBoxId);
        }
        catch (Exception ex)
        {
            _log.Error($"QueueManager -> ReleaseMailbox(Tenant = {mailBoxData.TenantId} MailboxId = {mailBoxData.MailBoxId}, Address = '{mailBoxData.Account}')\r\nException: {ex} \r\n");
            _lockedMailBoxList.RemoveAll(m => m.MailBoxId == mailBoxData.MailBoxId);
        }
    }

    public int ProcessingCount
    {
        get { return _lockedMailBoxList.Count; }
    }

    public void LoadMailboxesFromDump()
    {
        if (!_mailSettings.Aggregator.UseDump)
            return;

        if (_lockedMailBoxList.Any())
            return;

        try
        {
            _log.Debug("QueueManager -> LoadMailboxesFromDump()");

            lock (_locker)
            {
                var list = _mailboxes.FindAll().ToList();

                _lockedMailBoxList = list.ConvertAll(m => m.ToMailbox()).ToList();
            }
        }
        catch (Exception ex)
        {
            _log.Error($"QueueManager -> LoadMailboxesFromDump: {ex}");

            ReCreateDump();
        }
    }

    public void LoadTenantsFromDump()
    {
        if (!_mailSettings.Aggregator.UseDump)
            return;

        try
        {
            _log.Debug("QueueManager -> LoadTenantsFromDump()");

            lock (_locker)
            {
                var list = _tenants.FindAll().ToList();

                foreach (var tenantData in list)
                {
                    AddTenantToCache(tenantData, false);
                }
            }

        }
        catch (Exception ex)
        {
            _log.Error($"QueueManager -> LoadTenantsFromDump: {ex}");

            ReCreateDump();
        }
    }

    #endregion

    #region - private methods -

    private void ReCreateDump()
    {
        if (!_mailSettings.Aggregator.UseDump)
            return;

        try
        {
            if (File.Exists(_dbcFile))
            {
                _log.Debug($"Dump file '{_dbcFile}' exists, trying delete");

                File.Delete(_dbcFile);

                _log.Debug($"Dump file '{_dbcFile}' deleted");
            }

            if (File.Exists(_dbcJournalFile))
            {
                _log.Debug($"Dump journal file '{_dbcJournalFile}' exists, trying delete");

                File.Delete(_dbcJournalFile);

                _log.Debug($"Dump journal file '{_dbcJournalFile}' deleted");
            }

            _db = new LiteDatabase(_dbcFile);

            lock (_locker)
            {
                _mailboxes = _db.GetCollection<MailboxData>(DBC_MAILBOXES);
                _tenants = _db.GetCollection<TenantData>(DBC_TENANTS);
            }
        }
        catch (Exception ex)
        {
            _log.Error($"QueueManager -> ReCreateDump() failed Exception: {ex}");
        }
    }

    private void AddMailboxToDumpDb(MailboxData mailboxData)
    {
        if (!_mailSettings.Aggregator.UseDump)
            return;

        try
        {
            lock (_locker)
            {
                var mailbox = _mailboxes.FindOne(Query.EQ("MailboxId", mailboxData.MailboxId));

                if (mailbox != null)
                    return;

                _mailboxes.Insert(mailboxData);

                // Create, if not exists, new index on Name field
                _mailboxes.EnsureIndex(x => x.MailboxId);
            }
        }
        catch (Exception ex)
        {
            _log.Error($"QueueManager -> AddMailboxToDumpDb(Id = {mailboxData.MailboxId}) Exception: {ex}");

            ReCreateDump();
        }
    }

    private void DeleteMailboxFromDumpDb(int mailBoxId)
    {
        if (!_mailSettings.Aggregator.UseDump)
            return;

        try
        {
            lock (_locker)
            {
                var mailbox = _mailboxes.FindOne(Query.EQ("MailboxId", mailBoxId));

                if (mailbox == null)
                    return;

                _mailboxes.DeleteMany(Query.EQ("MailboxId", mailBoxId));
            }
        }
        catch (Exception ex)
        {
            _log.Error($"QueueManager -> DeleteMailboxFromDumpDb(MailboxId = {mailBoxId}) Exception: {ex}");

            ReCreateDump();
        }
    }

    private void LoadDump()
    {
        if (!_mailSettings.Aggregator.UseDump)
            return;

        try
        {
            if (File.Exists(_dbcJournalFile))
                throw new Exception($"Temp dump journal file exists in {_dbcJournalFile}");

            _db = new LiteDatabase(_dbcFile);

            lock (_locker)
            {
                _tenants = _db.GetCollection<TenantData>(DBC_TENANTS);
                _mailboxes = _db.GetCollection<MailboxData>(DBC_MAILBOXES);
            }
        }
        catch (Exception ex)
        {
            _log.Error($"QueueManager -> LoadDump() failed Exception: {ex}");

            ReCreateDump();
        }
    }

    private void AddTenantToDumpDb(TenantData tenantData)
    {
        if (!_mailSettings.Aggregator.UseDump)
            return;

        try
        {
            lock (_locker)
            {
                var tenant = _tenants.FindOne(Query.EQ("Tenant", tenantData.Tenant));

                if (tenant != null)
                    return;

                _tenants.Insert(tenantData);

                // Create, if not exists, new index on Name field
                _tenants.EnsureIndex(x => x.Tenant);
            }
        }
        catch (Exception ex)
        {
            _log.Error($"QueueManager -> AddTenantToDumpDb(TenantId = {tenantData.Tenant}) Exception: {ex}");

            ReCreateDump();
        }
    }

    private void DeleteTenantFromDumpDb(int tenantId)
    {
        if (!_mailSettings.Aggregator.UseDump)
            return;

        try
        {
            lock (_locker)
            {
                var tenant = _tenants.FindOne(Query.EQ("Tenant", tenantId));

                if (tenant == null)
                    return;

                _tenants.DeleteMany(Query.EQ("Tenant", tenantId));
            }
        }
        catch (Exception ex)
        {
            _log.Error($"QueueManager -> DeleteTenantFromDumpDb(TenantId = {tenantId}) Exception: {ex}");

            ReCreateDump();
        }
    }

    private void AddTenantToCache(TenantData tenantData, bool needDump = true)
    {
        var now = DateTime.UtcNow;

        if (tenantData.Expired < now)
        {
            DeleteTenantFromDumpDb(tenantData.Tenant);
            return; // Skip Expired tenant
        }

        var cacheItem = new CacheItem(tenantData.Tenant.ToString(CultureInfo.InvariantCulture), tenantData);

        var nowOffset = tenantData.Expired - now;

        var absoluteExpiration = DateTime.UtcNow.Add(nowOffset);

        var cacheItemPolicy = new CacheItemPolicy
        {
            RemovedCallback = CacheEntryRemove,
            AbsoluteExpiration = absoluteExpiration
        };

        _tenantMemCache.Add(cacheItem, cacheItemPolicy);

        if (!needDump)
            return;

        AddTenantToDumpDb(tenantData);
    }

    private bool QueueIsEmpty
    {
        get { return !_mailBoxQueue.Any(); }
    }

    private bool QueueLifetimeExpired
    {
        get { return DateTime.UtcNow - _loadQueueTime >= _mailSettings.Defines.QueueLifetime; }
    }

    private void LoadQueue()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var mailboxEngine = scope.ServiceProvider.GetService<MailboxEngine>();
            var mbList = mailboxEngine.GetMailboxesForProcessing(_mailSettings, _maxItemsLimit).ToList();

            ReloadQueue(mbList);
        }
        catch (Exception ex)
        {
            _log.Error($"QueueManager -> LoadQueue()\r\nException: \r\n {ex}");
        }
    }

    private MailBoxData GetQueuedMailbox()
    {
        if (QueueIsEmpty || QueueLifetimeExpired)
        {
            var queueStr = QueueIsEmpty ? "EMPTY" : "EXPIRED";
            _log.Debug($"Queue is {queueStr}. Load new queue.");

            LoadQueue();
        }

        return !QueueIsEmpty ? _mailBoxQueue.Dequeue() : null;
    }

    private void RemoveFromQueue(int tenant)
    {
        var mbList = _mailBoxQueue.Where(mb => mb.TenantId != tenant).Select(mb => mb).ToList();
        ReloadQueue(mbList);
    }

    private void RemoveFromQueue(int tenant, string user)
    {
        _log.Debug("RemoveFromQueue()");
        var list = _mailBoxQueue.ToList();

        foreach (var b in list)
        {
            if (b.UserId == user)
                _log.Debug($"Next mailbox will be removed from queue: {b.MailBoxId}");
        }

        var mbList = _mailBoxQueue.Where(mb => mb.UserId != user).Select(mb => mb).ToList();

        foreach (var box in list.Except(mbList))
        {
            _log.Debug($"Mailbox with id |{box.MailBoxId}| for user {box.UserId} from tenant {box.TenantId} was removed from queue");
        }

        ReloadQueue(mbList);
    }

    private void ReloadQueue(IEnumerable<MailBoxData> mbList)
    {
        _mailBoxQueue.Clear();
        _mailBoxQueue.PushRange(mbList);
        _loadQueueTime = DateTime.UtcNow;
    }

    private bool TryLockMailbox(MailBoxData mailbox)
    {
        try
        {
            var contains = _tenantMemCache.Contains(mailbox.TenantId.ToString(CultureInfo.InvariantCulture));

            using var scope = _serviceProvider.CreateScope();

            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();
            var apiHelper = scope.ServiceProvider.GetService<ApiHelper>();
            var mailboxEngine = scope.ServiceProvider.GetService<MailboxEngine>();
            var alertEngine = scope.ServiceProvider.GetService<AlertEngine>();
            var userManager = scope.ServiceProvider.GetService<UserManager>();

            if (!contains)
            {
                _log.Debug($"Tenant {mailbox.TenantId} isn't in cache");
                try
                {
                    var type = mailbox.GetTenantStatus(tenantManager, securityContext, apiHelper, (int)_mailSettings.Aggregator.TenantOverdueDays, _log);

                    _log.InfoFormat("TryLockMailbox -> Returned tenant {0} status: {1}.", mailbox.TenantId, type);
                    switch (type)
                    {
                        case DefineConstants.TariffType.LongDead:
                            _log.InfoFormat("Tenant {0} is not paid. Disable mailboxes.", mailbox.TenantId);

                            mailboxEngine.DisableMailboxes(
                                new TenantMailboxExp(mailbox.TenantId));

                            var userIds =
                                mailboxEngine.GetMailUsers(new TenantMailboxExp(mailbox.TenantId))
                                    .ConvertAll(t => t.Item2);

                            alertEngine.CreateDisableAllMailboxesAlert(mailbox.TenantId, userIds);

                            RemoveFromQueue(mailbox.TenantId);

                            return false;

                        case DefineConstants.TariffType.Overdue:
                            _log.InfoFormat("Tenant {0} is not paid. Stop processing mailboxes.", mailbox.TenantId);
                            mailboxEngine.SetNextLoginDelay(new TenantMailboxExp(mailbox.TenantId),
                                _mailSettings.Defines.OverdueAccountDelay);

                            RemoveFromQueue(mailbox.TenantId);

                            return false;

                        case DefineConstants.TariffType.Active:
                            _log.InfoFormat("Tenant {0} is paid.", mailbox.TenantId);

                            var expired = DateTime.UtcNow.Add(_mailSettings.Defines.TenantCachingPeriod);

                            var tenantData = new TenantData
                            {
                                Tenant = mailbox.TenantId,
                                TariffType = type,
                                Expired = expired
                            };

                            AddTenantToCache(tenantData);

                            break;
                        default:
                            _log.InfoFormat($"Cannot get tariff type for {mailbox.MailBoxId} mailbox");
                            mailboxEngine.SetNextLoginDelay(new TenantMailboxExp(mailbox.TenantId),
                                _mailSettings.Defines.OverdueAccountDelay);

                            RemoveFromQueue(mailbox.TenantId);
                            break;
                    }
                }
                catch (Exception e)
                {
                    _log.Error($"QueueManager -> TryLockMailbox(): GetTariffType \r\nException:{e}\r\n");
                }
            }
            else
            {
                _log.Debug($"Tenant {mailbox.TenantId} is in cache");
            }

            var isUserTerminated = mailbox.IsUserTerminated(tenantManager, userManager, _log);
            var isUserRemoved = mailbox.IsUserRemoved(tenantManager, userManager, _log);

            if (isUserTerminated || isUserRemoved)
            {
                string userStatus = "";
                if (isUserRemoved) userStatus = "removed";
                else if (isUserTerminated) userStatus = "terminated";

                _log.InfoFormat($"User '{mailbox.UserId}' was {userStatus}. Tenant = {mailbox.TenantId}. Disable mailboxes for user.");

                mailboxEngine.DisableMailboxes(
                    new UserMailboxExp(mailbox.TenantId, mailbox.UserId));

                alertEngine.CreateDisableAllMailboxesAlert(mailbox.TenantId,
                    new List<string> { mailbox.UserId });

                RemoveFromQueue(mailbox.TenantId, mailbox.UserId);

                return false;
            }

            if (mailbox.IsTenantQuotaEnded(tenantManager, (int)_mailSettings.Aggregator.TenantMinQuotaBalance, _log))
            {
                _log.InfoFormat($"Tenant = {mailbox.TenantId} User = {mailbox.UserId}. Quota is ended.");

                if (!mailbox.QuotaError)
                    alertEngine.CreateQuotaErrorWarningAlert(mailbox.TenantId, mailbox.UserId);

                mailboxEngine.SetNextLoginDelay(new UserMailboxExp(mailbox.TenantId, mailbox.UserId),
                                _mailSettings.Defines.QuotaEndedDelay);

                RemoveFromQueue(mailbox.TenantId, mailbox.UserId);

                return false;
            }

            var active = mailbox.Active ? "active" : "inactive";
            _log.Debug($"TryLockMailbox {mailbox.EMail.Address} (MailboxId: {mailbox.MailBoxId} is {active})");

            return mailboxEngine.LockMaibox(mailbox.MailBoxId);

        }
        catch (Exception ex)
        {
            _log.ErrorFormat("QueueManager -> TryLockMailbox(MailboxId={0} is {1})\r\nException:{2}\r\n", mailbox.MailBoxId,
                       mailbox.Active ? "active" : "inactive", ex.ToString());

            return false;
        }

    }

    private void CacheEntryRemove(CacheEntryRemovedArguments arguments)
    {
        if (arguments.RemovedReason == CacheEntryRemovedReason.CacheSpecificEviction)
            return;

        var tenantId = Convert.ToInt32(arguments.CacheItem.Key);

        _log.InfoFormat($"Tenant {tenantId} payment cache is expired.");

        DeleteTenantFromDumpDb(tenantId);
    }

    #endregion

    public void Dispose()
    {
        if (_tenantMemCache != null)
            _tenantMemCache.Dispose();
        _tenantMemCache = null;

        if (_mailSettings.Aggregator.UseDump)
        {
            if (_db != null)
                _db.Dispose();
            _db = null;
        }
    }
}
