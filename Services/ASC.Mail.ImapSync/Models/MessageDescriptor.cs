namespace ASC.Mail.ImapSync;

public class MessageDescriptor
{
    public MessageFlags? Flags;
    public int Index;
    public UniqueId UniqueId;
    public DateTimeOffset? InternalDate;
    public int MessageIdInDB;
    public int MessageIdOutDB;

    public MessageDescriptor(IMessageSummary message)
    {
        Flags = message.Flags;
        Index = message.Index;
        UniqueId = message.UniqueId;
        InternalDate = message.InternalDate;
        MessageIdInDB = -1;
    }
}
