using ASC.Common;
using ASC.Common.Logging;
using ASC.Common.Utils;
using ASC.Core;
using ASC.Data.Storage;
using ASC.Mail.Aggregator.Service.Console;
using ASC.Mail.Aggregator.Service.Queue;
using ASC.Mail.Aggregator.Service.Queue.Data;
using ASC.Mail.Clients;
using ASC.Mail.Configuration;
using ASC.Mail.Core;
using ASC.Mail.Core.Dao.Expressions.Mailbox;
using ASC.Mail.Core.Engine;
using ASC.Mail.Extensions;
using ASC.Mail.Models;
using ASC.Mail.Storage;
using ASC.Mail.Utils;

using MailKit.Net.Imap;
using MailKit.Net.Pop3;
using MailKit.Security;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using MimeKit;

using NLog;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;


namespace ASC.Mail.Aggregator.Service.Service
{
    [Singletone]
    public class AggregatorService
    {
        public const string ASC_MAIL_COLLECTION_SERVICE_NAME = "ASC Mail Collection Service";
        private const string S_FAIL = "error";
        private const string S_OK = "success";
        private const string PROCESS_MESSAGE = "process message";
        private const string PROCESS_MAILBOX = "process mailbox";
        private const string CONNECT_MAILBOX = "connect mailbox";
        private const int SIGNALR_WAIT_SECONDS = 30;

        private readonly TimeSpan TaskStateCheck;
        private readonly TimeSpan TaskSecondsLifetime;

        private bool IsFirstTime = true;
        private Timer AggregatorTimer;

        private ILog Log { get; }
        private ILog LogStat { get; }
        private IOptionsMonitor<ILog> LogOptions { get; }
        private MailSettings Settings { get; }
        private ConsoleParameters ConsoleParameters { get; }
        private IServiceProvider ServiceProvider { get; }
        private QueueManager QueueManager { get; }
        private SignalrWorker SignalrWorker { get; }

        public Dictionary<string, int> ImapFlags { get; set; }
        public Dictionary<string, Dictionary<string, MailBoxData.MailboxInfo>> SpecialDomainFolders { get; set; }
        public Dictionary<string, int> DefaultFolders { get; set; }
        public string[] SkipImapFlags { get; set; }

        public AggregatorService(
            QueueManager queueManager,
            ConsoleParser consoleParser,
            IOptionsMonitor<ILog> optionsMonitor,
            MailSettings mailSettings,
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ConfigurationExtension configurationExtension,
            SignalrWorker signalrWorker,
            MailQueueItemSettings mailQueueItemSettings)
        {

            ServiceProvider = serviceProvider;
            ConsoleParameters = consoleParser.GetParsedParameters();
            QueueManager = queueManager;

            ConfigureLogNLog(configuration, configurationExtension);
            LogOptions = optionsMonitor;


            Log = optionsMonitor.Get("ASC.Mail.MainThread");
            LogStat = optionsMonitor.Get("ASC.Mail.Stat");

            Settings = mailSettings;

            Settings.DefaultFolders = mailQueueItemSettings.DefaultFolders;
            Settings.ImapFlags = mailQueueItemSettings.ImapFlags;
            Settings.SkipImapFlags = mailQueueItemSettings.SkipImapFlags;
            Settings.SpecialDomainFolders = mailQueueItemSettings.SpecialDomainFolders;

            TaskStateCheck = Settings.Aggregator.TaskCheckState;

            if (ConsoleParameters.OnlyUsers != null) Settings.Defines.WorkOnUsersOnlyList.AddRange(ConsoleParameters.OnlyUsers.ToList());

            if (ConsoleParameters.NoMessagesLimit) Settings.Aggregator.MaxMessagesPerSession = -1;

            TaskSecondsLifetime = Settings.Aggregator.TaskLifetime;
            if (Settings.Aggregator.EnableSignalr) SignalrWorker = signalrWorker;

            Filters = new ConcurrentDictionary<string, List<MailSieveFilterData>>();

            Log.Info("Service is ready.");
        }

        #region methods

        public ConcurrentDictionary<string, List<MailSieveFilterData>> Filters { get; set; }

