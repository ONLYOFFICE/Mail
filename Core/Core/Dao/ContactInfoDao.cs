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

using ContactInfo = ASC.Mail.Core.Entities.ContactInfo;
using IContactInfoDao = ASC.Mail.Core.Dao.Interfaces.IContactInfoDao;
using SecurityContext = ASC.Core.SecurityContext;


namespace ASC.Mail.Core.Dao;

[Scope]
public class ContactInfoDao : BaseMailDao, IContactInfoDao
{
    public ContactInfoDao(
         TenantManager tenantManager,
         SecurityContext securityContext,
         MailDbContext dbContext)
        : base(tenantManager, securityContext, dbContext)
    {
    }

    public int SaveContactInfo(ContactInfo contactInfo)
    {
        var mailContactInfo = new MailContactInfo
        {
            Id = contactInfo.Id,
            TenantId = contactInfo.Tenant,
            IdUser = contactInfo.User,
            IdContact = contactInfo.ContactId,
            Data = contactInfo.Data,
            Type = contactInfo.Type,
            IsPrimary = contactInfo.IsPrimary
        };

        var entity = MailDbContext.AddOrUpdate(MailDbContext.MailContactInfos, mailContactInfo);
        MailDbContext.SaveChanges();

        return entity.Id;
    }

    public int RemoveContactInfo(int id)
    {
        var queryDelete = MailDbContext.MailContactInfos
            .Where(c => c.TenantId == Tenant
                && c.IdUser == UserId
                && c.Id == id);

        MailDbContext.MailContactInfos.RemoveRange(queryDelete);

        var result = MailDbContext.SaveChanges();

        return result;
    }

    //TODO: Move this method into ContactDao
    public int RemoveByContactIds(List<int> contactIds)
    {
        var queryDelete = MailDbContext.MailContacts
            .Where(c => c.TenantId == Tenant
                && c.IdUser == UserId
                && contactIds.Contains((int)c.Id));

        MailDbContext.MailContacts.RemoveRange(queryDelete);

        var result = MailDbContext.SaveChanges();

        return result;
    }
}
