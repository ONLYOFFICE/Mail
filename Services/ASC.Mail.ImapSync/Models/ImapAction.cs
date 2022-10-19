namespace ASC.Mail.ImapSync
{
    public class ImapAction
    {
        public MailUserAction Action { set; get; }
        public List<int> MessageIdsInDB { set; get; }

        public ImapAction(MailUserAction action)
        {
            Action = action;
            MessageIdsInDB = new();
        }
    }
}
