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

    private readonly ILogger _log;
    private readonly MailSettings _mailSettings;
    private readonly IServiceProvider _serviceProvider;

    public ManualResetEvent CancelHandler { get; set; }

    public QueueManager(
        MailSettings mailSettings,
        ILoggerProvider logProvider,
        IServiceProvider serviceProvider)
    {
        _maxItemsLimit = mailSettings.Aggregator.MaxTasksAtOnce;
        _mailBoxQueue = new Queue<MailBoxData>();
        _lockedMailBoxList = new List<MailBoxData>();

        _mailSettings = mailSettings;
        _serviceProvider = serviceProvider;

        _log = logProvider.CreateLogger("ASC.Mail.MainThread");
        _loadQueueTime = DateTime.UtcNow;
        _tenantMemCache = new MemoryCache("QueueManagerTenantCache");

        CancelHandler = new ManualResetEvent(false);

        if (_mailSettings.Aggregator.UseDump)
        {
            _dbcFile = Path.Combine(Environment.CurrentDirectory, "dump.db");
            _dbcJournalFile = Path.Combine(Environment.CurrentDirectory, "dump-journal.db");

            _log.DebugQueueManagerDumpFilePath(_dbcFile);

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
            _log.ErrorQueueManagerStoredDublicateMailbox(mailBoxData.MailBoxId, mailBoxData.EMail.Address);
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

        _log.InfoQueueManagerReleaseAllMailboxes();

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
                _log.WarnQueueManagerReleaseMailboxNotFound(mailBoxData.TenantId, mailBoxData.MailBoxId, mailBoxData.EMail.ToString());

                return;
            }

            _log.InfoQueueManagerReleaseMailbox(mailBoxData.MailBoxId, mailBoxData.EMail.ToString());

            using var scope = _serviceProvider.CreateScope();

            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();

            tenantManager.SetCurrentTenant(mailBoxData.TenantId);

            var mailboxEngine = scope.ServiceProvider.GetService<MailboxEngine>();

            mailboxEngine.ReleaseMailbox(mailBoxData, _mailSettings);

            _log.DebugQueueManagerReleaseMailboxOk(mailBoxData.MailBoxId);

            _lockedMailBoxList.RemoveAll(m => m.MailBoxId == mailBoxData.MailBoxId);

            DeleteMailboxFromDumpDb(mailBoxData.MailBoxId);
        }
        catch (Exception ex)
        {
            _log.ErrorQueueManagerReleaseMailbox(mailBoxData.TenantId, mailBoxData.MailBoxId, mailBoxData.Account, ex.ToString());
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
            _log.DebugQueueManagerLoadMailboxesFromDump();

            lock (_locker)
            {
                var list = _mailboxes.FindAll().ToList();

                _lockedMailBoxList = list.ConvertAll(m => m.ToMailbox()).ToList();
            }
        }
        catch (Exception ex)
        {
            _log.ErrorQueueManagerLoadMailboxesFromDump(ex.ToString());

            ReCreateDump();
        }
    }

    public void LoadTenantsFromDump()
    {
        if (!_mailSettings.Aggregator.UseDump)
            return;

        try
        {
            _log.DebugQueueManagerLoadTenantsFromDump();

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
            _log.ErrorQueueManagerLoadTenantsFromDump(ex.ToString());

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
                _log.DebugQueueManagerDumpFileExists(_dbcFile);

                File.Delete(_dbcFile);

                _log.DebugQueueManagerDumpFileDeleted(_dbcFile);
            }

            if (File.Exists(_dbcJournalFile))
            {
                _log.DebugQueueManagerDumpJournalFileExists(_dbcJournalFile);

                File.Delete(_dbcJournalFile);

                _log.DebugQueueManagerDumpFileJournalDeleted(_dbcJournalFile);
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
            _log.ErrorQueueManagerReCreateDumpFailed(ex.ToString());
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
            _log.ErrorQueueManagerAddMailboxToDumpDb(mailboxData.MailboxId, ex.ToString());

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
            _log.ErrorQueueManagerDeleteMailboxFromDumpDb(mailBoxId, ex.ToString());

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
            _log.ErrorQueueManagerLoadDumpFailed(ex.ToString());

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
            _log.ErrorQueueManagerAddTenantToDumpDb(tenantData.Tenant, ex.ToString());

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
            _log.ErrorQueueManagerDeleteTenantFromDump(tenantId, ex.ToString());

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
            _log.ErrorQueueManagerLoadQueue(ex.ToString());
        }
    }

    private MailBoxData GetQueuedMailbox()
    {
        if (QueueIsEmpty || QueueLifetimeExpired)
        {
            var queueStr = QueueIsEmpty ? "EMPTY" : "EXPIRED";
            _log.DebugQueueManagerLoadQueue(queueStr);

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
        _log.DebugQueueManagerRemoveFromQueue();
        var list = _mailBoxQueue.ToList();

        foreach (var b in list)
        {
            if (b.UserId == user)
                _log.DebugQueueManagerMailboxWillBeRemoved(b.MailBoxId);
        }

        var mbList = _mailBoxQueue.Where(mb => mb.UserId != user).Select(mb => mb).ToList();

        foreach (var box in list.Except(mbList))
        {
            _log.DebugQueueManagermailboxwasRemovedFromQueue(box.MailBoxId, box.UserId, box.TenantId);
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
                _log.DebugQueueManagerTenantIsntInCache(mailbox.TenantId);
                try
                {
                    var type = mailbox.GetTenantStatus(tenantManager, securityContext, apiHelper, (int)_mailSettings.Aggregator.TenantOverdueDays);

                    _log.InfoQueueManagerReturnedTenantStatus(mailbox.TenantId, type.ToString());
                    switch (type)
                    {
                        case DefineConstants.TariffType.LongDead:

                            _log.InfoQueueManagerReturnedTenantLongDead(mailbox.TenantId);

                            mailboxEngine.DisableMailboxes(
                                new TenantMailboxExp(mailbox.TenantId));

                            var userIds =
                                mailboxEngine.GetMailUsers(new TenantMailboxExp(mailbox.TenantId))
                                    .ConvertAll(t => t.Item2);

                            alertEngine.CreateDisableAllMailboxesAlert(mailbox.TenantId, userIds);

                            RemoveFromQueue(mailbox.TenantId);

                            return false;

                        case DefineConstants.TariffType.Overdue:

                            _log.InfoQueueManagerReturnedTenantOverdue(mailbox.TenantId);

                            mailboxEngine.SetNextLoginDelay(new TenantMailboxExp(mailbox.TenantId),
                                _mailSettings.Defines.OverdueAccountDelay);

                            RemoveFromQueue(mailbox.TenantId);

                            return false;

                        case DefineConstants.TariffType.Active:
                            _log.InfoQueueManagerReturnedTenantPaid(mailbox.TenantId);

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

                            _log.InfoQueueManagerCannotGetTariffType(mailbox.MailBoxId);

                            mailboxEngine.SetNextLoginDelay(new TenantMailboxExp(mailbox.TenantId),
                                _mailSettings.Defines.OverdueAccountDelay);

                            RemoveFromQueue(mailbox.TenantId);
                            break;
                    }
                }
                catch (Exception e)
                {
                    _log.ErrorQueueManagerGetTariffType(e.ToString());
                }
            }
            else
            {
                _log.DebugQueueManagerTenantIsInCache(mailbox.TenantId);
            }

            var isUserTerminated = mailbox.IsUserTerminated(tenantManager, userManager);
            var isUserRemoved = mailbox.IsUserRemoved(tenantManager, userManager);

            if (isUserTerminated || isUserRemoved)
            {
                string userStatus = "";
                if (isUserRemoved) userStatus = "removed";
                else if (isUserTerminated) userStatus = "terminated";

                _log.InfoQueueManagerDisableMailboxesForUser(mailbox.UserId, userStatus, mailbox.TenantId);

                mailboxEngine.DisableMailboxes(
                    new UserMailboxExp(mailbox.TenantId, mailbox.UserId));

                alertEngine.CreateDisableAllMailboxesAlert(mailbox.TenantId,
                    new List<string> { mailbox.UserId });

                RemoveFromQueue(mailbox.TenantId, mailbox.UserId);

                return false;
            }

            if (mailbox.IsTenantQuotaEnded(tenantManager, (int)_mailSettings.Aggregator.TenantMinQuotaBalance))
            {
                _log.InfoQueueManagerQuotaIsEnded(mailbox.TenantId, mailbox.UserId);

                if (!mailbox.QuotaError)
                    alertEngine.CreateQuotaErrorWarningAlert(mailbox.TenantId, mailbox.UserId);

                mailboxEngine.SetNextLoginDelay(new UserMailboxExp(mailbox.TenantId, mailbox.UserId),
                                _mailSettings.Defines.QuotaEndedDelay);

                RemoveFromQueue(mailbox.TenantId, mailbox.UserId);

                return false;
            }

            var active = mailbox.Active ? "active" : "inactive";
            _log.DebugQueueManagerTryLockMailbox(mailbox.EMail.Address, mailbox.MailBoxId, active);

            return mailboxEngine.LockMaibox(mailbox.MailBoxId);

        }
        catch (Exception ex)
        {
            var active = mailbox.Active ? "active" : "inactive";
            _log.ErrorQueueManagerTryLockMailbox(mailbox.MailBoxId, active, ex.ToString());

            return false;
        }

    }

    private void CacheEntryRemove(CacheEntryRemovedArguments arguments)
    {
        if (arguments.RemovedReason == CacheEntryRemovedReason.CacheSpecificEviction)
            return;

        var tenantId = Convert.ToInt32(arguments.CacheItem.Key);

        _log.InfoQueueManagerPaymentCacheIsExpired(tenantId);

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
