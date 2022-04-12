namespace ASC.Mail.ImapSync
{
    public class CashedMailUserAction
    {
        public string UserName;
        public int Tenant;
        public List<int> Uds;
        public MailUserAction Action;
        public int Destination;
    }

    public enum MailUserAction
    {
        Nothing,
        SetAsRead,
        SetAsUnread,
        SetAsImportant,
        SetAsNotImpotant,
        SetAsDeleted,
        StartImapClient,
        MoveTo,
        New,
        RemovedFromFolder
    }
}