        private void AggregatorWork(object state)
        {
            var cancelToken = state as CancellationToken? ?? new CancellationToken();

            try
            {
                if (IsFirstTime)
                {
                    QueueManager.LoadMailboxesFromDump();

                    if (QueueManager.ProcessingCount > 0)
                    {
                        Log.InfoFormat("Found {0} tasks to release", QueueManager.ProcessingCount);

                        QueueManager.ReleaseAllProcessingMailboxes(true);
                    }

                    QueueManager.LoadTenantsFromDump();

                    IsFirstTime = false;
                }

                if (cancelToken.IsCancellationRequested)
                {
                    Log.Debug("Aggregator work: IsCancellationRequested. Quit.");
                    return;
                }

                StopTimer();

                var tasks = CreateTasks(Settings.Aggregator.MaxTasksAtOnce, cancelToken);

                while (tasks.Any())
                {
                    var indexTask = Task.WaitAny(tasks.Select(t => t.Task).ToArray(), (int)TaskStateCheck.TotalMilliseconds, cancelToken);

                    if (indexTask > -1)
                    {
                        var outTask = tasks[indexTask];
                        FreeTask(outTask, tasks);
                    }
                    else
                    {
                        Log.InfoFormat("Task.WaitAny timeout. Tasks count = {0}\r\nTasks:\r\n{1}", tasks.Count,
                            string.Join("\r\n", tasks.Select(t =>
                                        $"Id: {t.Task.Id} Status: {t.Task.Status}, MailboxId: {t.Mailbox.MailBoxId} Address: '{t.Mailbox.EMail}'")));
                    }

                    var tasks2Free =
                        tasks.Where(t =>
                            t.Task.Status == TaskStatus.Canceled ||
                            t.Task.Status == TaskStatus.Faulted ||
                            t.Task.Status == TaskStatus.RanToCompletion)
                        .ToList();

                    if (tasks2Free.Any())
                    {
                        Log.InfoFormat("Need free next tasks = {0}: ({1})", tasks2Free.Count,
                                  string.Join(",",
                                              tasks2Free.Select(t => t.Task.Id.ToString(CultureInfo.InvariantCulture))));

                        tasks2Free.ForEach(task => FreeTask(task, tasks));
                    }

                    var difference = Settings.Aggregator.MaxTasksAtOnce - tasks.Count;

                    if (difference <= 0) continue;

                    var newTasks = CreateTasks(difference, cancelToken);

                    tasks.AddRange(newTasks);

                    Log.InfoFormat("Total tasks count = {0} ({1}).", tasks.Count,
                              string.Join(",", tasks.Select(t => t.Task.Id)));
                }

                Log.Info("All mailboxes were processed. Go back to timer.");
            }
            catch (Exception ex) //Exceptions while boxes in process
            {
                if (ex is AggregateException)
                {
                    ex = ((AggregateException)ex).GetBaseException();
                }

                if (ex is TaskCanceledException || ex is OperationCanceledException)
                {
                    Log.Info("Execution was canceled.");

                    QueueManager.ReleaseAllProcessingMailboxes();

                    QueueManager.CancelHandler.Set();

                    return;
                }

                Log.ErrorFormat("Aggregator work exception:\r\n{0}\r\n", ex.ToString());

                if (QueueManager.ProcessingCount != 0)
                {
                    QueueManager.ReleaseAllProcessingMailboxes();
                }
            }

            QueueManager.CancelHandler.Set();

            StartTimer(cancelToken);
        }

        internal Task StartTimer(CancellationToken token, bool immediately = false)
        {
            if (AggregatorTimer == null)
                AggregatorTimer = new Timer(AggregatorWork, token, Timeout.Infinite, Timeout.Infinite);

            Log.DebugFormat("Setup Work timer to {0} seconds", Settings.Defines.CheckTimerInterval.TotalSeconds);

            if (immediately)
            {
                AggregatorTimer.Change(0, Timeout.Infinite);
            }
            else
            {
                AggregatorTimer.Change(Settings.Defines.CheckTimerInterval, Settings.Defines.CheckTimerInterval);
            }

            return Task.CompletedTask;
        }

