
using AuthenticationException = MailKit.Security.AuthenticationException;
using SecurityContext = ASC.Core.SecurityContext;

namespace ASC.Mail.Aggregator.Service.Service;

public class MailboxHandler : IDisposable
{
    private readonly IServiceScope _scope;
    private MailClient _client;
    private readonly MailBoxData _box;
    private readonly MailSettings _settings;
    private readonly CancellationTokenSource _tokenSource;
    private readonly List<ServerFolderAccessInfo> _serverFolderAccessInfo;
    private ILogger _log;
    private ILogger _logStat;

    private readonly TenantManager _tenantManager;
    private readonly MailboxEngine _mailboxEngine;
    private readonly MessageEngine _messageEngine;
    private readonly SecurityContext _securityContext;

    private readonly string _boxInfo;
    private const int SOCKET_WAIT_SECONDS = 30;

    private const string S_FAIL = "error";
    private const string S_OK = "success";

    public MailboxHandler(
        IServiceProvider serviceProvider,
        MailBoxData mailBox,
        MailSettings mailSettings,
        ILogger logger,
        ILoggerProvider logProvider,
        CancellationTokenSource tokenSource,
        List<ServerFolderAccessInfo> serverFolderAccessInfo)
    {
        _scope = serviceProvider.CreateScope();
        _box = mailBox;
        _settings = mailSettings;

        _log = logger;

        if (_settings.Aggregator.CollectStatistics)
            _logStat = logProvider.CreateLogger("ASC.Mail.Stat");

        _tokenSource = tokenSource;
        _serverFolderAccessInfo = serverFolderAccessInfo;

        _tenantManager = _scope.ServiceProvider.GetService<TenantManager>();
        _mailboxEngine = _scope.ServiceProvider.GetService<MailboxEngine>();
        _messageEngine = _scope.ServiceProvider.GetService<MessageEngine>();
        _securityContext = _scope.ServiceProvider.GetService<SecurityContext>();

        _boxInfo = $"Tenant: {_box.TenantId}, MailboxId: {_box.MailBoxId}, Address: {_box.EMail}";
    }

    public void DoProcess()
    {
        _tenantManager.SetCurrentTenant(_box.TenantId);

        CreateClient();

        if (_client == null || !_client.IsConnected || !_client.IsAuthenticated || _client.IsDisposed)
        {
            if (_client != null)
            {
                _log.InfoMailboxHandlerCreateClient(
                    !_client.IsConnected ? "Yes" : "No",
                    !_client.IsAuthenticated ? "Yes" : "No",
                    _client.IsDisposed ? "Yes" : "No");
            }

            else _log.InfoMailboxHandlerNullClient();

            _log.InfoMailboxHandlerReleaseMailbox(_box.TenantId, _box.MailBoxId, _box.EMail.ToString());

            return;
        }

        var mailbox = _client.Account;

        Stopwatch watch = null;
        if (_settings.Aggregator.CollectStatistics) watch = Stopwatch.StartNew();

        var active = mailbox.Active ? "Active" : "Inactive";

        _log.InfoMailboxHandlerProcessMailbox(mailbox.TenantId, mailbox.MailBoxId, mailbox.EMail.ToString(), active, Task.CurrentId);

        var failed = false;

        try
        {
            _client.Log = _log;
            _client.GetMessage += ClientOnGetMessage;
            _client.Aggregate(_settings, _settings.Aggregator.MaxMessagesPerSession);
        }
        catch (OperationCanceledException)
        {
            _log.InfoMailboxHandlerOperationCancel(mailbox.TenantId, mailbox.MailBoxId, mailbox.EMail.ToString());

            AggregatorService.NotifySocketIO(mailbox, _log);
        }
        catch (Exception ex)
        {
            _log.ErrorMailboxHandlerProcessMailbox(
                mailbox.TenantId, mailbox.MailBoxId, mailbox.EMail.ToString(),
                ex is ImapProtocolException || ex is Pop3ProtocolException ? ex.Message : ex.ToString());

            failed = true;
        }
        finally
        {
            if (_settings.Aggregator.CollectStatistics)
            {
                watch.Stop();

                LogStatistic(mailbox, "process mailbox", watch.Elapsed.TotalSeconds, failed);
            }
        }

        CheckMailboxState(mailbox);

        _log.InfoMailboxHandlerHasBeenProcessed(mailbox.MailBoxId, mailbox.EMail.ToString());
    }

    public void Dispose()
    {
        if (_scope != null)
            _scope.Dispose();

        if (_tokenSource != null)
            _tokenSource.Dispose();

        if (_log != null)
            _log = null;

        if (_logStat != null)
            _logStat = null;

        if (_client != null)
            CloseClient();
    }

    #region private

