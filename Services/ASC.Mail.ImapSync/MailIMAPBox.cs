using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASC.Mail.ImapSync
{
    public class MailIMAPBox
    {
        public MailBoxData Account { get; }

        private readonly List<SimpleImapClient> imapWorker;


        public MailIMAPBox(MailBoxData account)
        {
            Account = account;
        }


    }
}
