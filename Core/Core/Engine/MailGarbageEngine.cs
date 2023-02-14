/*
 *
 * (c) Copyright Ascensio System Limited 2010-2020
 *
 * This program is freeware. You can redistribute it and/or modify it under the terms of the GNU 
 * General Public License (GPL) version 3 as published by the Free Software Foundation (https://www.gnu.org/copyleft/gpl.html). 
 * In accordance with Section 7(a) of the GNU GPL its Section 15 shall be amended to the effect that 
 * Ascensio System SIA expressly excludes the warranty of non-infringement of any third-party rights.
 *
 * THIS PROGRAM IS DISTRIBUTED WITHOUT ANY WARRANTY; WITHOUT EVEN THE IMPLIED WARRANTY OF MERCHANTABILITY OR
 * FITNESS FOR A PARTICULAR PURPOSE. For more details, see GNU GPL at https://www.gnu.org/copyleft/gpl.html
 *
 * You can contact Ascensio System SIA by email at sales@onlyoffice.com
 *
 * The interactive user interfaces in modified source and object code versions of ONLYOFFICE must display 
 * Appropriate Legal Notices, as required under Section 5 of the GNU GPL version 3.
 *
 * Pursuant to Section 7 § 3(b) of the GNU GPL you must retain the original ONLYOFFICE logo which contains 
 * relevant author attributions when distributing the software. If the display of the logo in its graphic 
 * form is not reasonably feasible for technical reasons, you must include the words "Powered by ONLYOFFICE" 
 * in every copy of the program you distribute. 
 * Pursuant to Section 7 § 3(e) we decline to grant you any rights under trademark law for use of our trademarks.
 *
*/

using ASC.Mail.Core.Core.Storage;
using Action = System.Action;
using SecurityContext = ASC.Core.SecurityContext;
using Task = System.Threading.Tasks.Task;

namespace ASC.Mail.Core.Engine;

[Scope]
public class MailGarbageEngine : BaseEngine, IDisposable
{
    private static MemoryCache _tenantMemCache;
    private static TaskFactory _taskFactory;
    private static object _locker;
    private readonly ILogger _log;
    private readonly IServiceProvider _serviceProvider;

    public MailGarbageEngine(
        MailSettings mailSettings,
        ILoggerProvider logProvider,
        IServiceProvider serviceProvider) : base(mailSettings)
    {
        _serviceProvider = serviceProvider;

        _log = logProvider.CreateLogger("ASC.Mail.GarbageEngine");

        _tenantMemCache = new MemoryCache("GarbageEraserTenantCache");

        var scheduler = new ConcurrentExclusiveSchedulerPair(TaskScheduler.Default, MailSettings.Aggregator.MaxTasksAtOnce).ConcurrentScheduler;

        _taskFactory = new TaskFactory(scheduler);

        _locker = new object();
    }

    #region - Public methods -

    public void ClearMailGarbage(CancellationToken cancelToken)
    {
        _log.DebugMailGarbageBegin();

        using var scope = _serviceProvider.CreateScope();
        var mailboxEngine = scope.ServiceProvider.GetService<MailboxEngine>();

        var tasks = new List<Task>();

        var mailboxIterator = new MailboxIterator(mailboxEngine, isRemoved: null, log: _log);

        var mailbox = mailboxIterator.First();

        while (!mailboxIterator.IsDone)
        {
            try
            {
                if (cancelToken.IsCancellationRequested)
                    break;

                var mb = mailbox;

                var tenantManager = scope.ServiceProvider.GetService<TenantManager>();

                tenantManager.SetCurrentTenant(mailbox.TenantId);

                var task = Queue(() => ClearGarbage(mb), cancelToken);

                tasks.Add(task);

                if (tasks.Count == MailSettings.Cleaner.MaxTasksAtOnce)
                {
                    _log.InfoMailGarbageWaitTasks();

                    Task.WaitAll(tasks.ToArray());

                    tasks = new List<Task>();
                }
            }
            catch (Exception ex)
            {
                _log.ErrorMailGarbage(ex.ToString());
            }

            if (!cancelToken.IsCancellationRequested)
            {
                mailbox = mailboxIterator.Next();
                continue;
            }

            _log.DebugMailGarbageQuit();

            break;
        }

        RemoveUselessMsDomains();

        _log.DebugMailGarbageEnd();
    }

