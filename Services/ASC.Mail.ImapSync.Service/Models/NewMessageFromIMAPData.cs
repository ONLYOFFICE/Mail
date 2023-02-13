namespace ASC.Mail.ImapSync.Models
{
    public class NewMessageFromIMAPData
    {
        public MimeMessage MimeMessage { get; set; }
        public MessageDescriptor MessageDescriptor { get; set; }
        public SimpleImapClient SimpleImapClient { get; set; }
    }
}
