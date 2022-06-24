using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace ASC.Mail.ImapSync;

public class SimpleImapClient : IDisposable
{
    public bool IsReady { get; private set; } = false;
    public int CheckServerAliveMitutes { get; set; } = 1;
    public List<MessageDescriptor> ImapMessagesList { get; set; }
    public IMailFolder ImapWorkFolder { get; private set; }
    public string ImapWorkFolderFullName => ImapWorkFolder.FullName;
    public ASC.Mail.Models.MailFolder MailWorkFolder => foldersDictionary[ImapWorkFolder];
    public FolderType Folder => MailWorkFolder.Folder;
    public int FolderTypeInt => (int)MailWorkFolder.Folder;
    public IMailFolder GetImapFolderByType(int folderType) => GetImapFolderByType((FolderType)folderType);
    public IMailFolder GetImapFolderByType(FolderType folderType) => foldersDictionary.FirstOrDefault(x => x.Value.Folder == folderType).Key;
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

    public IEnumerable<string> ImapFoldersFullName => foldersDictionary.Keys.Where(x => x != ImapWorkFolder).Select(x => x.FullName);

    public readonly MailBoxData Account;
    public event EventHandler<ImapAction> NewActionFromImap;
    public event EventHandler<(MimeMessage, MessageDescriptor)> NewMessage;
    public event EventHandler MessagesListUpdated;
    public event EventHandler<bool> OnCriticalError;

    private readonly ILog _log;
    private readonly MailSettings _mailSettings;
    private IMailFolder _trashFolder;
    private CancellationTokenSource DoneToken;
    private readonly CancellationTokenSource CancelToken;
    private readonly ImapClient imap;
    private readonly ConcurrentQueue<Task> asyncTasks;
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

        var protocolLogger = mailSettings.ImapSync.WriteIMAPLog ?
            new ProtocolLogger(_log.LogDirectory + $"/imap_{Account.MailBoxId}_{folderName}.log", true) :
            (IProtocolLogger)new NullProtocolLogger();

