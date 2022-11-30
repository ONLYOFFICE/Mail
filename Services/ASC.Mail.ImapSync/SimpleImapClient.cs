using ASC.Mail.ImapSync.Models;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace ASC.Mail.ImapSync;

public class SimpleImapClient : IDisposable
{
    public readonly MessageSummaryItems messageSummaryItems = MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId | MessageSummaryItems.Flags;
    public bool IsReady { get; private set; } = false;
    public int? UserFolderID { get; set; } = null;
    public int CheckServerAliveMitutes { get; set; } = 0;
    public List<MessageDescriptor> ImapMessagesList { get; set; }
    public IMailFolder ImapWorkFolder { get; private set; }
    public string ImapWorkFolderFullName => ImapWorkFolder.FullName;
    public ASC.Mail.Models.MailFolder MailWorkFolder => foldersDictionary[ImapWorkFolder];
    public FolderType Folder => MailWorkFolder.Folder;
    public IMailFolder GetImapFolderByType(int folderType) => foldersDictionary.FirstOrDefault(x => x.Value.Folder == (FolderType)folderType).Key;
    public string[] ExcludeTags
    {
        get
        {
            List<string> result = new();

            foreach (var item in foldersDictionary)
            {
                if (item.Key != ImapWorkFolder)
                {
                    result.AddRange(item.Value.Tags);
                }
            }

            return result.ToArray();
        }
    }

    public IEnumerable<(string Fullname, bool IsUserFolder)> ImapFoldersFullName => foldersDictionary.Where(x => x.Key != ImapWorkFolder).Select(x => (x.Key.FullName, x.Value.Folder == FolderType.UserFolder));

    public readonly MailBoxData Account;
    public event EventHandler<ImapAction> NewActionFromImap;
    public event EventHandler<NewMessageFromIMAPData> NewMessage;
    public event EventHandler MessagesListUpdated;
    public event EventHandler<bool> OnCriticalError;
    public event EventHandler<(string, bool)> OnNewFolderCreate;
    public event EventHandler<string> OnFolderDelete;

    private readonly ILogger _log;
    private readonly MailSettings _mailSettings;
    private IMailFolder _trashFolder;
    private CancellationTokenSource DoneToken;
    private readonly CancellationTokenSource CancelToken;
    private readonly ImapClient imap;
    private readonly ConcurrentQueue<Task<bool>> asyncTasks;
    private readonly Dictionary<IMailFolder, ASC.Mail.Models.MailFolder> foldersDictionary;

    private void ImapMessageFlagsChanged(object sender, MessageFlagsChangedEventArgs e) => DoneToken?.Cancel();

    private void ImapFolderCountChanged(object sender, EventArgs e) => DoneToken?.Cancel();

    private void ImapWorkFolder_MessageExpunged(object sender, MessageEventArgs e) => DoneToken?.Cancel();

    public SimpleImapClient(MailBoxData mailbox, MailSettings mailSettings, ILoggerProvider logProvider, string folderName, CancellationToken cancelToken)
    {
        Account = mailbox;
        _mailSettings = mailSettings;

        folderName = string.IsNullOrEmpty(folderName) ? "INBOX" : folderName.Replace('/', '_');

        _log = logProvider.CreateLogger($"ASC.Mail.SImap_{Account.MailBoxId}_{folderName}");

        var protocolLogger = mailSettings.ImapSync.WriteIMAPLog && _mailSettings.Aggregator.ProtocolLogPath != "" ?
            new ProtocolLogger(_mailSettings.Aggregator.ProtocolLogPath + $"/imap_{Account.MailBoxId}_{folderName}.log", true) :
            (IProtocolLogger)new NullProtocolLogger();

        CancelToken = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);

        asyncTasks = new ConcurrentQueue<Task<bool>>();

        foldersDictionary = new Dictionary<IMailFolder, ASC.Mail.Models.MailFolder>();

        imap = new ImapClient(protocolLogger)
        {
            Timeout = _mailSettings.Aggregator.TcpTimeout
        };