    private void CheckMailboxState(MailBoxData mailbox)
    {
        try
        {
            _log.DebugMailboxHandlerGetState();

            var status = _mailboxEngine.GetMailboxStatus(
                new СoncreteUserMailboxExp(mailbox.MailBoxId, mailbox.TenantId, mailbox.UserId, null));

            if (mailbox.BeginDate != status.BeginDate)
            {
                mailbox.BeginDateChanged = true;
                mailbox.BeginDate = status.BeginDate;

                _log.InfoMailboxHandlerBeginDateWasChanged(mailbox.MailBoxId);
                return;
            }

            if (status.IsRemoved)
            {
                _log.InfoMailboxHandlerWasRemoved(mailbox.MailBoxId);

                try
                {
                    _mailboxEngine.RemoveMailBox(mailbox);
                }
                catch (Exception ex)
                {
                    _log.ErrorMailboxHandlerRemoveMailbox(mailbox.MailBoxId, ex.Message);
                }
                return;
            }

            if (!status.Enabled)
            {
                _log.InfoMailboxHandlerMailboxDeactivated(mailbox.MailBoxId);
                return;
            }

            _log.InfoMailboxHandlerMailboxNotChanged(mailbox.MailBoxId);
        }
        catch (Exception ex)
        {
            _log.ErrorMailboxHandlerCheckState(ex.Message);
        }
    }

    private void CreateClient()
    {
        Stopwatch watch = null;
        if (_settings.Aggregator.CollectStatistics)
            watch = Stopwatch.StartNew();

        var connectError = false;
        var stopClient = false;

        try
        {
            _client = new MailClient(
                _box,
                _tokenSource.Token,
                _serverFolderAccessInfo,
                _log,
                _settings.Aggregator.TcpTimeout,
                _box.IsTeamlab || _settings.Defines.SslCertificatesErrorsPermit,
                _settings.Defines.CheckCertificateRevocation,
                _settings.Aggregator.ProtocolLogPath, true);

            if (!_box.Imap)
                _client.FuncGetPop3NewMessagesIDs = uidls => _messageEngine.GetPop3NewMessagesIDs(
                    _scope.ServiceProvider.GetService<IMailDaoFactory>(), _box, uidls,
                    _settings.Aggregator.ChunkOfPop3Uidl);

            _client.Authenticated += ClientOnAuthenticated;
            _client.LoginClient();
        }
        catch (TimeoutException tEx)
        {
            _log.WarnMailboxHandlerLoginTimeout(_boxInfo, tEx.ToString());

            connectError = true;
            stopClient = true;
        }
        catch (ImapProtocolException iEx)
        {
            _log.ErrorMailboxHandlerAtLogin(_boxInfo, iEx.ToString());

            connectError = true;
            stopClient = true;
        }
        catch (OperationCanceledException ocEx)
        {
            _log.InfoMailboxHandlerOperationCancelAtLogin(_boxInfo, ocEx.ToString());

            stopClient = true;
        }
        catch (AuthenticationException aEx)
        {
            _log.ErrorMailboxHandlerAuthentication(_boxInfo, aEx.ToString());

            connectError = true;
            stopClient = true;
        }
        catch (WebException wEx)
        {
            _log.ErrorMailboxHandlerWeb(_boxInfo, wEx.ToString());

            connectError = true;
            stopClient = true;
        }
        catch (SslHandshakeException sslEx)
        {
            if (sslEx.Message.Contains("The remote certificate was rejected") || sslEx.Message.Contains("certificate has expired"))
            {
                _log.ErrorMailboxHandlerCertificateExpired(_boxInfo, sslEx.ToString());
                connectError = true;
            }
            else
            {
                _log.ErrorMailboxHandlerSSL(_boxInfo, sslEx.ToString());
            }

            stopClient = true;
        }
        catch (Exception ex)
        {
            if (ex is System.Net.Sockets.SocketException)
            {
                if (ex.Message.Contains("Error_11001"))
                {
                    _log.ErrorMailboxHandlerCouldNotResolveHost(_boxInfo, ex.ToString());
                    connectError = true;
                }
            }
            else
            {
                _log.ErrorMailboxHandlerUnknownEx(_boxInfo, ex is ImapProtocolException || ex is Pop3ProtocolException ? ex.Message : ex.ToString());
            }

            stopClient = true;
        }
        finally
        {
            if (connectError)
            {
                SetMailboxAuthError();
            }
            if (stopClient)
            {
                CloseClient();
            }
            if (_settings.Aggregator.CollectStatistics)
            {
                watch.Stop();
                LogStatistic(_box, "connect mailbox", watch.Elapsed.TotalSeconds, connectError);
            }
        }
    }