    public void RemoveUselessMsDomains()
    {
        _log.DebugMailGarbageStartRemoveDomains();

        try
        {
            using var scope = _serviceProvider.CreateScope();

            var serverDomainEngine = scope.ServiceProvider.GetService<ServerDomainEngine>();
            var mailboxEngine = scope.ServiceProvider.GetService<MailboxEngine>();

            var domains = serverDomainEngine.GetAllDomains();

            foreach (var domain in domains)
            {
                if (domain.Tenant == -1)
                    continue;

                var status = GetTenantStatus(domain.Tenant);

                if (status != TenantStatus.RemovePending)
                    continue;

                var exp = new TenantServerMailboxesExp(domain.Tenant, null);

                var mailboxes = mailboxEngine.GetMailboxDataList(exp);

                if (mailboxes.Any())
                {
                    _log.WarnMailGarbageDomainHasUnremovedMailboxes(domain.Name, domain.Tenant, mailboxes.Count);

                    continue;
                }

                _log.InfoMailGarbageDomainLetsRemove(domain.Name, domain.Tenant);

                var count = domains.Count(d => d.Name.Equals(domain.Name, StringComparison.InvariantCultureIgnoreCase));

                var skipMS = count > 1;

                if (skipMS)
                {
                    _log.InfoMailGarbageDomainDuplicated(domain.Name);
                }

                RemoveDomain(domain, skipMS);
            }

        }
        catch (Exception ex)
        {
            _log.ErrorMailGarbageRemoveDomainFailed(ex.ToString());
        }

        _log.DebugMailGarbageEndRemoveDomains();
    }

    public TenantStatus GetTenantStatus(int tenant)
    {
        using var scope = _serviceProvider.CreateScope();
        var tenantManager = scope.ServiceProvider.GetService<TenantManager>();

        try
        {
            tenantManager.SetCurrentTenant(tenant);

            var tenantInfo = tenantManager.GetCurrentTenant();

            return tenantInfo.Status;
        }
        catch (Exception ex)
        {
            _log.ErrorMailGarbageGetTenantStatusFailed(tenant, ex.ToString());
        }

        return TenantStatus.Active;
    }

    public void RemoveDomain(ServerDomain domain, bool skipMS = false)
    {
        using var scope = _serviceProvider.CreateScope();

        var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
        tenantManager.SetCurrentTenant(domain.Tenant);
        _log.DebugMailGarbageRemoveDomainSetTenant(tenantManager.GetCurrentTenant().Id);

        var daoFactory = scope.ServiceProvider.GetService<MailDaoFactory>();
        var context = daoFactory.GetContext();

        try
        {
            var strategy = context.Database.CreateExecutionStrategy();

            strategy.Execute(() =>
            {
                using var tx = daoFactory.BeginTransaction(IsolationLevel.ReadUncommitted);

                var serverEngine = scope.ServiceProvider.GetService<Server.Core.ServerEngine>();

                _log.DebugMailGarbageStartDeleteDomain(domain.Id);

                daoFactory.GetServerDomainDao().Delete(domain.Id);

                if (!skipMS)
                {
                    _log.DebugMailGarbageTryGetServer(domain.Tenant);

                    var server = daoFactory.GetServerDao().Get(domain.Tenant);

                    if (server == null)
                        throw new Exception(string.Format("Information for Tenant's Mail Server not found (Tenant = {0})", domain.Tenant));

                    serverEngine.InitServer(server.Id, server.ConnectionString);

                    _log.DebugMailGarbageSuccessfullInitServer(
                        serverEngine.ServerApi.port,
                        serverEngine.ServerApi.protocol,
                        serverEngine.ServerApi.server_ip,
                        serverEngine.ServerApi.token,
                        serverEngine.ServerApi.version);

                    serverEngine.RemoveDomain(domain.Name);
                }

                tx.Commit();
            });
        }
        catch (Exception ex)
        {
            _log.ErrorMailGarbageRemoveDomainIfUseless(domain.Name, domain.Id, ex.ToString());
        }
    }

