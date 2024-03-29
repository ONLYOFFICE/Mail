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

using Mailbox = ASC.Mail.Core.Entities.Mailbox;
using SecurityContext = ASC.Core.SecurityContext;

namespace ASC.Mail.Core.Dao;

[Scope]
public class MailboxAutoreplyDao : BaseMailDao, IMailboxAutoreplyDao
{
    public MailboxAutoreplyDao(
         TenantManager tenantManager,
         SecurityContext securityContext,
         MailDbContext dbContext)
        : base(tenantManager, securityContext, dbContext)
    {
    }

    public MailboxAutoreply GetAutoreply(Mailbox mailbox)
    {
        var autoreply = MailDbContext.MailMailboxAutoreplies
            .AsNoTracking()
            .Where(a => a.Tenant == mailbox.Tenant && a.IdMailbox == mailbox.Id)
            .Select(ToAutoreply)
            .FirstOrDefault();

        return autoreply ?? new MailboxAutoreply
        {
            MailboxId = mailbox.Id,
            Tenant = mailbox.Tenant,
            TurnOn = false,
            OnlyContacts = false,
            TurnOnToDate = false,
            FromDate = DateTime.MinValue,
            ToDate = DateTime.MinValue,
            Subject = string.Empty,
            Html = string.Empty
        };
    }

    public int SaveAutoreply(MailboxAutoreply autoreply)
    {
        var newValue = new MailMailboxAutoreply
        {
            IdMailbox = autoreply.MailboxId,
            Tenant = autoreply.Tenant,
            TurnOn = autoreply.TurnOn,
            TurnOnToDate = autoreply.TurnOnToDate,
            OnlyContacts = autoreply.OnlyContacts,
            FromDate = autoreply.FromDate,
            ToDate = autoreply.ToDate,
            Subject = autoreply.Subject,
            Html = autoreply.Html
        };

        MailDbContext.AddOrUpdate(MailDbContext.MailMailboxAutoreplies, newValue);

        return 1;
    }

    public int DeleteAutoreply(int mailboxId)
    {
        var range = MailDbContext.MailMailboxAutoreplies
            .Where(r => r.Tenant == Tenant && r.IdMailbox == mailboxId);

        MailDbContext.MailMailboxAutoreplies.RemoveRange(range);

        var count = MailDbContext.SaveChanges();

        return count;
    }

    protected MailboxAutoreply ToAutoreply(MailMailboxAutoreply r)
    {
        var obj = new MailboxAutoreply
        {
            MailboxId = r.IdMailbox,
            Tenant = r.Tenant,
            TurnOn = r.TurnOn,
            OnlyContacts = r.OnlyContacts,
            TurnOnToDate = r.TurnOnToDate,
            FromDate = r.FromDate,
            ToDate = r.ToDate,
            Subject = r.Subject,
            Html = r.Html
        };

        return obj;
    }
}