    private void ClientOnAuthenticated(object sender, MailClientEventArgs args)
    {
        if (!args.Mailbox.AuthErrorDate.HasValue) return;

        args.Mailbox.AuthErrorDate = null;

        _mailboxEngine.SetMaiboxAuthError(args.Mailbox.MailBoxId, args.Mailbox.AuthErrorDate);
    }

    private void ClientOnGetMessage(object sender, MailClientMessageEventArgs args)
    {
        Stopwatch watch = null;

        if (_settings.Aggregator.CollectStatistics)
            watch = Stopwatch.StartNew();

        var failed = false;
        var box = args.Mailbox;

        try
        {
            BoxSaveInfo boxSaveInfo = new BoxSaveInfo()
            {
                Uid = args.MessageUid,
                MimeMessage = args.Message,
                Folder = args.Folder,
                Unread = args.Unread
            };

            var uidl = _box.Imap ? $"{boxSaveInfo.Uid}-{(int)boxSaveInfo.Folder.Folder}" : boxSaveInfo.Uid;

            _log.InfoMailboxHandlerFoundMessage(uidl, _boxInfo);

            if (!SaveAndOptional(box, boxSaveInfo, uidl)) return;

            boxSaveInfo.MimeMessage.Dispose();
        }
        catch (Exception ex)
        {
            _log.ErrorMailboxHandlerOnGetMessage(ex.ToString());
            failed = true;
        }
        finally
        {

            if (watch != null)
            {
                watch.Stop();
                LogStatistic(box, "process message", watch.Elapsed.TotalMilliseconds, failed);
            }
        }
    }

    private bool SaveAndOptional(MailBoxData box, BoxSaveInfo saveInfo, string uidl)
    {
        _securityContext.AuthenticateMe(new Guid(box.UserId));

        var message = _messageEngine.Save(box, saveInfo.MimeMessage, uidl, saveInfo.Folder, null, saveInfo.Unread);

        if (message == null || message.Id <= 0) return false;

        _log.InfoMailboxHandlerMessageSaved(message.Id, box.MailBoxId, box.EMail.Address);

        DoOptionalOperations(message, saveInfo, box);

        return true;
    }

    private void DoOptionalOperations(MailMessageData message, BoxSaveInfo boxSaveInfo, MailBoxData box)
    {
        try
        {
            var tagsIds = new List<int>();

            var tagEngine = _scope.ServiceProvider.GetService<TagEngine>();
            var indexEngine = _scope.ServiceProvider.GetService<IndexEngine>();
            var crmLinkEngine = _scope.ServiceProvider.GetService<CrmLinkEngine>();
            var emailInEngine = _scope.ServiceProvider.GetService<EmailInEngine>();
            var autoreplyEngine = _scope.ServiceProvider.GetService<AutoreplyEngine>();
            var calendarEngine = _scope.ServiceProvider.GetService<CalendarEngine>();
            var filterEngine = _scope.ServiceProvider.GetService<FilterEngine>();

            if (boxSaveInfo.Folder.Tags.Length > 0)
            {
                _log.DebugMailboxHandlerGetOrCreateTags();
                tagsIds = tagEngine.GetOrCreateTags(box.TenantId, box.UserId, boxSaveInfo.Folder.Tags);
            }

            if (IsCrmAvailable(box))
            {
                _log.DebugMailboxHandlerGetCrmTags();

                var crmTagsIds = tagEngine.GetCrmTags(message.FromEmail);

                if (crmTagsIds.Count > 0)
                {
                    tagsIds.AddRange(crmTagsIds.Select(t => t.TagId));
                }
            }

            if (tagsIds.Count > 0)
            {
                if (message.TagIds == null || message.TagIds.Count == 0)
                    message.TagIds = tagsIds;
                else message.TagIds.AddRange(tagsIds);

                message.TagIds = message.TagIds.Distinct().ToList();
            }

            _log.DebugMailboxHandlerAddMessageToIndex();

            var mailMessage = message.ToMailMail(box.TenantId, new Guid(box.UserId));

            indexEngine.Add(mailMessage);

            _log.DebugMailboxHandlerSetMessagesTags();

            foreach (var tag in tagsIds)
            {
                try
                {
                    tagEngine.SetMessagesTag(new List<int> { message.Id }, tag);
                }
                catch (Exception ex)
                {
                    var tags = tagsIds != null ? string.Join(", ", tagsIds) : "null";
                    _log.ErrorMailboxHandlerSetMessagesTags(tags, message.Id, box.TenantId, box.UserId, ex.Message);
                }
            }

            _log.DebugMailboxHandlerAddRelationshipEvent();

            crmLinkEngine.AddRelationshipEventForLinkedAccounts(box, message);

            _log.DebugMailboxHandlerSaveEmailInData();

            emailInEngine.SaveEmailInData(box, message, _settings.Defines.DefaultApiSchema);

            _log.DebugMailboxHandlerSendAutoreply();

            autoreplyEngine.SendAutoreply(box, message, _settings.Defines.DefaultApiSchema);

            if (boxSaveInfo.Folder.Folder != Enums.FolderType.Spam)
            {
                _log.DebugMailboxHandlerUploadIcs();

                calendarEngine.UploadIcsToCalendar(box, message.CalendarId, message.CalendarUid, message.CalendarEventIcs,
                    message.CalendarEventCharset, message.CalendarEventMimeType);
            }

            if (_settings.Defines.SaveOriginalMessage)
            {
                _log.DebugMailboxHandlerStoreMailEml();
                StoreMailEml(message.StreamId, boxSaveInfo.MimeMessage, box);
            }

            _log.DebugMailboxHandlerApplyFilters();

            var filters = GetFilters(filterEngine, box.UserId);
            _ = filterEngine.ApplyFilters(message, box, boxSaveInfo.Folder, filters);

            _log.DebugMailboxHandlerNotifySocketIO();

            if (!_settings.Aggregator.EnableSignalr)
                _log.DebugMailboxHandlerSkipNotifySocketIO();

            else AggregatorService.NotifySocketIO(box, _log);
        }
        catch (Exception ex)
        {
            _log.ErrorMailboxHandlerOptionalOperations(ex.Message);
        }
    }