    public void ClearUserMail(Guid userId, Tenant tenantId = null)
    {
        using var scope = _serviceProvider.CreateScope();

        var tenantManager = scope.ServiceProvider.GetService<TenantManager>();

        var tenant = tenantId != null ? tenantId.Id : tenantManager.GetCurrentTenant().Id;

        _log.InfoMailGarbageClearUserMail(userId, tenant);

        var user = userId.ToString();

        RemoveUserFolders();

        //RemoveUserMailboxes(tenant, user, Log);

        //TODO: RemoveUserTags

        //TODO: RemoveUserContacts

        //TODO: RemoveUserAlerts

        //TODO: RemoveUserDisplayImagesAddresses

        //TODO: RemoveUserFolderCounters
    }

    #endregion

    #region - Private methods -

    private Task Queue(Action action, CancellationToken cancelToken)
    {
        var task = _taskFactory.StartNew(action, cancelToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);

        task.ConfigureAwait(false)
            .GetAwaiter()
            .OnCompleted(() =>
            {
                _log.DebugMailGarbageEndTask(task.Id, task.Status);
            });

        return task;
    }

    private bool NeedRemove(MailBoxData mailbox)
    {
        var needRemove = false;

        lock (_locker)
        {
            using var scope = _serviceProvider.CreateScope();

            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();
            var apiHelper = scope.ServiceProvider.GetService<ApiHelper>();

            DefineConstants.TariffType type;

            var memTenantItem = _tenantMemCache.Get(mailbox.TenantId.ToString(CultureInfo.InvariantCulture));

            if (memTenantItem == null)
            {
                _log.InfoMailGarbageTenantIsntInCache(mailbox.TenantId);

                _log.DebugMailGarbageGetTenantStatus(MailSettings.Cleaner.TenantOverdueDays);

                type = mailbox.GetTenantStatus(tenantManager, securityContext, apiHelper, (int)MailSettings.Cleaner.TenantOverdueDays, _log);

                var cacheItem = new CacheItem(mailbox.TenantId.ToString(CultureInfo.InvariantCulture), type);

                var cacheItemPolicy = new CacheItemPolicy
                {
                    AbsoluteExpiration =
                        DateTimeOffset.UtcNow.AddDays((int)MailSettings.Cleaner.TenantCacheDays)
                };

                _tenantMemCache.Add(cacheItem, cacheItemPolicy);
            }
            else
            {
                _log.InfoMailGarbageTenantIsInCache(mailbox.TenantId);

                type = (DefineConstants.TariffType)memTenantItem;
            }

            _log.InfoMailGarbageTenantStatus(mailbox.TenantId, type.ToString());

            if (type == DefineConstants.TariffType.LongDead)
            {
                _log.InfoMailGarbageMailboxWillBeDeleted(mailbox.MailBoxId);
                needRemove = true;
            }
            else
            {
                var isUserRemoved = mailbox.IsUserRemoved(tenantManager, userManager, _log);

                var status = isUserRemoved ? "Terminated. The mailbox will be deleted" : "Not terminated";

                _log.InfoMailGarbageUserStatus(mailbox.UserId, status);

                if (isUserRemoved)
                {
                    needRemove = true;
                }
            }

        }

        return needRemove;
    }

    private void ClearGarbage(MailBoxData mailbox)
    {
        _log.InfoMailGarbageProcessingMailbox(mailbox.MailBoxId, mailbox.EMail.Address, mailbox.TenantId, mailbox.UserId);

        try
        {
            if (NeedRemove(mailbox))
            {
                _log.DebugMailGarbageMailboxNeedRemove(mailbox.MailBoxId);

                RemoveMailboxData(mailbox, true);
            }
            else if (mailbox.IsRemoved)
            {
                _log.InfoMailGarbageMailboxMarkedForDeletion(mailbox.MailBoxId);

                RemoveMailboxData(mailbox, false);
            }
            else
            {
                RemoveGarbageMailData(mailbox, (int)MailSettings.Cleaner.GarbageOverdueDays);
            }

            _log.InfoMailGarbageMailboxProcessingComplete(mailbox.MailBoxId);
        }
        catch (Exception ex)
        {
            _log.InfoMailGarbageMailboxProcessedWithError(mailbox.MailBoxId, ex.ToString());
        }
    }