        CancelToken = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);

        asyncTasks = new ConcurrentQueue<Task>();

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
        _log.Debug($"CertificateValidationCallback(). Certificate callback: {certificate.Subject}.");

        if (sslPolicyErrors == SslPolicyErrors.None)
        {
            _log.Debug("CertificateValidationCallback(). No Ssl policy errors...");
            return true;
        }

        return _mailSettings.Defines.SslCertificatesErrorsPermit;
    }

    internal void ExecuteUserAction(List<int> clientMessages, MailUserAction action, int destination)
    {
        if (clientMessages.Count == 0 || ImapMessagesList == null) return;

        try
        {
            var messagesOfThisClient = ImapMessagesList.Where(x => clientMessages.Contains(x.MessageIdInDB)).ToList();

            if (messagesOfThisClient.Count == 0) return;

            if ((FolderType)destination == FolderType.Trash)
            {
                AddTask(new Task(() => MoveMessageInImap(ImapWorkFolder, messagesOfThisClient, _trashFolder)));

                return;
            }

            if (action == MailUserAction.MoveTo)
            {
                var imapDestinationFolder = GetImapFolderByType(destination);

                if (imapDestinationFolder == null)
                {
                    _log.Error($"ExecuteUserAction: Destination ({destination}) didn't found.");

                    return;
                }

                AddTask(new Task(() => MoveMessageInImap(ImapWorkFolder, messagesOfThisClient, imapDestinationFolder)));
            }
            else
            {
                AddTask(new Task(() => SetFlagsInImap(messagesOfThisClient, action)));
            }
        }
        catch (Exception ex)
        {
            _log.Error($"ExecuteUserAction exception: {ex.Message}");
        }
    }

    private void Imap_Disconnected(object sender, DisconnectedEventArgs e)
    {
        if (e.IsRequested)
        {
            _log.Info("Try reconnect to IMAP...");

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

        _log.Info($"Authenticate: {Account.Name}");

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

        _log.Debug($"Connect to {Account.Server}:{Account.Port}, {secureSocketOptions})");

        imap.SslProtocols = sslProtocols;

        if (!imap.IsConnected)
        {
            imap.Connect(Account.Server, Account.Port, secureSocketOptions, CancelToken.Token);
        }

        try
        {
            if (enableUtf8 && (imap.Capabilities & ImapCapabilities.UTF8Accept) != ImapCapabilities.None)
            {
                _log.Debug("Imap.EnableUTF8");

                imap.EnableUTF8(CancelToken.Token);
            }

            if (string.IsNullOrEmpty(Account.OAuthToken))
            {
                _log.DebugFormat("Imap.Authentication({0})", Account.Account);

                imap.Authenticate(Account.Account, Account.Password, CancelToken.Token);
            }
            else
            {
                _log.DebugFormat("Imap.AuthenticationByOAuth({0})", Account.Account);

                var oauth2 = new SaslMechanismOAuth2(Account.Account, Account.AccessToken);

                imap.Authenticate(oauth2, CancelToken.Token);
            }
        }
        catch (Exception ex)
        {
            CriticalError($"Authentication error: {ex}", true);

            return false;
        }

        _log.Debug("IMAP: logged in.");

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

        _log.Warn(message);

        DoneToken?.Cancel();

        OnCriticalError?.Invoke(this, IsAuthenticationError);
    }

    private void LoadFoldersFromIMAP()
    {
        _log.Debug("Load folders from IMAP.");

        try
        {
            GetIMAPFolders().ForEach(x => AddImapFolderToDictionary(x));

            _log.Debug($"Find {foldersDictionary.Count} folders in IMAP.");
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
            _log.Error($"GetImapSubFolders: {folder.Name} Exception: {ex.Message}");
        }

        return new List<IMailFolder>();
    }

    #endregion


    private async Task SetNewImapWorkFolder(IMailFolder imapFolder)
    {
        if (imapFolder == ImapWorkFolder) return;

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
            _log.Error($"SetNewImapWorkFolder {imapFolder.FullName}: {ex.Message}");

            IsReady = false;
        }

        _log.Debug($"SetNewImapWorkFolder: Work folder changed to {imapFolder.FullName}.");
    }

    private async Task UpdateMessagesList()
    {
        List<MessageDescriptor> newMessageDescriptors;

        try
        {
            await ImapWorkFolder.StatusAsync(StatusItems.Count);

            if (ImapWorkFolder.Count > 0)
            {
                newMessageDescriptors = (await ImapWorkFolder.FetchAsync(0, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.Flags))
                    .Select(x => new MessageDescriptor(x)).ToList();
            }
            else
            {
                newMessageDescriptors = new List<MessageDescriptor>();
            }
        }
        catch (Exception ex)
        {
            _log.Error($"UpdateMessagesList: Try fetch messages from IMAP folder={ImapWorkFolder?.FullName}: {ex.Message}.");

            newMessageDescriptors = null;

            return;
        }

        if (ImapMessagesList == null)
        {
            ImapMessagesList = newMessageDescriptors;

            MessagesListUpdated?.Invoke(this, EventArgs.Empty);

            _log.Debug($"UpdateMessagesList: Load {ImapMessagesList?.Count} messages from IMAP.");

            return;
        }

        List<MessageDescriptor> deleteList = new();

        foreach (var oldMessage in ImapMessagesList)
        {
            var newMessage = newMessageDescriptors.FirstOrDefault(x => x.UniqueId == oldMessage.UniqueId);

            if (newMessage == null)
            {
                deleteList.Add(oldMessage);

                _log.Debug($"UpdateMessagesList: Delete message detect. Uid= {oldMessage.UniqueId} DBid={oldMessage.MessageIdInDB} IMAPIndex={oldMessage.Index}.");
            }
            else
            {
                if (oldMessage.Index != newMessage.Index)
                {
                    _log.Debug($"UpdateMessagesList: Change IMAP index. Uid= {oldMessage.UniqueId} DBid={oldMessage.MessageIdInDB} IMAPIndex={oldMessage.Index}->{newMessage.Index}.");

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
    }

    public void TryGetNewMessage(MessageDescriptor messageDescriptors) => AddTask(new Task(() => GetNewMessage(messageDescriptors)));

    private void GetNewMessage(MessageDescriptor messageDescriptors)
    {
        if (messageDescriptors == null) return;

        try
        {
            _log.Debug($"GetNewMessage task run: UniqueId={messageDescriptors.UniqueId}.");

            var mimeMessage = ImapWorkFolder.GetMessage(messageDescriptors.UniqueId, CancelToken.Token);

            NewMessage?.Invoke(this, (mimeMessage, messageDescriptors));
        }
        catch (Exception ex)
        {
            _log.Error($"GetNewMessage: Try fetch one mimeMessage from imap with UniqueId={messageDescriptors.UniqueId}: {ex.Message}.");
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

        await UpdateMessagesList();

        return true;
    }

    private void MoveMessageInImap(IMailFolder sourceFolder, List<MessageDescriptor> messageDescriptors, IMailFolder destinationFolder)
    {
        if (sourceFolder == null || destinationFolder == null || messageDescriptors.Count == 0)
        {
            _log.Debug($"MoveMessageInImap: Bad parametrs. Source={sourceFolder?.Name}, Destination={destinationFolder?.Name}.");

            return;
        }

        var uniqueIds = messageDescriptors.Select(x => x.UniqueId).ToList();

        _log.Debug($"MoveMessageInImap: Source={sourceFolder?.Name}, Destination={destinationFolder?.Name}, Count={uniqueIds.Count}.");

        try
        {
            var returnedUidl = sourceFolder.MoveTo(uniqueIds, destinationFolder);

            messageDescriptors.ForEach(messageDescriptor => ImapMessagesList.Remove(messageDescriptor));
        }
        catch (Exception ex)
        {
            _log.Error($"MoveMessageInImap: {ex.Message}");
        }
    }

    private bool SetFlagsInImap(List<MessageDescriptor> messageDescriptors, MailUserAction action)
    {
        if (messageDescriptors.Count == 0) return false;

        try
        {
            var uniqueIds = messageDescriptors.Select(x => x.UniqueId).ToList();

            _log.Debug($"SetFlagsInImap task run: In {ImapWorkFolder} set {action} for {uniqueIds.Count} messages.");

            switch (action)
            {
                case MailUserAction.SetAsRead:

                    ImapWorkFolder.AddFlags(uniqueIds, MessageFlags.Seen, true);

                    messageDescriptors.ForEach(x => x.Flags |= MessageFlags.Seen);

                    break;

                case MailUserAction.SetAsUnread:

                    ImapWorkFolder.RemoveFlags(uniqueIds, MessageFlags.Seen, true);

                    messageDescriptors.ForEach(x => x.Flags = x.Flags.Value.HasFlag(MessageFlags.Seen) ? x.Flags.Value ^ MessageFlags.Seen : x.Flags.Value);

                    break;

                case MailUserAction.SetAsImportant:

                    ImapWorkFolder.AddFlags(uniqueIds, MessageFlags.Flagged, true);

                    messageDescriptors.ForEach(x => x.Flags |= MessageFlags.Flagged);

                    break;

                case MailUserAction.SetAsNotImpotant:

                    ImapWorkFolder.RemoveFlags(uniqueIds, MessageFlags.Flagged, true);

                    messageDescriptors.ForEach(x => x.Flags = x.Flags.Value.HasFlag(MessageFlags.Flagged) ? x.Flags.Value ^ MessageFlags.Flagged : x.Flags.Value);

                    break;
            }
        }
        catch (Exception ex)
        {
            _log.Error($"SetMessageFlagIMAP->{ImapWorkFolder.FullName}, {action}->{ex.Message}");

            return false;
        }

        return true;
    }

    private void CompareImapFlags(MessageDescriptor oldMessageDescriptor, MessageDescriptor newMessageDescriptor)
    {
        if (!(oldMessageDescriptor.Flags.HasValue && oldMessageDescriptor.Flags.HasValue))
        {
            _log.Error($"CompareImapFlags: No flags in MessageDescriptor.");
        }

        if (oldMessageDescriptor.Flags == newMessageDescriptor.Flags)
        {
            _log.Debug($"CompareImapFlags: flag is equal.");

            return;
        }

        _log.Debug($"CompareImapFlags: Old flags=({oldMessageDescriptor.Flags}). New flags {newMessageDescriptor.Flags}.");

        try
        {
            bool oldSeen = oldMessageDescriptor.Flags.Value.HasFlag(MessageFlags.Seen);
            bool newSeen = newMessageDescriptor.Flags.Value.HasFlag(MessageFlags.Seen);

            bool oldImportant = oldMessageDescriptor.Flags.Value.HasFlag(MessageFlags.Flagged);
            bool newImportant = newMessageDescriptor.Flags.Value.HasFlag(MessageFlags.Flagged);

            if (oldSeen != newSeen)
            {
                InvokeImapAction(oldSeen ? MailUserAction.SetAsUnread : MailUserAction.SetAsRead,
                    oldMessageDescriptor);
            }

            if (oldImportant != newImportant)
            {
                InvokeImapAction(oldImportant ? MailUserAction.SetAsNotImpotant : MailUserAction.SetAsImportant,
                    oldMessageDescriptor);
            }
        }
        catch (Exception ex)
        {
            _log.Error($"CompareImapFlags Uidl={newMessageDescriptor.UniqueId} exception: {ex.Message}");
        }

        oldMessageDescriptor.Flags = newMessageDescriptor.Flags;
    }

    private void TaskManager(Task previosTask)
    {
        if (previosTask.Exception != null)
        {
            _log.Error($"Task manager: {previosTask.Exception.Message}");
        }

        if (CancelToken.IsCancellationRequested) return;

        if (asyncTasks.TryDequeue(out var task))
        {
            task.ContinueWith(TaskManager);

            task.Start();

            return;
        }

        if (CancelToken.IsCancellationRequested || (ImapWorkFolder == null) || (!imap.IsAuthenticated) || (!IsReady))
        {
            _log.Debug($"TaskManager Cancellation Requested, folder {ImapWorkFolder?.FullName}.");

            OnCriticalError?.Invoke(this, false);
        }
        else
        {
            SetIdle().ContinueWith(TaskManager);
        }
    }

    private void AddTask(Task task)
    {
        asyncTasks.Enqueue(task);

        try
        {
            DoneToken?.Cancel();
        }
        catch (Exception ex)
        {
            _log.Warn($"AddTask exception: {ex.Message}");
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
            _log.Warn($"Dispose exception: {ex.Message}");
        }

        GC.SuppressFinalize(this);
    }

    private ASC.Mail.Models.MailFolder DetectFolder(IMailFolder folder)
    {
        var folderName = folder.Name.ToLowerInvariant();

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
            return new ASC.Mail.Models.MailFolder(FolderType.Inbox, folder.Name, new[] { folder.FullName });

        folderId = (FolderType)_mailSettings.DefaultFolders[folderName];
        return new ASC.Mail.Models.MailFolder(folderId, folder.Name);
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
            _log.Warn($"Stop exception: {ex.Message}");
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

            _log.Debug($"LoadFoldersFromIMAP-> Detect folder {folder.Name}.");

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
            MessageIdInDB = messageDescriptor.MessageIdInDB
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

    public void TryCreateFolderInIMAP(string name) => AddTask(new Task(() => CreateFolderInIMAP(name)));

    private bool CreateFolderInIMAP(string name)
    {
        var rootFolder = imap.GetFolder(imap.PersonalNamespaces[0].Path);

        var newFolder = rootFolder.Create(name, true);

        if (newFolder == null) return false;

        AddImapFolderToDictionary(newFolder);

        return true;
    }
}