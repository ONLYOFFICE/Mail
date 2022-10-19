using ASC.Mail.Configuration;
using ASC.Mail.Core.Engine;
using Google.Api.Gax.ResourceNames;
using System.Security.Cryptography;

namespace ASC.Mail.ImapSync
{
    public class MailIMAPBox
    {
        public MailBoxData Account { get; }
        public int ClientCount
        {
            get
            {
                if (imapWorker == null) return 0;
                return imapWorker.Count;
            }
        }

        private readonly List<SimpleImapClient> imapWorker;


        public MailIMAPBox(MailBoxData account)
        {
            Account = account;
        }

        public void StartSync()
        {
            if (simpleImapClients.Any(x => x.Account.MailBoxId == mailbox.MailBoxId))
            {
                DeleteSimpleImapClients(mailbox);
            }
            try
            {
                logProvider.CreateLogger($"ASC.Mail.SImap_{Account.MailBoxId}_{folderName}");

                var rootSimpleImapClient = new SimpleImapClient(mailbox, _mailSettings, _logProvider, "", _cancelToken.Token);

                rootSimpleImapClient.imap.Authenticate
    
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

                _log.DebugMailImapClientCreateSimpleImapClients(mailbox.MailBoxId, isLocked);
            }
            catch (Exception ex)
            {
                _log.ErrorMailImapClient($"Create IMAP clients for {mailbox.EMail.Address}", ex.Message);
            }
            finally
            {
                if (_enginesFactorySemaphore.CurrentCount == 0) _enginesFactorySemaphore.Release();
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
                    var userFolder = _userFolderEngine.GetByNameOrCreate(folder.folderName);

                    simpleImapClient.UserFolderID = userFolder.Id;
                }

                simpleImapClient.Init(folder.folderName);
            }
            catch (Exception ex)
            {
                _log.ErrorMailImapClient($"Create IMAP client for {mailbox.EMail.Address}, folder {folder.folderName}", ex.Message);
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

                _log.DebugMailImapClientDeleteSimpleImapClients(deletedSimpleImapClients.Count, mailbox.MailBoxId, isLocked);
            }
            catch (Exception ex)
            {
                _log.ErrorMailImapClient($"Delete IMAP clients for {mailbox.EMail.Address}", ex.Message);
            }
            finally
            {
                if (_enginesFactorySemaphore.CurrentCount == 0) _enginesFactorySemaphore.Release();
            }
        }

        public void StopSync()
        {

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
                _mailSettings.SpecialDomainFolders.ContainsKey(domain))
            {
                var domainSpecialFolders = _mailSettings.SpecialDomainFolders[domain];

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
    }
}
