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
    private readonly List<SimpleImapClient> simpleImapClients;

    private readonly SemaphoreSlim _enginesFactorySemaphore;

    private readonly IServiceProvider clientScope;
    private readonly MailEnginesFactory _mailEnginesFactory;
    private readonly MailSettings _mailSettings;
    private readonly IMailInfoDao _mailInfoDao;
    private readonly StorageFactory _storageFactory;
    private readonly FolderEngine _folderEngine;
    private readonly SignalrServiceClient _signalrServiceClient;
    private readonly RedisClient _redisClient;
    private readonly ILog _log;
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

                if (actionFromCache.Action == MailUserAction.StartImapClient) continue;

                simpleImapClients.ForEach(x => x.ExecuteUserAction(actionFromCache.Uds, actionFromCache.Action, actionFromCache.Destination));
            }
        }
        catch (Exception ex)
        {
            _log.Error($"CheckRedis error: {ex.Message}.");
        }

        _log.Debug($"CheckRedis: {iterationCount} keys readed. User have {simpleImapClients.Count} clients");
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

    public MailImapClient(string userName, int tenant, MailSettings mailSettings, IServiceProvider serviceProvider, SignalrServiceClient signalrServiceClient, CancellationToken cancelToken)
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
        _signalrServiceClient = signalrServiceClient;

        _log = clientScope.GetService<ILog>();
        _apiHelper = clientScope.GetService<ApiHelper>();

        _mailEnginesFactory = clientScope.GetService<MailEnginesFactory>();
        _enginesFactorySemaphore = new SemaphoreSlim(1, 1);

        _log.Name = $"ASC.Mail.User_{userName}";

        _cancelToken = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);

        simpleImapClients = new List<SimpleImapClient>();
        imapActionsQueue = new ConcurrentQueue<ImapAction>();

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
            _log.Error($"GetUserMailBoxes exception: {ex}");

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
            if (simpleImapClients.Any(x => x.Account.MailBoxId == mailBox.MailBoxId)) continue;

            CreateSimpleImapClients(mailBox);
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
            var rootSimpleImapClient = new SimpleImapClient(mailbox, _mailSettings, clientScope.GetService<ILog>(), "", _cancelToken.Token);

            if (!SetEvents(rootSimpleImapClient)) return;

            simpleImapClients.Add(rootSimpleImapClient);

            rootSimpleImapClient.Init("");

            foreach (var folder in rootSimpleImapClient.ImapFoldersFullName)
            {
                CreateSimpleImapClient(mailbox, folder);
            }

            _enginesFactorySemaphore.Wait();

            string isLocked = _mailEnginesFactory.MailboxEngine.LockMaibox(mailbox.MailBoxId) ? "locked" : "didn`t lock";

            _log.Debug($"CreateSimpleImapClients: MailboxId={mailbox.MailBoxId} created and {isLocked}.");
        }
        catch (Exception ex)
        {
            _log.Error($"CreateSimpleImapClients exception: {ex}");
        }
        finally
        {
            if (_enginesFactorySemaphore.CurrentCount == 0) _enginesFactorySemaphore.Release();
        }
    }

    private void CreateSimpleImapClient(MailBoxData mailbox, string folderName)
    {
        try
        {
            var simpleImapClient = new SimpleImapClient(mailbox, _mailSettings, clientScope.GetService<ILog>(), folderName, _cancelToken.Token);

            if (!SetEvents(simpleImapClient)) return;

            simpleImapClients.Add(simpleImapClient);

            simpleImapClient.Init(folderName);
        }
        catch (Exception ex)
        {
            _log.Error($"CreateSimpleImapClient {mailbox.Name}.{folderName} exception: {ex}");
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

    private void DeleteSimpleImapClients(MailBoxData mailBoxData)
    {
        try
        {
            var deletedSimpleImapClients = simpleImapClients.Where(x => x.Account.MailBoxId == mailBoxData.MailBoxId).ToList();

            deletedSimpleImapClients.ForEach(DeleteSimpleImapClient);

            _enginesFactorySemaphore.Wait();

            string isLocked = _mailEnginesFactory.MailboxEngine.ReleaseMailbox(mailBoxData, _mailSettings) ? "unlocked" : "didn`t unlock";

            _log.Debug($"DeleteSimpleImapClients: {deletedSimpleImapClients.Count} clients with MailboxId={mailBoxData.MailBoxId} removed and {isLocked}.");
        }
        catch (Exception ex)
        {
            _log.Error($"DeleteSimpleImapClient exception: {ex}");
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

                bool result = false;

                switch (imapAction.FolderAction)
                {
                    case MailUserAction.Nothing:
                        break;
                    case MailUserAction.SetAsRead:
                        result = _mailEnginesFactory.MessageEngine.SetUnread(ids, false);
                        break;
                    case MailUserAction.SetAsUnread:
                        result = _mailEnginesFactory.MessageEngine.SetUnread(ids, true);
                        break;
                    case MailUserAction.SetAsImportant:
                        result = _mailEnginesFactory.MessageEngine.SetImportant(ids, true);
                        break;
                    case MailUserAction.SetAsNotImpotant:
                        result = _mailEnginesFactory.MessageEngine.SetImportant(ids, false);
                        break;
                    case MailUserAction.SetAsDeleted:
                        _mailEnginesFactory.MessageEngine.SetRemoved(ids);
                        result = true;
                        break;
                    default:
                        break;
                }

                if (result) needUserUpdate = true;

                _log.Debug($"ProcessActionFromImapTimer_Elapsed Action {imapAction.FolderAction} complete with result {result.ToString().ToUpper()} for {ids.Count} messages.");

                StringBuilder sb = new();

                ids.ForEach(x => sb.Append(x.ToString() + ", "));

                _log.Debug($"ProcessActionFromImapTimer_Elapsed ids: {sb}");

                ids.Clear();
            }
        }
        catch (Exception ex)
        {
            _log.Error($"ProcessActionFromImap exception: {ex}");
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

        _log.Debug($"IAliveTimer. No user online.");

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
                CreateSimpleImapClient(simpleImapClient.Account, simpleImapClient.ImapWorkFolderFullName);
            }

            DeleteSimpleImapClient(simpleImapClient);
        }
    }

    private void ImapClient_NewActionFromImap(object sender, ImapAction e)
    {
        imapActionsQueue.Enqueue(e);

        _log.Debug($"ImapClient_NewActionFromImap: imapActionsQueue.Count={imapActionsQueue.Count}. Action={e.FolderAction}");
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
            _log.Error($"SetMailboxAuthError(Tenant = {Tenant}, MailboxId = {simpleImapClient.Account.MailBoxId}, Address = '{simpleImapClient.Account.EMail}') Exception: {ex}");
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
            _log.Debug($"UpdateDbFolder: ImapMessagesList==null.");

            return;
        }

        _enginesFactorySemaphore.Wait();

        try
        {
            var workFolderMails = GetMailFolderMessages(simpleImapClient);

            _log.Debug($"UpdateDbFolder: simpleImapClient.WorkFolderMails.Count={workFolderMails.Count}.");

            if (simpleImapClient.ImapMessagesList != null)
            {
                foreach (var imap_message in simpleImapClient.ImapMessagesList)
                {
                    _log.Debug($"UpdateDbFolder: imap_message_Uidl={imap_message.UniqueId.Id}.");

                    var uidl = imap_message.UniqueId.ToUidl(simpleImapClient.Folder);

                    var db_message = workFolderMails.FirstOrDefault(x => x.Uidl == uidl && (!simpleImapClient.IsMessageTracked(x.Id)));

                    if (db_message == null)
                    {
                        _log.Debug($"UpdateDbFolder: imap_message_Uidl={uidl} not found in DB.");

                        simpleImapClient.TryGetNewMessage(imap_message);

                        continue;
                    }

                    imap_message.MessageIdInDB = db_message.Id;

                    SetMessageFlagsFromImap(imap_message, db_message);

                    workFolderMails.Remove(db_message);
                }
            }

            if (workFolderMails.Any()) _mailEnginesFactory.MessageEngine.SetRemoved(workFolderMails.Select(x => x.Id).ToList());

        }
        catch (Exception ex)
        {
            _log.Error($"UpdateDbFolder {simpleImapClient.ImapWorkFolderFullName} exception {ex.Message}.");
        }
        finally
        {
            if (_enginesFactorySemaphore.CurrentCount == 0) _enginesFactorySemaphore.Release();

            needUserUpdate = true;
        }
    }

    private void SetMessageFlagsFromImap(MessageDescriptor imap_message, MailInfo db_message)
    {
        if (imap_message == null || db_message == null) return;

        try
        {
            _log.Debug($"SetMessageFlagsFromImap: imap_message_Uidl={imap_message.UniqueId.Id}, flag={imap_message.Flags.Value}.");
            _log.Debug($"SetMessageFlagsFromImap: db_message={db_message.Uidl}, folder={db_message.Folder}, IsRemoved={db_message.IsRemoved}.");

            bool unread = !imap_message.Flags.Value.HasFlag(MessageFlags.Seen);
            bool important = imap_message.Flags.Value.HasFlag(MessageFlags.Flagged);
            bool removed = imap_message.Flags.Value.HasFlag(MessageFlags.Deleted);

            if (db_message.IsNew ^ unread) _mailEnginesFactory.MessageEngine.SetUnread(new List<int>() { db_message.Id }, unread, true);
            if (db_message.Importance ^ important) _mailEnginesFactory.MessageEngine.SetImportant(new List<int>() { db_message.Id }, important);
            if (removed) _mailEnginesFactory.MessageEngine.SetRemoved(new List<int>() { db_message.Id });
        }
        catch (Exception ex)
        {
            _log.Error($"SetMessageFlagsFromImap: {ex.Message}");
        }
    }

    private bool CreateMessageInDB(SimpleImapClient simpleImapClient, MimeMessage message, MessageDescriptor imap_message)
    {
        _log.Debug($"NewMessage: Folder={simpleImapClient.ImapWorkFolderFullName} Uidl={imap_message.UniqueId}.");

        bool result = true;

        Stopwatch watch = null;

        if (_mailSettings.Aggregator.CollectStatistics)
        {
            watch = new Stopwatch();
            watch.Start();
        }

        _enginesFactorySemaphore.Wait();

        message.FixDateIssues(imap_message?.InternalDate, _log);

        bool unread = false, impotant = false;

        if ((imap_message != null) && imap_message.Flags.HasValue)
        {
            unread = !imap_message.Flags.Value.HasFlag(MessageFlags.Seen);
            impotant = imap_message.Flags.Value.HasFlag(MessageFlags.Flagged);
        }

        message.FixEncodingIssues(_log);

        var folder = simpleImapClient.MailWorkFolder;
        var uidl = imap_message.UniqueId.ToUidl(simpleImapClient.Folder);

        _log.Info($"Get message (UIDL: '{uidl}', MailboxId = {simpleImapClient.Account.MailBoxId}, Address = '{simpleImapClient.Account.EMail}')");

        try
        {
            var findedMessages = GetMailFolderMessages(simpleImapClient, message.MessageId, null);

            findedMessages.RemoveAll(x => simpleImapClient.IsMessageTracked(x.Id));

            if (findedMessages.Count == 0)
            {
                var messageDB = _mailEnginesFactory.MessageEngine.SaveWithoutCheck(simpleImapClient.Account, message, uidl, folder, null, unread, _log, impotant);

                if (messageDB == null || messageDB.Id <= 0)
                {
                    _log.Debug("CreateMessageInDB: failed.");

                    return false;
                }

                imap_message.MessageIdInDB = messageDB.Id;

                DoOptionalOperations(messageDB, message, simpleImapClient);

                _log.Info($"Message saved (id: {messageDB.Id}, From: '{messageDB.From}', Subject: '{messageDB.Subject}', Unread: {messageDB.IsNew})");

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

            _log.Info($"Message updated (id: {messageInfo.Id}, Folder: '{simpleImapClient.Folder}'), Subject: '{messageInfo.Subject}'");

            SetMessageFlagsFromImap(imap_message, messageInfo);

            needUserUpdate = true;

            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"CreateMessageInDB:{ex.Message}");

            result = false;
        }
        finally
        {
            if (_enginesFactorySemaphore.CurrentCount == 0) _enginesFactorySemaphore.Release();

            if (_mailSettings.Aggregator.CollectStatistics && watch != null)
            {
                watch.Stop();

                LogStat(simpleImapClient, "CreateMessageInDB", watch.Elapsed, result);

                _log.Debug($"CreateMessageInDB time={watch.Elapsed.TotalMilliseconds} ms.");
            }
        }

        return result;
    }

    private List<MailInfo> GetMailFolderMessages(SimpleImapClient simpleImapClient, string mimeMessageId = null, bool? isRemoved = false)
    {
        var exp = SimpleMessagesExp.CreateBuilder(Tenant, UserName, isRemoved)
            .SetMailboxId(simpleImapClient.Account.MailBoxId)
            .SetFolder(simpleImapClient.FolderTypeInt);

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

    private void LogStat(SimpleImapClient simpleImapClient, string method, TimeSpan duration, bool failed)
    {
        if (!_mailSettings.Aggregator.CollectStatistics) return;

        _log.DebugWithProps(method, new List<KeyValuePair<string, object>>() {
            new KeyValuePair<string, object>("duration", duration.TotalMilliseconds),
            new KeyValuePair<string, object>("mailboxId", simpleImapClient.Account.MailBoxId),
            new KeyValuePair<string, object>("address", simpleImapClient.Account.EMail.ToString()),
            new KeyValuePair<string, object>("isFailed", failed)});
    }

    private bool SendUnreadUser()
    {
        try
        {
            var mailFolderInfos = _folderEngine.GetFolders(UserName);

            var count = (from mailFolderInfo in mailFolderInfos
                         where mailFolderInfo.id == FolderType.Inbox
                         select mailFolderInfo.unreadMessages)
                .FirstOrDefault();

            if (UserName != Constants.LostUser.ID.ToString())
            {
                _signalrServiceClient.SendUnreadUser(Tenant, UserName, count);
            }
        }
        catch (Exception ex)
        {
            _log.Error($"SendUnreadUser error {ex.Message}. Inner error: {ex.InnerException?.Message}.");

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
                _log.Debug("DoOptionalOperations -> GetOrCreateTags()");

                tagIds = _mailEnginesFactory.TagEngine.GetOrCreateTags(Tenant, UserName, simpleImapClient.MailWorkFolder.Tags);
            }

            _log.Debug("DoOptionalOperations -> IsCrmAvailable()");

            if (crmAvailable)
            {
                _log.Debug("DoOptionalOperations -> GetCrmTags()");

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

            _log.Debug("DoOptionalOperations -> AddMessageToIndex()");

            var mailMail = message.ToMailMail(Tenant, new Guid(UserName));

            _mailEnginesFactory.IndexEngine.Add(mailMail);

            foreach (var tagId in tagIds)
            {
                try
                {
                    _log.DebugFormat($"DoOptionalOperations -> SetMessagesTag(tagId: {tagId})");

                    _mailEnginesFactory.TagEngine.SetMessagesTag(new List<int> { message.Id }, tagId);
                }
                catch (Exception e)
                {
                    _log.ErrorFormat(
                        "SetMessagesTag(tenant={0}, userId='{1}', messageId={2}, tagid = {3})\r\nException:{4}\r\n",
                        Tenant, UserName, message.Id, e.ToString(),
                        tagIds != null ? string.Join(",", tagIds) : "null");
                }
            }

            _log.Debug("DoOptionalOperations -> AddRelationshipEventForLinkedAccounts()");

            _mailEnginesFactory.CrmLinkEngine.AddRelationshipEventForLinkedAccounts(simpleImapClient.Account, message);

            _log.Debug("DoOptionalOperations -> SaveEmailInData()");

            _mailEnginesFactory.EmailInEngine.SaveEmailInData(simpleImapClient.Account, message, _mailSettings.Defines.DefaultApiSchema);

            _log.Debug("DoOptionalOperations -> SendAutoreply()");

            _mailEnginesFactory.AutoreplyEngine.SendAutoreply(simpleImapClient.Account, message, _mailSettings.Defines.DefaultApiSchema, _log);

            _log.Debug("DoOptionalOperations -> UploadIcsToCalendar()");

            if (simpleImapClient.MailWorkFolder.Folder != Enums.FolderType.Spam)
            {
                _mailEnginesFactory.CalendarEngine
                    .UploadIcsToCalendar(simpleImapClient.Account, message.CalendarId, message.CalendarUid, message.CalendarEventIcs,
                        message.CalendarEventCharset, message.CalendarEventMimeType);
            }

            if (_mailSettings.Defines.SaveOriginalMessage)
            {
                _log.Debug("DoOptionalOperations -> StoreMailEml()");
                StoreMailEml(Tenant, UserName, message.StreamId, mimeMessage);
            }

            _log.Debug("DoOptionalOperations -> ApplyFilters()");

            var filters = _mailEnginesFactory.FilterEngine.GetList();

            var filtersAppliedSuccessfull = _mailEnginesFactory.FilterEngine.ApplyFilters(message, simpleImapClient.Account, simpleImapClient.MailWorkFolder, filters).OrderByDescending(x => x.Action).ToList();

            foreach (var filterAppliedSuccessfull in filtersAppliedSuccessfull)
            {
                switch (filterAppliedSuccessfull.Action)
                {
                    case Enums.Filter.ActionType.MarkAsImportant:
                        simpleImapClient.ExecuteUserAction(new List<int>() { message.Id }, MailUserAction.SetAsImportant, 0);
                        break;
                    case Enums.Filter.ActionType.MarkAsRead:
                        simpleImapClient.ExecuteUserAction(new List<int>() { message.Id }, MailUserAction.SetAsRead, 0);
                        break;
                    case Enums.Filter.ActionType.DeleteForever:
                        simpleImapClient.ExecuteUserAction(new List<int>() { message.Id }, MailUserAction.SetAsDeleted, 0);
                        break;
                    case Enums.Filter.ActionType.MoveTo:
                        string destination = new(filterAppliedSuccessfull.Data.Where(x => Char.IsDigit(x)).ToArray());
                        if (int.TryParse(destination, out int result))
                        {
                            simpleImapClient.ExecuteUserAction(new List<int>() { message.Id }, MailUserAction.MoveTo, result);
                        }
                        break;
                    case Enums.Filter.ActionType.MarkTag:
                        break;
                }
            }

            _log.Debug("DoOptionalOperations -> NotifySignalrIfNeed()");
        }
        catch (Exception ex)
        {
            _log.Error($"DoOptionalOperations() ->\r\nException:{ex}\r\n");
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

            _log.InfoFormat($"StoreMailEml: Tenant = {tenant}, UserId = {userId}, SaveEmlPath = {savePath}. Result: {res}");

            return res;
        }
        catch (Exception ex)
        {
            _log.Error($"StoreMailEml exception: {ex}");
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
            _log.Error($"Stop exception: {ex}");
        }
    }

    public void Dispose()
    {
        Stop();

        _log.Info($"Dispose");

        GC.SuppressFinalize(this);
    }
}
