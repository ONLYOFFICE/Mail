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

using Account = ASC.Mail.Core.Entities.Account;
using SecurityContext = ASC.Core.SecurityContext;

namespace ASC.Mail.Core.Dao;

[Scope]
public class AccountDao : BaseMailDao, IAccountDao
{
    public AccountDao(
        TenantManager tenantManager,
        SecurityContext securityContext,
        DbContextManager<MailDbContext> dbContext)
       : base(tenantManager, securityContext, dbContext)
    {
    }

    public List<Account> GetAccounts()
    {
        var accounts = (from mb in MailDbContext.MailMailboxes
                        join signature in MailDbContext.MailMailboxSignatures.DefaultIfEmpty() on mb.Id equals (uint)signature.IdMailbox into Signature
                        from sig in Signature.DefaultIfEmpty()
                        join autoreply in MailDbContext.MailMailboxAutoreplies.DefaultIfEmpty() on mb.Id equals (uint)autoreply.IdMailbox into Autoreply
                        from reply in Autoreply.DefaultIfEmpty()
                        join address in MailDbContext.MailServerAddresses.DefaultIfEmpty() on mb.Id equals (uint)address.IdMailbox into Address
                        from sa in Address.DefaultIfEmpty()
                        join domain in MailDbContext.MailServerDomains.DefaultIfEmpty() on sa.IdDomain equals domain.Id into Domain
                        from sd in Domain.DefaultIfEmpty()
                        join groupXaddress in MailDbContext.MailServerMailGroupXMailServerAddresses.DefaultIfEmpty() on sa.Id equals groupXaddress.IdAddress into GroupXaddress
                        from sgxa in GroupXaddress.DefaultIfEmpty()
                        join servergroup in MailDbContext.MailServerMailGroups.DefaultIfEmpty() on sgxa.IdMailGroup equals servergroup.Id into ServerGroup
                        from sg in ServerGroup.DefaultIfEmpty()
                        where mb.Tenant == Tenant && mb.IsRemoved == false && mb.IdUser == UserId
                        orderby sa.IsAlias
                        select new Account
                        {
                            MailboxId = (int)mb.Id,
                            MailboxAddress = mb.Address,
                            MailboxEnabled = mb.Enabled,
                            MailboxAddressName = mb.Name,
                            MailboxQuotaError = mb.QuotaError,
                            MailboxDateAuthError = mb.DateAuthError,
                            MailboxOAuthToken = mb.Token,
                            MailboxIsServerMailbox = mb.IsServerMailbox,
                            MailboxEmailInFolder = mb.EmailInFolder,
                            ServerAddressIsAlias = sa.IsAlias != null ? sa.IsAlias : false,
                            ServerAddressId = sa.Id != null ? sa.Id : 0,
                            ServerDomainId = sd.Id != null ? sd.Id : 0,
                            ServerMailGroupId = sg.Id != null ? sg.Id : 0,
                            ServerDomainTenant = sd.Tenant != null ? sd.Tenant : 0,
                            ServerAddressName = sa.Name,
                            ServerDomainName = sd.Name,
                            ServerMailGroupAddress = sg.Address,
                            MailboxSignature = sig != null
                             ? new MailSignatureData((int)mb.Id, mb.Tenant, sig.Html, sig.IsActive)
                             : new MailSignatureData((int)mb.Id, mb.Tenant, string.Empty, false),
                            MailboxAutoreply = reply != null
                             ? new MailAutoreplyData((int)mb.Id, mb.Tenant, reply.TurnOn, reply.OnlyContacts,
                                 reply.TurnOnToDate, reply.FromDate, reply.ToDate, reply.Subject, reply.Html)
                             : new MailAutoreplyData((int)mb.Id, mb.Tenant, false, false,
                                 false, DateTime.MinValue, DateTime.MinValue, string.Empty, string.Empty)
                        }).AsNoTracking().ToList();

        return accounts;
    }
}