        imap.ServerCertificateValidationCallback = CertificateValidationCallback;
        imap.CheckCertificateRevocation = true;

        imap.Disconnected += Imap_Disconnected;

        if (Authenticate()) LoadFoldersFromIMAP();
    }

    bool CertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
    {
        _log.DebugSimpleImapClientCertificateCallback(certificate.Subject);

        if (sslPolicyErrors == SslPolicyErrors.None)
        {
            _log.DebugSimpleImapClientNoSslErrors();
            return true;
        }

        return _mailSettings.Defines.SslCertificatesErrorsPermit;
    }

    internal void ExecuteUserAction(CashedMailUserAction cachedMailUserAction)
    {
        if (cachedMailUserAction.Uds.Count == 0 || ImapMessagesList == null) return;

        try
        {
            var messagesOfThisClient = ImapMessagesList.Where(x => cachedMailUserAction.Uds.Contains(x.MessageIdInDB)).ToList();

            if (messagesOfThisClient.Count == 0) return;

            if ((FolderType)cachedMailUserAction.Destination == FolderType.Trash)
            {
                AddTask(new Task<bool>(() => MoveMessageInImap(ImapWorkFolder, messagesOfThisClient, _trashFolder).Result));

                return;
            }

            if (cachedMailUserAction.Action == MailUserAction.MoveTo)
            {
                IMailFolder imapDestinationFolder;

                if ((FolderType)cachedMailUserAction.Destination == FolderType.UserFolder)
                {
                    imapDestinationFolder = foldersDictionary.Keys.FirstOrDefault(x => x.FullName == cachedMailUserAction.Data);
                }
                else
                {

                    imapDestinationFolder = GetImapFolderByType(cachedMailUserAction.Destination);
                }

                if (imapDestinationFolder == null)
                {
                    _log.ErrorSimpleImapClientExecuteUserActionDest(cachedMailUserAction.Destination);

                    return;
                }

                AddTask(new Task<bool>(() => MoveMessageInImap(ImapWorkFolder, messagesOfThisClient, imapDestinationFolder).Result));
            }
            else
            {
                AddTask(new Task<bool>(() => SetFlagsInImap(messagesOfThisClient, cachedMailUserAction.Action).Result));
            }
        }
        catch (Exception ex)
        {
            _log.ErrorSimpleImapClientExecuteUserAction(ex.Message);
        }
    }

    private void Imap_Disconnected(object sender, DisconnectedEventArgs e)
    {
        if (e.IsRequested)
        {
            _log.InfoSimpleImapClientReconnectToIMAP();

            Authenticate();
        }
        else
        {
            CriticalError("Imap disconected without chance to reconect.");
        }
    }

    #region Load Folders from Imap to foldersList

    private bool Authenticate(bool enableUtf8 = true)
    {
        if (imap.IsAuthenticated) return true;

        _log.InfoSimpleImapClientAuth(Account.Name);

        var secureSocketOptions = SecureSocketOptions.Auto;
        var sslProtocols = SslProtocols.None;

        switch (Account.Encryption)
        {
            case EncryptionType.StartTLS:
                secureSocketOptions = SecureSocketOptions.StartTlsWhenAvailable;
                sslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13;
                break;
            case EncryptionType.SSL:
                secureSocketOptions = SecureSocketOptions.SslOnConnect;
                sslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13;
                break;
            case EncryptionType.None:
                secureSocketOptions = SecureSocketOptions.None;
                sslProtocols = SslProtocols.None;
                break;
        }

        _log.DebugSimpleImapConnectTo(Account.Server, Account.Port, secureSocketOptions.ToString());

        imap.SslProtocols = sslProtocols;

        if (!imap.IsConnected)
        {
            imap.Connect(Account.Server, Account.Port, secureSocketOptions, CancelToken.Token);
        }

        try
        {
            if (enableUtf8 && (imap.Capabilities & ImapCapabilities.UTF8Accept) != ImapCapabilities.None)
            {
                _log.DebugSimpleImapEnableUTF8();

                imap.EnableUTF8(CancelToken.Token);
            }

            if (string.IsNullOrEmpty(Account.OAuthToken))
            {
                _log.DebugSimpleImapAuth(Account.Account);

                imap.Authenticate(Account.Account, Account.Password, CancelToken.Token);
            }
            else
            {
                _log.DebugSimpleImapAuthByOAuth(Account.Account);

                var oauth2 = new SaslMechanismOAuth2(Account.Account, Account.AccessToken);

                imap.Authenticate(oauth2, CancelToken.Token);
            }
        }
        catch (Exception ex)
        {
            CriticalError($"Authentication error: {ex}", true);

            return false;
        }

        _log.DebugSimpleImapLoggedIn();

        return true;
    }

    internal void Init(string folderName)
    {
        if (string.IsNullOrEmpty(folderName))
        {
            SetNewImapWorkFolder(imap.Inbox).ContinueWith(TaskManager);
        }
        else
        {
            var folder = foldersDictionary.Keys.FirstOrDefault(x => x.FullName == folderName);

            if (folder != null) SetNewImapWorkFolder(folder).ContinueWith(TaskManager);
        }
    }

    private void CriticalError(string message, bool IsAuthenticationError = false)
    {
        IsReady = false;

        _log.WarnSimpleImap(message);

        DoneToken?.Cancel();

        OnCriticalError?.Invoke(this, IsAuthenticationError);
    }

    private void LoadFoldersFromIMAP()
    {
        _log.DebugSimpleImapLoadFolders();

        try
        {
            GetIMAPFolders().ForEach(x => AddImapFolderToDictionary(x));

            _log.DebugSimpleImapLoadFoldersCount(foldersDictionary.Count);
        }
        catch (AggregateException aggEx)
        {
            if (aggEx.InnerException != null)
            {
                throw aggEx.InnerException;
            }
            throw new Exception("LoadFoldersFromIMAP failed", aggEx);
        }
    }

    private List<IMailFolder> GetIMAPFolders()
    {
        var rootFolder = imap.GetFolder(imap.PersonalNamespaces[0].Path);

        var subfolders = GetImapSubFolders(rootFolder);

        var imapFoldersList = subfolders.Where(ImapFolderFilter).ToList();

        return imapFoldersList;
    }

    private IEnumerable<IMailFolder> GetImapSubFolders(IMailFolder folder)
    {
        try
        {
            var result = folder.GetSubfolders(true, CancelToken.Token).ToList();

            if (result.Count > 0)
            {
                var resultWithSubfolders = result.Where(x => x.Attributes.HasFlag(FolderAttributes.HasChildren)).ToList();

                foreach (var subfolder in resultWithSubfolders)
                {
                    result.AddRange(GetImapSubFolders(subfolder));
                }

                return result;
            }
        }
        catch (Exception ex)
        {
            _log.ErrorSimpleImapGetSubFolders(folder.Name, ex.Message);
        }

        return new List<IMailFolder>();
    }

    #endregion


    private async Task<bool> SetNewImapWorkFolder(IMailFolder imapFolder)
    {
        if (imapFolder == ImapWorkFolder) return false;

        try
        {
            RemoveEventHandler();

            ImapWorkFolder = imapFolder;
            ImapMessagesList = null;

            await ImapWorkFolder.OpenAsync(FolderAccess.ReadWrite);

            IsReady = true;

            SetEventHandler();

            await UpdateMessagesList();
        }
        catch (Exception ex)
        {
            _log.ErrorSimpleImapSetNewWorkFolder(imapFolder.FullName, ex.Message);

            IsReady = false;

            return false;
        }

        _log.DebugSimpleImapSetNewWorkFolder(imapFolder.FullName);

        return true;
    }

    private async Task<bool> UpdateMessagesList()
    {
        List<MessageDescriptor> newMessageDescriptors;

        try
        {
            await ImapWorkFolder.StatusAsync(StatusItems.Count);

            if (ImapWorkFolder.Count > 0)
            {
                newMessageDescriptors = (await ImapWorkFolder.FetchAsync(0, -1, messageSummaryItems))
                    .Where(x => x.Flags.HasValue && !x.Flags.Value.HasFlag(MessageFlags.Deleted))
                    .Select(x => new MessageDescriptor(x)).ToList();
            }
            else
            {
                newMessageDescriptors = new List<MessageDescriptor>();
            }
        }
        catch (Exception ex)
        {
            var fName = ImapWorkFolder == null ? "" : ImapWorkFolder.FullName;
            _log.ErrorSimpleImapUpdateMessagesList(fName, ex.Message);

            newMessageDescriptors = null;

            return false;
        }

        if (ImapMessagesList == null)
        {
            ImapMessagesList = newMessageDescriptors;

            MessagesListUpdated?.Invoke(this, EventArgs.Empty);

            var count = ImapMessagesList == null ? 0 : ImapMessagesList.Count;

            _log.DebugSimpleImapLoadCountMessages(count);

            return true;
        }

        List<MessageDescriptor> deleteList = new();

        foreach (var oldMessage in ImapMessagesList)
        {
            var newMessage = newMessageDescriptors.FirstOrDefault(x => x.UniqueId == oldMessage.UniqueId);

            if (newMessage == null)
            {
                deleteList.Add(oldMessage);

                _log.DebugSimpleImapDeleteMessageDetect(oldMessage.UniqueId.ToString(), oldMessage.MessageIdInDB, oldMessage.Index);
            }
            else
            {
                if (oldMessage.Index != newMessage.Index)
                {
                    _log.DebugSimpleImapChangeIMAPIndex(oldMessage.UniqueId.ToString(), oldMessage.MessageIdInDB, oldMessage.Index, newMessage.Index);

                    oldMessage.Index = newMessage.Index;
                }

                CompareImapFlags(oldMessage, newMessage);

                newMessageDescriptors.Remove(newMessage);
            }
        }

        deleteList.ForEach(InvokeImapDeleteAction);

        newMessageDescriptors.ForEach(messageDescriptors =>
        {
            ImapMessagesList.Add(messageDescriptors);

            TryGetNewMessage(messageDescriptors);
        });

        return true;
    }

    private void UpdateIMAPFolders()
    {
        try
        {
            var newFoldersList = GetIMAPFolders();

            foreach (var newFolder in newFoldersList)
            {
                if (!foldersDictionary.Keys.Any(y => y.FullName == newFolder.FullName))
                {
                    if (AddImapFolderToDictionary(newFolder))
                    {
                        OnNewFolderCreate?.Invoke(this, (newFolder.FullName, foldersDictionary[newFolder].Folder == FolderType.UserFolder));
                    }
                }
            }

            foreach (var oldFolder in foldersDictionary.Keys)
            {
                if (!newFoldersList.Any(y => y.FullName == oldFolder.FullName))
                {
                    foldersDictionary.Remove(oldFolder);

                    OnFolderDelete?.Invoke(this, oldFolder.FullName);
                }
            }
        }
        catch (Exception ex)
        {

        }
    }

    private async Task<bool> SetIdle()
    {
        try
        {
            if (imap.Capabilities.HasFlag(ImapCapabilities.Idle))
            {
                DoneToken = new CancellationTokenSource(new TimeSpan(0, CheckServerAliveMitutes, 10));

                await imap.IdleAsync(DoneToken.Token);
            }
            else
            {
                await Task.Delay(new TimeSpan(0, CheckServerAliveMitutes, 10));
                await imap.NoOpAsync();
            }
        }
        catch (Exception ex)
        {
            CriticalError($"SetIdle error: {ex.Message}.");

            return false;
        }
        finally
        {
            DoneToken?.Dispose();

            DoneToken = null;
        }

        UpdateIMAPFolders();

        await UpdateMessagesList();

        return true;
    }

    private async Task<bool> MoveMessageInImap(IMailFolder sourceFolder, List<MessageDescriptor> messageDescriptors, IMailFolder destinationFolder)
    {
        var sFolder = sourceFolder == null ? "" : sourceFolder.Name;
        var dFolder = destinationFolder == null ? "" : destinationFolder.Name;

        if (sourceFolder == null || destinationFolder == null || messageDescriptors.Count == 0)
        {
            _log.DebugSimpleImapBadParametrs(sFolder, dFolder);

            return false;
        }

        var uniqueIds = messageDescriptors.Select(x => x.UniqueId).ToList();

        _log.DebugSimpleImapMoveMessageInImap(sFolder, dFolder, uniqueIds.Count);

        try
        {
            var returnedUidl = await sourceFolder.MoveToAsync(uniqueIds, destinationFolder);

            messageDescriptors.ForEach(messageDescriptor => ImapMessagesList.Remove(messageDescriptor));
        }
        catch (Exception ex)
        {
            _log.ErrorSimpleImapMoveMessageInImap(ex.Message);

            return false;
        }

        return true;
    }

    private async Task<bool> SetFlagsInImap(List<MessageDescriptor> messageDescriptors, MailUserAction action)
    {
        if (messageDescriptors.Count == 0) return false;

        try
        {
            var uniqueIds = messageDescriptors.Select(x => x.UniqueId).ToList();

            _log.DebugSimpleImapSetFlags(ImapWorkFolder.ToString(), action.ToString(), uniqueIds.Count);

            switch (action)
            {
                case MailUserAction.SetAsRead:

                    await ImapWorkFolder.AddFlagsAsync(uniqueIds, MessageFlags.Seen, true);

                    messageDescriptors.ForEach(x => x.Flags |= MessageFlags.Seen);

                    break;

                case MailUserAction.SetAsUnread:

                    await ImapWorkFolder.RemoveFlagsAsync(uniqueIds, MessageFlags.Seen, true);

                    messageDescriptors.ForEach(x => x.Flags = x.Flags.Value.HasFlag(MessageFlags.Seen) ? x.Flags.Value ^ MessageFlags.Seen : x.Flags.Value);

                    break;

                case MailUserAction.SetAsImportant:

                    await ImapWorkFolder.AddFlagsAsync(uniqueIds, MessageFlags.Flagged, true);

                    messageDescriptors.ForEach(x => x.Flags |= MessageFlags.Flagged);

                    break;

                case MailUserAction.SetAsNotImpotant:

                    await ImapWorkFolder.RemoveFlagsAsync(uniqueIds, MessageFlags.Flagged, true);

                    messageDescriptors.ForEach(x => x.Flags = x.Flags.Value.HasFlag(MessageFlags.Flagged) ? x.Flags.Value ^ MessageFlags.Flagged : x.Flags.Value);

                    break;
            }
        }
        catch (Exception ex)
        {
            _log.ErrorSimpleImapSetFlags(ImapWorkFolder.FullName, action.ToString(), ex.Message);

            return false;
        }

        return true;
    }

    private void CompareImapFlags(MessageDescriptor oldMessageDescriptor, MessageDescriptor newMessageDescriptor)
    {
        if (!(oldMessageDescriptor.HasFlags && oldMessageDescriptor.HasFlags))
        {
            _log.ErrorSimpleImapCompareImapFlagsNoFlags();
        }

        if (oldMessageDescriptor.Flags == newMessageDescriptor.Flags)
        {
            _log.DebugSimpleImapCompareImapFlagsEqual();

            return;
        }

        _log.DebugSimpleImapCompareImapFlagsNewOld(oldMessageDescriptor.Flags.ToString(), newMessageDescriptor.Flags.ToString());

        try
        {
            if (oldMessageDescriptor.IsSeen != newMessageDescriptor.IsSeen)
            {
                InvokeImapAction(oldMessageDescriptor.IsSeen ? MailUserAction.SetAsUnread : MailUserAction.SetAsRead,
                    oldMessageDescriptor);
            }

            if (oldMessageDescriptor.IsImpornant != newMessageDescriptor.IsImpornant)
            {
                InvokeImapAction(oldMessageDescriptor.IsImpornant ? MailUserAction.SetAsNotImpotant : MailUserAction.SetAsImportant,
                    oldMessageDescriptor);
            }
        }
        catch (Exception ex)
        {
            _log.ErrorSimpleImapCompareImapFlags(newMessageDescriptor.UniqueId.ToString(), ex.Message);
        }

        oldMessageDescriptor.Flags = newMessageDescriptor.Flags;
    }

    private async Task TaskManager(Task<bool> previosTask)
    {
        if (previosTask.Exception != null)
        {
            _log.ErrorSimpleImapTaskManager(previosTask.Exception.Message);
        }

        if (CancelToken.IsCancellationRequested) return;

        while (asyncTasks.TryDequeue(out var task))
        {
            task.Start();

            var result = await task.WaitAsync(CancelToken.Token);

            if (!result) _log.WarnSimpleImapAddTask($"Task {task.Id} fault.");
        }

        if (CancelToken.IsCancellationRequested || (ImapWorkFolder == null) || (!imap.IsAuthenticated) || (!IsReady))
        {
            var fName = ImapWorkFolder == null ? "" : ImapWorkFolder.FullName;
            _log.DebugSimpleImapTaskManagerCancellationRequested(fName);

            OnCriticalError?.Invoke(this, false);
        }
        else
        {
            SetIdle().ContinueWith(TaskManager);
        }
    }

    private void AddTask(Task<bool> task)
    {
        asyncTasks.Enqueue(task);

        try
        {
            DoneToken?.Cancel();
        }
        catch (Exception ex)
        {
            _log.WarnSimpleImapAddTask(ex.Message);
        }
    }

    public void Dispose()
    {
        RemoveEventHandler();

        if (imap != null)
        {
            imap.Disconnected -= Imap_Disconnected;
        }
        try
        {
            DoneToken?.Cancel();
            DoneToken?.Dispose();

            CancelToken?.Cancel();
            CancelToken?.Dispose();

            imap?.Dispose();
        }
        catch (Exception ex)
        {
            _log.WarnSimpleImapDispose(ex.Message);
        }

        GC.SuppressFinalize(this);
    }

    private ASC.Mail.Models.MailFolder DetectFolder(IMailFolder folder)
    {
        var folderName = folder.Name.ToLowerInvariant();
        var fullFolderName = folder.FullName.ToLowerInvariant();

        if (_mailSettings.SkipImapFlags != null &&
            _mailSettings.SkipImapFlags.Any() &&
            _mailSettings.SkipImapFlags.Contains(folderName))
        {
            return null;
        }

        FolderType folderId;

        if ((folder.Attributes & FolderAttributes.Inbox) != 0)
        {
            return new ASC.Mail.Models.MailFolder(FolderType.Inbox, folder.Name);
        }
        if ((folder.Attributes & FolderAttributes.Sent) != 0)
        {
            return new ASC.Mail.Models.MailFolder(FolderType.Sent, folder.Name);
        }
        if ((folder.Attributes & FolderAttributes.Junk) != 0)
        {
            return new ASC.Mail.Models.MailFolder(FolderType.Spam, folder.Name);
        }

        if ((folder.Attributes & FolderAttributes.Drafts) != 0)
        {
            return new ASC.Mail.Models.MailFolder(FolderType.Draft, folder.Name);
        }

        if ((folder.Attributes & FolderAttributes.Trash) != 0)
        {
            _trashFolder = folder;

            return null;
        }

        if ((folder.Attributes &
             (FolderAttributes.All |
              FolderAttributes.NoSelect |
              FolderAttributes.NonExistent |
              FolderAttributes.Archive |
              FolderAttributes.Drafts |
              FolderAttributes.Flagged)) != 0)
        {
            return null; // Skip folders
        }

        if (_mailSettings.ImapFlags != null &&
            _mailSettings.ImapFlags.Any() &&
            _mailSettings.ImapFlags.ContainsKey(folderName))
        {
            folderId = (FolderType)_mailSettings.ImapFlags[folderName];
            return new ASC.Mail.Models.MailFolder(folderId, folder.Name);
        }

        if (_mailSettings.SpecialDomainFolders.Any() &&
            _mailSettings.SpecialDomainFolders.ContainsKey(Account.Server))
        {
            var domainSpecialFolders = _mailSettings.SpecialDomainFolders[Account.Server];

            if (domainSpecialFolders.Any() &&
                domainSpecialFolders.ContainsKey(folderName))
            {
                var info = domainSpecialFolders[folderName];
                return info.skip ? null : new ASC.Mail.Models.MailFolder(info.folder_id, folder.Name);
            }
        }

        if (_mailSettings.DefaultFolders == null || !_mailSettings.DefaultFolders.ContainsKey(folderName))
        {
            if (fullFolderName.StartsWith("trash")) return null;

            if (DetectUserFolder(folder))
            {
                return new ASC.Mail.Models.MailFolder(FolderType.UserFolder, folder.Name);
            }

            return new ASC.Mail.Models.MailFolder(FolderType.Inbox, folder.Name, new[] { folder.FullName });
        }

        folderId = (FolderType)_mailSettings.DefaultFolders[folderName];
        return new ASC.Mail.Models.MailFolder(folderId, folder.Name);
    }

    private bool DetectUserFolder(IMailFolder folder)
    {
        if (folder == null) return false;

        if (folder.Attributes > FolderAttributes.All) return false;

        var parentFolder = folder.ParentFolder;

        if (parentFolder.ParentFolder == null &&
            parentFolder.Attributes < FolderAttributes.All) return true;

        return DetectUserFolder(parentFolder);
    }

    public void Stop()
    {
        try
        {
            DoneToken?.Cancel();
            CancelToken?.Cancel();
        }
        catch (Exception ex)
        {
            _log.WarnSimpleImapStop(ex.Message);
        }

        ((IDisposable)this).Dispose();
    }

    public bool IsMessageTracked(int id) => ImapMessagesList.Any(x => x.MessageIdInDB == id);

    private bool ImapFolderFilter(IMailFolder folder)
    {
        if (_mailSettings.SkipImapFlags.Contains(folder.Name.ToLowerInvariant())) return false;

        if (folder.Attributes.HasFlag(FolderAttributes.NoSelect)) return false;

        if (folder.Attributes.HasFlag(FolderAttributes.NonExistent)) return false;

        return true;
    }

    private bool AddImapFolderToDictionary(IMailFolder folder)
    {
        var mailFolder = DetectFolder(folder);

        if (mailFolder == null)
        {
            return false;
        }
        else
        {
            foldersDictionary.Add(folder, mailFolder);

            _log.DebugSimpleImapDetectFolder(folder.Name);

            return true;
        }
    }

    private void InvokeImapAction(MailUserAction mailUserAction, MessageDescriptor messageDescriptor)
    {
        NewActionFromImap?.Invoke(this, new ImapAction()
        {
            FolderAction = mailUserAction,
            MessageFolderName = ImapWorkFolderFullName,
            MessageUniqueId = messageDescriptor.UniqueId,
            MessageFolderType = Folder,
            MailBoxId = Account.MailBoxId,
            MessageIdInDB = messageDescriptor.MessageIdInDB,
            UserFolderId = UserFolderID
        });
    }

    private void InvokeImapDeleteAction(MessageDescriptor messageDescriptor)
    {
        InvokeImapAction(MailUserAction.SetAsDeleted, messageDescriptor);

        ImapMessagesList?.Remove(messageDescriptor);
    }
    private void SetEventHandler()
    {
        if (ImapWorkFolder != null)
        {
            ImapWorkFolder.MessageFlagsChanged += ImapMessageFlagsChanged;
            ImapWorkFolder.CountChanged += ImapFolderCountChanged;
            ImapWorkFolder.MessageExpunged += ImapWorkFolder_MessageExpunged;
        }
    }

    private void RemoveEventHandler()
    {
        if (ImapWorkFolder != null)
        {
            ImapWorkFolder.MessageFlagsChanged -= ImapMessageFlagsChanged;
            ImapWorkFolder.CountChanged -= ImapFolderCountChanged;
            ImapWorkFolder.MessageExpunged -= ImapWorkFolder_MessageExpunged;
        }
    }

    public void TryCreateFolderInIMAP(string name, string parentName) => AddTask(new Task<bool>(() => CreateFolderInIMAP(name, parentName).Result));
    public void TryDeleteFolderInIMAP(string name, string parentName) => AddTask(new Task<bool>(() => DeleteFolderInIMAP(name).Result));
    public void TryCreateMessageInIMAP(MimeMessage message, MessageFlags flags, int messageId) => AddTask(new Task<bool>(() => CreateMessageInIMAP(message, flags, messageId).Result));
    public void TryGetNewMessage(MessageDescriptor messageDescriptors) => AddTask(new Task<bool>(() => GetNewMessage(messageDescriptors).Result));

    private async Task<bool> CreateFolderInIMAP(string name, string parentName)
    {
        var parentFolder = foldersDictionary.Keys.FirstOrDefault(x => x.Name == parentName);

        var newFolder = await parentFolder.CreateAsync(name, true);

        if (newFolder == null) return false;

        AddImapFolderToDictionary(newFolder);

        return true;
    }

    private async Task<bool> DeleteFolderInIMAP(string name)
    {
        var folder = foldersDictionary.Keys.FirstOrDefault(x => x.Name == name);

        await folder.DeleteAsync();

        return true;
    }

    private async Task<bool> CreateMessageInIMAP(MimeMessage message, MessageFlags flags, int messageId)
    {
        if (message == null) return false;

        var oldMessage = ImapMessagesList.FirstOrDefault(x => x.MessageIdInDB == messageId);

        var newMessageUid = await ImapWorkFolder.AppendAsync(message, flags | MessageFlags.Draft);

        if (!newMessageUid.HasValue) return false;

        if (oldMessage != null && newMessageUid != oldMessage.UniqueId)
        {
            await ImapWorkFolder.AddFlagsAsync(oldMessage.UniqueId, MessageFlags.Deleted, true);

            await ImapWorkFolder.ExpungeAsync();

            ImapMessagesList.Remove(oldMessage);
        }

        var messageSamary = (await ImapWorkFolder.FetchAsync(new List<UniqueId>() { newMessageUid.Value }, messageSummaryItems))
            .FirstOrDefault();

        if (messageSamary == null) return false;

        var newMessageDescriptor = new MessageDescriptor(messageSamary)
        {
            MessageIdInDB = messageId
        };

        ImapMessagesList.Add(newMessageDescriptor);

        InvokeImapAction(MailUserAction.MessageUidlUpdate, newMessageDescriptor);

        return true;
    }

    private async Task<bool> GetNewMessage(MessageDescriptor messageDescriptors)
    {
        if (messageDescriptors == null) return false;

        try
        {
            _log.DebugSimpleImapGetNewMessageTaskRun(messageDescriptors.UniqueId.ToString());

            var mimeMessage = await ImapWorkFolder.GetMessageAsync(messageDescriptors.UniqueId, CancelToken.Token);

            NewMessage?.Invoke(this, new NewMessageFromIMAPData()
            {
                MessageDescriptor = messageDescriptors,
                MimeMessage = mimeMessage,
                SimpleImapClient = this
            });

            return true;
        }
        catch (Exception ex)
        {
            _log.ErrorSimpleImapGetNewMessageTryFetchMime(messageDescriptors.UniqueId.ToString(), ex.Message);

            return false;
        }
    }
}