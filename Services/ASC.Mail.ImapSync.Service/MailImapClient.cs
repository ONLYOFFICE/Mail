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

using ASC.Mail.Core.Core.Storage;
using ASC.Mail.ImapSync.Models;

namespace ASC.Mail.ImapSync;

public class MailImapClient : IDisposable
{
    public readonly string UserName;
    public readonly int Tenant;
    public readonly string RedisKey;

    public bool IsReady { get; private set; } = false;

    private readonly ConcurrentQueue<ImapAction> imapActionsQueue;
    private readonly ConcurrentQueue<NewMessageFromIMAPData> NewMessageQueue;
    private readonly List<SimpleImapClient> simpleImapClients;
    private readonly IServiceProvider clientScope;

    private readonly SemaphoreSlim _enginesFactorySemaphore;

    private readonly MailEnginesFactory _mailEnginesFactory;
    private readonly MailSettings _mailSettings;

    private readonly SocketServiceClient _signalrServiceClient;
    private readonly RedisClient _redisClient;

    private readonly ILogger _log;
    private readonly ILogger _logStat;
    private readonly ILoggerProvider _logProvider;

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

                switch (actionFromCache.Action)
                {
                    case MailUserAction.StartImapClient:
                    case MailUserAction.Nothing:
                        break;
                    case MailUserAction.SendDraft:
                        simpleImapClients.FirstOrDefault(x => x.Folder == FolderType.Draft).ExecuteUserAction(actionFromCache);
                        break;
                    case MailUserAction.SetAsRead:
                    case MailUserAction.SetAsUnread:
                    case MailUserAction.SetAsImportant:
                    case MailUserAction.SetAsNotImpotant:
                    case MailUserAction.SetAsDeleted:
                    case MailUserAction.MoveTo:
                        if (actionFromCache.Action == MailUserAction.MoveTo && actionFromCache.Destination == (int)FolderType.UserFolder)
                        {
                            actionFromCache.Data = simpleImapClients.FirstOrDefault(x => x.UserFolderID == (int?)actionFromCache.UserFolderId)?.ImapWorkFolderFullName;
                        }
                        simpleImapClients.ForEach(x => x.ExecuteUserAction(actionFromCache));
                        break;
                    case MailUserAction.ReceiptStatusChanged:
                        break;
                    case MailUserAction.Restore:
                        break;
                    case MailUserAction.CreateFolder:
                        ExecutActionCreateUserFolder(actionFromCache);
                        break;
                    case MailUserAction.UpdateDrafts:
                        ExecutActionUpdateDrafts(actionFromCache);
                        break;
                    case MailUserAction.DeleteUserFolder:
                        break;
                    case MailUserAction.UpdateUserFolder:
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            _log.ErrorMailImapClientFromRedisPipeline("CheckRedis method", ex.Message);
        }

