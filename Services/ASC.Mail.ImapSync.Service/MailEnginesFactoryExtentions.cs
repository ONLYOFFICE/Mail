using ASC.Mail.Core.Engine;
using net.openstack.Providers.Rackspace.Objects.Databases;

namespace ASC.Mail.ImapSync
{
    public static class MailEnginesFactoryExtentions
    {
        public static bool ChangeMessageId(this MailEnginesFactory mailEnginesFactory, int id, string newUidl)
        {
            var result = mailEnginesFactory.MailInfoDao.SetFieldValue(
                SimpleMessagesExp.CreateBuilder(mailEnginesFactory.Tenant, mailEnginesFactory.UserId)
                .SetMessageId(id).Build(),
                "Uidl", newUidl);
            return result == 1;
        }

        public static bool SetUnRemoved(this MailEnginesFactory mailEnginesFactory, int id)
        {
            var result = mailEnginesFactory.MailInfoDao.SetFieldValue(
                SimpleMessagesExp.CreateBuilder(mailEnginesFactory.Tenant, mailEnginesFactory.UserId, isRemoved: true)
                .SetMessageId(id).Build(),
                "IsRemoved", false);
            return result == 1;
        }

        public static MailInfo GetMailInfo(this MailEnginesFactory mailEnginesFactory, int id)
        {

            var exp = SimpleMessagesExp.CreateBuilder(mailEnginesFactory.Tenant, mailEnginesFactory.UserId)
                .SetMessageId(id);

            return mailEnginesFactory.MailInfoDao.GetMailInfoList(exp.Build()).FirstOrDefault();
        }
    }
}