    private List<MailSieveFilterData> GetFilters(FilterEngine fEngine, string userId)
    {
        if (string.IsNullOrEmpty(userId)) return new List<MailSieveFilterData>();

        try
        {
            lock (AggregatorService.filtersLocker)
            {
                if (AggregatorService.Filters.ContainsKey(userId)) return AggregatorService.Filters[userId];
                var filters = fEngine.GetList();
                AggregatorService.Filters.TryAdd(userId, filters);
                return filters;
            }
        }
        catch (Exception ex)
        {
            _log.ErrorMailboxHandlerGetFilters(ex.Message);
        }

        return new List<MailSieveFilterData>();
    }

    private void StoreMailEml(string streamId, MimeMessage message, MailBoxData mailBox)
    {
        if (message == null) return;
        var savePath = MailStoragePathCombiner.GetEmlKey(mailBox.UserId, streamId);

        var storageFactory = _scope.ServiceProvider.GetService<StorageFactory>();
        var storage = storageFactory.GetMailStorage(mailBox.TenantId);

        try
        {
            using var stream = new MemoryStream();
            message.WriteTo(stream);
            var res = storage.SaveAsync(savePath, stream, MailStoragePathCombiner.EML_FILE_NAME).Result.ToString();
            _log.DebugMailboxHandlerStoreMailEmlResult(mailBox.TenantId, mailBox.UserId, res, savePath);
            return;
        }
        catch (Exception ex)
        {
            _log.ErrorMailboxHandlerStoreMailEml(ex.Message);
        }

        return;
    }

    private bool IsCrmAvailable(MailBoxData box)
    {
        var available = false;

        var apiHelper = _scope.ServiceProvider.GetService<ApiHelper>();

        lock (AggregatorService.crmAvailabeLocker)
        {
            if (AggregatorService.UserCrmAvailabeDictionary.TryGetValue(box.UserId, out available)) return available;

            available = box.IsCrmAvailable(_tenantManager, _securityContext, apiHelper, _log);
            AggregatorService.UserCrmAvailabeDictionary.GetOrAdd(box.UserId, available);
        }

        return available;
    }

    private void SetMailboxAuthError()
    {
        try
        {
            if (_box.AuthErrorDate.HasValue) return;

            _box.AuthErrorDate = DateTime.UtcNow;
            _mailboxEngine.SetMaiboxAuthError(_box.MailBoxId, _box.AuthErrorDate.Value);
        }
        catch (Exception ex)
        {
            _log.ErrorMailboxHandlerSetAuthError(_boxInfo, ex.Message);
        }
    }

    private void CloseClient()
    {
        if (_client == null) return;

        try
        {
            _client.Authenticated -= ClientOnAuthenticated;
            _client.GetMessage -= ClientOnGetMessage;

            _client.Cancel();
            _client.Dispose();
        }
        catch (Exception ex)
        {
            _log.ErrorMailboxHandlerTryCloseClient(_boxInfo, ex.Message);
        }
    }
    #endregion

    internal void LogStatistic(MailBoxData mailBoxData, string method, double duration, bool failed)
    {
        _logStat.DebugStatistic(duration, method, failed ? S_FAIL : S_OK, mailBoxData.MailBoxId, mailBoxData.EMail.ToString());
    }
}

internal class BoxSaveInfo
{
    public string Uid { get; set; }
    public MimeMessage MimeMessage { get; set; }
    public MailFolder Folder { get; set; }
    public bool Unread { get; set; }
}
