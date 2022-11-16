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

    public bool HasFlags=>Flags.HasValue;
    public bool IsSeen=>Flags.Value.HasFlag(MessageFlags.Seen);
    public bool IsImpornant=>Flags.Value.HasFlag(MessageFlags.Flagged);
}