    private void RemoveMailboxData(MailBoxData mailbox, bool totalMailRemove)
    {
        _log.InfoMailGarbageRemoveMailboxData(mailbox.MailBoxId, mailbox.EMail.ToString());

        try
        {
            using var scope = _serviceProvider.CreateScope();

            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();

            tenantManager.SetCurrentTenant(mailbox.TenantId);
            _log.DebugMailGarbageRemoveMailboxDataSetTenant(tenantManager.GetCurrentTenant().Id);

            var mbEngine = scope.ServiceProvider.GetService<MailboxEngine>();
            var factory = scope.ServiceProvider.GetService<MailDaoFactory>();

            if (!mailbox.IsRemoved)
            {
                _log.InfoMailGarbageMaiboxIsntRemove();

                var needRecalculateFolders = !totalMailRemove;

                if (mailbox.IsTeamlab)
                {
                    _log.InfoMailGarbageRemoveTeamlabMailbox();

                    var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

                    securityContext.AuthenticateMe(ASC.Core.Configuration.Constants.CoreSystem);

                    RemoveTeamlabMailbox(mailbox);
                }

                _log.InfoMailGarbageSetMailboxRemoved();

                mbEngine.RemoveMailBox(mailbox, needRecalculateFolders);

                mailbox.IsRemoved = true;
            }

            _log.DebugMailGarbageGetDataStore(mailbox.TenantId);

            var storageFactory = scope.ServiceProvider.GetService<StorageFactory>();

            var mailTenantQuotaController = scope.ServiceProvider.GetService<MailTenantQuotaController>();

            var dataStorage = storageFactory.GetMailStorage(mailbox.TenantId, mailTenantQuotaController);

            dataStorage.QuotaController = null;

            _log.DebugMailGarbageGetMailboxAttachsCount();

            var countAttachs = factory.GetMailGarbageDao().GetMailboxAttachsCount(mailbox);

            _log.DebugMailGarbageCountAttachs(countAttachs);

            if (countAttachs > 0)
            {
                var sumCount = 0;

                _log.DebugMailGarbageGetAttachsGarbage(MailSettings.Cleaner.MaxFilesToRemoveAtOnce);

                var attachGrbgList = factory.GetMailGarbageDao().GetMailboxAttachs(mailbox, (int)MailSettings.Cleaner.MaxFilesToRemoveAtOnce);

                sumCount += attachGrbgList.Count;

                _log.InfoMailGarbageClearingAttachments(attachGrbgList.Count, sumCount, countAttachs);

                while (attachGrbgList.Any())
                {
                    foreach (var attachGrbg in attachGrbgList)
                    {
                        RemoveFile(dataStorage, attachGrbg.Path);
                    }

                    _log.DebugMailGarbageCleanupMailboxAttachs();

                    factory.GetMailGarbageDao().CleanupMailboxAttachs(attachGrbgList);

                    _log.DebugMailGarbageGetMailboxAttachs();

                    attachGrbgList = factory.GetMailGarbageDao().GetMailboxAttachs(mailbox, (int)MailSettings.Cleaner.MaxFilesToRemoveAtOnce);

                    if (!attachGrbgList.Any()) continue;

                    sumCount += attachGrbgList.Count;

                    _log.InfoMailGarbageFoundAttachments(attachGrbgList.Count, sumCount, countAttachs);
                }
            }

            _log.DebugMailGarbageGetMessagesCount();

            var countMessages = factory.GetMailGarbageDao().GetMailboxMessagesCount(mailbox);

            _log.InfoMailGarbageFountCountMsg(countMessages);

            if (countMessages > 0)
            {
                var sumCount = 0;

                _log.DebugMailGarbageGetMessagesLimit(MailSettings.Cleaner.MaxFilesToRemoveAtOnce);

                var messageGrbgList = factory.GetMailGarbageDao().GetMailboxMessages(mailbox, (int)MailSettings.Cleaner.MaxFilesToRemoveAtOnce);

                sumCount += messageGrbgList.Count;

                _log.InfoMailGarbageClearingMessages(messageGrbgList.Count, sumCount, countMessages);

                while (messageGrbgList.Any())
                {
                    foreach (var mailMessageGarbage in messageGrbgList)
                    {
                        RemoveFile(dataStorage, mailMessageGarbage.Path);
                    }

                    _log.DebugMailGarbageCleanupMessages();

                    factory.GetMailGarbageDao().CleanupMailboxMessages(messageGrbgList);

                    _log.DebugMailGarbageGetMessages();

                    messageGrbgList = factory.GetMailGarbageDao().GetMailboxMessages(mailbox, (int)MailSettings.Cleaner.MaxFilesToRemoveAtOnce);

                    if (!messageGrbgList.Any()) continue;

                    sumCount += messageGrbgList.Count;

                    _log.InfoMailGarbageFountMessages(messageGrbgList.Count, sumCount, countMessages);
                }
            }

            _log.DebugMailGarbageClearMailboxData();

            CleanupMailboxData(mailbox, totalMailRemove, factory);

            _log.DebugMailGarbageMailboxWasRemoved(mailbox.EMail.Address);
        }
        catch (Exception ex)
        {
            _log.ErrorMailGarbageMailbox(mailbox.MailBoxId, ex.ToString());

            throw;
        }
    }

