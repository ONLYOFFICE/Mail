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
    private readonly ILog _log;
    private readonly IServiceProvider _serviceProvider;

    public MailGarbageEngine(
        MailSettings mailSettings,
        IOptionsMonitor<ILog> option,
        IServiceProvider serviceProvider) : base(mailSettings)
    {
        _serviceProvider = serviceProvider;

        _log = option.Get("ASC.Mail.GarbageEngine");

        _tenantMemCache = new MemoryCache("GarbageEraserTenantCache");

        var scheduler = new ConcurrentExclusiveSchedulerPair(TaskScheduler.Default, MailSettings.Aggregator.MaxTasksAtOnce).ConcurrentScheduler;

        _taskFactory = new TaskFactory(scheduler);

        _locker = new object();
    }

    #region - Public methods -

    public void ClearMailGarbage(CancellationToken cancelToken)
    {
        _log.Debug("Begin ClearMailGarbage()");

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
                    _log.Info("Wait all tasks to complete");

                    Task.WaitAll(tasks.ToArray());

                    tasks = new List<Task>();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex.ToString());
            }

            if (!cancelToken.IsCancellationRequested)
            {
                mailbox = mailboxIterator.Next();
                continue;
            }

            _log.Debug("ClearMailGarbage: IsCancellationRequested. Quit.");
            break;
        }

        RemoveUselessMsDomains();

        _log.Debug("End ClearMailGarbage()\r\n");
    }

    public void RemoveUselessMsDomains()
    {
        _log.Debug("Start RemoveUselessMsDomains()\r\n");

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
                    _log.WarnFormat("Domain's '{0}' Tenant={1} is removed, but it has unremoved server mailboxes (count={2}). Skip it.",
                        domain.Name, domain.Tenant, mailboxes.Count);

                    continue;
                }

                _log.InfoFormat("Domain's '{0}' Tenant = {1} is removed. Lets remove domain.", domain.Name, domain.Tenant);

                var count = domains.Count(d => d.Name.Equals(domain.Name, StringComparison.InvariantCultureIgnoreCase));

                var skipMS = count > 1;

                if (skipMS)
                {
                    _log.InfoFormat("Domain's '{0}' has duplicated entry for another tenant. Remove only current entry.", domain.Name);
                }

                RemoveDomain(domain, skipMS);
            }

        }
        catch (Exception ex)
        {
            _log.Error(string.Format("RemoveUselessMsDomains failed. Exception: {0}", ex.ToString()));
        }

        _log.Debug("End RemoveUselessMsDomains()\r\n");
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
            _log.Error($"GetTenantStatus(tenant='{tenant}') failed. Exception: {ex}");
        }

        return TenantStatus.Active;
    }

    public void RemoveDomain(Entities.ServerDomain domain, bool skipMS = false)
    {
        using var scope = _serviceProvider.CreateScope();

        var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
        tenantManager.SetCurrentTenant(domain.Tenant);
        _log.Debug($"RemoveDomain. Set current tenant: {tenantManager.GetCurrentTenant().TenantId}");

        var daoFactory = scope.ServiceProvider.GetService<MailDaoFactory>();
        var context = daoFactory.GetContext();

        try
        {
            using (var tx = daoFactory.BeginTransaction(IsolationLevel.ReadUncommitted))
            {
                var serverEngine = scope.ServiceProvider.GetService<Server.Core.ServerEngine>();

                _log.Debug($"MailGarbageEngine -> RemoveDomain: 1) Delete domain by id {domain.Id}...");

                daoFactory.GetServerDomainDao().Delete(domain.Id);

                if (!skipMS)
                {
                    _log.Debug($"MailGarbageEngine -> RemoveDomain: 2) Try get server by tenant {domain.Tenant}...");

                    var server = daoFactory.GetServerDao().Get(domain.Tenant);

                    if (server == null)
                        throw new Exception(string.Format("Information for Tenant's Mail Server not found (Tenant = {0})", domain.Tenant));

                    serverEngine.InitServer(server.Id, server.ConnectionString);

                    _log.Debug($"MailGarbageEngine -> RemoveDomain: 3) Successfull init server. " +
                        $"\nServer Api | " +
                        $"\nPort: {serverEngine.ServerApi.port} " +
                        $"\nProtocol: {serverEngine.ServerApi.protocol}" +
                        $"\nIP: {serverEngine.ServerApi.server_ip}" +
                        $"\nToken: {serverEngine.ServerApi.token}" +
                        $"\nVersion: {serverEngine.ServerApi.version}");

                    serverEngine.RemoveDomain(domain.Name);
                }

                tx.Commit();
            }
        }
        catch (Exception ex)
        {
            _log.Error(string.Format("RemoveDomainIfUseless(Domain: '{0}', ID='{1}') failed. Exception: {2}", domain.Name, domain.Id, ex.ToString()));
        }
    }

    public void ClearUserMail(Guid userId, Tenant tenantId = null)
    {
        using var scope = _serviceProvider.CreateScope();

        var tenantManager = scope.ServiceProvider.GetService<TenantManager>();

        var tenant = tenantId != null ? tenantId.TenantId : tenantManager.GetCurrentTenant().TenantId;

        _log.InfoFormat("ClearUserMail(userId: '{0}' tenant: {1})", userId, tenant);

        var user = userId.ToString();

        RemoveUserFolders(_log);

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
                _log.Debug($"End Task {task.Id} with status = '{task.Status}'.");
            });

        return task;
    }

    private bool NeedRemove(MailBoxData mailbox, ILog taskLog)
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
                taskLog.InfoFormat("Tenant {0} isn't in cache", mailbox.TenantId);

                taskLog.Debug($"GetTenantStatus(OverdueDays={MailSettings.Cleaner.TenantOverdueDays})");

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
                taskLog.InfoFormat("Tenant {0} is in cache", mailbox.TenantId);

                type = (DefineConstants.TariffType)memTenantItem;
            }

            taskLog.InfoFormat("Tenant {0} has status '{1}'", mailbox.TenantId, type.ToString());

            if (type == DefineConstants.TariffType.LongDead)
            {
                taskLog.InfoFormat($"The mailbox {mailbox.MailBoxId} will be deleted");
                needRemove = true;
            }
            else
            {
                var isUserRemoved = mailbox.IsUserRemoved(tenantManager, userManager);

                taskLog.InfoFormat("User '{0}' status is '{1}'", mailbox.UserId, isUserRemoved ? "Terminated. The mailbox will be deleted" : "Not terminated");

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
        _log.InfoFormat("Processing MailboxId = {0}, email = '{1}', tenant = '{2}', user = '{3}'",
            mailbox.MailBoxId, mailbox.EMail.Address, mailbox.TenantId, mailbox.UserId);

        try
        {
            if (NeedRemove(mailbox, _log))
            {
                _log.Debug($"Mailbox {mailbox.MailBoxId} need remove. Removal started...");
                RemoveMailboxData(mailbox, true, _log);
            }
            else if (mailbox.IsRemoved)
            {
                _log.Info($"Mailbox {mailbox.MailBoxId} has been marked for deletion. Removal started...");
                RemoveMailboxData(mailbox, false, _log);
            }
            else
            {
                RemoveGarbageMailData(mailbox, (int)MailSettings.Cleaner.GarbageOverdueDays, _log);
            }

            _log.InfoFormat("Mailbox {0} processing complete.", mailbox.MailBoxId);
        }
        catch (Exception ex)
        {
            _log.ErrorFormat("Mailbox {0} processed with error : {1}", mailbox.MailBoxId, ex.ToString());
        }
    }

    private void RemoveMailboxData(MailBoxData mailbox, bool totalMailRemove, ILog log)
    {
        log.InfoFormat("RemoveMailboxData(id: {0} address: {1})", mailbox.MailBoxId, mailbox.EMail.ToString());

        try
        {
            using var scope = _serviceProvider.CreateScope();

            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();

            tenantManager.SetCurrentTenant(mailbox.TenantId);
            log.Debug($"RemoveMailboxData. Set current tenant: {tenantManager.GetCurrentTenant().TenantId}");

            var mbEngine = scope.ServiceProvider.GetService<MailboxEngine>();
            var factory = scope.ServiceProvider.GetService<MailDaoFactory>();

            if (!mailbox.IsRemoved)
            {
                log.Info("Mailbox is't removed.");

                var needRecalculateFolders = !totalMailRemove;

                if (mailbox.IsTeamlab)
                {
                    log.Info("RemoveTeamlabMailbox()");

                    var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

                    securityContext.AuthenticateMe(ASC.Core.Configuration.Constants.CoreSystem);

                    RemoveTeamlabMailbox(mailbox, log);
                }

                log.Info("SetMailboxRemoved()");

                mbEngine.RemoveMailBox(mailbox, needRecalculateFolders);

                mailbox.IsRemoved = true;
            }

            log.Debug($"MailDataStore.GetDataStore(Tenant = {mailbox.TenantId})");

            var storageFactory = scope.ServiceProvider.GetService<StorageFactory>();

            var dataStorage = storageFactory.GetMailStorage(mailbox.TenantId);

            dataStorage.QuotaController = null;

            log.Debug("GetMailboxAttachsCount()");

            var countAttachs = factory.GetMailGarbageDao().GetMailboxAttachsCount(mailbox);

            log.InfoFormat("Found {0} garbage attachments", countAttachs);

            if (countAttachs > 0)
            {
                var sumCount = 0;

                log.Debug($"GetMailboxAttachsGarbage(limit = {MailSettings.Cleaner.MaxFilesToRemoveAtOnce})");

                var attachGrbgList = factory.GetMailGarbageDao().GetMailboxAttachs(mailbox, (int)MailSettings.Cleaner.MaxFilesToRemoveAtOnce);

                sumCount += attachGrbgList.Count;

                log.InfoFormat("Clearing {0} garbage attachments ({1}/{2})", attachGrbgList.Count, sumCount, countAttachs);

                while (attachGrbgList.Any())
                {
                    foreach (var attachGrbg in attachGrbgList)
                    {
                        RemoveFile(dataStorage, attachGrbg.Path, log);
                    }

                    log.Debug("CleanupMailboxAttachs()");

                    factory.GetMailGarbageDao().CleanupMailboxAttachs(attachGrbgList);

                    log.Debug("GetMailboxAttachs()");

                    attachGrbgList = factory.GetMailGarbageDao().GetMailboxAttachs(mailbox, (int)MailSettings.Cleaner.MaxFilesToRemoveAtOnce);

                    if (!attachGrbgList.Any()) continue;

                    sumCount += attachGrbgList.Count;

                    log.InfoFormat("Found {0} garbage attachments ({1}/{2})", attachGrbgList.Count, sumCount,
                             countAttachs);
                }
            }

            log.Debug("GetMailboxMessagesCount()");

            var countMessages = factory.GetMailGarbageDao().GetMailboxMessagesCount(mailbox);

            log.InfoFormat("Found {0} garbage messages", countMessages);

            if (countMessages > 0)
            {
                var sumCount = 0;

                log.Debug($"GetMailboxMessagesGarbage(limit = {MailSettings.Cleaner.MaxFilesToRemoveAtOnce})");

                var messageGrbgList = factory.GetMailGarbageDao().GetMailboxMessages(mailbox, (int)MailSettings.Cleaner.MaxFilesToRemoveAtOnce);

                sumCount += messageGrbgList.Count;

                log.InfoFormat("Clearing {0} garbage messages ({1}/{2})", messageGrbgList.Count, sumCount, countMessages);

                while (messageGrbgList.Any())
                {
                    foreach (var mailMessageGarbage in messageGrbgList)
                    {
                        RemoveFile(dataStorage, mailMessageGarbage.Path, log);
                    }

                    log.Debug("CleanupMailboxMessages()");

                    factory.GetMailGarbageDao().CleanupMailboxMessages(messageGrbgList);

                    log.Debug("GetMailboxMessages()");

                    messageGrbgList = factory.GetMailGarbageDao().GetMailboxMessages(mailbox, (int)MailSettings.Cleaner.MaxFilesToRemoveAtOnce);

                    if (!messageGrbgList.Any()) continue;

                    sumCount += messageGrbgList.Count;

                    log.InfoFormat("Found {0} garbage messages ({1}/{2})", messageGrbgList.Count, sumCount,
                             countMessages);
                }
            }

            log.Debug("ClearMailboxData()");

            CleanupMailboxData(mailbox, totalMailRemove, factory);

            log.Debug($"Garbage mailbox '{mailbox.EMail.Address}' was totaly removed.");
        }
        catch (Exception ex)
        {
            log.ErrorFormat("RemoveMailboxData(mailboxId = {0}) Failure\r\nException: {1}", mailbox.MailBoxId, ex.ToString());

            throw;
        }
    }

    public void CleanupMailboxData(MailBoxData mailbox, bool totalRemove, MailDaoFactory mailDaoFactory)
    {
        if (!mailbox.IsRemoved)
            throw new Exception("Mailbox is not removed.");

        var mailDbContext = mailDaoFactory.GetContext();

        using var tx = mailDaoFactory.BeginTransaction(System.Data.IsolationLevel.ReadUncommitted);

        var exp = new СoncreteUserMailboxExp(mailbox.MailBoxId, mailbox.TenantId, mailbox.UserId, true);

        var mb = mailDaoFactory.GetMailboxDao().GetMailBox(exp);

        var deleteMailboxMessagesQuery = mailDbContext.MailMail
            .Where(m => m.MailboxId == mb.Id && m.TenantId == mb.Tenant && m.UserId == mb.User);

        mailDbContext.MailMail.RemoveRange(deleteMailboxMessagesQuery);

        mailDbContext.SaveChanges();

        var deleteMailboxAttachmentsQuery = mailDbContext.MailAttachment
            .Where(a => a.IdMailbox == mb.Id && a.Tenant == mb.Tenant);

        mailDbContext.MailAttachment.RemoveRange(deleteMailboxAttachmentsQuery);

        mailDbContext.SaveChanges();

        mailDaoFactory.GetMailboxDao().RemoveMailbox(mb, mailDbContext);

        if (totalRemove)
        {
            mailDaoFactory.GetFolderDao().Delete();

            var deleteContactInfoQuery = mailDbContext.MailContactInfo
                .Where(c => c.IdUser == mb.User && c.TenantId == mb.Tenant);

            mailDbContext.MailContactInfo.RemoveRange(deleteContactInfoQuery);

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
    }

    private void RemoveFile(IDataStore dataStorage, string path, ILog log)
    {
        try
        {
            log.Debug($"Removing file: {path}");

            dataStorage.DeleteAsync(string.Empty, path).Wait();

            log.InfoFormat("File: '{0}' removed successfully", path);
        }
        catch (FileNotFoundException)
        {
            log.WarnFormat("File: {0} not found", path);
        }
        catch (Exception ex)
        {
            log.ErrorFormat("RemoveFile(path: {0}) failed. Error: {1}", path, ex.ToString());
        }
    }

    private void RemoveUserMailDirectory(int tenant, string userId, ILog log)
    {
        log.Debug($"MailDataStore.GetDataStore(Tenant = {tenant})");

        using var scope = _serviceProvider.CreateScope();

        var storageFactory = scope.ServiceProvider.GetService<StorageFactory>();

        var dataStorage = storageFactory.GetMailStorage(tenant);

        var userMailDir = MailStoragePathCombiner.GetUserMailsDirectory(userId);

        try
        {
            log.InfoFormat("RemoveUserMailDirectory(Path: {0}, Tenant = {1} User = '{2}')", userMailDir, tenant, userId);

            dataStorage.DeleteDirectoryAsync(userMailDir).Wait();
        }
        catch (Exception ex)
        {
            log.ErrorFormat("MailDataStore.DeleteDirectory(path: {0}) failed. Error: {1}", userMailDir, ex.ToString());

            throw;
        }
    }

    public bool RemoveGarbageMailData(MailBoxData mailbox, int garbageDaysLimit, ILog log)
    {
        //TODO: Implement cleanup data marked as removed and trash messages exceeded garbageDaysLimit

        return true;
    }

    private void RemoveTeamlabMailbox(MailBoxData mailbox, ILog log)
    {
        if (mailbox == null)
            throw new ArgumentNullException("mailbox");

        if (!mailbox.IsTeamlab)
            return;

        using var scope = _serviceProvider.CreateScope();
        var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
        tenantManager.SetCurrentTenant(mailbox.TenantId);
        log.Debug($"RemoveTeamlabMailbox. Set current tenant: {tenantManager.GetCurrentTenant().TenantId}");

        var serverMailboxEngine = scope.ServiceProvider.GetService<ServerMailboxEngine>();

        try
        {
            serverMailboxEngine.RemoveMailbox(mailbox);
        }
        catch (Exception ex)
        {
            log.ErrorFormat("RemoveTeamlabMailbox(mailboxId = {0}) Failure\r\nException: {1}", mailbox.MailBoxId,
                ex.ToString());
        }
    }

    private void RemoveUserFolders(ILog log)
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
            log.ErrorFormat("RemoveUserFolders() Failure\r\nException: {0}", ex.ToString());
        }
    }

    private void RemoveUserMailboxes(int tenant, string user, ILog log)
    {
        using var scope = _serviceProvider.CreateScope();

        var mailboxEngine = scope.ServiceProvider.GetService<MailboxEngine>();

        var mailboxIterator = new MailboxIterator(mailboxEngine, tenant, user);

        var mailbox = mailboxIterator.First();

        if (mailboxIterator.IsDone)
        {
            log.Info("There are no user's mailboxes for deletion");
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
                log.ErrorFormat("RemoveMailboxData(MailboxId: {0}) failed. Error: {1}", mailbox.MailBoxId, ex);
            }

            mailbox = mailboxIterator.Next();
        }

        RemoveUserMailDirectory(tenant, user, log);
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
