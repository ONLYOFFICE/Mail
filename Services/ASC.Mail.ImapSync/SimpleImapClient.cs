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
            List<string> result = new List<string>();

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
    public event EventHandler<string> OnNewFolderCreate;

    private readonly ILog _log;
    private readonly MailSettings _mailSettings;
    private IMailFolder _trashFolder;
    private CancellationTokenSource DoneToken;
    private CancellationTokenSource CancelToken;
    private readonly ImapClient imap;
    private ConcurrentQueue<Task> asyncTasks;
    private Task CurentTask;
    private Dictionary<IMailFolder, ASC.Mail.Models.MailFolder> foldersDictionary;

    #region Event from Imap handlers

    private void ImapMessageFlagsChanged(object sender, MessageFlagsChangedEventArgs e)
    {
        _log.Debug($"ImapMessageFlagsChanged. Index={e?.Index}. ImapMessagesList.Count={ImapMessagesList?.Count}");

        MessageDescriptor messageDescriptor = ImapMessagesList?.FirstOrDefault(x => x.Index == e?.Index);

        if (messageDescriptor == null)
        {
            _log.Warn($"ImapMessageFlagsChanged. Message summary didn't found.");

            return;
        }

        CompareImapFlags(messageDescriptor, e.Flags);
    }

    private void ImapFolderCountChanged(object sender, EventArgs e)
    {
        _log.Debug($"ImapFolderCountChanged {ImapWorkFolder?.Name} Count={ImapWorkFolder?.Count}.");

        AddTask(new Task(() => UpdateMessagesList()));
    }

    private void ImapWorkFolder_MessageExpunged(object sender, MessageEventArgs e)
    {
        _log.Debug($"ImapFolderMessageExpunged {ImapWorkFolder?.Name} Index={e?.Index}.");

        MessageDescriptor messageSummary = ImapMessagesList?.FirstOrDefault(x => x.Index == e?.Index);

        if (messageSummary == null)
        {
            AddTask(new Task(() => UpdateMessagesList()));
        }
        else
        {
            InvokeImapDeleteAction(messageSummary);
        }
    }

    #endregion

    public SimpleImapClient(MailBoxData mailbox, CancellationToken cancelToken, MailSettings mailSettings, ILog log, string folderName = "INBOX")
    {
        Account = mailbox;

        _mailSettings = mailSettings;

        _log = log;
        _log.Name = $"ASC.Mail.SImap_{Account.MailBoxId}";

        folderName = folderName.Replace('/', '_');

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

        imap.Disconnected += Imap_Disconnected;

        if (Authenticate()) LoadFoldersFromIMAP();
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

        var secureSocketOptions = SecureSocketOptions.Auto;
        var sslProtocols = SslProtocols.None;

        switch (Account.Encryption)
        {
            case EncryptionType.StartTLS:
                secureSocketOptions = SecureSocketOptions.StartTlsWhenAvailable;
                sslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12;
                break;
            case EncryptionType.SSL:
                secureSocketOptions = SecureSocketOptions.SslOnConnect;
                sslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12;
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

    internal bool Init(string FullName)
    {
        if (string.IsNullOrEmpty(FullName))
        {
            SetNewImapWorkFolder(imap.Inbox);

            _log.Name = $"ASC.Mail.SImap_{Account.MailBoxId}_INBOX";
        }
        else
        {
            var folder = foldersDictionary.Keys.FirstOrDefault(x => x.FullName == FullName);

            if (folder != null) SetNewImapWorkFolder(folder);

            _log.Name = $"ASC.Mail.SImap_{Account.MailBoxId}_{FullName}";
        }

        TaskManager(Task.CompletedTask);

        return IsReady;
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

    private void UpdateIMAPFolders()
    {
        try
        {
            var newFoldersList = GetIMAPFolders().Where(x => !foldersDictionary.Keys.Any(y => y.FullName == x.FullName)).ToList();

            foreach (var newFolder in newFoldersList)
            {
                if (AddImapFolderToDictionary(newFolder))
                {
                    OnNewFolderCreate?.Invoke(this, newFolder.FullName);
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error($"UpdateIMAPFolders exception: {ex.Message}");
        }
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

    private bool OpenFolder(IMailFolder folder)
    {
        if (folder.IsOpen) return true;

        try
        {
            folder.Open(FolderAccess.ReadWrite);

            _log.Debug($"OpenFolder: Folder {folder.Name} opened.");
        }
        catch (Exception ex)
        {
            _log.Error($"OpenFolder {folder.Name}: {ex.Message}");

            return false;
        }

        return true;
    }

    private void SetNewImapWorkFolder(IMailFolder imapFolder)
    {
        if (imapFolder == ImapWorkFolder) return;

        try
        {
            if (ImapWorkFolder != null)
            {
                ImapWorkFolder.MessageFlagsChanged -= ImapMessageFlagsChanged;
                ImapWorkFolder.CountChanged -= ImapFolderCountChanged;
                ImapWorkFolder.MessageExpunged -= ImapWorkFolder_MessageExpunged;
            }

            ImapWorkFolder = imapFolder;
            ImapMessagesList = null;

            ImapWorkFolder.MessageFlagsChanged += ImapMessageFlagsChanged;
            ImapWorkFolder.CountChanged += ImapFolderCountChanged;
            ImapWorkFolder.MessageExpunged += ImapWorkFolder_MessageExpunged;

            IsReady = OpenFolder(ImapWorkFolder);
        }
        catch (Exception ex)
        {
            _log.Error($"SetNewImapWorkFolder {imapFolder.Name}: {ex.Message}");
        }

        _log.Debug($"SetNewImapWorkFolder: Work folder changed to {imapFolder.Name}.");
    }

    private void UpdateMessagesList()
    {
        List<MessageDescriptor> newMessageDescriptors;

        try
        {
            if (ImapWorkFolder.Count > 0)
            {
                newMessageDescriptors = ImapWorkFolder.Fetch(0, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.Flags)
                    .Select(x => new MessageDescriptor(x)).ToList();
            }
            else
            {
                newMessageDescriptors = null;
            }
        }
        catch (Exception ex)
        {
            _log.Error($"UpdateMessagesList: Try fetch messages from IMAP folder={ImapWorkFolder?.FullName}: {ex.Message}.");

            return;
        }

        if (ImapMessagesList == null)
        {
            ImapMessagesList = newMessageDescriptors;

            MessagesListUpdated?.Invoke(this, EventArgs.Empty);

            _log.Debug($"UpdateMessagesList: Load {ImapMessagesList?.Count} messages from IMAP.");

            return;
        }

        List<MessageDescriptor> deleteList = new List<MessageDescriptor>();

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
                DoneToken = new CancellationTokenSource(new TimeSpan(0, CheckServerAliveMitutes, 0));

                await imap.IdleAsync(DoneToken.Token);
            }
            else
            {
                await Task.Delay(new TimeSpan(0, CheckServerAliveMitutes, 0));
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

    private void CompareImapFlags(MessageDescriptor messageDescriptor, MessageFlags newFlag)
    {
        if (!messageDescriptor.Flags.HasValue)
        {
            _log.Debug($"CompareImapFlags: No flags in MessageDescriptor.");
        }

        if (newFlag == messageDescriptor.Flags)
        {
            _log.Debug($"CompareImapFlags: flag is equal.");

            return;
        }

        _log.Debug($"CompareImapFlags: Old flags=({messageDescriptor.Flags}). New flags {newFlag}.");

        try
        {
            bool oldSeen = messageDescriptor.Flags.Value.HasFlag(MessageFlags.Seen);
            bool newSeen = newFlag.HasFlag(MessageFlags.Seen);

            bool oldImportant = messageDescriptor.Flags.Value.HasFlag(MessageFlags.Flagged);
            bool newImportant = newFlag.HasFlag(MessageFlags.Flagged);

            if (oldSeen != newSeen)
            {
                InvokeImapAction(oldSeen ? MailUserAction.SetAsUnread : MailUserAction.SetAsRead,
                    messageDescriptor);
            }

            if (oldImportant != newImportant)
            {
                InvokeImapAction(oldImportant ? MailUserAction.SetAsNotImpotant : MailUserAction.SetAsImportant,
                    messageDescriptor);

            }
        }
        catch (Exception ex)
        {
            _log.Error($"CompareImapFlags Uidl={messageDescriptor.UniqueId} exception: {ex.Message}");
        }

        messageDescriptor.Flags = newFlag;
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
            CurentTask = task.ContinueWith(TaskManager);

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
            CurentTask = SetIdle().ContinueWith(TaskManager);
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
        if (ImapWorkFolder != null)
        {
            ImapWorkFolder.MessageFlagsChanged -= ImapMessageFlagsChanged;
            ImapWorkFolder.CountChanged -= ImapFolderCountChanged;
            ImapWorkFolder.MessageExpunged -= ImapWorkFolder_MessageExpunged;
        }

        if (imap != null)
        {
            imap.Disconnected -= Imap_Disconnected;
        }
        try
        {
            DoneToken?.Cancel();
            DoneToken?.Dispose();

            imap?.Dispose();
        }
        catch (Exception ex)
        {
            _log.Warn($"Dispose exception: {ex.Message}");
        }
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
            CancelToken.Cancel();
        }
        catch (Exception ex)
        {
            _log.Warn($"Stop exception: {ex.Message}");
        }

        Dispose();
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
}