namespace ASC.Mail.ImapSync;

public static class UidlExtentions
{
    public static string ToUidl(this UniqueId uniqueId, FolderType folder)
    {
        return $"{uniqueId.Id}-{(int)folder}";
    }

    public static UniqueId ToUniqueId(this string uidl)
    {
        if (!uint.TryParse(uidl.Split('-')[0].Trim(), out uint uidlInt))
        {
            return UniqueId.Invalid;
        }

        return new UniqueId(uidlInt);
    }
}
