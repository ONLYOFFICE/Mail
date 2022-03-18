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


using ASC.Common.Logging;
using ASC.Core;
using ASC.Core.Notify.Signalr;
using ASC.Core.Users;
using ASC.Data.Storage;
using ASC.Mail.Configuration;
using ASC.Mail.Core.Dao.Expressions.Mailbox;
using ASC.Mail.Core.Dao.Expressions.Message;
using ASC.Mail.Core.Dao.Interfaces;
using ASC.Mail.Core.Engine;
using ASC.Mail.Core.Entities;
using ASC.Mail.Enums;
using ASC.Mail.Extensions;
using ASC.Mail.Models;
using ASC.Mail.Storage;
using ASC.Mail.Utils;

using MailKit;

using Microsoft.Extensions.DependencyInjection;

using MimeKit;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ASC.Mail.ImapSync
{
    public class MailImapClient : IDisposable
    {
        public readonly string UserName;
        public readonly int Tenant;
        public readonly string RedisKey;

        public bool IsReady { get; private set; } = false;

        public ConcurrentDictionary<string, List<MailSieveFilterData>> Filters { get; set; }

        private readonly ConcurrentQueue<ImapAction> imapActionsQueue;
        private List<SimpleImapClient> simpleImapClients;

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

        private bool crmAvailable;
        private bool needUserUpdate;
        private bool needUserMailBoxUpdate;
        private bool userActivityDetected;

        private CancellationTokenSource CancelToken { get; set; }

        public EventHandler OnCriticalError;

        private readonly System.Timers.Timer aliveTimer;
        private readonly System.Timers.Timer processActionFromImapTimer;

        public async Task CheckRedis(int folderActivity, IEnumerable<int> tags)
        {
            needUserMailBoxUpdate = true;

            if (folderActivity == -1)
            {
                userActivityDetected = true;
            }

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

        public MailImapClient(string userName, int tenant, CancellationToken cancelToken, MailSettings mailSettings, IServiceProvider serviceProvider, SignalrServiceClient signalrServiceClient)
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

            Filters = new ConcurrentDictionary<string, List<MailSieveFilterData>>();

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

            _log.Name = $"ASC.Mail.MailUser_{userName}";

            CancelToken = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);

            simpleImapClients = new List<SimpleImapClient>();
            imapActionsQueue = new ConcurrentQueue<ImapAction>();

            aliveTimer = new System.Timers.Timer((_mailSettings.ImapSync.AliveTimeInMinutes ?? 1) * 60 * 1000);

            aliveTimer.Elapsed += AliveTimer_Elapsed;

            processActionFromImapTimer = new System.Timers.Timer(1000);

            processActionFromImapTimer.Elapsed += ProcessActionFromImapTimer_Elapsed;

            processActionFromImapTimer.Enabled = true;

            UpdateSimplImapClients();

            aliveTimer.Enabled = true;

            IsReady = true;
            needUserUpdate = false;
        }

        private List<MailBoxData> GetUserMailBoxes()
        {
            List<MailBoxData> mailboxes;
            _enginesFactorySemaphore.Wait();

            try
            {
                var userMailboxesExp = new UserMailboxesExp(Tenant, UserName, onlyTeamlab: true);

                mailboxes = _mailEnginesFactory.MailboxEngine.GetMailboxDataList(userMailboxesExp);
            }
            catch (Exception ex)
            {
                mailboxes = new List<MailBoxData>();

                _log.Error($"GetUserMailBoxes exception: {ex}");
            }
            finally
            {
                if (_enginesFactorySemaphore.CurrentCount == 0) _enginesFactorySemaphore.Release();
            }

            return mailboxes.Where(x => x.Enabled).ToList();
        }

        private bool IsUserOnLine()
        {
            var mailboxes = GetUserMailBoxes();

            return mailboxes.Any(x => x.Active);
        }

        private void UpdateSimplImapClients()
        {
            var mailBoxes = GetUserMailBoxes();

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
        }

        private void CreateSimpleImapClients(MailBoxData mailbox)
        {
            if (simpleImapClients.Any(x => x.Account.MailBoxId == mailbox.MailBoxId))
            {
                DeleteSimpleImapClients(mailbox);
            }

            try
            {
                var rootSimpleImapClient = new SimpleImapClient(mailbox, CancelToken.Token, _mailSettings, clientScope.GetService<ILog>());

                if (!SetEvents(rootSimpleImapClient)) return;

                simpleImapClients.Add(rootSimpleImapClient);

                rootSimpleImapClient.Init("");

                foreach (var folder in rootSimpleImapClient.ImapFoldersFullName)
                {
                    var simpleImapClient = new SimpleImapClient(mailbox, CancelToken.Token, _mailSettings, clientScope.GetService<ILog>());

                    if (!SetEvents(simpleImapClient)) continue;

                    simpleImapClients.Add(simpleImapClient);

                    simpleImapClient.Init(folder);
                }

                _enginesFactorySemaphore.Wait();

                string isLocked = _mailEnginesFactory.MailboxEngine.LockMaibox(mailbox.MailBoxId) ? "locked" : "didn`t lock";

                _log.Debug($"CreateSimpleImapClient: MailboxId={mailbox.MailBoxId} created and {isLocked}.");
            }
            catch (Exception ex)
            {
                _log.Error($"CreateSimpleImapClient exception: {ex}");
            }
            finally
            {
                if (_enginesFactorySemaphore.CurrentCount == 0) _enginesFactorySemaphore.Release();
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

        private void DeleteSimpleImapClients(MailBoxData mailBoxData)
        {
            int deletedClientCount = 0;

            var deletedSimpleImapClients = simpleImapClients.Where(x => x.Account.MailBoxId == mailBoxData.MailBoxId).ToList();

            deletedSimpleImapClients.ForEach(x => UnSetEvents(x));

            deletedSimpleImapClients.ForEach(x => x.Stop());

            simpleImapClients.RemoveAll(x => x.Account.MailBoxId == mailBoxData.MailBoxId);

            _enginesFactorySemaphore.Wait();

            try
            {
                string isLocked = _mailEnginesFactory.MailboxEngine.ReleaseMailbox(mailBoxData, _mailSettings) ? "unlocked" : "didn`t unlock";

                _log.Debug($"DeleteSimpleImapClients: {deletedClientCount} clients with MailboxId={mailBoxData.MailBoxId} removed and {isLocked}.");
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
            if (IsReady && needUserMailBoxUpdate) UpdateSimplImapClients();

            _enginesFactorySemaphore.Wait();

            try
            {
                var uids = new List<int>();

                while (imapActionsQueue.TryDequeue(out ImapAction imapAction))
                {
                    uids.Add(imapAction.MessageIdInDB);

                    if (imapActionsQueue.TryPeek(out ImapAction nextImapAction))
                    {
                        if (imapAction.IsSameImapFolderAndAction(nextImapAction)) continue;
                    }

                    if (uids.Count() == 0)
                    {
                        _log.Debug($"CompareFlags: No messages in DB.");

                        return;
                    }

                    bool result = false;

                    switch (imapAction.FolderAction)
                    {
                        case MailUserAction.Nothing:
                            break;
                        case MailUserAction.SetAsRead:
                            result = _mailEnginesFactory.MessageEngine.SetUnread(uids, false);
                            break;
                        case MailUserAction.SetAsUnread:
                            result = _mailEnginesFactory.MessageEngine.SetUnread(uids, true);
                            break;
                        case MailUserAction.SetAsImportant:
                            result = _mailEnginesFactory.MessageEngine.SetImportant(uids, true);
                            break;
                        case MailUserAction.SetAsNotImpotant:
                            result = _mailEnginesFactory.MessageEngine.SetImportant(uids, false);
                            break;
                        case MailUserAction.SetAsDeleted:
                            _mailEnginesFactory.MessageEngine.DeleteConversations(Tenant, UserName, uids);
                            break;
                        case MailUserAction.RemovedFromFolder:
                            break;
                        case MailUserAction.New:
                            break;
                        default:
                            break;
                    }

                    if (result) needUserUpdate = true;

                    _log.Debug($"ProcessActionFromImapTimer_Elapsed Action {imapAction.FolderAction} complete with result {result.ToString().ToUpper()} for {uids.Count} messages.");

                    StringBuilder sb= new StringBuilder();

                    uids.ForEach(x => sb.Append(x.ToString()+", "));

                    _log.Debug($"ProcessActionFromImapTimer_Elapsed ids: {sb.ToString()}");

                    uids.Clear();
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

                CreateSimpleImapClients(simpleImapClient.Account);
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
                _log.Debug($"NewMessage: {e.Item2.UniqueId}");

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

        public void Dispose()
        {
            _log.Info("Dispose.");

            try
            {
                CancelToken?.Cancel();

                aliveTimer.Dispose();
                processActionFromImapTimer.Dispose();
            }
            catch (Exception ex)
            {
                _log.Error($"Dispose(Tenant={Tenant} UserName: '{UserName}') Exception: {ex.Message}");
            }
        }

        private void UpdateDbFolder(SimpleImapClient simpleImapClient)
        {
            List<string> uidlInIMAP = new List<string>();

            if (simpleImapClient.ImapMessagesList == null)
            {
                _log.Debug($"UpdateDbFolder: ImapMessagesList==null.");

                return;
            }

            _enginesFactorySemaphore.Wait();

            try
            {
                var exp = SimpleMessagesExp.CreateBuilder(Tenant, UserName)
                                            .SetMailboxId(simpleImapClient.Account.MailBoxId)
                                            .SetFolder(simpleImapClient.FolderInt);

                if (simpleImapClient.MailWorkFolder.Tags.Length > 0)
                {
                    var tags = _mailEnginesFactory.TagEngine.GetOrCreateTags(Tenant, UserName, simpleImapClient.MailWorkFolder.Tags);

                    exp.SetTagIds(tags);
                }

                var workFolderMails = _mailInfoDao.GetMailInfoList(exp.Build());

                _log.Debug($"UpdateDbFolder: simpleImapClient.WorkFolderMails.Count={workFolderMails.Count}.");

                foreach (var imap_message in simpleImapClient.ImapMessagesList)
                {
                    _log.Debug($"UpdateDbFolder: imap_message_Uidl={imap_message.UniqueId.Id}.");

                    var uidl = imap_message.UniqueId.ToUidl(simpleImapClient.Folder);

                    uidlInIMAP.Add(uidl);

                    var db_message = workFolderMails.FirstOrDefault(x => x.Uidl == uidl);

                    if (db_message == null)
                    {
                        _log.Debug($"UpdateDbFolder: imap_message_Uidl={uidl} not found in DB.");

                        simpleImapClient.TryGetNewMessage(imap_message);

                        continue;
                    }

                    imap_message.MessageIdInDB = db_message.Id;

                    SetMessageFlagsFromImap(imap_message, db_message);
                }

                //if (!simpleImapClient.IsRootFolder)
                //{
                //    List<int> messagesToRemove = new List<int>();

                //    foreach (var dbMessage in workFolderMails)
                //    {
                //        if (uidlInIMAP.Contains(dbMessage.Uidl)) continue;

                //        messagesToRemove.Add(dbMessage.Id);
                //    }

                //    _mailEnginesFactory.MessageEngine.SetRemoved(messagesToRemove);
                //}
            }
            catch (Exception ex)
            {
                _log.Error($"UpdateDbFolder(IMailFolder->{ex.Message}");
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
            bool result = true;

            Stopwatch watch = null;

            if (_mailSettings.Aggregator.CollectStatistics)
            {
                watch = new Stopwatch();
                watch.Start();
            }

            _enginesFactorySemaphore.Wait();

            try
            {
                var exp = SimpleMessagesExp.CreateBuilder(Tenant, UserName, null)
                                                    .SetMailboxId(simpleImapClient.Account.MailBoxId)
                                                    .SetMimeMessageId(message.MessageId);

                var messagesInfo = _mailInfoDao.GetMailInfoList(exp.Build());

                if (!messagesInfo.Any())
                {
                    message.FixDateIssues(imap_message?.InternalDate, _log);

                    bool unread = false, important = false;

                    if ((imap_message != null) && imap_message.Flags.HasValue)
                    {
                        unread = !imap_message.Flags.Value.HasFlag(MessageFlags.Seen);
                        important = imap_message.Flags.Value.HasFlag(MessageFlags.Flagged);
                    }

                    message.FixEncodingIssues(_log);

                    var folder = simpleImapClient.MailWorkFolder;
                    var uidl = imap_message.UniqueId.ToUidl(simpleImapClient.Folder);

                    _log.Info($"Get message (UIDL: '{uidl}', MailboxId = {simpleImapClient.Account.MailBoxId}, Address = '{simpleImapClient.Account.EMail}')");

                    var messageDB = _mailEnginesFactory.MessageEngine.Save(simpleImapClient.Account, message, uidl, folder, null, unread, _log);

                    if (messageDB == null || messageDB.Id <= 0)
                    {
                        _log.Debug("CreateMessageInDB: failed.");

                        return false;
                    }

                    imap_message.MessageIdInDB = messageDB.Id;

                    DoOptionalOperations(messageDB, message, simpleImapClient.Account, folder, _log, _mailEnginesFactory);

                    _log.Info($"Message saved (id: {messageDB.Id}, From: '{messageDB.From}', Subject: '{messageDB.Subject}', Unread: {messageDB.IsNew})");

                    needUserUpdate = true;

                    return true;
                }

                var messageInfo = messagesInfo[0];

                imap_message.MessageIdInDB = messageInfo.Id;

                string imap_message_uidl = imap_message.UniqueId.ToUidl(simpleImapClient.Folder);

                if (imap_message_uidl != messageInfo.Uidl)
                {
                    var updateUidlQuery = SimpleMessagesExp.CreateBuilder(Tenant, UserName)
                                .SetMessageId(messageInfo.Id)
                                .Build();

                    _mailInfoDao.SetFieldValue(updateUidlQuery, "Uidl", imap_message_uidl);
                }

                if (messageInfo.Folder != simpleImapClient.Folder)
                {
                    _mailEnginesFactory.MessageEngine.SetFolder(new List<int>() { messageInfo.Id }, simpleImapClient.Folder);
                }

                if (messageInfo.IsRemoved)
                {
                    var restoreQuery = SimpleMessagesExp.CreateBuilder(simpleImapClient.Account.TenantId, simpleImapClient.Account.UserId, isRemoved: true)
                        .SetMessageId(messageInfo.Id)
                        .Build();

                    if (_mailInfoDao.SetFieldValue(restoreQuery, "IsRemoved", false) > 0) messageInfo.IsRemoved = false;
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

        private void DoOptionalOperations(MailMessageData message, MimeMessage mimeMessage, MailBoxData mailbox, ASC.Mail.Models.MailFolder folder, ILog log, MailEnginesFactory mailFactory)
        {
            try
            {
                var tagIds = new List<int>();

                if (folder.Tags.Any())
                {
                    log.Debug("DoOptionalOperations -> GetOrCreateTags()");

                    tagIds = mailFactory.TagEngine.GetOrCreateTags(mailbox.TenantId, mailbox.UserId, folder.Tags);
                }

                log.Debug("DoOptionalOperations -> IsCrmAvailable()");

                if (crmAvailable)
                {
                    log.Debug("DoOptionalOperations -> GetCrmTags()");

                    var crmTagIds = mailFactory.TagEngine.GetCrmTags(message.FromEmail);

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

                log.Debug("DoOptionalOperations -> AddMessageToIndex()");

                var mailMail = message.ToMailMail(mailbox.TenantId, new Guid(mailbox.UserId));

                mailFactory.IndexEngine.Add(mailMail);

                foreach (var tagId in tagIds)
                {
                    try
                    {
                        log.DebugFormat($"DoOptionalOperations -> SetMessagesTag(tagId: {tagId})");

                        mailFactory.TagEngine.SetMessagesTag(new List<int> { message.Id }, tagId);
                    }
                    catch (Exception e)
                    {
                        log.ErrorFormat(
                            "SetMessagesTag(tenant={0}, userId='{1}', messageId={2}, tagid = {3})\r\nException:{4}\r\n",
                            mailbox.TenantId, mailbox.UserId, message.Id, e.ToString(),
                            tagIds != null ? string.Join(",", tagIds) : "null");
                    }
                }

                log.Debug("DoOptionalOperations -> AddRelationshipEventForLinkedAccounts()");

                mailFactory.CrmLinkEngine.AddRelationshipEventForLinkedAccounts(mailbox, message);

                log.Debug("DoOptionalOperations -> SaveEmailInData()");

                mailFactory.EmailInEngine.SaveEmailInData(mailbox, message, _mailSettings.Defines.DefaultApiSchema);

                log.Debug("DoOptionalOperations -> SendAutoreply()");

                mailFactory.AutoreplyEngine.SendAutoreply(mailbox, message, _mailSettings.Defines.DefaultApiSchema, log);

                log.Debug("DoOptionalOperations -> UploadIcsToCalendar()");

                if (folder.Folder != Enums.FolderType.Spam)
                {
                    mailFactory.CalendarEngine
                        .UploadIcsToCalendar(mailbox, message.CalendarId, message.CalendarUid, message.CalendarEventIcs,
                            message.CalendarEventCharset, message.CalendarEventMimeType);
                }

                if (_mailSettings.Defines.SaveOriginalMessage)
                {
                    log.Debug("DoOptionalOperations -> StoreMailEml()");
                    StoreMailEml(mailbox.TenantId, mailbox.UserId, message.StreamId, mimeMessage, log);
                }

                log.Debug("DoOptionalOperations -> ApplyFilters()");

                var filters = GetFilters(mailFactory, log);

                mailFactory.FilterEngine.ApplyFilters(message, mailbox, folder, filters);

                log.Debug("DoOptionalOperations -> NotifySignalrIfNeed()");
            }
            catch (Exception ex)
            {
                log.Error($"DoOptionalOperations() ->\r\nException:{ex}\r\n");
            }

            needUserUpdate = true;
        }

        private List<MailSieveFilterData> GetFilters(MailEnginesFactory factory, ILog log)
        {
            var user = factory.UserId;

            if (string.IsNullOrEmpty(user)) return new List<MailSieveFilterData>();

            try
            {
                if (Filters.ContainsKey(user)) return Filters[user];

                var filters = factory.FilterEngine.GetList();

                Filters.TryAdd(user, filters);

                return filters;
            }
            catch (Exception ex)
            {
                log.Error("GetFilters failed", ex);
            }

            return new List<MailSieveFilterData>();
        }

        public string StoreMailEml(int tenant, string userId, string streamId, MimeMessage message, ILog log)
        {
            if (message == null)
                return string.Empty;

            // Using id_user as domain in S3 Storage - allows not to add quota to tenant.
            var savePath = MailStoragePathCombiner.GetEmlKey(userId, streamId);

            var storage = _storageFactory.GetMailStorage(tenant);

            try
            {
                using (var stream = new MemoryStream())
                {
                    message.WriteTo(stream);

                    var res = storage.SaveAsync(savePath, stream, MailStoragePathCombiner.EML_FILE_NAME).Result.ToString();

                    log.InfoFormat($"StoreMailEml() Tenant = {tenant}, UserId = {userId}, SaveEmlPath = {savePath}. Result: {res}");

                    return res;
                }
            }
            catch (Exception ex)
            {
                log.Error($"StoreMailEml Exception: {ex}");
            }

            return string.Empty;
        }

        public void Stop()
        {
            IsReady = false;

            var allAccounts= simpleImapClients.GroupBy(x=>x.Account).Select(x=>x.Key).ToList();

            aliveTimer.Stop();
            aliveTimer.Elapsed -= AliveTimer_Elapsed;

            processActionFromImapTimer.Stop();
            processActionFromImapTimer.Elapsed -= ProcessActionFromImapTimer_Elapsed;

            CancelToken?.Cancel();

            allAccounts.ForEach(x => DeleteSimpleImapClients(x));
        }
    }
}