        private void StopTimer()
        {
            if (AggregatorTimer == null)
                return;

            Log.Debug("Setup Work timer to Timeout.Infinite");
            AggregatorTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        internal void StopService(CancellationTokenSource tokenSource)
        {
            if (tokenSource != null)
                tokenSource.Cancel();

            if (QueueManager != null)
            {
                QueueManager.CancelHandler.WaitOne();
            }

            StopTimer();
            DisposeWorkers();
        }

        private void DisposeWorkers()
        {
            if (AggregatorTimer != null)
            {
                AggregatorTimer.Dispose();
                AggregatorTimer = null;
            }

            if (QueueManager != null)
                QueueManager.Dispose();

            if (SignalrWorker != null)
                SignalrWorker.Dispose();
        }

        public List<TaskData> CreateTasks(int needCount, CancellationToken cancelToken)
        {
            Log.InfoFormat($"Create tasks (need {needCount}).");

            var mailboxes = QueueManager.GetLockedMailboxes(needCount).ToList();

            var tasks = new List<TaskData>();

            foreach (var mailbox in mailboxes)
            {
                var commonCancelToken =
                    CancellationTokenSource.CreateLinkedTokenSource(cancelToken, new CancellationTokenSource(TaskSecondsLifetime).Token).Token;

                var log = LogOptions.Get($"ASC.Mail Mbox_{mailbox.MailBoxId}");

                var task = Task.Run(() => ProcessMailbox(mailbox, Settings, log, commonCancelToken), commonCancelToken);

                tasks.Add(new TaskData(mailbox, task));
            }

            if (tasks.Any()) Log.InfoFormat("Created {0} tasks.", tasks.Count);
            else Log.Info("No more mailboxes for processing.");

            return tasks;
        }

        private void ProcessMailbox(MailBoxData mailBox, MailSettings mailSettings, ILog log, CancellationToken token)
        {
            using var client = CreateMailClient(mailBox, log, token);

            if (client == null || !client.IsConnected || !client.IsAuthenticated || client.IsDisposed)
            {
                if (client != null)
                    log.InfoFormat("Client -> Could not connect: {0} | Not authenticated: {1} | Was disposed: {2}",
                        !client.IsConnected ? "Yes" : "No",
                        !client.IsAuthenticated ? "Yes" : "No",
                        client.IsDisposed ? "Yes" : "No");

                else log.InfoFormat("Client was null");

                log.InfoFormat($"Release mailbox (Tenant: {mailBox.TenantId} MailboxId: {mailBox.MailBoxId}, Address: '{mailBox.EMail}')");
                return;
            }

            using var scope = ServiceProvider.CreateScope();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            tenantManager.SetCurrentTenant(client.Account.TenantId);

            var mailbox = client.Account;

            Stopwatch watch = null;

            if (mailSettings.Aggregator.CollectStatistics)
            {
                watch = new Stopwatch();
                watch.Start();
            }

            log.InfoFormat(
                "Process mailbox(Tenant: {0}, MailboxId: {1} Address: \"{2}\") Is {3}. | Task №: {4}",
                mailbox.TenantId, mailbox.MailBoxId,
                mailbox.EMail, mailbox.Active ? "Active" : "Inactive", Task.CurrentId);

            var failed = false;

            try
            {
                client.Log = log;
                client.GetMessage += ClientOnGetMessage;
                client.Aggregate(mailSettings, mailSettings.Aggregator.MaxMessagesPerSession);
            }
            catch (OperationCanceledException)
            {
                log.InfoFormat(
                    $"Operation cancel: ProcessMailbox(Tenant = {mailbox.TenantId}, MailboxId = {mailbox.MailBoxId}, Address = \"{mailbox.EMail}\")");

                NotifySignalrIfNeed(mailbox, log);
            }
            catch (Exception ex)
            {
                log.ErrorFormat(
                    "ProcessMailbox(Tenant = {0}, MailboxId = {1}, Address = \"{2}\")\r\nException: {3}\r\n",
                    mailbox.TenantId, mailbox.MailBoxId, mailbox.EMail,
                    ex is ImapProtocolException || ex is Pop3ProtocolException ? ex.Message : ex.ToString());

                failed = true;
            }
            finally
            {
                CloseMailClient(client, mailbox, Log);

                if (Settings.Aggregator.CollectStatistics && watch != null)
                {
                    watch.Stop();

                    LogStatistic(PROCESS_MAILBOX, mailbox, watch.Elapsed, failed);
                }
            }

            var state = GetMailboxState(mailbox, log);

            switch (state)
            {
                case MailboxState.NoChanges:
                    log.InfoFormat($"MailBox with Id = {mailbox.MailBoxId} not changed.");
                    break;

                case MailboxState.Disabled:
                    log.InfoFormat($"MailBox with Id = {mailbox.MailBoxId} is deactivated.");
                    break;

                case MailboxState.Deleted:
                    log.InfoFormat($"MailBox with Id = {mailbox.MailBoxId} is removed.");
                    try
                    {
                        log.InfoFormat($"RemoveMailBox(Id = {mailbox.MailBoxId}) -> Try clear new data from removed mailbox");

                        var mailboxEngine = scope.ServiceProvider.GetService<MailboxEngine>();
                        mailboxEngine.RemoveMailBox(mailbox);
                    }
                    catch (Exception exRem)
                    {
                        log.InfoFormat(
                            $"ProcessMailbox -> RemoveMailBox(Tenant = {mailbox.TenantId}, MailboxId = {mailbox.MailBoxId}, Address = {mailbox.EMail})\r\nException:{exRem.Message}\r\n");
                    }
                    break;

                case MailboxState.DateChanged:
                    log.InfoFormat($"MailBox with Id = {mailbox.MailBoxId}: Begin date was changed.");
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            log.InfoFormat($"Mailbox \"{mailbox.EMail}\" has been processed.");

            //dispose log?
        }

        private MailClient CreateMailClient(MailBoxData mailbox, ILog log, CancellationToken cancelToken)
        {
            MailClient client = null;

            var connectError = false;
            var stopClient = false;

            Stopwatch watch = null;

            if (Settings.Aggregator.CollectStatistics)
            {
                watch = new Stopwatch();
                watch.Start();
            }

            try
            {
                using var scope = ServiceProvider.CreateScope();
                var factory = scope.ServiceProvider.GetService<IMailDaoFactory>();

                var serverFolderAccessInfos = factory
                    .GetImapSpecialMailboxDao()
                    .GetServerFolderAccessInfoList();

                client = new MailClient(
                    mailbox, cancelToken,
                    serverFolderAccessInfos,
                    Settings.Aggregator.TcpTimeout,
                    mailbox.IsTeamlab || Settings.Defines.SslCertificatesErrorsPermit,
                    Settings.Aggregator.ProtocolLogPath, log, true);

                log.DebugFormat($"Login client (" +
                    $"Tenant: {mailbox.TenantId}, " +
                    $"MailboxId: {mailbox.MailBoxId} " +
                    $"Address: '{mailbox.EMail}')");

                if (!mailbox.Imap)
                {
                    client.FuncGetPop3NewMessagesIDs = uidls => MessageEngine.GetPop3NewMessagesIDs(
                            factory, mailbox, uidls, Settings.Aggregator.ChunkOfPop3Uidl);
                }

                client.Authenticated += ClientOnAuthenticated;
                client.LoginClient();
            }

            #region Exceptions while login failed

            catch (TimeoutException exTimeout)
            {
                log.WarnFormat($"LOGIN IMAP [TIMEOUT]\r\nTenant: {mailbox.TenantId}, MailboxId: {mailbox.MailBoxId}, Address: \"{mailbox.EMail}\"\r\nException: {exTimeout}");

                connectError = true;
                stopClient = true;
            }
            catch (ImapProtocolException protocolEx)
            {
                log.ErrorFormat($"LOGIN IMAP [IMAP PROTOCOL]\r\nTenant: {mailbox.TenantId}, MailboxId: {mailbox.MailBoxId}, Address: \"{mailbox.EMail}\"\r\n{protocolEx}");

                connectError = true;
                stopClient = true;
            }
            catch (OperationCanceledException ocEx)
            {
                log.InfoFormat($"LOGIN IMAP [OPERATION CANCEL]\r\nTenant: {mailbox.TenantId}, MailboxId: {mailbox.MailBoxId}, Address: \"{mailbox.EMail}\"{ocEx}");

                stopClient = true;
            }
            catch (AuthenticationException authEx)
            {
                log.ErrorFormat($"LOGIN IMAP [AUTHENTICATION]\r\nTenant: {mailbox.TenantId}, MailboxId: {mailbox.MailBoxId}, Address: \"{mailbox.EMail}\")\r\n{authEx}");

                connectError = true;
                stopClient = true;
            }
            catch (WebException webEx)
            {
                log.ErrorFormat($"LOGIN IMAP [WEB]\r\nTenant: {mailbox.TenantId}, MailboxId: {mailbox.MailBoxId}, Address: \"{mailbox.EMail}\")\r\n{webEx}");

                connectError = true;
                stopClient = true;
            }
            catch (Exception ex)
            {
                log.ErrorFormat("LOGIN IMAP [UNREGISTERED EXCEPTION]\r\nTenant: {0}, MailboxId: {1}, Address: \"{2}\")\r\n{3}",
                    mailbox.TenantId, mailbox.MailBoxId, mailbox.EMail,
                    ex is ImapProtocolException || ex is Pop3ProtocolException ? ex.Message : ex.ToString());

                stopClient = true;
            }
            finally
            {
                if (connectError)
                {
                    SetMailboxAuthError(mailbox, log);
                }

                if (stopClient)
                {
                    CloseMailClient(client, mailbox, log);
                }

                if (Settings.Aggregator.CollectStatistics && watch != null)
                {
                    watch.Stop();

                    LogStatistic(CONNECT_MAILBOX, mailbox, watch.Elapsed, connectError);
                }
            }
            #endregion
            return client;
        }

        private void NotifySignalrIfNeed(MailBoxData mailbox, ILog log)
        {
            if (!Settings.Aggregator.EnableSignalr)
            {
                log.Debug("Skip NotifySignalrIfNeed: EnableSignalr == false");

                return;
            }

            var now = DateTime.UtcNow;

            try
            {
                if (mailbox.LastSignalrNotify.HasValue &&
                    !((now - mailbox.LastSignalrNotify.Value).TotalSeconds > SIGNALR_WAIT_SECONDS))
                {
                    mailbox.LastSignalrNotifySkipped = true;
                    log.InfoFormat($"Skip NotifySignalrIfNeed: last notification has occurend less then {SIGNALR_WAIT_SECONDS} seconds ago");
                    return;
                }

                if (SignalrWorker == null)
                    throw new NullReferenceException("SignalrWorker");

                SignalrWorker.AddMailbox(mailbox);

                log.InfoFormat("NotifySignalrIfNeed(UserId = {0} TenantId = {1}) has been succeeded",
                    mailbox.UserId, mailbox.TenantId);
            }
            catch (Exception ex)
            {
                log.ErrorFormat("NotifySignalrIfNeed(UserId = {0} TenantId = {1}) Exception: {2}", mailbox.UserId,
                    mailbox.TenantId, ex.ToString());
            }

            mailbox.LastSignalrNotify = now;
            mailbox.LastSignalrNotifySkipped = false;
        }

        private void SetMailboxAuthError(MailBoxData mailbox, ILog log)
        {
            try
            {
                if (mailbox.AuthErrorDate.HasValue)
                    return;

                mailbox.AuthErrorDate = DateTime.UtcNow;

                using var scope = ServiceProvider.CreateScope();

                var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
                tenantManager.SetCurrentTenant(mailbox.TenantId);

                var mailboxEngine = scope.ServiceProvider.GetService<MailboxEngine>();

                mailboxEngine.SetMaiboxAuthError(mailbox.MailBoxId, mailbox.AuthErrorDate.Value);
            }
            catch (Exception ex)
            {
                log.ErrorFormat(
                    $"CreateTasks() -> SetMailboxAuthError(Tenant = {mailbox.TenantId}, MailboxId = {mailbox.MailBoxId}, Address = \"{mailbox.EMail}\")\r\nException:{ex.Message}\r\n");
            }
        }

        private void CloseMailClient(MailClient client, MailBoxData mailbox, ILog log)
        {
            if (client == null)
                return;

            try
            {
                client.Authenticated -= ClientOnAuthenticated;
                client.GetMessage -= ClientOnGetMessage;

                client.Cancel();
                client.Dispose();
            }
            catch (Exception ex)
            {
                log.ErrorFormat(
                    $"CloseMailClient(Tenant = {mailbox.TenantId}, MailboxId = {mailbox.MailBoxId}, Address = \"{mailbox.EMail}\")\r\nException:{ex.Message}\r\n");
            }
        }

        private void ClientOnAuthenticated(object sender, MailClientEventArgs mailClientEventArgs)
        {
            if (!mailClientEventArgs.Mailbox.AuthErrorDate.HasValue)
                return;

            mailClientEventArgs.Mailbox.AuthErrorDate = null;

            using var scope = ServiceProvider.CreateScope();

            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            tenantManager.SetCurrentTenant(mailClientEventArgs.Mailbox.TenantId);

            var mailboxEngine = scope.ServiceProvider.GetService<MailboxEngine>();
            mailboxEngine.SetMaiboxAuthError(mailClientEventArgs.Mailbox.MailBoxId, mailClientEventArgs.Mailbox.AuthErrorDate);
        }

        private void ClientOnGetMessage(object sender, MailClientMessageEventArgs mailClientMessageEventArgs)
        {
            var log = Log;

            Stopwatch watch = null;

            if (Settings.Aggregator.CollectStatistics)
            {
                watch = new Stopwatch();
                watch.Start();
            }

            var failed = false;

            var mailbox = mailClientMessageEventArgs.Mailbox;

            try
            {
                MailBoxSaveInfo boxInfo = new MailBoxSaveInfo()
                {
                    Uid = mailClientMessageEventArgs.MessageUid,
                    MimeMessage = mailClientMessageEventArgs.Message,
                    Folder = mailClientMessageEventArgs.Folder,
                    Unread = mailClientMessageEventArgs.Unread
                };

                log = mailClientMessageEventArgs.Logger;

                var uidl = mailbox.Imap ? $"{boxInfo.Uid}-{(int)boxInfo.Folder.Folder}" : boxInfo.Uid;

                log.InfoFormat($"Found message (UIDL: '{uidl}', MailboxId = {mailbox.MailBoxId}, Address = {mailbox.EMail})");

                if (!SaveAndOptional(mailbox, boxInfo, uidl, log)) return;
            }
            catch (Exception ex)
            {
                Log.ErrorFormat($"ClientOnGetMessage() -> \r\nException:{ex}\r\n");
                failed = true;
            }
            finally
            {
                if (Settings.Aggregator.CollectStatistics && watch != null)
                {
                    watch.Stop();

                    LogStatistic(PROCESS_MESSAGE, mailbox, watch.Elapsed, failed);

                    GC.Collect();
                }
            }
        }

        private class MailBoxSaveInfo
        {
            public string Uid { get; set; }
            public MimeMessage MimeMessage { get; set; }
            public MailFolder Folder { get; set; }
            public bool Unread { get; set; }
        }

        private bool SaveAndOptional(MailBoxData mailbox, MailBoxSaveInfo boxInfo, string uidl, ILog log)
        {
            using var scope = ServiceProvider.CreateScope();

            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(mailbox.TenantId);
            securityContext.AuthenticateMe(new Guid(mailbox.UserId));

            var messageEngine = scope.ServiceProvider.GetService<MessageEngine>();

            var message = messageEngine.Save(mailbox, boxInfo.MimeMessage, uidl, boxInfo.Folder, null, boxInfo.Unread, log);

            if (message == null || message.Id <= 0) return false;

            log.InfoFormat($"Message saved (to Mbox_{mailbox.MailBoxId} - {mailbox.EMail.Address}) (Id: {message.Id}, From: {message.From}, Subject: {message.Subject}, Unread: {message.IsNew})");

            log.Info("DoOptionalOperations -> START");

            DoOptionalOperations(message, boxInfo.MimeMessage, mailbox, boxInfo.Folder, log, scope);

            log.Info("DoOptionalOperations -> END");

            return true;
        }

        enum MailboxState
        {
            NoChanges,
            Disabled,
            Deleted,
            DateChanged
        }

        private MailboxState GetMailboxState(MailBoxData mailbox, ILog log)
        {
            try
            {
                log.Debug("GetMailBoxState()");

                using var scope = ServiceProvider.CreateScope();
                var mailboxEngine = scope.ServiceProvider.GetService<MailboxEngine>();
                var status = mailboxEngine.GetMailboxStatus(new СoncreteUserMailboxExp(mailbox.MailBoxId, mailbox.TenantId, mailbox.UserId, null));

                if (mailbox.BeginDate != status.BeginDate)
                {
                    mailbox.BeginDateChanged = true;
                    mailbox.BeginDate = status.BeginDate;

                    return MailboxState.DateChanged;
                }

                if (status.IsRemoved)
                    return MailboxState.Deleted;

                if (!status.Enabled)
                    return MailboxState.Disabled;
            }
            catch (Exception exGetMbInfo)
            {
                log.ErrorFormat(
                    $"GetMailBoxState(Tenant = {mailbox.TenantId}, " +
                    $"MailboxId = {mailbox.MailBoxId}, " +
                    $"Address = '{mailbox.EMail}') " +
                    $"Exception: {exGetMbInfo.Message} \n{exGetMbInfo.StackTrace}");
            }

            return MailboxState.NoChanges;
        }

        private readonly ConcurrentDictionary<string, bool> _userCrmAvailabeDictionary = new ConcurrentDictionary<string, bool>();
        private readonly object _locker = new object();

        private bool IsCrmAvailable(MailBoxData mailbox, ILog log)
        {
            bool crmAvailable;

            using var tempScope = ServiceProvider.CreateScope();

            var tenantManager = tempScope.ServiceProvider.GetService<TenantManager>();
            var securityContext = tempScope.ServiceProvider.GetService<SecurityContext>();
            var apiHelper = tempScope.ServiceProvider.GetService<ApiHelper>();

            lock (_locker)
            {
                if (_userCrmAvailabeDictionary.TryGetValue(mailbox.UserId, out crmAvailable))
                    return crmAvailable;

                crmAvailable = mailbox.IsCrmAvailable(tenantManager, securityContext, apiHelper, log);
                _userCrmAvailabeDictionary.GetOrAdd(mailbox.UserId, crmAvailable);
            }

            return crmAvailable;
        }

        private List<MailSieveFilterData> GetFilters(FilterEngine filterEngine, string userId, ILog log)
        {
            if (string.IsNullOrEmpty(userId))
                return new List<MailSieveFilterData>();

            try
            {
                if (Filters.ContainsKey(userId)) return Filters[userId];

                var filters = filterEngine.GetList();

                Filters.TryAdd(userId, filters);

                return filters;
            }
            catch (Exception ex)
            {
                log.Error("GetFilters failed", ex);
            }

            return new List<MailSieveFilterData>();
        }

        private void DoOptionalOperations(MailMessageData message, MimeMessage mimeMessage, MailBoxData mailbox, MailFolder folder, ILog log, IServiceScope scope)
        {
            try
            {
                var tagIds = new List<int>();

                var tagEngine = scope.ServiceProvider.GetService<TagEngine>();
                var indexEngine = scope.ServiceProvider.GetService<IndexEngine>();
                var crmLinkEngine = scope.ServiceProvider.GetService<CrmLinkEngine>();
                var emailInEngine = scope.ServiceProvider.GetService<EmailInEngine>();
                var autoreplyEngine = scope.ServiceProvider.GetService<AutoreplyEngine>();
                var calendarEngine = scope.ServiceProvider.GetService<CalendarEngine>();
                var filterEngine = scope.ServiceProvider.GetService<FilterEngine>();

                if (folder.Tags.Any())
                {
                    log.Debug("DoOptionalOperations -> GetOrCreateTags()");

                    tagIds = tagEngine.GetOrCreateTags(mailbox.TenantId, mailbox.UserId, folder.Tags);
                }

                log.Debug("DoOptionalOperations -> IsCrmAvailable()");

                if (IsCrmAvailable(mailbox, log))
                {
                    log.Debug("DoOptionalOperations -> GetCrmTags()");

                    var crmTagIds = tagEngine.GetCrmTags(message.FromEmail);

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

                indexEngine.Add(mailMail);

                foreach (var tagId in tagIds)
                {
                    try
                    {
                        log.DebugFormat($"DoOptionalOperations -> SetMessagesTag(tagId: {tagId})");

                        tagEngine.SetMessagesTag(new List<int> { message.Id }, tagId);
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

                crmLinkEngine.AddRelationshipEventForLinkedAccounts(mailbox, message);

                log.Debug("DoOptionalOperations -> SaveEmailInData()");

                emailInEngine.SaveEmailInData(mailbox, message, Settings.Defines.DefaultApiSchema);

                log.Debug("DoOptionalOperations -> SendAutoreply()");

                autoreplyEngine.SendAutoreply(mailbox, message, Settings.Defines.DefaultApiSchema, log);

                log.Debug("DoOptionalOperations -> UploadIcsToCalendar()");

                if (folder.Folder != Enums.FolderType.Spam)
                {
                    calendarEngine
                        .UploadIcsToCalendar(mailbox, message.CalendarId, message.CalendarUid, message.CalendarEventIcs,
                            message.CalendarEventCharset, message.CalendarEventMimeType, mailbox.EMail.Address,
                            Settings.Defines.DefaultApiSchema);
                }

                if (Settings.Defines.SaveOriginalMessage)
                {
                    log.Debug("DoOptionalOperations -> StoreMailEml()");
                    StoreMailEml(mailbox.TenantId, mailbox.UserId, message.StreamId, mimeMessage, log);
                }

                log.Debug("DoOptionalOperations -> ApplyFilters()");

                var filters = GetFilters(filterEngine, mailbox.UserId, log);

                filterEngine.ApplyFilters(message, mailbox, folder, filters);

                log.Debug("DoOptionalOperations -> NotifySignalrIfNeed()");

                NotifySignalrIfNeed(mailbox, log);
            }
            catch (Exception ex)
            {
                log.ErrorFormat($"DoOptionalOperations() ->\r\nException:{ex}\r\n");
            }
        }

        public string StoreMailEml(int tenant, string userId, string streamId, MimeMessage message, ILog log)
        {
            if (message == null)
                return string.Empty;

            // Using id_user as domain in S3 Storage - allows not to add quota to tenant.
            var savePath = MailStoragePathCombiner.GetEmlKey(userId, streamId);

            using var scope = ServiceProvider.CreateScope();
            var storageFactory = scope.ServiceProvider.GetService<StorageFactory>();

            var storage = storageFactory.GetMailStorage(tenant);

            try
            {
                using (var stream = new MemoryStream())
                {
                    message.WriteTo(stream);

                    var res = storage.Save(savePath, stream, MailStoragePathCombiner.EML_FILE_NAME).ToString();

                    log.InfoFormat($"StoreMailEml() Tenant = {tenant}, UserId = {userId}, SaveEmlPath = {savePath}. Result: {res}");

                    return res;
                }
            }
            catch (Exception ex)
            {
                log.ErrorFormat($"StoreMailEml Exception: {ex}");
            }

            return string.Empty;
        }

        public void FreeTask(TaskData taskData, ICollection<TaskData> tasks)
        {
            try
            {
                //make dispose in TaskData
                Log.DebugFormat($"End Task {taskData.Task.Id} with status = '{taskData.Task.Status}'.");

                if (!tasks.Remove(taskData))
                    Log.Error("Task not exists in tasks array.");

                ReleaseMailbox(taskData.Mailbox);

                taskData.Task.Dispose();  //=null

                GC.Collect();
            }
            catch (Exception ex)
            {
                Log.ErrorFormat($"FreeTask(Id: {taskData.Mailbox.MailBoxId}, Email: {taskData.Mailbox.EMail}):\r\nException:{ex}\r\n");
            }
        }

        private void ReleaseMailbox(MailBoxData mailbox)
        {
            if (mailbox == null)
                return;

            if (mailbox.LastSignalrNotifySkipped)
                NotifySignalrIfNeed(mailbox, Log);

            QueueManager.ReleaseMailbox(mailbox);

            if (!Filters.ContainsKey(mailbox.UserId))
                return;

            List<MailSieveFilterData> filters;
            if (!Filters.TryRemove(mailbox.UserId, out filters))
            {
                Log.Error("Try forget Filters for user failed");
            }
        }

        private void ConfigureLogNLog(IConfiguration configuration, ConfigurationExtension configurationExtension)
        {
            var fileName = CrossPlatform.PathCombine(configuration["pathToNlogConf"], "nlog.config");

            LogManager.Configuration = new NLog.Config.XmlLoggingConfiguration(fileName);
            LogManager.ThrowConfigExceptions = false;

            var settings = configurationExtension.GetSetting<NLogSettings>("log");
            if (!string.IsNullOrEmpty(settings.Name))
            {
                LogManager.Configuration.Variables["name"] = settings.Name;
            }

            if (!string.IsNullOrEmpty(settings.Dir))
            {
                LogManager.Configuration.Variables["dir"] = settings.Dir.TrimEnd('/').TrimEnd('\\') + Path.DirectorySeparatorChar;
            }

            NLog.Targets.Target.Register<SelfCleaningTarget>("SelfCleaning");
        }

        private void LogStatistic(string method, MailBoxData mailBoxData, TimeSpan duration, bool failed)
        {
            if (!Settings.Aggregator.CollectStatistics)
                return;

            var pairs = new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>("duration", duration.TotalMilliseconds),
                new KeyValuePair<string, object>("mailboxId", mailBoxData.MailBoxId),
                new KeyValuePair<string, object>("address", mailBoxData.EMail.ToString()),
                new KeyValuePair<string, object>("status", failed ? S_FAIL : S_OK)
            };

            LogStat.DebugWithProps(method, pairs);
        }
        #endregion
    }

}
