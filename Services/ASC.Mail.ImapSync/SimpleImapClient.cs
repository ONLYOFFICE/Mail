using ASC.Common.Log;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace ASC.Mail.ImapSync;

public class SimpleImapClient : IDisposable
{
    public bool IsReady { get; private set; } = false;
    public int? UserFolderID { get; set; } = null;
    public int CheckServerAliveMitutes { get; set; } = 0;
    public List<MessageDescriptor> ImapMessagesList { get; set; }
    public IMailFolder ImapWorkFolder { get; private set; }
    //public IMailFolder GetImapFolderByType(int folderType) => foldersDictionary.FirstOrDefault(x => x.Value.Folder == (FolderType)folderType).Key;
    //public string[] ExcludeTags
    //{
    //    get
    //    {
    //        List<string> result = new();

    //        foreach (var item in foldersDictionary)
    //        {
    //            if (item.Key != ImapWorkFolder)
    //            {
    //                result.AddRange(item.Value.Tags);
    //            }
    //        }

    //        return result.ToArray();
    //    }
    //}

    //public IEnumerable<(string Fullname, bool IsUserFolder)> ImapFoldersFullName => foldersDictionary.Where(x => x.Key != ImapWorkFolder).Select(x => (x.Key.FullName, x.Value.Folder == FolderType.UserFolder));

    public event EventHandler<ImapAction> NewActionFromImap;
    public event EventHandler<(MimeMessage, MessageDescriptor)> NewMessage;

    private string domain;
    private readonly ILogger _log;
    private readonly MailSettings _mailSettings;
    private IMailFolder _trashFolder;
    private CancellationTokenSource DoneToken;
    private readonly CancellationTokenSource CancelToken;
    private ImapClient imap;
    private readonly ConcurrentQueue<Task> asyncTasks;
    private List<IMailFolder> IMAPFolders;

    private void ImapMessageFlagsChanged(object sender, MessageFlagsChangedEventArgs e) => DoneToken?.Cancel();

    private void ImapFolderCountChanged(object sender, EventArgs e) => DoneToken?.Cancel();

    private void ImapWorkFolder_MessageExpunged(object sender, MessageEventArgs e) => DoneToken?.Cancel();

    public SimpleImapClient(MailSettings mailSettings, ILogger log, CancellationToken cancelToken)
    {
        _mailSettings = mailSettings;
        _log = log;

        CancelToken = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);

        asyncTasks = new ConcurrentQueue<Task>();

        IMAPFolders = new();
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
                AddTask(new Task(() => MoveMessageInImap(ImapWorkFolder, messagesOfThisClient, _trashFolder)));