        _log.DebugMailImapClientFromRedisPipeline($"Read {iterationCount} keys. Total IMAP clients: {simpleImapClients.Count}");
    }

    public MailImapClient(
        string userName,
        int tenant,
        MailSettings mailSettings,
        IServiceProvider serviceProvider,
        SocketServiceClient signalrServiceClient,
        CancellationToken cancelToken,
        ILoggerProvider logProvider)
    {
        _mailSettings = mailSettings;

        UserName = userName;
        Tenant = tenant;
        RedisKey = "ASC.MailAction:" + userName;

        clientScope = serviceProvider.CreateScope().ServiceProvider;

        _redisClient = clientScope.GetService<RedisClient>();

        if (_redisClient == null)
        {
            throw new Exception($"No redis connection. UserName={UserName}");
        }

        _mailEnginesFactory = clientScope.GetService<MailEnginesFactory>();
        _mailEnginesFactory.SetTenantAndUser(tenant, UserName);

        _signalrServiceClient = signalrServiceClient;

        _log = logProvider.CreateLogger($"ASC.Mail.User_{userName}");
        _logStat = logProvider.CreateLogger($"ASC.Mail.User_{userName}");
        _logProvider = logProvider;

        if (_mailSettings.Aggregator.CollectStatistics)
            _logStat = logProvider.CreateLogger("ASC.Mail.Stat");

        _enginesFactorySemaphore = new SemaphoreSlim(1, 1);

        _cancelToken = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);

        simpleImapClients = new List<SimpleImapClient>();
        imapActionsQueue = new ConcurrentQueue<ImapAction>();
        NewMessageQueue = new ConcurrentQueue<NewMessageFromIMAPData>();

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
        crmAvailable = _mailEnginesFactory.ApiHelper.IsCrmModuleAvailable();
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
            _log.ErrorMailImapClientDBPipeline("Get User mailboxes", ex.Message);

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

            _log.DebugMailImapClientFromIMAPPipeline($"Create IMAP clients and {isLocked} {mailbox.EMail.Address}.");
        }
        catch (Exception ex)
        {
            _log.ErrorMailImapClientFromIMAPPipeline($"Create IMAP clients for {mailbox.EMail.Address}", ex.Message);
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
                var userFolder = _mailEnginesFactory.UserFolderEngine.GetByNameOrCreate(folder.folderName);

                simpleImapClient.UserFolderID = userFolder.Id;
            }

            simpleImapClient.Init(folder.folderName);
        }
        catch (Exception ex)
        {
            _log.ErrorMailImapClientFromIMAPPipeline($"Create IMAP client for {mailbox.EMail.Address}, folder {folder.folderName}", ex.Message);
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

            _log.DebugMailImapClientFromIMAPPipeline($"Delete {deletedSimpleImapClients.Count} IMAP clients and {isLocked} {mailbox.EMail.Address}.");
        }
        catch (Exception ex)
        {
            _log.ErrorMailImapClientFromIMAPPipeline($"Delete IMAP clients for {mailbox.EMail.Address}", ex.Message);
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

                    _log.DebugMailImapClientFromIMAPPipeline($"Process action from IMAP. Added to chain: {imapAction}");
                }

                needUserUpdate = imapAction.FolderAction switch
                {
                    MailUserAction.Nothing => true,
                    MailUserAction.SetAsRead => _mailEnginesFactory.MessageEngine.SetUnread(ids, false),
                    MailUserAction.SetAsUnread => _mailEnginesFactory.MessageEngine.SetUnread(ids, true),
                    MailUserAction.SetAsImportant => _mailEnginesFactory.MessageEngine.SetImportant(ids, true),
                    MailUserAction.SetAsNotImpotant => _mailEnginesFactory.MessageEngine.SetImportant(ids, false),
                    MailUserAction.SetAsDeleted => _mailEnginesFactory.MessageEngine.SetRemoved(ids),
                    MailUserAction.StartImapClient => false,
                    MailUserAction.MoveTo => false,
                    MailUserAction.ReceiptStatusChanged => false,
                    MailUserAction.Restore => false,
                    MailUserAction.CreateFolder => false,
                    MailUserAction.MessageUidlUpdate => _mailEnginesFactory.ChangeMessageId(imapAction.MessageIdInDB,
                    imapAction.MessageUniqueId.ToUidl(imapAction.MessageFolderType)),
                    _ => false
                };

                if (_mailEnginesFactory.FolderEngine.needRecalculateFolders) _mailEnginesFactory.FolderEngine.RecalculateFolders();

                _log.DebugMailImapClientFromIMAPPipeline($"Process action from IMAP. Chain executed.");

                ids.Clear();
            }

            while (NewMessageQueue.TryDequeue(out NewMessageFromIMAPData newMessageFromIMAPData))
            {
                if (newMessageFromIMAPData.SimpleImapClient.Folder == FolderType.Draft) CreateDraftMessageInDB(newMessageFromIMAPData);
                else CreateMessageInDB(newMessageFromIMAPData);
            }
        }
        catch (Exception ex)
        {
            _log.ErrorMailImapClientFromIMAPPipeline($"Process action from IMAP ", ex.Message);
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

        _log.DebugMailImapClientDBPipeline($"User leave portal. Client will destroy.");

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

        _log.DebugMailImapClientFromIMAPPipeline($"New action from IMAP (Id={e.MessageIdInDB}, {e.FolderAction}). Queue count={imapActionsQueue.Count}");
    }

    private void ImapClient_MessagesListUpdated(object sender, EventArgs e)
    {
        if (sender is SimpleImapClient simpleImapClient)
        {
            if (simpleImapClient.Folder == FolderType.Draft)
            {
                UpdateDraftDbFolder(simpleImapClient);
            }
            else
            {
                UpdateDbFolder(simpleImapClient);
            }
        }
    }

    private void ImapClient_NewMessage(object sender,
        NewMessageFromIMAPData newMessageFromIMAPData) => NewMessageQueue.Enqueue(newMessageFromIMAPData);

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
            _log.ErrorMailImapClientDBPipeline($"SetMailboxAuthError MailboxId = {simpleImapClient.Account.MailBoxId}, Address = '{simpleImapClient.Account.EMail.Address}')", ex.Message);
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
            _log.DebugMailImapClientDBPipeline($"Update folder in DB. No messages in IMAP folder {simpleImapClient.ImapWorkFolderFullName}.");

            return;
        }

        _enginesFactorySemaphore.Wait();

        try
        {
            List<MailInfo> workFolderMails;

            if (simpleImapClient.UserFolderID.HasValue) workFolderMails = GetMailUserFolderMessages(simpleImapClient);
            else workFolderMails = GetMailFolderMessages(simpleImapClient);

            _log.DebugMailImapClientDBPipeline($"Update folder {simpleImapClient.ImapWorkFolderFullName} in DB. In DB {workFolderMails.Count} messages. In IMAP folder {simpleImapClient.ImapMessagesList.Count} messages.");

            if (simpleImapClient.ImapMessagesList != null)
            {
                foreach (var imap_message in simpleImapClient.ImapMessagesList)
                {
                    var uidl = imap_message.UniqueId.ToUidl(simpleImapClient.Folder);

                    var db_message = workFolderMails.FirstOrDefault(x => x.Uidl == uidl && (!simpleImapClient.IsMessageTracked(x.Id)));

                    if (db_message == null)
                    {
                        _log.DebugMailImapClientDBPipeline($"Update folder {simpleImapClient.ImapWorkFolderFullName} in DB. Message {uidl} didn't found.");

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
            _log.ErrorMailImapClientDBPipeline($"Update folder {simpleImapClient.ImapWorkFolderFullName}", ex.Message);
        }
        finally
        {
            if (_enginesFactorySemaphore.CurrentCount == 0) _enginesFactorySemaphore.Release();

            needUserUpdate = true;

            if (_mailEnginesFactory.FolderEngine.needRecalculateFolders) _mailEnginesFactory.FolderEngine.RecalculateFolders();
        }
    }

    private void UpdateDraftDbFolder(SimpleImapClient simpleImapClient)
    {
        if (simpleImapClient.ImapMessagesList == null)
        {
            _log.DebugMailImapClientDBPipeline($"Update draft folder in DB. No messages in IMAP folder {simpleImapClient.ImapWorkFolderFullName}.");

            return;
        }

        _enginesFactorySemaphore.Wait();

        try
        {
            List<MailInfo> workFolderMails = GetMailFolderMessages(simpleImapClient);

            if (workFolderMails.Any())
            {
                var filteredMail = workFolderMails.GroupBy(x => x.MimeMessageId).Select(x => x.FirstOrDefault(y => y.DateSent == x.Max(z => z.DateSent))).ToList();

                List<int> ids = workFolderMails.Where(x => !(filteredMail.Any(y => y.Id == x.Id))).Select(x => x.Id).ToList();

                if (ids.Any()) _mailEnginesFactory.MessageEngine.SetRemoved(ids);

                workFolderMails = filteredMail;
            }

            _log.DebugMailImapClientDBPipeline($"Update folder {simpleImapClient.ImapWorkFolderFullName} in DB. In DB {workFolderMails.Count} messages. In IMAP folder {simpleImapClient.ImapMessagesList.Count} messages.");

            foreach (var imap_message in simpleImapClient.ImapMessagesList)
            {
                var uidl = imap_message.UniqueId.ToUidl(simpleImapClient.Folder);

                var db_message = workFolderMails.FirstOrDefault(x => x.Uidl == uidl && (!simpleImapClient.IsMessageTracked(x.Id)));

                if (db_message == null)
                {
                    db_message = workFolderMails.FirstOrDefault(x => x.MimeMessageId == imap_message.IMAPMessageId);

                    if (db_message == null)
                    {
                        _log.DebugMailImapClientDBPipeline($"Update folder {simpleImapClient.ImapWorkFolderFullName} in DB. Message {uidl} didn't found.");

                        simpleImapClient.TryGetNewMessage(imap_message);

                        continue;
                    }
                }

                imap_message.MessageIdInDB = db_message.Id;

                SetMessageFlagsFromImap(imap_message, db_message);

                workFolderMails.Remove(db_message);
            }

            if (workFolderMails.Any())
            {
                //Checking emails in work folder, if it was sent already

                var sentFolderIMAPClient = simpleImapClients.FirstOrDefault(x => x.Folder == FolderType.Sent);

                if (sentFolderIMAPClient != null)
                {
                    for (int i = workFolderMails.Count - 1; i >= 0; i--)
                    {
                        var workFolderMail = workFolderMails[i];

                        if (sentFolderIMAPClient.ImapMessagesList.Any(x => x.IMAPMessageId == workFolderMail.MimeMessageId))
                        {
                            workFolderMails.Remove(workFolderMail);
                        }
                    }
                }
                ExecutActionUpdateDrafts(new CashedMailUserAction()
                {
                    Action = MailUserAction.UpdateDrafts,
                    Uds = workFolderMails.Select(x => x.Id).ToList()
                }, false);
            }

        }
        catch (Exception ex)
        {
            _log.ErrorMailImapClientDBPipeline($"Update folder {simpleImapClient.ImapWorkFolderFullName}", ex.Message);
        }
        finally
        {
            if (_enginesFactorySemaphore.CurrentCount == 0) _enginesFactorySemaphore.Release();

            needUserUpdate = true;

            if (_mailEnginesFactory.FolderEngine.needRecalculateFolders) _mailEnginesFactory.FolderEngine.RecalculateFolders();
        }
    }

    private void SetMessageFlagsFromImap(MessageDescriptor imap_message, MailInfo db_message)
    {
        if (imap_message == null || db_message == null) return;

        try
        {
            bool unread = !imap_message.Flags.Value.HasFlag(MessageFlags.Seen);
            bool important = imap_message.Flags.Value.HasFlag(MessageFlags.Flagged);
            bool removed = imap_message.Flags.Value.HasFlag(MessageFlags.Deleted);

            if (db_message.IsNew ^ unread) _mailEnginesFactory.MessageEngine.SetUnread(new List<int>() { db_message.Id }, unread, true);
            if (db_message.Importance ^ important) _mailEnginesFactory.MessageEngine.SetImportant(new List<int>() { db_message.Id }, important);
            if (removed) _mailEnginesFactory.MessageEngine.SetRemoved(new List<int>() { db_message.Id });
        }
        catch (Exception ex)
        {
            _log.ErrorMailImapClientDBPipeline($"Set massage flag from IMAP", ex.Message);
        }

        if (_mailEnginesFactory.FolderEngine.needRecalculateFolders) _mailEnginesFactory.FolderEngine.RecalculateFolders();
    }

    private bool CreateMessageInDB(NewMessageFromIMAPData newMessageFromIMAPData)
    {
        var message = newMessageFromIMAPData.MimeMessage;
        var imap_message = newMessageFromIMAPData.MessageDescriptor;
        var simpleImapClient = newMessageFromIMAPData.SimpleImapClient;

        bool result = true;

        Stopwatch watch = null;

        if (_mailSettings.Aggregator.CollectStatistics)
        {
            watch = new Stopwatch();
            watch.Start();
        }

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

        _log.InfoMailImapClient($"Start to create message. {simpleImapClient.Account.EMail.Address}\\{simpleImapClient.ImapWorkFolderFullName}\\{uidl}.");

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
                    _log.DebugMailImapClientDBPipeline($"Create message in DB failed. {simpleImapClient.Account.EMail.Address}\\{simpleImapClient.ImapWorkFolderFullName}\\{uidl}");

                    return false;
                }

                imap_message.MessageIdInDB = messageDB.Id;

                DoOptionalOperations(messageDB, message, simpleImapClient);

                _log.InfoMailImapClient($"Message (Id in DB = {messageDB.Id}) Saved. {messageDB.From}, {messageDB.Subject}.");

                needUserUpdate = true;

                return true;
            }

            MailInfo messageInfo = findedMessages.FirstOrDefault(x => x.IsRemoved == false);

            if (messageInfo == null)
            {
                messageInfo = findedMessages[0];

                if (_mailEnginesFactory.SetUnRemoved(messageInfo.Id)) messageInfo.IsRemoved = false;
            }

            imap_message.MessageIdInDB = messageInfo.Id;

            string imap_message_uidl = imap_message.UniqueId.ToUidl(simpleImapClient.Folder);

            if (imap_message_uidl != messageInfo.Uidl &&
                _mailEnginesFactory.ChangeMessageId(messageInfo.Id, imap_message_uidl))
            {
                messageInfo.Uidl = imap_message_uidl;
            }

            _log.InfoMailImapClientMessageUpdated(messageInfo.Id, simpleImapClient.Folder.ToString(), messageInfo.Subject);

            SetMessageFlagsFromImap(imap_message, messageInfo);

            needUserUpdate = true;

            return true;
        }
        catch (Exception ex)
        {
            _log.ErrorMailImapClientDBPipeline($"Create message in DB", ex.Message);

            result = false;
        }
        finally
        {
            if (_mailSettings.Aggregator.CollectStatistics && watch != null)
            {
                watch.Stop();

                LogStat(simpleImapClient, "CreateMessageInDB", watch.Elapsed, result);
            }
        }

        return result;
    }

    private bool CreateDraftMessageInDB(NewMessageFromIMAPData newMessageFromIMAPData)
    {
        var message = newMessageFromIMAPData.MimeMessage;
        var imap_message = newMessageFromIMAPData.MessageDescriptor;
        var simpleImapClient = newMessageFromIMAPData.SimpleImapClient;

        bool result = true;

        Stopwatch watch = null;

        if (_mailSettings.Aggregator.CollectStatistics)
        {
            watch = new Stopwatch();
            watch.Start();
        }

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

        _log.InfoMailImapClient($"Start create message: {simpleImapClient.Account.EMail.Address}\\{simpleImapClient.ImapWorkFolderFullName}\\{uidl}.");

        try
        {
            var findedMessages = GetMailFolderMessages(simpleImapClient, message.MessageId, false);

            if (findedMessages.Any())
            {
                _mailEnginesFactory.MessageEngine.SetRemoved(findedMessages.Select(x => x.Id).ToList());
            }

            var messageDB = _mailEnginesFactory.MessageEngine
                .SaveWithoutCheck(simpleImapClient.Account, message, uidl, folder, simpleImapClient.UserFolderID, unread, impotant);

            if (messageDB == null || messageDB.Id <= 0)
            {
                _log.DebugMailImapClientDBPipeline($"Didn't create message: {simpleImapClient.Account.EMail.Address}\\{simpleImapClient.ImapWorkFolderFullName}\\{uidl}.");

                return false;
            }

            imap_message.MessageIdInDB = messageDB.Id;

            DoOptionalOperations(messageDB, message, simpleImapClient);

            _log.InfoMailImapClient($"Message (Id in DB = {messageDB.Id}) Saved. {messageDB.From}, {messageDB.Subject}.");

            needUserUpdate = true;

            return true;
        }
        catch (Exception ex)
        {
            _log.ErrorMailImapClientDBPipeline($"Create message in DB", ex.Message);

            result = false;
        }
        finally
        {
            if (_mailSettings.Aggregator.CollectStatistics && watch != null)
            {
                watch.Stop();

                LogStat(simpleImapClient, "CreateMessageInDB", watch.Elapsed, result);
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

        return _mailEnginesFactory.MailInfoDao.GetMailInfoList(exp.Build());
    }

    private List<MailInfo> GetMailUserFolderMessages(SimpleImapClient simpleImapClient, bool? isRemoved = false)
    {
        if (simpleImapClient.UserFolderID == null) return null;

        var exp = SimpleMessagesExp.CreateBuilder(Tenant, UserName, isRemoved)
            .SetMailboxId(simpleImapClient.Account.MailBoxId)
            .SetUserFolderId(simpleImapClient.UserFolderID.Value);

        return _mailEnginesFactory.MailInfoDao.GetMailInfoList(exp.Build());
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
            var count = _mailEnginesFactory.FolderEngine.GetUserUnreadMessageCount(UserName);

            _signalrServiceClient.MakeRequest("sendUnreadUsers", count);
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
                tagIds = _mailEnginesFactory.TagEngine.GetOrCreateTags(Tenant, UserName, simpleImapClient.MailWorkFolder.Tags);

                _log.DebugMailImapClientDBPipeline($"Do optional operations. Create tag {tagIds}.");
            }

            if (crmAvailable)
            {
                var crmTagIds = _mailEnginesFactory.TagEngine.GetCrmTags(message.FromEmail);

                if (crmTagIds.Any())
                {
                    tagIds ??= new List<int>();

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

            var mailMail = message.ToMailMail(Tenant, new Guid(UserName));

            _mailEnginesFactory.IndexEngine.Add(mailMail);

            foreach (var tagId in tagIds)
            {
                try
                {
                    _mailEnginesFactory.TagEngine.SetMessagesTag(new List<int> { message.Id }, tagId);
                }
                catch (Exception e)
                {
                    _log.ErrorMailImapClientSetMessagesTag(Tenant, UserName, message.Id,
                        tagIds != null ? string.Join(",", tagIds) : "null", e.ToString());
                }
            }

            _mailEnginesFactory.CrmLinkEngine.AddRelationshipEventForLinkedAccounts(simpleImapClient.Account, message);

            _mailEnginesFactory.EmailInEngine.SaveEmailInData(simpleImapClient.Account, message, _mailSettings.Defines.DefaultApiSchema);

            _mailEnginesFactory.AutoreplyEngine.SendAutoreply(simpleImapClient.Account, message, _mailSettings.Defines.DefaultApiSchema);

            if (simpleImapClient.MailWorkFolder.Folder != Enums.FolderType.Spam)
            {
                _mailEnginesFactory.CalendarEngine
                    .UploadIcsToCalendar(simpleImapClient.Account, message.CalendarId, message.CalendarUid, message.CalendarEventIcs,
                        message.CalendarEventCharset, message.CalendarEventMimeType,
                        message.Attachments, mimeMessage.Attachments);
            }

            if (_mailSettings.Defines.SaveOriginalMessage)
            {
                StoreMailEml(Tenant, UserName, message.StreamId, mimeMessage);
            }

            var filters = _mailEnginesFactory.FilterEngine.GetList();

            var filtersAppliedSuccessfull = _mailEnginesFactory.FilterEngine.ApplyFilters(message, simpleImapClient.Account, simpleImapClient.MailWorkFolder, filters)
                .OrderByDescending(x => x.Action).ToList();

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
            _log.ErrorMailImapClientDBPipeline($"Do optional operations", ex.Message);
        }

        needUserUpdate = true;
    }

    public string StoreMailEml(int tenant, string userId, string streamId, MimeMessage message)
    {
        if (message == null)
            return string.Empty;

        // Using id_user as domain in S3 Storage - allows not to add quota to tenant.
        var savePath = MailStoragePathCombiner.GetEmlKey(userId, streamId);

        var mailTenantQuotaController = clientScope.GetRequiredService<MailTenantQuotaController>();

        var storage = _mailEnginesFactory.StorageFactory.GetMailStorage(tenant, mailTenantQuotaController);

        try
        {
            using var stream = new MemoryStream();

            message.WriteTo(stream);

            var res = storage.SaveAsync(savePath, stream, MailStoragePathCombiner.EML_FILE_NAME).Result.ToString();

            _log.InfoMailImapClient($"Store mail EML. Path={savePath}, Uri={res}");

            return res;
        }
        catch (Exception ex)
        {
            _log.ErrorMailImapClientDBPipeline($"Store mail EML", ex.Message);
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
            _log.ErrorMailImapClientDBPipeline($"Stop error", ex.Message);
        }
    }

    public void Dispose()
    {
        Stop();

        _log.DebugMailImapClientDBPipeline("Dispose");

        GC.SuppressFinalize(this);
    }

    public MimeMessage GetMimeMessage(int messageId, MailBoxData mailBoxData)
    {
        MailMessageData message = null;

        _mailEnginesFactory.SetTenantAndUser(Tenant, UserName);

        try
        {
            message = _mailEnginesFactory.MessageEngine.GetMessage(messageId, new MailMessageData.Options
            {
                LoadImages = false,
                LoadBody = true,
                NeedProxyHttp = _mailSettings.NeedProxyHttp,
                NeedSanitizer = false
            });
        }
        catch (Exception ex)
        {
            //_log.Error($"ConvertMessageToMimeMessage: Can't get message from DB. {ex.Message}");
        }

        if (message == null) return null;

        var to = message.To == null || string.IsNullOrEmpty(message.To) ? new List<string>() : message.To.Split(',').ToList<string>();
        var cc = message.Cc == null || string.IsNullOrEmpty(message.Cc) ? new List<string>() : message.Cc.Split(',').ToList<string>();
        var bcc = message.Bcc == null || string.IsNullOrEmpty(message.Bcc) ? new List<string>() : message.Bcc.Split(',').ToList<string>();

        var model = new MessageModel
        {
            Id = message.Id,
            From = message.From,
            To = to,
            Cc = cc,
            Bcc = bcc,
            MimeReplyToId = message.MimeReplyToId,
            Importance = message.Important,
            Subject = message.Subject,
            Tags = message.TagIds,
            Body = message.HtmlBody,
            Attachments = message.Attachments,
            CalendarIcs = message.CalendarEventIcs
        };

        var mimeMessageId = message.MimeMessageId;

        var streamId = message.StreamId;

        var previousMailboxId = message.MailboxId;

        var fromAddress = message.From;

        var compose = new MailDraftData(model.Id, mailBoxData, fromAddress, model.To, model.Cc, model.Bcc, model.Subject, mimeMessageId,
                model.MimeReplyToId, model.Importance, model.Tags, model.Body, streamId, model.Attachments, model.CalendarIcs)
        {
            PreviousMailboxId = previousMailboxId
        };

        return compose.ToMimeMessage(_mailEnginesFactory.StorageManager);
    }

    private void ExecutActionUpdateDrafts(CashedMailUserAction action, bool needSemaphore = true)
    {
        try
        {
            if (needSemaphore) _enginesFactorySemaphore.Wait();

            _mailEnginesFactory.SetTenantAndUser(Tenant, UserName);

            foreach (var mailId in action.Uds)
            {
                var mail = _mailEnginesFactory.GetMailInfo(mailId);

                if (mail == null) continue;

                var simpleImapClient = simpleImapClients.Where(x => x.Account.MailBoxId == mail.MailboxId && x.Folder == FolderType.Draft).FirstOrDefault();

                if (simpleImapClient == null) continue;

                var mimeMessage = GetMimeMessage(mailId, simpleImapClient.Account);

                if (mimeMessage == null) continue;

                simpleImapClient.TryCreateMessageInIMAP(mimeMessage, MessageFlags.Draft, mailId);
            }
        }
        catch (Exception ex)
        {
            _log.ErrorMailImapClientFromRedisPipeline($"ExecutActionUpdateDrafts", ex.Message);
        }
        finally
        {
            if (needSemaphore && _enginesFactorySemaphore.CurrentCount == 0) _enginesFactorySemaphore.Release();
        }
    }

    private void ExecutActionCreateUserFolder(CashedMailUserAction action)
    {
        _enginesFactorySemaphore.Wait();

        try
        {
            _mailEnginesFactory.SetTenantAndUser(Tenant, UserName);

            var accounts = simpleImapClients.GroupBy(x => x.Account.MailBoxId).Select(x => x.Key).ToList();

            if (accounts.Count == 1)
            {
                var simpleImapClient = simpleImapClients.FirstOrDefault(x => x.Folder == FolderType.Inbox);

                var newFolders = _mailEnginesFactory.UserFolderEngine.GetList(action.Uds);

                foreach (var folder in newFolders)
                {
                    var perentFolder = _mailEnginesFactory.UserFolderEngine.Get(folder.ParentId);

                    if (simpleImapClient != null) simpleImapClient.TryCreateFolderInIMAP(folder.Name, perentFolder.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _log.ErrorMailImapClientFromRedisPipeline($"ExecutActionCreateUserFolder", ex.Message);
        }
        finally
        {
            if (_enginesFactorySemaphore.CurrentCount == 0) _enginesFactorySemaphore.Release();
        }
    }
}
