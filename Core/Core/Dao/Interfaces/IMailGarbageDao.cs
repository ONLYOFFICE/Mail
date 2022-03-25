using ASC.Common;
using ASC.Mail.Models;

using System.Collections.Generic;

namespace ASC.Mail.Core.Dao.Interfaces
{
    [Scope(typeof(MailGarbageDao))]
    public interface IMailGarbageDao
    {
        int GetMailboxAttachsCount(MailBoxData mailBoxData);

        List<MailAttachGarbage> GetMailboxAttachs(MailBoxData mailBoxData, int limit);

        void CleanupMailboxAttachs(List<MailAttachGarbage> attachGarbageList);

        int GetMailboxMessagesCount(MailBoxData mailBoxData);

        List<MailMessageGarbage> GetMailboxMessages(MailBoxData mailBoxData, int limit);

        void CleanupMailboxMessages(List<MailMessageGarbage> messageGarbageList);
    }
}
