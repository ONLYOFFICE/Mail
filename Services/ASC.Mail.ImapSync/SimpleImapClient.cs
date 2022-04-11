namespace ASC.Mail.ImapSync;

public class SimpleImapClient : IDisposable
{
    public bool IsReady { get; private set; } = false;
    public int CheckServerAliveMitutes { get; set; } = 1;
    private Task CurentTask { get; set; }

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

    private readonly ILog _log;
    private readonly MailSettings _mailSettings;
    private IMailFolder _trashFolder;
    public readonly MailBoxData Account;

    public event EventHandler<ImapAction> NewActionFromImap;
    public event EventHandler<(MimeMessage, MessageDescriptor)> NewMessage;
    public event EventHandler MessagesListUpdated;
    public event EventHandler<bool> OnCriticalError;
    public event EventHandler<string> OnNewFolderCreate;

    private CancellationTokenSource DoneToken { get; set; }
    private CancellationTokenSource CancelToken { get; set; }
    private readonly ImapClient imap;
    private ConcurrentQueue<Task> asyncTasks;

    private Dictionary<IMailFolder, ASC.Mail.Models.MailFolder> foldersDictionary { get; set; }
    public IEnumerable<string> ImapFoldersFullName => foldersDictionary.Keys.Where(x => x != ImapWorkFolder).Select(x => x.FullName);

    #region Event from Imap handlers

    private void ImapMessageFlagsChanged(object sender, MessageFlagsChangedEventArgs e)
    {
        _log.Debug($"ImapMessageFlagsChanged. Folder= {ImapWorkFolder?.Name} Index={e.Index}. ImapMessagesList.Count={ImapMessagesList?.Count}");

        MessageDescriptor messageSummary = ImapMessagesList?.FirstOrDefault(x => x.Index == e.Index);

        if (messageSummary == null)
        {
            _log.Warn($"ImapMessageFlagsChanged. No Message summary found.");

            return;
        }

        if (messageSummary.Flags.HasValue && IsReady)
        {
            CompareFlags(messageSummary, e.Flags);
        }
        else
        {
            _log.Debug($"ImapMessageFlagsChanged. messageSummary.Flags.HasValue=false.");
        }
    }

    private void ImapFolderCountChanged(object sender, EventArgs e)
    {
        _log.Debug($"ImapFolderCountChanged {ImapWorkFolder?.Name} Count={ImapWorkFolder?.Count}.");

        AddTask(new Task(() => UpdateMessagesList()));
    }

    private void ImapWorkFolder_MessageExpunged(object sender, MessageEventArgs e)
    {
        _log.Debug($"ImapFolderMessageExpunged {ImapWorkFolder?.Name} Index={e?.Index}.");

        MessageDescriptor messageSummary = ImapMessagesList?.FirstOrDefault(x => x.Index == e.Index);

        if (messageSummary == null)
        {
            AddTask(new Task(() => UpdateMessagesList()));

            return;
        }

        ImapAction imapAction = new ImapAction()
        {
            FolderAction = MailUserAction.SetAsDeleted,
            MessageFolderName = ImapWorkFolderFullName,
            MessageUniqueId = messageSummary.UniqueId,
            MessageFolderType = Folder,
            MailBoxId = Account.MailBoxId,
            MessageIdInDB = messageSummary.MessageIdInDB
        };
        NewActionFromImap(this, imapAction);

        ImapMessagesList?.Remove(messageSummary);
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

        imap.FolderCreated += Imap_FolderCreated;

        if (Authenticate()) LoadFoldersFromIMAP();
    }

