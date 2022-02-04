using ASC.Common;
using ASC.Mail.Configuration;

namespace ASC.Mail.Core
{
    [Scope]
    public class BaseEngine
    {
        internal static readonly object sync = new object();
        internal MailSettings MailSettings;
        public BaseEngine(MailSettings mailSettings)
        {
            MailSettings = mailSettings;
        }
    }
}