    public void CleanupMailboxData(MailBoxData mailbox, bool totalRemove, MailDaoFactory mailDaoFactory)
    {
        if (!mailbox.IsRemoved)
            throw new Exception("Mailbox is not removed.");

        var mailDbContext = mailDaoFactory.GetContext();

        var strategy = mailDbContext.Database.CreateExecutionStrategy();

        strategy.Execute(() =>
        {
            using var tx = mailDaoFactory.BeginTransaction(IsolationLevel.ReadUncommitted);

            var exp = new ConcreteUserMailboxExp(mailbox.MailBoxId, mailbox.TenantId, mailbox.UserId, true);

            var mb = mailDaoFactory.GetMailboxDao().GetMailBox(exp);

            var deleteMailboxMessagesQuery = mailDbContext.MailMails
                .Where(m => m.MailboxId == mb.Id && m.TenantId == mb.Tenant && m.UserId == mb.User);

            mailDbContext.MailMails.RemoveRange(deleteMailboxMessagesQuery);

            mailDbContext.SaveChanges();

            var deleteMailboxAttachmentsQuery = mailDbContext.MailAttachments
                .Where(a => a.IdMailbox == mb.Id && a.Tenant == mb.Tenant);

            mailDbContext.MailAttachments.RemoveRange(deleteMailboxAttachmentsQuery);

            mailDbContext.SaveChanges();

            mailDaoFactory.GetMailboxDao().RemoveMailbox(mb, mailDbContext);

            if (totalRemove)
            {
                mailDaoFactory.GetFolderDao().Delete();

                var deleteContactInfoQuery = mailDbContext.MailContactInfos
                    .Where(c => c.IdUser == mb.User && c.TenantId == mb.Tenant);

                mailDbContext.MailContactInfos.RemoveRange(deleteContactInfoQuery);

                mailDbContext.SaveChanges();

                var deleteContactsQuery = mailDbContext.MailContacts
                    .Where(c => c.IdUser == mb.User && c.TenantId == mb.Tenant);

                mailDbContext.MailContacts.RemoveRange(deleteContactsQuery);

                mailDbContext.SaveChanges();

                var deleteDisplayImagesQuery = mailDbContext.MailDisplayImages
                   .Where(c => c.IdUser == mb.User && c.Tenant == mb.Tenant);

                mailDbContext.MailDisplayImages.RemoveRange(deleteDisplayImagesQuery);

                mailDbContext.SaveChanges();
            }

            tx.Commit();
        });
    }

