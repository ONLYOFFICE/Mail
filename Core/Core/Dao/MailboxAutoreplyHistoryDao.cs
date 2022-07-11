/*
 *
 * (c) Copyright Ascensio System Limited 2010-2018
 *
 * This program is freeware. You can redistribute it and/or modify it under the terms of the GNU 
 * General Public License (GPL) version 3 as published by the Free Software Foundation (https://www.gnu.org/copyleft/gpl.html). 
 * In accordance with Section 7(a) of the GNU GPL its Section 15 shall be amended to the effect that 
 * Ascensio System SIA expressly excludes the warranty of non-infringement of any third-party rights.
 *
 * THIS PROGRAM IS DISTRIBUTED WITHOUT ANY WARRANTY; WITHOUT EVEN THE IMPLIED WARRANTY OF MERCHANTABILITY OR
 * FITNESS FOR A PARTICULAR PURPOSE. For more details, see GNU GPL at https://www.gnu.org/copyleft/gpl.html
 *
 * You can contact Ascensio System SIA by email at sales@onlyoffice.com
 *
 * The interactive user interfaces in modified source and object code versions of ONLYOFFICE must display 
 * Appropriate Legal Notices, as required under Section 5 of the GNU GPL version 3.
 *
 * Pursuant to Section 7 § 3(b) of the GNU GPL you must retain the original ONLYOFFICE logo which contains 
 * relevant author attributions when distributing the software. If the display of the logo in its graphic 
 * form is not reasonably feasible for technical reasons, you must include the words "Powered by ONLYOFFICE" 
 * in every copy of the program you distribute. 
 * Pursuant to Section 7 § 3(e) we decline to grant you any rights under trademark law for use of our trademarks.
 *
*/

using SecurityContext = ASC.Core.SecurityContext;

namespace ASC.Mail.Core.Dao;

[Scope]
public class MailboxAutoreplyHistoryDao : BaseMailDao, IMailboxAutoreplyHistoryDao
{
    public MailboxAutoreplyHistoryDao(
         TenantManager tenantManager,
         SecurityContext securityContext,
         DbContextManager<MailDbContext> dbContext)
        : base(tenantManager, securityContext, dbContext)
    {
    }

    public List<string> GetAutoreplyHistorySentEmails(int mailboxId, string email, int autoreplyDaysInterval)
    {
        var emails = MailDbContext.MailMailboxAutoreplyHistories
            .AsNoTracking()
            .Where(h => h.IdMailbox == mailboxId
                && h.SendingEmail == email
                && EF.Functions.DateDiffDay(h.SendingDate, DateTime.UtcNow) <= autoreplyDaysInterval)
            .Select(h => h.SendingEmail)
            .ToList();

        return emails;
    }

    public int SaveAutoreplyHistory(MailboxAutoreplyHistory autoreplyHistory)
    {
        var model = new MailMailboxAutoreplyHistory
        {
            IdMailbox = autoreplyHistory.MailboxId,
            Tenant = autoreplyHistory.Tenant,
            SendingEmail = autoreplyHistory.SendingEmail,
            SendingDate = autoreplyHistory.SendingDate
        };

        MailDbContext.MailMailboxAutoreplyHistories.Add(model);

        var count = MailDbContext.SaveChanges();

        return count;
    }

    public int DeleteAutoreplyHistory(int mailboxId)
    {
        var history = MailDbContext.MailMailboxAutoreplyHistories
            .Where(h => h.IdMailbox == mailboxId && h.Tenant == Tenant)
            .FirstOrDefault();

        if (history != null)
        {
            MailDbContext.MailMailboxAutoreplyHistories.Remove(history);
            return MailDbContext.SaveChanges();
        }

        return 0;
    }

    protected MailboxAutoreplyHistory ToAutoreplyHistory(MailMailboxAutoreplyHistory r)
    {
        var obj = new MailboxAutoreplyHistory
        {
            MailboxId = r.IdMailbox,
            Tenant = r.Tenant,
            SendingDate = r.SendingDate,
            SendingEmail = r.SendingEmail
        };

        return obj;
    }
}
