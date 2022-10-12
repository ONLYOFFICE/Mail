/*
 *
 * (c) Copyright Ascensio System Limited 2010-2020
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * http://www.apache.org/licenses/LICENSE-2.0
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
*/

namespace ASC.Mail.ImapSync;

public class MailImapClient : IDisposable
{
    public readonly string UserName;
    public readonly int Tenant;
    public readonly string RedisKey;

    public bool IsReady { get; private set; } = false;

    private readonly ConcurrentQueue<ImapAction> imapActionsQueue;
    private readonly List<MailIMAPBox> mailIMAPBoxes;

    private readonly SemaphoreSlim _enginesFactorySemaphore;

    private readonly IServiceProvider clientScope;
    private readonly MailEnginesFactory _mailEnginesFactory;
    private readonly MailSettings _mailSettings;
    private readonly IMailInfoDao _mailInfoDao;
    private readonly StorageFactory _storageFactory;
    private readonly FolderEngine _folderEngine;
    private readonly UserFolderEngine _userFolderEngine;
    private readonly SignalrServiceClient _signalrServiceClient;
    private readonly RedisClient _redisClient;
    private readonly ILogger _log;
    private readonly ILogger _logStat;
    private readonly ILoggerProvider _logProvider;
    private readonly ApiHelper _apiHelper;
    private readonly TenantManager tenantManager;
    private readonly SecurityContext securityContext;
    private readonly System.Timers.Timer aliveTimer;
    private readonly System.Timers.Timer processActionFromImapTimer;

    private bool crmAvailable;
    private bool needUserUpdate;
    private bool needUserMailBoxUpdate;
    private bool userActivityDetected;

    private readonly CancellationTokenSource _cancelToken;

    public EventHandler OnCriticalError;

    public async Task CheckRedis()
    {
        needUserMailBoxUpdate = true;

        userActivityDetected = true;

        int iterationCount = 0;

        try
        {
            while (true)
            {
                var actionFromCache = await _redisClient.PopFromQueue<CashedMailUserAction>(RedisKey);

                if (actionFromCache == null) break;

                iterationCount++;

                if (actionFromCache.Action == MailUserAction.StartImapClient) continue;

                if (actionFromCache.Action == MailUserAction.MoveTo && actionFromCache.Destination == (int)FolderType.UserFolder)
                {
                    actionFromCache.Data = simpleImapClients.FirstOrDefault(x => x.UserFolderID == (int?)actionFromCache.UserFolderId)?.ImapWorkFolderFullName;
                }

                simpleImapClients.ForEach(x => x.ExecuteUserAction(actionFromCache));
            }
        }
        catch (Exception ex)
        {
            _log.ErrorMailImapClient("CheckRedis method", ex.Message);
        }

        _log.DebugMailImapClient($"Read {iterationCount} keys. Total IMAP clients: {simpleImapClients.Count}");
    }

    public async Task<int> ClearUserRedis()
    {
        int result = 0;

        while (true)
        {
            var actionFromCache = await _redisClient.PopFromQueue<CashedMailUserAction>(RedisKey);

            if (actionFromCache == null) break;

            result++;
        }

        return result;
    }

    public MailImapClient(
        string userName,
        int tenant,
        MailSettings mailSettings,
        IServiceProvider serviceProvider,
        SignalrServiceClient signalrServiceClient,
        CancellationToken cancelToken,
        ILoggerProvider logProvider)
    {
        _mailSettings = mailSettings;

        UserName = userName;
        Tenant = tenant;
        RedisKey = "ASC.MailAction:" + userName;

        clientScope = serviceProvider.CreateScope().ServiceProvider;

        var redisFactory = clientScope.GetService<RedisFactory>();

        _redisClient = redisFactory.GetRedisClient();

        if (_redisClient == null)
        {
            throw new Exception($"No redis connection. UserName={UserName}");
        }

        tenantManager = clientScope.GetService<TenantManager>();
        tenantManager.SetCurrentTenant(tenant);

        securityContext = clientScope.GetService<SecurityContext>();
        securityContext.AuthenticateMe(new Guid(UserName));

        _storageFactory = clientScope.GetService<StorageFactory>();
        _mailInfoDao = clientScope.GetService<IMailInfoDao>();

        _folderEngine = clientScope.GetService<FolderEngine>();
        _userFolderEngine = clientScope.GetService<UserFolderEngine>();
        _signalrServiceClient = signalrServiceClient;

        _log = logProvider.CreateLogger($"ASC.Mail.User_{userName}");

        _logStat = logProvider.CreateLogger($"ASC.Mail.User_{userName}");
        _logProvider = logProvider;

        if (_mailSettings.Aggregator.CollectStatistics)
            _logStat = logProvider.CreateLogger("ASC.Mail.Stat");

        _apiHelper = clientScope.GetService<ApiHelper>();

        _mailEnginesFactory = clientScope.GetService<MailEnginesFactory>();
        _enginesFactorySemaphore = new SemaphoreSlim(1, 1);

        _cancelToken = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);

