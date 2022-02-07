using System;
using System.Collections.Generic;
using System.Text;

namespace ASC.Mail.ImapSync
{
    [Serializable]
    public class CachedTenantUserMailBox
    {
        public string UserName { get; set; }
        public int Tenant { get; set; }
        public int MailBoxId { get; set; }
        public int Folder { get; set; }
        public IEnumerable<int> Tags { get; set; }
    }
}
