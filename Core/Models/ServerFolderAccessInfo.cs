using FolderType = ASC.Mail.Enums.FolderType;

namespace ASC.Mail.Models;

public class ServerFolderAccessInfo
{
    public string Server { get; set; }

    public Dictionary<string, FolderInfo> FolderAccessList { get; set; }

    public class FolderInfo
    {
        public FolderType folder_id;
        public bool skip;
    }
}
