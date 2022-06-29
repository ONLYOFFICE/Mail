/*
 *
 * (c) Copyright Ascensio System Limited 2010-2020
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



using ContactInfoType = ASC.Mail.Enums.ContactInfoType;
using SecurityContext = ASC.Core.SecurityContext;
using Task = System.Threading.Tasks.Task;

namespace ASC.Mail.Core.Engine;

[Scope]
public class ContactEngine
{
    private int Tenant => _tenantManager.GetCurrentTenant().Id;
    private string User => _securityContext.CurrentAccount.ID.ToString();

    private readonly ILogger _log;
    private readonly SecurityContext _securityContext;
    private readonly TenantManager _tenantManager;
    private readonly IMailDaoFactory _mailDaoFactory;
    private readonly IndexEngine _indexEngine;
    private readonly AccountEngine _accountEngine;
    private readonly ApiHelper _apiHelper;
    private readonly FactoryIndexer<MailContact> _factoryIndexer;
    private readonly FactoryIndexer _factoryIndexerCommon;
    private readonly IServiceProvider _serviceProvider;
    private readonly WebItemSecurity _webItemSecurity;
    private readonly CommonLinkUtility _commonLinkUtility;
    private readonly MailDbContext _mailDbContext;

    public ContactEngine(
        SecurityContext securityContext,
        DbContextManager<MailDbContext> dbContextManager,
        TenantManager tenantManager,
        IMailDaoFactory mailDaoFactory,
        IndexEngine indexEngine,
        AccountEngine accountEngine,
        ApiHelper apiHelper,
        FactoryIndexer<MailContact> factoryIndexer,
        FactoryIndexer factoryIndexerCommon,
        WebItemSecurity webItemSecurity,
        CommonLinkUtility commonLinkUtility,
        IServiceProvider serviceProvider,
        ILoggerProvider logProvider)
    {
        _securityContext = securityContext;
        _mailDbContext = dbContextManager.Get("mail");
        _tenantManager = tenantManager;
        _mailDaoFactory = mailDaoFactory;
        _indexEngine = indexEngine;
        _accountEngine = accountEngine;
        _apiHelper = apiHelper;
        _factoryIndexer = factoryIndexer;
        _factoryIndexerCommon = factoryIndexerCommon;
        _serviceProvider = serviceProvider;
        _webItemSecurity = webItemSecurity;
        _commonLinkUtility = commonLinkUtility;
        _log = logProvider.CreateLogger("ASC.Mail.ContactEngine");
    }

    public List<MailContactData> GetContacts(string search, int? contactType, int? pageSize, int fromIndex,
        string sortorder, out int totalCount)
    {
        var exp = string.IsNullOrEmpty(search) && !contactType.HasValue
            ? new SimpleFilterContactsExp(Tenant, User, sortorder == DefineConstants.ASCENDING, fromIndex, pageSize)
            : new FullFilterContactsExp(Tenant, User, _mailDbContext, _factoryIndexer, _factoryIndexerCommon, _serviceProvider, search, contactType,
                orderAsc: sortorder == DefineConstants.ASCENDING,
                startIndex: fromIndex, limit: pageSize);

        var contacts = GetContactCards(exp);

        if (contacts.Any() && contacts.Count() < pageSize)
        {
            totalCount = fromIndex + contacts.Count;
        }
        else
        {
            totalCount = GetContactCardsCount(exp);
        }

        return contacts.ToMailContactDataList(_commonLinkUtility);
    }

    public List<MailContactData> GetContactsByContactInfo(ContactInfoType infoType, string data, bool? isPrimary)
    {
        var exp = new FullFilterContactsExp(Tenant, User, _mailDbContext, _factoryIndexer, _factoryIndexerCommon, _serviceProvider,
            data, infoType: infoType, isPrimary: isPrimary);

        var contacts = GetContactCards(exp);

        return contacts.ToMailContactDataList(_commonLinkUtility);
    }

    public MailContactData CreateContact(ContactModel model)
    {
        if (model.Emails == null || !model.Emails.Any())
            throw new ArgumentException(@"Invalid list of emails.", "emails");

        var contactCard = new ContactCard(0, Tenant, User, model.Name, model.Description, ContactType.Personal, model.Emails,
            model.PhoneNumbers);

        var newContact = SaveContactCard(contactCard);

        return newContact.ToMailContactData(_commonLinkUtility);
    }

    public List<ContactCard> GetContactCards(IContactsExp exp)
    {
        if (exp == null)
            throw new ArgumentNullException("exp");

        var list = _mailDaoFactory.GetContactCardDao().GetContactCards(exp);

        return list;
    }

    public int GetContactCardsCount(IContactsExp exp)
    {
        if (exp == null)
            throw new ArgumentNullException("exp");

        var count = _mailDaoFactory.GetContactCardDao().GetContactCardsCount(exp);

        return count;
    }

    public ContactCard GetContactCard(int id)
    {
        var contactCard = _mailDaoFactory.GetContactCardDao().GetContactCard(id);

        return contactCard;
    }

    public ContactCard SaveContactCard(ContactCard contactCard)
    {
        var strategy = _mailDaoFactory.GetContext().Database.CreateExecutionStrategy();

        strategy.Execute(() =>
        {
            using var tx = _mailDaoFactory.BeginTransaction(IsolationLevel.ReadUncommitted);

            var contactId = _mailDaoFactory.GetContactDao().SaveContact(contactCard.ContactInfo);

            contactCard.ContactInfo.Id = contactId;

            foreach (var contactItem in contactCard.ContactItems)
            {
                contactItem.ContactId = contactId;

                var contactItemId = _mailDaoFactory.GetContactInfoDao().SaveContactInfo(contactItem);

                contactItem.Id = contactItemId;
            }

            tx.Commit();
        });

        _log.DebugContactEngineSaveContact();

        _indexEngine.Add(contactCard.ToMailContactWrapper());

        return contactCard;
    }

    public MailContactData UpdateContact(ContactModel model)
    {
        if (model.Id < 0)
            throw new ArgumentException(@"Invalid contact id.", "id");

        if (model.Emails == null || !model.Emails.Any())
            throw new ArgumentException(@"Invalid list of emails.", "emails");

        var contactCard = new ContactCard(model.Id, Tenant, User, model.Name, model.Description,
            ContactType.Personal, model.Emails, model.PhoneNumbers);

        var contact = UpdateContactCard(contactCard);

        return contact.ToMailContactData(_commonLinkUtility);
    }

    public ContactCard UpdateContactCard(ContactCard newContactCard)
    {
        var contactId = newContactCard.ContactInfo.Id;

        if (contactId < 0)
            throw new ArgumentException("Invalid contact id");

        var contactCard = GetContactCard(contactId);

        if (null == contactCard)
            throw new ArgumentException("Contact not found");

        var contactChanged = !contactCard.ContactInfo.Equals(newContactCard.ContactInfo);

        var newContactItems = newContactCard.ContactItems.Where(c => !contactCard.ContactItems.Contains(c)).ToList();

        var removedContactItems = contactCard.ContactItems.Where(c => !newContactCard.ContactItems.Contains(c)).ToList();

        if (!contactChanged && !newContactItems.Any() && !removedContactItems.Any())
            return contactCard;

        var strategy = _mailDaoFactory.GetContext().Database.CreateExecutionStrategy();

        strategy.Execute(() =>
        {
            using var tx = _mailDaoFactory.BeginTransaction(IsolationLevel.ReadUncommitted);
            if (contactChanged)
            {
                _mailDaoFactory.GetContactDao().SaveContact(newContactCard.ContactInfo);

                contactCard.ContactInfo = newContactCard.ContactInfo;
            }

            if (newContactItems.Any())
            {
                foreach (var contactItem in newContactItems)
                {
                    contactItem.ContactId = contactId;

                    var contactItemId = _mailDaoFactory.GetContactInfoDao().SaveContactInfo(contactItem);

                    contactItem.Id = contactItemId;

                    contactCard.ContactItems.Add(contactItem);
                }
            }

            if (removedContactItems.Any())
            {
                foreach (var contactItem in removedContactItems)
                {
                    _mailDaoFactory.GetContactInfoDao().RemoveContactInfo(contactItem.Id);

                    contactCard.ContactItems.Remove(contactItem);
                }
            }

            tx.Commit();
        });

        _log.DebugContactEngineUpdateContact();

        _indexEngine.Update(new List<MailContact> { contactCard.ToMailContactWrapper() });

        return contactCard;
    }

    public void RemoveContacts(List<int> ids)
    {
        if (!ids.Any())
            throw new ArgumentException(@"Empty ids collection", "ids");

        var strategy = _mailDaoFactory.GetContext().Database.CreateExecutionStrategy();

        strategy.Execute(() =>
        {
            using var tx = _mailDaoFactory.BeginTransaction();

            _mailDaoFactory.GetContactDao().RemoveContacts(ids);

            _mailDaoFactory.GetContactInfoDao().RemoveByContactIds(ids);

            tx.Commit();
        });

        _log.DebugContactEngineRemoveContacts();

        _indexEngine.RemoveContacts(ids, Tenant, new Guid(User));
    }

    /// <summary>
    /// Search emails in Accounts, Mail, CRM, Peaople Contact System
    /// </summary>
    /// <param name="tenant">Tenant id</param>
    /// <param name="userName">User id</param>
    /// <param name="term">Search word</param>
    /// <param name="maxCountPerSystem">limit result per Contact System</param>
    /// <param name="timeout">Timeout in milliseconds</param>
    /// <param name="httpContextScheme"></param>
    /// <returns></returns>
    public List<string> SearchEmails(int tenant, string userName, string term, int maxCountPerSystem, int timeout = -1)
    {
        var equality = new ContactEqualityComparer();
        var contacts = new List<string>();
        var userGuid = new Guid(userName);

        var watch = new Stopwatch();

        watch.Start();

        var taskList = new List<Task<List<string>>>()
        {
            Task.Run(() =>
            {
                _tenantManager.SetCurrentTenant(tenant);
                _securityContext.AuthenticateMe(userGuid);

                var exp = new FullFilterContactsExp(tenant, userName, _mailDbContext, _factoryIndexer, _factoryIndexerCommon, _serviceProvider,
                    term, infoType: ContactInfoType.Email, orderAsc: true, limit: maxCountPerSystem);

                var contactCards = GetContactCards(exp);

                return (from contactCard in contactCards
                    from contactItem in contactCard.ContactItems
                    select
                        string.IsNullOrEmpty(contactCard.ContactInfo.ContactName)
                            ? contactItem.Data
                            : MailUtil.CreateFullEmail(contactCard.ContactInfo.ContactName, contactItem.Data))
                    .ToList();
            }),

            Task.Run(() =>
            {
                _tenantManager.SetCurrentTenant(tenant);
                _securityContext.AuthenticateMe(userGuid);

                return _accountEngine.SearchAccountEmails(term);
            }),

            Task.Run(() =>
            {
                _tenantManager.SetCurrentTenant(tenant);
                _securityContext.AuthenticateMe(userGuid);

                return _webItemSecurity.IsAvailableForMe(WebItemManager.CRMProductID)
                    ? _apiHelper.SearchCrmEmails(term, maxCountPerSystem)
                    : new List<string>();
            }),

            Task.Run(() =>
            {
                _tenantManager.SetCurrentTenant(tenant);
                _securityContext.AuthenticateMe(userGuid);

                return _webItemSecurity.IsAvailableForMe(WebItemManager.PeopleProductID)
                    ? _apiHelper.SearchPeopleEmails(term, 0, maxCountPerSystem)
                    : new List<string>();
            })
        };

        try
        {
            var taskArray = taskList.ToArray<Task>();

            Task.WaitAll(taskArray, timeout);

            watch.Stop();
        }
        catch (AggregateException e)
        {
            watch.Stop();

            var errorText =
                new StringBuilder("SearchEmails: \nThe following exceptions have been thrown by WaitAll():");

            foreach (var t in e.InnerExceptions)
            {
                errorText
                    .AppendFormat("\n-------------------------------------------------\n{0}", t);
            }

            _log.ErrorContactEngineError(errorText.ToString());
        }

        contacts =
            taskList.Aggregate(contacts,
                (current, task) => !task.IsFaulted
                                   && task.IsCompleted
                                   && !task.IsCanceled
                    ? current.Concat(task.Result).ToList()
                    : current)
                .Distinct(equality)
                .ToList();

        _log.DebugContactEngineSearchEmails(term, watch.Elapsed.TotalSeconds, contacts.Count);

        return contacts;
    }

    public class ContactEqualityComparer : IEqualityComparer<string>
    {
        public bool Equals(string contact1, string contact2)
        {
            if (contact1 == null && contact2 == null)
                return true;

            if (contact1 == null || contact2 == null)
                return false;

            var contact1Parts = contact1.Split('<');
            var contact2Parts = contact2.Split('<');

            return contact1Parts.Last().Replace(">", "") == contact2Parts.Last().Replace(">", "");
        }

        public int GetHashCode(string str)
        {
            var strParts = str.Split('<');
            return strParts.Last().Replace(">", "").GetHashCode();
        }
    }
}