        mailIMAPBoxes = new();
        imapActionsQueue = new();

        aliveTimer = new System.Timers.Timer((_mailSettings.ImapSync.AliveTimeInMinutes ?? 1) * 60 * 1000);

        aliveTimer.Elapsed += AliveTimer_Elapsed;

        processActionFromImapTimer = new System.Timers.Timer(1000);

        processActionFromImapTimer.Elapsed += ProcessActionFromImapTimer_Elapsed;

        processActionFromImapTimer.Enabled = true;

        if (!UpdateSimplImapClients())
        {
            throw new Exception($"No MailBoxes. UserName={UserName}");
        }

        aliveTimer.Enabled = true;

        IsReady = true;
        needUserUpdate = false;
    }

    private List<MailBoxData> GetUserMailBoxes()
    {
        _enginesFactorySemaphore.Wait();

        try
        {
            var userMailboxesExp = new UserMailboxesExp(Tenant, UserName, _mailSettings.Defines.ImapSyncStartDate.Value, onlyTeamlab: true);

            return _mailEnginesFactory.MailboxEngine.GetMailboxDataList(userMailboxesExp).Where(x => x.Enabled).ToList();
        }
        catch (Exception ex)
        {
            _log.ErrorMailImapClient("Get User mailboxes", ex.Message);

            return null;
        }
        finally
        {
            if (_enginesFactorySemaphore.CurrentCount == 0) _enginesFactorySemaphore.Release();
        }
    }

    private bool IsUserOnLine()
    {
        var mailboxes = GetUserMailBoxes();

        if (mailboxes == null) return false;

        return mailboxes.Any(x => x.Active);
    }

    private bool UpdateSimplImapClients()
    {
        var mailBoxes = GetUserMailBoxes();

        if (mailBoxes == null) return false;

        foreach (var mailBox in mailBoxes)
        {
            var mailIMAPBox = mailIMAPBoxes.FirstOrDefault(x => x.Account.MailBoxId == mailBox.MailBoxId);

            if (mailIMAPBox == null)
            {
                mailIMAPBox = new(mailBox);

                mailIMAPBoxes.Add(mailIMAPBox);
            }
        }

        var mailBoxIds = mailBoxes.Select(x => x.MailBoxId).ToList();

        foreach (var client in simpleImapClients)
        {
            if (mailBoxIds.Contains(client.Account.MailBoxId)) continue;

            DeleteSimpleImapClients(client.Account);
        }

        if (simpleImapClients.Count == 0)
        {
            OnCriticalError?.Invoke(this, EventArgs.Empty);
        }

        crmAvailable = simpleImapClients.Any(client => client.Account.IsCrmAvailable(tenantManager, securityContext, _apiHelper, _log));

        needUserMailBoxUpdate = false;

        return true;
    }

    private void CreateSimpleImapClients(MailBoxData mailbox)
    {
        if (simpleImapClients.Any(x => x.Account.MailBoxId == mailbox.MailBoxId))
        {
            DeleteSimpleImapClients(mailbox);
        }

        try
        {
            var rootSimpleImapClient = new SimpleImapClient(mailbox, _mailSettings, _logProvider, "", _cancelToken.Token);

            if (!SetEvents(rootSimpleImapClient)) return;

            simpleImapClients.Add(rootSimpleImapClient);

            rootSimpleImapClient.Init("");

            rootSimpleImapClient.OnNewFolderCreate += RootSimpleImapClient_OnNewFolderCreate;

            rootSimpleImapClient.OnFolderDelete += RootSimpleImapClient_OnFolderDelete;

            foreach (var folder in rootSimpleImapClient.ImapFoldersFullName)
            {
                CreateSimpleImapClient(mailbox, folder);
            }

            _enginesFactorySemaphore.Wait();

            string isLocked = _mailEnginesFactory.MailboxEngine.LockMaibox(mailbox.MailBoxId) ? "locked" : "didn`t lock";

            _log.DebugMailImapClientCreateSimpleImapClients(mailbox.MailBoxId, isLocked);
        }
        catch (Exception ex)
        {
            _log.ErrorMailImapClient($"Create IMAP clients for {mailbox.EMail.Address}", ex.Message);
        }
        finally
        {
            if (_enginesFactorySemaphore.CurrentCount == 0) _enginesFactorySemaphore.Release();
        }
    }

    private void RootSimpleImapClient_OnFolderDelete(object sender, string e)
    {
        var deletedClient = simpleImapClients.FirstOrDefault(x => x.ImapWorkFolderFullName == e);

        if (deletedClient != null)
        {
            DeleteSimpleImapClient(deletedClient);
        }
    }

    private void RootSimpleImapClient_OnNewFolderCreate(object sender, (string, bool) e)
    {
        if (sender is SimpleImapClient simpleImapClient)
        {
            CreateSimpleImapClient(simpleImapClient.Account, e);
        }
    }

    private void CreateSimpleImapClient(MailBoxData mailbox, (string folderName, bool IsUserFolder) folder)
    {
        try
        {
            var simpleImapClient = new SimpleImapClient(mailbox, _mailSettings, _logProvider, folder.folderName, _cancelToken.Token);

            if (!SetEvents(simpleImapClient)) return;

            simpleImapClients.Add(simpleImapClient);

            if (folder.IsUserFolder)
            {
                var userFolder = _userFolderEngine.GetByNameOrCreate(folder.folderName);

                simpleImapClient.UserFolderID = userFolder.Id;
            }

            simpleImapClient.Init(folder.folderName);
        }
        catch (Exception ex)
        {
            _log.ErrorMailImapClient($"Create IMAP client for {mailbox.EMail.Address}, folder {folder.folderName}", ex.Message);
        }
    }

    private bool SetEvents(SimpleImapClient simpleImapClient)
    {
        if (simpleImapClient == null) return false;

        simpleImapClient.NewMessage += ImapClient_NewMessage;
        simpleImapClient.MessagesListUpdated += ImapClient_MessagesListUpdated;
        simpleImapClient.NewActionFromImap += ImapClient_NewActionFromImap;
        simpleImapClient.OnCriticalError += ImapClient_OnCriticalError;

        return true;
    }

    private bool UnSetEvents(SimpleImapClient simpleImapClient)
    {
        if (simpleImapClient == null) return false;

        simpleImapClient.NewMessage -= ImapClient_NewMessage;
        simpleImapClient.MessagesListUpdated -= ImapClient_MessagesListUpdated;
        simpleImapClient.NewActionFromImap -= ImapClient_NewActionFromImap;
        simpleImapClient.OnCriticalError -= ImapClient_OnCriticalError;

        return true;
    }

    private void DeleteSimpleImapClient(SimpleImapClient simpleImapClient)
    {
        UnSetEvents(simpleImapClient);

        simpleImapClient.Stop();

        simpleImapClients.Remove(simpleImapClient);
    }

    private void DeleteSimpleImapClients(MailBoxData mailbox)
    {
        try
        {
            var deletedSimpleImapClients = simpleImapClients.Where(x => x.Account.MailBoxId == mailbox.MailBoxId).ToList();

            deletedSimpleImapClients.ForEach(DeleteSimpleImapClient);

            _enginesFactorySemaphore.Wait();

            string isLocked = _mailEnginesFactory.MailboxEngine.ReleaseMailbox(mailbox, _mailSettings) ? "unlocked" : "didn`t unlock";

            _log.DebugMailImapClientDeleteSimpleImapClients(deletedSimpleImapClients.Count, mailbox.MailBoxId, isLocked);
        }
        catch (Exception ex)
        {
            _log.ErrorMailImapClient($"Delete IMAP clients for {mailbox.EMail.Address}", ex.Message);
        }
        finally
        {
            if (_enginesFactorySemaphore.CurrentCount == 0) _enginesFactorySemaphore.Release();
        }
    }

    private void ProcessActionFromImapTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        if (IsReady && needUserMailBoxUpdate)
        {
            if (!UpdateSimplImapClients()) return;
        }

        _enginesFactorySemaphore.Wait();

        try
        {
            var ids = new List<int>();

            while (imapActionsQueue.TryDequeue(out ImapAction imapAction))
            {
                ids.Add(imapAction.MessageIdInDB);

                if (imapActionsQueue.TryPeek(out ImapAction nextImapAction))
                {
                    if (imapAction.IsSameImapFolderAndAction(nextImapAction)) continue;
                }

                needUserUpdate = imapAction.FolderAction switch
                {
                    MailUserAction.Nothing => true,
                    MailUserAction.SetAsRead => _mailEnginesFactory.MessageEngine.SetUnread(ids, false),
                    MailUserAction.SetAsUnread => _mailEnginesFactory.MessageEngine.SetUnread(ids, true),
                    MailUserAction.SetAsImportant => _mailEnginesFactory.MessageEngine.SetImportant(ids, true),
                    MailUserAction.SetAsNotImpotant => _mailEnginesFactory.MessageEngine.SetImportant(ids, false),
                    MailUserAction.SetAsDeleted => _mailEnginesFactory.MessageEngine.SetRemoved(ids)
                };

                if (_folderEngine.needRecalculateFolders) _folderEngine.RecalculateFolders();

                _log.DebugMailImapClientProcessAction(imapAction.FolderAction.ToString(), needUserUpdate.ToString().ToUpper(), ids.Count);

                StringBuilder sb = new();

                ids.ForEach(x => sb.Append(x.ToString() + ", "));

                _log.DebugMailImapClientProcessActionIds(sb.ToString());

                ids.Clear();
            }
        }
        catch (Exception ex)
        {
            _log.ErrorMailImapClient($"Process action from IMAP ", ex.Message);
        }
        finally
        {
            if (needUserUpdate) needUserUpdate = !SendUnreadUser();

            if (_enginesFactorySemaphore.CurrentCount == 0) _enginesFactorySemaphore.Release();
        }
    }

    private void AliveTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        if (userActivityDetected || IsUserOnLine())
        {
            userActivityDetected = false;

            return;
        }

        _log.DebugMailImapClientNoUserOnline();

        OnCriticalError?.Invoke(this, EventArgs.Empty);
    }

    private void ImapClient_OnCriticalError(object sender, bool IsAuthenticationError)
    {
        if (sender is SimpleImapClient simpleImapClient)
        {
            if (IsAuthenticationError)
            {
                SetMailboxAuthError(simpleImapClient);
            }
            else
            {
                CreateSimpleImapClient(simpleImapClient.Account, (simpleImapClient.ImapWorkFolderFullName, simpleImapClient.UserFolderID.HasValue));
            }

            DeleteSimpleImapClient(simpleImapClient);
        }
    }

    private void ImapClient_NewActionFromImap(object sender, ImapAction e)
    {
        imapActionsQueue.Enqueue(e);

        _log.DebugMailImapClientNewActionFromImap(imapActionsQueue.Count, e.FolderAction.ToString());
    }

    private void ImapClient_MessagesListUpdated(object sender, EventArgs e)
    {
        if (sender is SimpleImapClient simpleImapClient)
        {
            UpdateDbFolder(simpleImapClient);
        }
    }

    private void ImapClient_NewMessage(object sender, (MimeMessage, MessageDescriptor) e)
    {
        if (sender is SimpleImapClient simpleImapClient)
        {
            CreateMessageInDB(simpleImapClient, e.Item1, e.Item2);
        }
    }

    private void SetMailboxAuthError(SimpleImapClient simpleImapClient)
    {
        _enginesFactorySemaphore.Wait();

        try
        {
            if (simpleImapClient.Account.AuthErrorDate.HasValue) return;

            simpleImapClient.Account.AuthErrorDate = DateTime.UtcNow;

            _mailEnginesFactory.MailboxEngine.SetMaiboxAuthError(simpleImapClient.Account.MailBoxId, simpleImapClient.Account.AuthErrorDate.Value);
        }
        catch (Exception ex)
        {
            _log.ErrorMailImapClientMailboxAuth(Tenant, simpleImapClient.Account.MailBoxId, simpleImapClient.Account.EMail.ToString(), ex.ToString());
        }
        finally
        {
            if (_enginesFactorySemaphore.CurrentCount == 0) _enginesFactorySemaphore.Release();
        }
    }

    private void UpdateDbFolder(SimpleImapClient simpleImapClient)
    {
        if (simpleImapClient.ImapMessagesList == null)
        {
            _log.DebugMailImapClientUpdateDbFolder();

            return;
        }

        _enginesFactorySemaphore.Wait();

        try
        {
            List<MailInfo> workFolderMails;

            if (simpleImapClient.UserFolderID.HasValue) workFolderMails = GetMailUserFolderMessages(simpleImapClient);
            else workFolderMails = GetMailFolderMessages(simpleImapClient);

            _log.DebugMailImapClientUpdateDbFolderMailsCount(workFolderMails.Count);

            if (simpleImapClient.ImapMessagesList != null)
            {
                foreach (var imap_message in simpleImapClient.ImapMessagesList)
                {
                    _log.DebugMailImapClientUpdateDbFolderMessageUidl(imap_message.UniqueId.Id);

                    var uidl = imap_message.UniqueId.ToUidl(simpleImapClient.Folder);

                    var db_message = workFolderMails.FirstOrDefault(x => x.Uidl == uidl && (!simpleImapClient.IsMessageTracked(x.Id)));

                    if (db_message == null)
                    {
                        _log.DebugMailImapClientUpdateDbFolderMessageUidlNotFound(uidl);

                        simpleImapClient.TryGetNewMessage(imap_message);

                        continue;
                    }

                    imap_message.MessageIdInDB = db_message.Id;

                    SetMessageFlagsFromImap(imap_message, db_message);

                    workFolderMails.Remove(db_message);
                }
            }

            if (workFolderMails.Any())
            {
                if (!simpleImapClient.UserFolderID.HasValue)
                {
                    _mailEnginesFactory.MessageEngine.SetRemoved(
                        workFolderMails.Select(x => x.Id).ToList(),
                        simpleImapClient.UserFolderID);
                }
            }

        }
        catch (Exception ex)
        {
            _log.ErrorMailImapClient($"Update folder {simpleImapClient.ImapWorkFolderFullName}", ex.Message);
        }
        finally
        {
            if (_enginesFactorySemaphore.CurrentCount == 0) _enginesFactorySemaphore.Release();

            needUserUpdate = true;

            if (_folderEngine.needRecalculateFolders) _folderEngine.RecalculateFolders();
        }
    }

    private void SetMessageFlagsFromImap(MessageDescriptor imap_message, MailInfo db_message)
    {
        if (imap_message == null || db_message == null) return;

        try
        {
            _log.DebugMailImapClientSetMessageFlagsFromImap(imap_message.UniqueId.Id, imap_message.Flags.Value.ToString(), db_message.Uidl, db_message.Folder.ToString(), db_message.IsRemoved);

            bool unread = !imap_message.Flags.Value.HasFlag(MessageFlags.Seen);
            bool important = imap_message.Flags.Value.HasFlag(MessageFlags.Flagged);
            bool removed = imap_message.Flags.Value.HasFlag(MessageFlags.Deleted);

            if (db_message.IsNew ^ unread) _mailEnginesFactory.MessageEngine.SetUnread(new List<int>() { db_message.Id }, unread, true);
            if (db_message.Importance ^ important) _mailEnginesFactory.MessageEngine.SetImportant(new List<int>() { db_message.Id }, important);
            if (removed) _mailEnginesFactory.MessageEngine.SetRemoved(new List<int>() { db_message.Id });
        }
        catch (Exception ex)
        {
            _log.ErrorMailImapClient($"Set massage flag from IMAP", ex.Message);
        }

        if (_folderEngine.needRecalculateFolders) _folderEngine.RecalculateFolders();
    }

    private bool CreateMessageInDB(SimpleImapClient simpleImapClient, MimeMessage message, MessageDescriptor imap_message)
    {
        _log.DebugMailImapClientNewMessage(simpleImapClient.ImapWorkFolderFullName, imap_message.UniqueId.ToString());

        bool result = true;

        Stopwatch watch = null;

        if (_mailSettings.Aggregator.CollectStatistics)
        {
            watch = new Stopwatch();
            watch.Start();
        }

        _enginesFactorySemaphore.Wait();

        message.FixDateIssues(_log, imap_message?.InternalDate);

        bool unread = false, impotant = false;

        if ((imap_message != null) && imap_message.Flags.HasValue)
        {
            unread = !imap_message.Flags.Value.HasFlag(MessageFlags.Seen);
            impotant = imap_message.Flags.Value.HasFlag(MessageFlags.Flagged);
        }

        message.FixEncodingIssues();

        var folder = simpleImapClient.MailWorkFolder;
        var uidl = imap_message.UniqueId.ToUidl(simpleImapClient.Folder);

        _log.InfoMailImapClientGetMessage(uidl, simpleImapClient.Account.MailBoxId, simpleImapClient.Account.EMail.ToString());

        try
        {
            var findedMessages = GetMailFolderMessages(simpleImapClient, message.MessageId, null);

            findedMessages.RemoveAll(x => simpleImapClient.IsMessageTracked(x.Id));

            if (findedMessages.Count == 0)
            {
                var messageDB = _mailEnginesFactory.MessageEngine
                    .SaveWithoutCheck(simpleImapClient.Account, message, uidl, folder, simpleImapClient.UserFolderID, unread, impotant);

                if (messageDB == null || messageDB.Id <= 0)
                {
                    _log.DebugMailImapClientCreateMessageInDBFailed();

                    return false;
                }

                imap_message.MessageIdInDB = messageDB.Id;

                DoOptionalOperations(messageDB, message, simpleImapClient);

                _log.InfoMailImapClientMessageSaved(messageDB.Id, messageDB.From, messageDB.Subject, messageDB.IsNew);

                needUserUpdate = true;

                return true;
            }

            MailInfo messageInfo = findedMessages.FirstOrDefault(x => x.IsRemoved == false);

            if (messageInfo == null)
            {
                messageInfo = findedMessages[0];

                var restoreQuery = SimpleMessagesExp.CreateBuilder(simpleImapClient.Account.TenantId, simpleImapClient.Account.UserId, isRemoved: true)
                                                    .SetMessageId(messageInfo.Id)
                                                    .Build();

                if (_mailInfoDao.SetFieldValue(restoreQuery, "IsRemoved", false) > 0) messageInfo.IsRemoved = false;
            }

            imap_message.MessageIdInDB = messageInfo.Id;

            string imap_message_uidl = imap_message.UniqueId.ToUidl(simpleImapClient.Folder);

            if (imap_message_uidl != messageInfo.Uidl)
            {
                var updateUidlQuery = SimpleMessagesExp.CreateBuilder(Tenant, UserName)
                            .SetMessageId(messageInfo.Id)
                            .Build();

                int resultSetFieldValue = _mailInfoDao.SetFieldValue(updateUidlQuery, "Uidl", imap_message_uidl);

                if (resultSetFieldValue > 0) messageInfo.Uidl = imap_message_uidl;
            }

            _log.InfoMailImapClientMessageUpdated(messageInfo.Id, simpleImapClient.Folder.ToString(), messageInfo.Subject);

            SetMessageFlagsFromImap(imap_message, messageInfo);

            needUserUpdate = true;

            return true;
        }
        catch (Exception ex)
        {
            _log.ErrorMailImapClient($"Create message in DB", ex.Message);

            result = false;
        }
        finally
        {
            if (_enginesFactorySemaphore.CurrentCount == 0) _enginesFactorySemaphore.Release();

            if (_mailSettings.Aggregator.CollectStatistics && watch != null)
            {
                watch.Stop();

                LogStat(simpleImapClient, "CreateMessageInDB", watch.Elapsed, result);

                _log.DebugMailImapClientCreateMessageInDB(watch.Elapsed.TotalMilliseconds);
            }
        }

        return result;
    }

    private List<MailInfo> GetMailFolderMessages(SimpleImapClient simpleImapClient, string mimeMessageId = null, bool? isRemoved = false)
    {
        var exp = SimpleMessagesExp.CreateBuilder(Tenant, UserName, isRemoved)
            .SetMailboxId(simpleImapClient.Account.MailBoxId)
            .SetFolder((int)simpleImapClient.MailWorkFolder.Folder);

        if (!string.IsNullOrEmpty(mimeMessageId)) exp.SetMimeMessageId(mimeMessageId);

        if (simpleImapClient.MailWorkFolder.Tags.Length > 0)
        {
            var tags = _mailEnginesFactory.TagEngine.GetOrCreateTags(Tenant, UserName, simpleImapClient.MailWorkFolder.Tags);

            exp.SetTagIds(tags);
        }

        var excludetags = _mailEnginesFactory.TagEngine.GetOrCreateTags(Tenant, UserName, simpleImapClient.ExcludeTags);

        exp.SetExcludeTagIds(excludetags);

        return _mailInfoDao.GetMailInfoList(exp.Build());
    }

    private List<MailInfo> GetMailUserFolderMessages(SimpleImapClient simpleImapClient, bool? isRemoved = false)
    {
        if (simpleImapClient.UserFolderID == null) return null;

        var exp = SimpleMessagesExp.CreateBuilder(Tenant, UserName, isRemoved)
            .SetMailboxId(simpleImapClient.Account.MailBoxId)
            .SetUserFolderId(simpleImapClient.UserFolderID.Value);

        return _mailInfoDao.GetMailInfoList(exp.Build());
    }

    private void LogStat(SimpleImapClient simpleImapClient, string method, TimeSpan duration, bool failed)
    {
        _logStat.DebugStatistic(duration.TotalMilliseconds, method, failed, simpleImapClient.Account.MailBoxId, simpleImapClient.Account.EMail.ToString());
    }

    private bool SendUnreadUser()
    {
        if (UserName == Constants.LostUser.Id.ToString()) return true;

        try
        {
            var count = _folderEngine.GetUserUnreadMessageCount(UserName);

            _signalrServiceClient.SendUnreadUser(Tenant, UserName, count);
        }
        catch (Exception ex)
        {
            var innerError = ex.InnerException == null ? "No" : ex.InnerException.Message;
            _log.ErrorMailImapClientSendUnreadUser(ex.Message, innerError);

            return false;
        }
        return true;
    }

    private void DoOptionalOperations(MailMessageData message, MimeMessage mimeMessage, SimpleImapClient simpleImapClient)
    {
        try
        {
            var tagIds = new List<int>();

            if (simpleImapClient.MailWorkFolder.Tags.Any())
            {
                _log.DebugMailImapClientGetOrCreateTags();

                tagIds = _mailEnginesFactory.TagEngine.GetOrCreateTags(Tenant, UserName, simpleImapClient.MailWorkFolder.Tags);
            }

            _log.DebugMailImapClientIsCrmAvailable();

            if (crmAvailable)
            {
                _log.DebugMailImapClientGetCrmTags();

                var crmTagIds = _mailEnginesFactory.TagEngine.GetCrmTags(message.FromEmail);

                if (crmTagIds.Any())
                {
                    if (tagIds == null)
                        tagIds = new List<int>();

                    tagIds.AddRange(crmTagIds.Select(t => t.TagId));
                }
            }

            if (tagIds.Any())
            {
                if (message.TagIds == null || !message.TagIds.Any())
                    message.TagIds = tagIds;
                else
                    message.TagIds.AddRange(tagIds);

                message.TagIds = message.TagIds.Distinct().ToList();
            }

            _log.DebugMailImapClientAddMessageToIndex();

            var mailMail = message.ToMailMail(Tenant, new Guid(UserName));

            _mailEnginesFactory.IndexEngine.Add(mailMail);

            foreach (var tagId in tagIds)
            {
                try
                {
                    _log.DebugMailImapClientSetMessagesTag(tagId);

                    _mailEnginesFactory.TagEngine.SetMessagesTag(new List<int> { message.Id }, tagId);
                }
                catch (Exception e)
                {
                    _log.ErrorMailImapClientSetMessagesTag(Tenant, UserName, message.Id,
                        tagIds != null ? string.Join(",", tagIds) : "null", e.ToString());
                }
            }

            _log.DebugMailImapClientAddRelationshipEvent();

            _mailEnginesFactory.CrmLinkEngine.AddRelationshipEventForLinkedAccounts(simpleImapClient.Account, message);

            _log.DebugMailImapClientSaveEmailInData();

            _mailEnginesFactory.EmailInEngine.SaveEmailInData(simpleImapClient.Account, message, _mailSettings.Defines.DefaultApiSchema);

            _log.DebugMailImapClientSendAutoreply();

            _mailEnginesFactory.AutoreplyEngine.SendAutoreply(simpleImapClient.Account, message, _mailSettings.Defines.DefaultApiSchema);

            _log.DebugMailImapClientUploadIcsToCalendar();

            if (simpleImapClient.MailWorkFolder.Folder != Enums.FolderType.Spam)
            {
                _mailEnginesFactory.CalendarEngine
                    .UploadIcsToCalendar(simpleImapClient.Account, message.CalendarId, message.CalendarUid, message.CalendarEventIcs,
                        message.CalendarEventCharset, message.CalendarEventMimeType,
                        message.Attachments, mimeMessage.Attachments);
            }

            if (_mailSettings.Defines.SaveOriginalMessage)
            {
                _log.DebugMailImapClientStoreMailEml();
                StoreMailEml(Tenant, UserName, message.StreamId, mimeMessage);
            }

            _log.DebugMailImapClientApplyFilters();

            var filters = _mailEnginesFactory.FilterEngine.GetList();

            var filtersAppliedSuccessfull = _mailEnginesFactory.FilterEngine.ApplyFilters(message, simpleImapClient.Account, simpleImapClient.MailWorkFolder, filters).OrderByDescending(x => x.Action).ToList();

            foreach (var filterAppliedSuccessfull in filtersAppliedSuccessfull)
            {
                int destination = -1;

                if (filterAppliedSuccessfull.Action == Enums.Filter.ActionType.MoveTo)
                {
                    string destinationString = new(filterAppliedSuccessfull.Data.Where(x => Char.IsDigit(x)).ToArray());

                    int.TryParse(destinationString, out destination);
                }

                CashedMailUserAction action = new()
                {
                    Uds = new List<int>() { message.Id },
                    Action = filterAppliedSuccessfull.Action switch
                    {
                        Enums.Filter.ActionType.MarkAsImportant => MailUserAction.SetAsImportant,
                        Enums.Filter.ActionType.MarkAsRead => MailUserAction.SetAsRead,
                        Enums.Filter.ActionType.DeleteForever => MailUserAction.SetAsDeleted,
                        Enums.Filter.ActionType.MoveTo => MailUserAction.MoveTo,
                        Enums.Filter.ActionType.MarkTag => MailUserAction.Nothing,
                        _ => MailUserAction.Nothing
                    },
                    Destination = destination
                };

                simpleImapClient.ExecuteUserAction(action);
            }
        }
        catch (Exception ex)
        {
            _log.ErrorMailImapClient($"Do optional operations", ex.Message);
        }

        needUserUpdate = true;
    }

    public string StoreMailEml(int tenant, string userId, string streamId, MimeMessage message)
    {
        if (message == null)
            return string.Empty;

        // Using id_user as domain in S3 Storage - allows not to add quota to tenant.
        var savePath = MailStoragePathCombiner.GetEmlKey(userId, streamId);

        var storage = _storageFactory.GetMailStorage(tenant);

        try
        {
            using var stream = new MemoryStream();

            message.WriteTo(stream);

            var res = storage.SaveAsync(savePath, stream, MailStoragePathCombiner.EML_FILE_NAME).Result.ToString();

            _log.InfoMailImapClientStoreMailEml(tenant, userId, savePath, res);

            return res;
        }
        catch (Exception ex)
        {
            _log.ErrorMailImapClient($"Store mail EML", ex.Message);
        }

        return string.Empty;
    }

    public void Stop()
    {
        IsReady = false;
        try
        {
            var mailBoxes = simpleImapClients.Select(x => x.Account).Distinct().ToList();

            mailBoxes.ForEach(DeleteSimpleImapClients);

            aliveTimer.Stop();
            aliveTimer.Elapsed -= AliveTimer_Elapsed;

            processActionFromImapTimer.Stop();
            processActionFromImapTimer.Elapsed -= ProcessActionFromImapTimer_Elapsed;

            _cancelToken?.Cancel();
        }
        catch (Exception ex)
        {
            _log.ErrorMailImapClient($"Stop error", ex.Message);
        }
    }

    public void Dispose()
    {
        Stop();

        _log.InfoMailImapClientDispose();

        GC.SuppressFinalize(this);
    }
}
