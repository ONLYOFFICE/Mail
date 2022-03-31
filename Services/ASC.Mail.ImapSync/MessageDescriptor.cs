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
}

public static class MessageDescriptorExtention
{
    public static List<MessageDescriptor> ToMessageDescriptorList(this IList<IMessageSummary> messages)
    {
        List<MessageDescriptor> result = messages.Select(x => new MessageDescriptor(x)).ToList();

        return result;
    }
}
