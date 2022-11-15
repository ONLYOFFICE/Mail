namespace ASC.Mail.ImapSync;

public class MessageDescriptor
{
    public MessageFlags? Flags { get; set; }
    public int Index { get; set; }
    public UniqueId UniqueId { get; set; }
    public DateTimeOffset? InternalDate { get; set; }
    public int MessageIdInDB { get; set; }

    public MessageDescriptor(IMessageSummary message)
    {
        Flags = message.Flags;
        Index = message.Index;
        UniqueId = message.UniqueId;
        InternalDate = message.InternalDate;
        MessageIdInDB = -1;
    }
}
