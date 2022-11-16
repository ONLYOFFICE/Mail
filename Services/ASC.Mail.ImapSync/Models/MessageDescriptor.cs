namespace ASC.Mail.ImapSync;

public class MessageDescriptor
{
    public MessageFlags? Flags;
    public int Index;
    public UniqueId UniqueId;
    public DateTimeOffset? InternalDate;
    public int MessageIdInDB;

    public MessageDescriptor(IMessageSummary message)
    {
        Flags = message.Flags;
        Index = message.Index;
        UniqueId = message.UniqueId;
        InternalDate = message.InternalDate;
        MessageIdInDB = -1;
    }

    public bool HasFlags=>Flags.HasValue;
    public bool IsSeen=>Flags.Value.HasFlag(MessageFlags.Seen);
    public bool IsImpornant=>Flags.Value.HasFlag(MessageFlags.Flagged);
}