    internal void ExecuteUserAction(IEnumerable<int> clientMessages, MailUserAction action, int destination)
    {
        if (!clientMessages.Any() || ImapMessagesList == null) return;

        try
        {

            var messagesOfThisClient = ImapMessagesList.Where(x => clientMessages.Contains(x.MessageIdInDB));

            if (!messagesOfThisClient.Any()) return;

            var messagesUids = messagesOfThisClient.Select(x => x.UniqueId).ToList();

            if ((FolderType)destination == FolderType.Trash && !_mailSettings.ImapSync.SynchronizeTresh)
            {
                AddTask(new Task(() => MoveMessageInImap(ImapWorkFolder, messagesUids, _trashFolder)));

                return;
            }

            if (action == MailUserAction.MoveTo)
            {
                var imapDestinationFolder = GetImapFolderByType(destination);

                if (imapDestinationFolder == null) return;

                AddTask(new Task(() => MoveMessageInImap(ImapWorkFolder, messagesUids, imapDestinationFolder)));
            }
            else
            {
                AddTask(new Task(() => SetFlagsInImap(messagesUids, action)));
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

    public void ReSync()
    {
        ImapMessagesList = null;

        UpdateMessagesList();
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

    private void Imap_FolderCreated(object sender, FolderCreatedEventArgs e)
    {
        if (e.Folder != null && ImapFolderFilter(e.Folder))
        {
            AddImapFolderToDictionary(e.Folder);

            OnNewFolderCreate?.Invoke(this, e.Folder.FullName);
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
            GetIMAPFolders().ForEach(x=>AddImapFolderToDictionary(x));

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
            var newFoldersList = GetIMAPFolders().Where(x=>!foldersDictionary.Keys.Any(y=>y.FullName==x.FullName)).ToList();

            foreach(var newFolder in newFoldersList)
            {
                if(AddImapFolderToDictionary(newFolder))
                {
                    OnNewFolderCreate?.Invoke(this, newFolder.FullName);
                }
            }
        }
        catch(Exception ex)
        {
            _log.Error($"UpdateIMAPFolders exception: {ex.Message}");
        }
    }

    private IEnumerable<IMailFolder> GetImapSubFolders(IMailFolder folder)
    {
        var result = new List<IMailFolder>();

        try
        {
            result = folder.GetSubfolders(true, CancelToken.Token).ToList();

            if (result.Any())
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
                newMessageDescriptors = ImapWorkFolder.Fetch(0, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.Flags).ToMessageDescriptorList();
            }
            else
            {
                newMessageDescriptors = null;
            }
        }
        catch (Exception ex)
        {
            _log.Error($"UpdateMessagesList: Try fetch messages from IMAP folder={ImapWorkFolder.FullName}: {ex.Message}.");

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

        deleteList.ForEach(x =>
        {
            ImapAction imapAction = new ImapAction()
            {
                FolderAction = MailUserAction.SetAsDeleted,
                MessageFolderName = ImapWorkFolderFullName,
                MessageUniqueId = x.UniqueId,
                MessageFolderType = Folder,
                MailBoxId = Account.MailBoxId,
                MessageIdInDB = x.MessageIdInDB
            };
            NewActionFromImap(this, imapAction);

            ImapMessagesList.Remove(x);
        });

        foreach (var message in newMessageDescriptors)
        {
            ImapMessagesList.Add(message);

            TryGetNewMessage(message);
        }
    }

    public void TryGetNewMessage(MessageDescriptor message) => AddTask(new Task(() => GetNewMessage(message)));

    private void GetNewMessage(MessageDescriptor message)
    {
        if (message == null) return;

        try
        {
            _log.Debug($"GetNewMessage task run: UniqueId={message.UniqueId}.");

            var mimeMessage = ImapWorkFolder.GetMessage(message.UniqueId, CancelToken.Token);

            if (NewMessage != null) NewMessage(this, (mimeMessage, message));
        }
        catch (Exception ex)
        {
            _log.Error($"GetNewMessage: Try fetch one mimeMessage from imap with UniqueId={message.UniqueId}: {ex.Message}.");
        }
    }

    private async Task<bool> SetIdle()
    {
        UpdateIMAPFolders();

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

    private void MoveMessageInImap(IMailFolder sourceFolder, List<UniqueId> uniqueIds, IMailFolder destinationFolder)
    {
        if (uniqueIds.Count == 0 || sourceFolder == null || destinationFolder == null)
        {
            _log.Debug($"MoveMessageInImap: Bad parametrs. Source={sourceFolder?.Name}, Count={uniqueIds.Count}, Destination={destinationFolder?.Name}.");

            return;
        }

        _log.Debug($"MoveMessageInImap task run: Source={sourceFolder?.Name}, Count={uniqueIds.Count}, Destination={destinationFolder?.Name}.");

        try
        {
            var returnedUidl = sourceFolder.MoveTo(uniqueIds, destinationFolder);

            ImapMessagesList.RemoveAll(x => uniqueIds.Contains(x.UniqueId));
        }
        catch (Exception ex)
        {
            _log.Error($"MoveMessageInImap: {ex.Message}");
        }
    }

    private bool SetFlagsInImap(List<UniqueId> uniqueIds, MailUserAction action)
    {
        if (uniqueIds.Count == 0) return false;

        _log.Debug($"SetFlagsInImap task run: In {ImapWorkFolder} set {action} for {uniqueIds.Count} messages.");

        try
        {
            switch (action)
            {
                case MailUserAction.SetAsRead:
                    ImapWorkFolder.AddFlags(uniqueIds, MessageFlags.Seen, true);
                    ImapMessagesList.ForEach(x =>
                    {
                        if (uniqueIds.Contains(x.UniqueId))
                        {
                            x.Flags = x.Flags.Value | MessageFlags.Seen;
                        }
                    });
                    break;
                case MailUserAction.SetAsUnread:
                    ImapWorkFolder.RemoveFlags(uniqueIds, MessageFlags.Seen, true);
                    ImapMessagesList.ForEach(x =>
                    {
                        if (uniqueIds.Contains(x.UniqueId))
                        {
                            x.Flags = x.Flags.Value ^ MessageFlags.Seen;
                        }
                    });
                    break;
                case MailUserAction.SetAsImportant:
                    ImapWorkFolder.AddFlags(uniqueIds, MessageFlags.Flagged, true);
                    ImapMessagesList.ForEach(x =>
                    {
                        if (uniqueIds.Contains(x.UniqueId))
                        {
                            x.Flags = x.Flags.Value ^ MessageFlags.Flagged;
                        }
                    });
                    break;
                case MailUserAction.SetAsNotImpotant:
                    ImapWorkFolder.RemoveFlags(uniqueIds, MessageFlags.Flagged, true);
                    ImapMessagesList.ForEach(x =>
                    {
                        if (uniqueIds.Contains(x.UniqueId))
                        {
                            x.Flags = x.Flags.Value ^ MessageFlags.Flagged;
                        }
                    });
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

    private void CompareFlags(MessageDescriptor oldMessage, MessageFlags newFlag)
    {
        if (newFlag == oldMessage.Flags)
        {
            _log.Debug($"CompareFlags: Same flags. No need compare.");

            return;
        }

        _log.Debug($"CompareFlags: {ImapWorkFolderFullName} Old flags=({oldMessage.Flags}) New flags {newFlag}.");

        try
        {
            bool oldSeen = oldMessage.Flags.Value.HasFlag(MessageFlags.Seen);
            bool newSeen = newFlag.HasFlag(MessageFlags.Seen);

            bool oldImportant = oldMessage.Flags.Value.HasFlag(MessageFlags.Flagged);
            bool newImportant = newFlag.HasFlag(MessageFlags.Flagged);

            if (oldSeen != newSeen)
            {
                if (NewActionFromImap != null)
                {
                    ImapAction imapAction = new ImapAction()
                    {
                        FolderAction = oldSeen ? MailUserAction.SetAsUnread : MailUserAction.SetAsRead,
                        MessageFolderName = ImapWorkFolderFullName,
                        MessageUniqueId = oldMessage.UniqueId,
                        MessageFolderType = Folder,
                        MailBoxId = Account.MailBoxId,
                        MessageIdInDB = oldMessage.MessageIdInDB
                    };
                    NewActionFromImap(this, imapAction);
                }
            }

            if (oldImportant != newImportant)
            {
                if (NewActionFromImap != null)
                {
                    ImapAction imapAction = new ImapAction()
                    {
                        FolderAction = oldImportant ? MailUserAction.SetAsNotImpotant : MailUserAction.SetAsImportant,
                        MessageFolderName = ImapWorkFolderFullName,
                        MessageUniqueId = oldMessage.UniqueId,
                        MessageFolderType = Folder,
                        MailBoxId = Account.MailBoxId,
                        MessageIdInDB = oldMessage.MessageIdInDB
                    };
                    NewActionFromImap(this, imapAction);
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error($"CompareFlags.Seen Uidl={oldMessage.UniqueId}, ImapFolder={ImapWorkFolderFullName}->{ex.Message}");
        }

        oldMessage.Flags = newFlag;
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
            imap.FolderCreated -= Imap_FolderCreated;
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

            if (_mailSettings.ImapSync.SynchronizeTresh)
            {
                return new ASC.Mail.Models.MailFolder(FolderType.Trash, folder.Name);
            }
            else
            {
                return null;
            }
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

        this.Dispose();
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

}