                return;
            }

            if (cachedMailUserAction.Action == MailUserAction.MoveTo)
            {
                IMailFolder imapDestinationFolder = IMAPFolders.FirstOrDefault(x => x.FullName == cachedMailUserAction.Data);

                if (imapDestinationFolder == null)
                {
                    _log.ErrorSimpleImapClientExecuteUserActionDest(cachedMailUserAction.Destination);

                    return;
                }

                AddTask(new Task(() => MoveMessageInImap(ImapWorkFolder, messagesOfThisClient, imapDestinationFolder)));
            }
            else
            {
                AddTask(new Task(() => SetFlagsInImap(messagesOfThisClient, cachedMailUserAction.Action)));
            }
        }
        catch (Exception ex)
        {
            _log.ErrorSimpleImapClientExecuteUserAction(ex.Message);
        }
    }

    private void Imap_Disconnected(object sender, DisconnectedEventArgs e)
    {
        CriticalError("Imap disconected without chance to reconect.");
    }

    #region Load Folders from Imap to foldersList

    internal void Init(MailBoxData mailBoxData, string folderName)
    {
        domain = mailBoxData.Server;

        folderName = string.IsNullOrEmpty(folderName) ? "INBOX" : folderName.Replace('/', '_');

        var protocolLogger = _mailSettings.ImapSync.WriteIMAPLog && _mailSettings.Aggregator.ProtocolLogPath != "" ?
            new ProtocolLogger(_mailSettings.Aggregator.ProtocolLogPath + $"/imap_{mailBoxData.MailBoxId}_{folderName}.log", true) :
            (IProtocolLogger)new NullProtocolLogger();

        imap = new ImapClient(protocolLogger)
        {
            Timeout = _mailSettings.Aggregator.TcpTimeout
        };

        imap.ServerCertificateValidationCallback = CertificateValidationCallback;
        imap.CheckCertificateRevocation = true;

        imap.Disconnected += Imap_Disconnected;

        imap.Authenticate(mailBoxData, _log, CancelToken.Token);

        if (string.IsNullOrEmpty(folderName))
        {
            SetNewImapWorkFolder(imap.Inbox).ContinueWith(TaskManager);
        }
        else
        {
            var folder = IMAPFolders.FirstOrDefault(x => x.FullName == folderName);

            if (folder != null) SetNewImapWorkFolder(folder).ContinueWith(TaskManager);
        }
    }

    private void CriticalError(string message, bool IsAuthenticationError = false)
    {
        IsReady = false;

        _log.WarnSimpleImap(message);

        DoneToken?.Cancel();

        if (IsAuthenticationError) InvokeImapAction(new ImapAction(MailUserAction.AuthError));
        else InvokeImapAction(new ImapAction(MailUserAction.CriticalError));
    }

    private void LoadFoldersFromIMAP()
    {
        _log.DebugSimpleImapLoadFolders();

        try
        {
            IMAPFolders=GetIMAPFolders();

            _log.DebugSimpleImapLoadFoldersCount(IMAPFolders.Count);
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
            _log.ErrorSimpleImapSetNewWorkFolder(imapFolder.FullName, ex.Message);

            IsReady = false;
        }

        _log.DebugSimpleImapSetNewWorkFolder(imapFolder.FullName);
    }

    private async Task<List<MessageDescriptor>> GetNewMessageDescriptors()
    {
        List<MessageDescriptor> result = new();
        try
        {
            await ImapWorkFolder.StatusAsync(StatusItems.Count);

            if (ImapWorkFolder.Count > 0)
            {
                result = (await ImapWorkFolder.FetchAsync(0, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.Flags))
                    .Select(x => new MessageDescriptor(x)).ToList();
            }
        }
        catch (Exception ex)
        {
            var fName = ImapWorkFolder == null ? "" : ImapWorkFolder.FullName;
            _log.ErrorSimpleImapUpdateMessagesList(fName, ex.Message);

            return null;
        }

        return result;
    }


    private async Task UpdateMessagesList()
    {
        List<MessageDescriptor> newMessageDescriptors = await GetNewMessageDescriptors();

        if (ImapMessagesList == null)
        {
            ImapMessagesList = newMessageDescriptors;

            InvokeImapAction(new ImapAction(MailUserAction.StartImapClient));

            var count = ImapMessagesList == null ? 0 : ImapMessagesList.Count;

            _log.DebugSimpleImapLoadCountMessages(count);

            return;
        }

        ImapAction imapActionDeleted = new(MailUserAction.SetAsDeleted);
        ImapAction imapActionRead = new(MailUserAction.SetAsRead);
        ImapAction imapActionUnread = new(MailUserAction.SetAsUnread);
        ImapAction imapActionImportant = new(MailUserAction.SetAsImportant);
        ImapAction imapActionNotImpotant = new(MailUserAction.SetAsNotImpotant);

        foreach (var oldMessage in ImapMessagesList)
        {
            var newMessage = newMessageDescriptors.FirstOrDefault(x => x.UniqueId == oldMessage.UniqueId);

            if (newMessage == null)
            {
                imapActionDeleted.MessageIdsInDB.Add(oldMessage.MessageIdInDB);

                _log.DebugSimpleImapDeleteMessageDetect(oldMessage.UniqueId.ToString(), oldMessage.MessageIdInDB, oldMessage.Index);
            }
            else
            {
                if (oldMessage.Index != newMessage.Index)
                {
                    _log.DebugSimpleImapChangeIMAPIndex(oldMessage.UniqueId.ToString(), oldMessage.MessageIdInDB, oldMessage.Index, newMessage.Index);

                    oldMessage.Index = newMessage.Index;
                }

                if (!(oldMessage.Flags.HasValue && oldMessage.Flags.HasValue))
                {
                    _log.Error($"CompareImapFlags: no Flags in message {oldMessage.MessageIdInDB}.");

                    continue;
                }

                if (oldMessage.Flags == newMessage.Flags) continue;

                try
                {
                    bool oldSeen = oldMessage.Flags.Value.HasFlag(MessageFlags.Seen);
                    bool newSeen = newMessage.Flags.Value.HasFlag(MessageFlags.Seen);

                    bool oldImportant = oldMessage.Flags.Value.HasFlag(MessageFlags.Flagged);
                    bool newImportant = newMessage.Flags.Value.HasFlag(MessageFlags.Flagged);

                    if (oldSeen != newSeen)
                    {
                        if (oldSeen) imapActionUnread.MessageIdsInDB.Add(oldMessage.MessageIdInDB);
                        else imapActionRead.MessageIdsInDB.Add(oldMessage.MessageIdInDB);
                    }

                    if (oldImportant != newImportant)
                    {
                        if (oldImportant) imapActionNotImpotant.MessageIdsInDB.Add(oldMessage.MessageIdInDB);
                        else imapActionImportant.MessageIdsInDB.Add(oldMessage.MessageIdInDB);
                    }

                    oldMessage.Flags = newMessage.Flags;

                    newMessageDescriptors.Remove(newMessage);
                }
                catch (Exception ex)
                {
                    _log.ErrorSimpleImapCompareImapFlags(newMessage.UniqueId.ToString(), ex.Message);
                }
            }
        }

        if (imapActionDeleted.MessageIdsInDB.Any()) InvokeImapAction(imapActionDeleted);
        if (imapActionRead.MessageIdsInDB.Any()) InvokeImapAction(imapActionRead);
        if (imapActionUnread.MessageIdsInDB.Any()) InvokeImapAction(imapActionUnread);
        if (imapActionImportant.MessageIdsInDB.Any()) InvokeImapAction(imapActionImportant);
        if (imapActionNotImpotant.MessageIdsInDB.Any()) InvokeImapAction(imapActionNotImpotant);


        newMessageDescriptors.ForEach(messageDescriptors =>
        {
            ImapMessagesList.Add(messageDescriptors);

            TryGetNewMessage(messageDescriptors);
        });
    }

    private void UpdateIMAPFolders()
    {
        try
        {
            var newFoldersList = GetIMAPFolders();

            foreach (var newFolder in newFoldersList)
            {
                if (!IMAPFolders.Any(y => y.FullName == newFolder.FullName))
                {
                    IMAPFolders.Add(newFolder);

                    InvokeImapAction(new ImapAction(MailUserAction.CreateFolder));
                }
            }

            foreach (var oldFolder in IMAPFolders)
            {
                //TODO: Check if this folder deleted
                if (!newFoldersList.Any(y => y.FullName == oldFolder.FullName))
                {
                    IMAPFolders.Remove(oldFolder);

                    InvokeImapAction(new ImapAction(MailUserAction.DeleteFolder));
                }
            }
        }
        catch (Exception ex)
        {

        }
    }

    public void TryGetNewMessage(MessageDescriptor messageDescriptors) => AddTask(new Task(() => GetNewMessage(messageDescriptors)));

    private void GetNewMessage(MessageDescriptor messageDescriptors)
    {
        if (messageDescriptors == null) return;

        try
        {
            _log.DebugSimpleImapGetNewMessageTaskRun(messageDescriptors.UniqueId.ToString());

            var mimeMessage = ImapWorkFolder.GetMessage(messageDescriptors.UniqueId, CancelToken.Token);

            NewMessage?.Invoke(this, (mimeMessage, messageDescriptors));
        }
        catch (Exception ex)
        {
            _log.ErrorSimpleImapGetNewMessageTryFetchMime(messageDescriptors.UniqueId.ToString(), ex.Message);
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

    private void MoveMessageInImap(IMailFolder sourceFolder, List<MessageDescriptor> messageDescriptors, IMailFolder destinationFolder)
    {
        var sFolder = sourceFolder == null ? "" : sourceFolder.Name;
        var dFolder = destinationFolder == null ? "" : destinationFolder.Name;

        if (sourceFolder == null || destinationFolder == null || messageDescriptors.Count == 0)
        {
            _log.DebugSimpleImapBadParametrs(sFolder, dFolder);

            return;
        }

        var uniqueIds = messageDescriptors.Select(x => x.UniqueId).ToList();

        _log.DebugSimpleImapMoveMessageInImap(sFolder, dFolder, uniqueIds.Count);

        try
        {
            var returnedUidl = sourceFolder.MoveTo(uniqueIds, destinationFolder);

            messageDescriptors.ForEach(messageDescriptor => ImapMessagesList.Remove(messageDescriptor));
        }
        catch (Exception ex)
        {
            _log.ErrorSimpleImapMoveMessageInImap(ex.Message);
        }
    }

    private bool SetFlagsInImap(List<MessageDescriptor> messageDescriptors, MailUserAction action)
    {
        if (messageDescriptors.Count == 0) return false;

        try
        {
            var uniqueIds = messageDescriptors.Select(x => x.UniqueId).ToList();

            _log.DebugSimpleImapSetFlags(ImapWorkFolder.ToString(), action.ToString(), uniqueIds.Count);

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
            _log.ErrorSimpleImapSetFlags(ImapWorkFolder.FullName, action.ToString(), ex.Message);

            return false;
        }

        return true;
    }

    private void TaskManager(Task previosTask)
    {
        if (previosTask.Exception != null)
        {
            _log.ErrorSimpleImapTaskManager(previosTask.Exception.Message);
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
            var fName = ImapWorkFolder == null ? "" : ImapWorkFolder.FullName;
            _log.DebugSimpleImapTaskManagerCancellationRequested(fName);

            InvokeImapAction(new ImapAction(MailUserAction.CriticalError));
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

    private void InvokeImapAction(ImapAction imapAction) => NewActionFromImap?.Invoke(this, imapAction);

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

        return true;
    }
}