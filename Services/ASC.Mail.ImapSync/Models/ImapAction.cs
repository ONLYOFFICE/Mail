namespace ASC.Mail.ImapSync
{
    public class ImapAction
    {
        public int MailBoxId { set; get; }
        public UniqueId MessageUniqueId { set; get; }
        public string MessageFolderName { set; get; }
        public FolderType MessageFolderType { set; get; }
        public MailUserAction FolderAction { set; get; }
        public int MessageIdInDB { set; get; }
        public int? UserFolderId     { set; get; }

        public bool IsSameImapFolderAndAction(ImapAction imapAction)
        {
            if (imapAction == null) return false;
            if (MailBoxId != imapAction.MailBoxId) return false;
            if (MessageFolderType != imapAction.MessageFolderType) return false;
            if (FolderAction != imapAction.FolderAction) return false;
            if (!MessageFolderName.Equals(imapAction.MessageFolderName)) return false;
            return true;
        }
    }
}