    private void RemoveFile(IDataStore dataStorage, string path)
    {
        try
        {
            _log.DebugMailGarbageRemovingFile(path);

            dataStorage.DeleteAsync(string.Empty, path).Wait();

            _log.InfoMailGarbageFileRemoved(path);
        }
        catch (FileNotFoundException)
        {
            _log.WarnMailGarbageFileNotFound(path);
        }
        catch (Exception ex)
        {
            _log.ErrorMailGarbageRemoveFile(path, ex.ToString());
        }
    }

    private void RemoveUserMailDirectory(int tenant, string userId)
    {
        _log.DebugMailGarbageGetDataStore(tenant);

        using var scope = _serviceProvider.CreateScope();

        var storageFactory = scope.ServiceProvider.GetService<StorageFactory>();

        var mailTenantQuotaController = scope.ServiceProvider.GetService<MailTenantQuotaController>();

        var dataStorage = storageFactory.GetMailStorage(tenant, mailTenantQuotaController);

        var userMailDir = MailStoragePathCombiner.GetUserMailsDirectory(userId);

        try
        {
            _log.InfoMailGarbageRemoveUserMailDirectory(userMailDir, tenant, userId);

            dataStorage.DeleteDirectoryAsync(userMailDir).Wait();
        }
        catch (Exception ex)
        {
            _log.ErrorMailGarbageDeleteDirectory(userMailDir, ex.ToString());

            throw;
        }
    }

    public bool RemoveGarbageMailData(MailBoxData mailbox, int garbageDaysLimit)
    {
        //TODO: Implement cleanup data marked as removed and trash messages exceeded garbageDaysLimit

        return true;
    }

    private void RemoveTeamlabMailbox(MailBoxData mailbox)
    {
        if (mailbox == null)
            throw new ArgumentNullException("mailbox");

        if (!mailbox.IsTeamlab)
            return;

        using var scope = _serviceProvider.CreateScope();
        var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
        tenantManager.SetCurrentTenant(mailbox.TenantId);
        _log.DebugMailGarbageRemoveTLMailboxDataSetTenant(tenantManager.GetCurrentTenant().Id);

        var serverMailboxEngine = scope.ServiceProvider.GetService<ServerMailboxEngine>();

        try
        {
            serverMailboxEngine.RemoveMailbox(mailbox);
        }
        catch (Exception ex)
        {
            _log.ErrorMailGarbageRemoveTLMailbox(mailbox.MailBoxId, ex.ToString());
        }
    }

    private void RemoveUserFolders()
    {
        using var scope = _serviceProvider.CreateScope();

        var userFolderEngine = scope.ServiceProvider.GetService<UserFolderEngine>();
        var operationEngine = scope.ServiceProvider.GetService<OperationEngine>();

        try
        {
            var folders = userFolderEngine.GetList(parentId: 0);

            foreach (var folder in folders)
            {
                operationEngine.RemoveUserFolder(folder.Id);
            }
        }
        catch (Exception ex)
        {
            _log.ErrorMailGarbageRemoveUserFolders(ex.ToString());
        }
    }

    private void RemoveUserMailboxes(int tenant, string user)
    {
        using var scope = _serviceProvider.CreateScope();

        var mailboxEngine = scope.ServiceProvider.GetService<MailboxEngine>();

        var mailboxIterator = new MailboxIterator(mailboxEngine, _log, tenant, user);

        var mailbox = mailboxIterator.First();

        if (mailboxIterator.IsDone)
        {
            _log.InfoMailGarbageNoUsersForDeletion();
            return;
        }

        while (!mailboxIterator.IsDone)
        {
            try
            {
                if (!mailbox.UserId.Equals(user))
                    throw new Exception(
                        string.Format("Mailbox (id:{0}) user '{1}' not equals to removed user: '{2}'",
                            mailbox.MailBoxId, mailbox.UserId, user));

                //RemoveMailboxData(mailbox, true, log);
            }
            catch (Exception ex)
            {
                _log.ErrorMailGarbageRemoveMailboxData(mailbox.MailBoxId, ex.ToString());
            }

            mailbox = mailboxIterator.Next();
        }

        RemoveUserMailDirectory(tenant, user);
    }

    #endregion

    public void Dispose()
    {
        if (_tenantMemCache != null)
        {
            _tenantMemCache.Dispose();
        }
    }
}
