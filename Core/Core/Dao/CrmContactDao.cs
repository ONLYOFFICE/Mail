using ASC.CRM.Core;
using ASC.Mail.Core.Core.Entities;
using ContactInfoType = ASC.Mail.Enums.ContactInfoType;
using SecurityContext = ASC.Core.SecurityContext;
using ShareType = ASC.Mail.Enums.ShareType;
namespace ASC.Mail.Core.Dao;

[Scope]
public class CrmContactDao : BaseMailDao, ICrmContactDao
{
    private readonly ILogger _log;

    private readonly CrmSecurity _crmSecurity;

    public CrmContactDao(
         TenantManager tenantManager,
         SecurityContext securityContext,
         DbContextManager<MailDbContext> dbContext,
         ILoggerProvider logProvider,
         CrmSecurity crmSecurity)
        : base(tenantManager, securityContext, dbContext)
    {
        _log = logProvider.CreateLogger("ASC.Mail.CrmContactDao");
        _crmSecurity = crmSecurity; 
    }

    public List<int> GetCrmContactIds(string email)
    {
        var ids = new List<int>();

        if (string.IsNullOrEmpty(email))
            return ids;
        try
        {
            var contactList = MailDbContext.CrmContacts
                .AsNoTracking()
                .Join(MailDbContext.CrmContactInfos, c => c.Id, ci => ci.ContactId,
                (c, ci) => new
                {
                    Contact = c,
                    Info = ci
                })
                .Where(o => o.Contact.TenantId == Tenant && o.Info.TenantId == Tenant && o.Info.Type == (int)ContactInfoType.Email && o.Info.Data == email)
                .Select(o => new
                {
                    o.Contact.Id,
                    Company = o.Contact.IsCompany,
                    ShareType = (ShareType)o.Contact.IsShared
                })
                .ToList();

            if (!contactList.Any())
                return ids;

            ids.AddRange(contactList.Select(c => c.Id));

            foreach (var info in contactList)
            {
                var contact = info.Company
                    ? new CrmCompany()
                    : (ASC.Core.Common.EF.Model.CrmContact)new CrmPerson();

                contact.CompanyId = info.Id;
                contact.ContactTypeId = (int)info.ShareType;

                //if (_crmSecurity.CanAccessTo(contact))
                //{
                    ids.Add(info.Id);
                //}
            }
        }
        catch (Exception e)
        {
            _log.WarnCrmContactDaoGetCrmContacts(Tenant, UserId, email, e.ToString());
        }

        return ids;
    }
}
