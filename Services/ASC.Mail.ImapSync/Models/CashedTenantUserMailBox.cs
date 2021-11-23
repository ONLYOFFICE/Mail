using System;
using System.Collections.Generic;
using System.Text;

namespace ASC.Mail.ImapSync
{
    public class CashedTenantUserMailBox
    {
        public string UserName;
        public int Tenant;
        public int MailBoxId;
        public int Folder;
        public IEnumerable<int> tags;
    }
}
