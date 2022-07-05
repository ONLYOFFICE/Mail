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

using Alias = ASC.Mail.Server.Core.Entities.Alias;
using Mailbox = ASC.Mail.Core.Entities.Mailbox;
using SecurityContext = ASC.Core.SecurityContext;

namespace ASC.Mail.Core.Engine;

[Scope]
public class ServerMailboxEngine : BaseEngine
{
    private int Tenant => _tenantManager.GetCurrentTenant().Id;
    private string User => _securityContext.CurrentAccount.ID.ToString();
    private bool IsAdmin => _webItemSecurity.IsProductAdministrator(WebItemManager.MailProductID, _securityContext.CurrentAccount.ID);

    private readonly SecurityContext _securityContext;
    private readonly TenantManager _tenantManager;
    private readonly IMailDaoFactory _mailDaoFactory;
    private readonly AccountEngine _accountEngine;
    private readonly CacheEngine _cacheEngine;
    private readonly ServerDomainEngine _serverDomainEngine;
    private readonly MailboxEngine _mailboxEngine;
    private readonly CoreBaseSettings _coreBaseSettings;
    private readonly WebItemSecurity _webItemSecurity;
    private readonly SettingsManager _settingsManager;
    private readonly UserManagerWrapper _userManagerWrapper;
    private readonly AuthManager _authManager;
    private readonly UserManager _userManager;
    private readonly DisplayUserSettingsHelper _displayUserSettingsHelper;

    public ServerMailboxEngine(
        SecurityContext securityContext,
        TenantManager tenantManager,
        IMailDaoFactory mailDaoFactory,
        AccountEngine accountEngine,
        CacheEngine cacheEngine,
        ServerDomainEngine serverDomainEngine,
        MailboxEngine mailboxEngine,
        CoreBaseSettings coreBaseSettings,
        WebItemSecurity webItemSecurity,
        SettingsManager settingsManager,
        UserManagerWrapper userManagerWrapper,
        AuthManager authManager,
        UserManager userManager,
        DisplayUserSettingsHelper displayUserSettingsHelper,
        MailSettings mailSettings) : base(mailSettings)
    {
        _securityContext = securityContext;
        _tenantManager = tenantManager;
        _mailDaoFactory = mailDaoFactory;
        _accountEngine = accountEngine;
        _cacheEngine = cacheEngine;
        _serverDomainEngine = serverDomainEngine;
        _mailboxEngine = mailboxEngine;
        _coreBaseSettings = coreBaseSettings;
        _webItemSecurity = webItemSecurity;
        _settingsManager = settingsManager;
        _userManagerWrapper = userManagerWrapper;
        _authManager = authManager;
        _userManager = userManager;
        _displayUserSettingsHelper = displayUserSettingsHelper;
    }

    public List<ServerMailboxData> GetMailboxes()
    {
        if (!IsAdmin)
            throw new SecurityException("Need admin privileges.");

        var mailboxes = _mailDaoFactory.GetMailboxDao().GetMailBoxes(new TenantServerMailboxesExp(Tenant));

        var addresses = _mailDaoFactory.GetServerAddressDao().GetList();

        var domains = _mailDaoFactory.GetServerDomainDao().GetDomains();

        var list = from mailbox in mailboxes
                   let address =
                       addresses.FirstOrDefault(
                           a => a.MailboxId == mailbox.Id && a.IsAlias == false && a.IsMailGroup == false)
                   where address != null
                   let domain = domains.FirstOrDefault(d => d.Id == address.DomainId)
                   where domain != null
                   let serverAddressData = ToServerDomainAddressData(address, domain)
                   let aliases =
                       addresses.Where(a => a.MailboxId == mailbox.Id && a.IsAlias && !a.IsMailGroup)
                           .ToList()
                           .ConvertAll(a => ToServerDomainAddressData(a, domain))
                   select ToMailboxData(mailbox, serverAddressData, aliases);

        return list.ToList();
    }

    public bool IsAddressAlreadyRegistered(string localPart, int domainId)
    {
        var serverDomain = _mailDaoFactory.GetServerDomainDao().GetDomain(domainId);

        var isSharedDomain = serverDomain.Tenant == DefineConstants.SHARED_TENANT_ID;

        if (!IsAdmin && !isSharedDomain)
            throw new SecurityException("Need admin privileges.");

        var tenantQuota = _tenantManager.GetTenantQuota(Tenant);

        if (isSharedDomain
            && (tenantQuota.Trial
                || tenantQuota.Free))
        {
            throw new SecurityException("Not available in unpaid version");
        }

        if (string.IsNullOrEmpty(localPart))
            throw new ArgumentException(@"Invalid local part.", "localPart");

        if (domainId < 0)
            throw new ArgumentException(@"Invalid domain id.", "domainId");

        var state = _mailDaoFactory.GetServerAddressDao().IsAddressAlreadyRegistered(localPart, serverDomain.Name);

        return state;
    }

    public bool IsAddressValid(string localPart, int domainId)
    {
        var serverDomain = _mailDaoFactory.GetServerDomainDao().GetDomain(domainId);

        var isSharedDomain = serverDomain.Tenant == DefineConstants.SHARED_TENANT_ID;

        if (!IsAdmin && !isSharedDomain)
            throw new SecurityException("Need admin privileges.");

        var tenantQuota = _tenantManager.GetTenantQuota(Tenant);

        if (isSharedDomain
            && (tenantQuota.Trial
                || tenantQuota.Free))
        {
            throw new SecurityException("Not available in unpaid version");
        }

        if (string.IsNullOrEmpty(localPart))
            return false;

        if (domainId < 0)
            return false;

        if (localPart.Length > 64)
            return false;

        if (!Parser.IsEmailLocalPartValid(localPart))
            return false;

        return true;
    }

    public ServerMailboxData CreateMailbox(string name, string localPart, int domainId, string userId)
    {
        ServerMailboxData mailboxData = new ServerMailboxData();

        var serverDomain = _mailDaoFactory.GetServerDomainDao().GetDomain(domainId);

        var isSharedDomain = serverDomain.Tenant == DefineConstants.SHARED_TENANT_ID;

        if (!IsAdmin && !isSharedDomain)
            throw new SecurityException("Need admin privileges.");

        var tenantQuota = _tenantManager.GetTenantQuota(Tenant);

        if (isSharedDomain
            && (tenantQuota.Trial
                || tenantQuota.Free))
        {
            throw new SecurityException("Not available in unpaid version");
        }

        if (string.IsNullOrEmpty(localPart))
            throw new ArgumentException(@"Invalid local part.", "localPart");

        if (domainId < 0)
            throw new ArgumentException(@"Invalid domain id.", "domainId");

        if (name.Length > 255)
            throw new ArgumentException(@"Sender name exceed limitation of 64 characters.", "name");

        if (!Guid.TryParse(userId, out Guid user))
            throw new ArgumentException(@"Invalid user id.", "userId");

        if (isSharedDomain && !IsAdmin && user != _securityContext.CurrentAccount.ID)
            throw new SecurityException(
                "Creation of a shared mailbox is allowed only for the current account if user is not admin.");

        var teamlabAccount = _authManager.GetAccountByID(Tenant, user);

        if (teamlabAccount == null)
            throw new InvalidDataException("Unknown user.");

        var userInfo = _userManager.GetUsers(user);

        if (userInfo.IsVisitor(_userManager))
            throw new InvalidDataException("User is visitor.");

        if (localPart.Length > 64)
            throw new ArgumentException(@"Local part of mailbox exceed limitation of 64 characters.",
                "localPart");

        if (!Parser.IsEmailLocalPartValid(localPart))
            throw new ArgumentException("Incorrect local part of mailbox.");

        if (_mailDaoFactory.GetServerAddressDao().IsAddressAlreadyRegistered(localPart, serverDomain.Name))
        {
            throw new DuplicateNameException("You want to create a mailbox with already existing address.");
        }

        if (MailSettings.Defines.ServerDomainMailboxPerUserLimit > 0)
        {
            var accounts = _accountEngine.GetAccountInfoList();

            var countDomainMailboxes =
                accounts.Count(a =>
                    a.IsTeamlabMailbox &&
                    Parser.ParseAddress(a.Email)
                        .Domain.Equals(serverDomain.Name, StringComparison.InvariantCultureIgnoreCase));

            if (countDomainMailboxes >= MailSettings.Defines.ServerDomainMailboxPerUserLimit)
            {
                throw new ArgumentOutOfRangeException(
                    string.Format("Count of user's mailboxes must be less or equal {0}.",
                        MailSettings.Defines.ServerDomainMailboxPerUserLimit));
            }
        }

        var server = _mailDaoFactory.GetServerDao().Get(Tenant);

        var mailboxLocalPart = localPart.ToLowerInvariant();

        var login = string.Format("{0}@{1}", mailboxLocalPart, serverDomain.Name);

        var existMailbox = _mailDaoFactory.GetMailboxDao().GetMailBox(new ConcreteUserMailboxExp(new MailAddress(login), Tenant, userId));

        if (existMailbox != null)
        {
            throw new DuplicateNameException("You want to create a mailbox that is already connected.");
        }

        var password = PasswordGenerator.GenerateNewPassword(12);

        var utcNow = DateTime.UtcNow;

        var strategy = _mailDaoFactory.GetContext().Database.CreateExecutionStrategy();

        strategy.Execute(() =>
        {
            using var tx = _mailDaoFactory.BeginTransaction(IsolationLevel.ReadUncommitted);

            var mailbox = new Mailbox
            {
                Id = 0,
                Tenant = Tenant,
                User = userId,
                Name = name,
                Address = login,
                OAuthToken = null,
                OAuthType = (int)AuthorizationServiceType.None,
                ServerId = server.ImapSettingsId,
                Password = password,
                SmtpServerId = server.SmtpSettingsId,
                SmtpPassword = password,
                SizeLast = 0,
                MsgCountLast = 0,
                BeginDate = DefineConstants.MinBeginDate,
                Imap = true,
                Enabled = true,
                IsTeamlabMailbox = true,
                IsRemoved = false,
                DateCreated = utcNow
            };

            mailbox.Id = _mailDaoFactory.GetMailboxDao().SaveMailBox(mailbox);

            var address = new ServerAddress
            {
                Id = 0,
                Tenant = Tenant,
                MailboxId = mailbox.Id,
                DomainId = serverDomain.Id,
                AddressName = localPart,
                IsAlias = false,
                IsMailGroup = false,
                DateCreated = utcNow
            };

            address.Id = _mailDaoFactory.GetServerAddressDao().Save(address);

            var engine = new Server.Core.ServerEngine(server.Id, server.ConnectionString);

            var maildir = PostfixMaildirUtil.GenerateMaildirPath(serverDomain.Name, localPart, utcNow);

            var serverMailbox = new Server.Core.Entities.Mailbox
            {
                Name = name,
                Password = password,
                Username = login,
                LocalPart = localPart,
                Domain = serverDomain.Name,
                Active = true,
                Quota = 0,
                Maildir = maildir,
                Modified = utcNow,
                Created = utcNow,
            };

            var serverAddress = new Alias
            {
                Name = name,
                Address = login,
                Goto = login,
                Domain = serverDomain.Name,
                Active = true,
                Islist = false,
                Modified = utcNow,
                Created = utcNow
            };

            engine.SaveMailbox(serverMailbox, serverAddress);

            tx.Commit();

            _cacheEngine.Clear(userId);

            mailboxData = ToMailboxData(mailbox, ToServerDomainAddressData(address, login),
                new List<ServerDomainAddressData>());
        });

        return mailboxData;
    }

    public ServerMailboxData CreateMyCommonDomainMailbox(string name)
    {
        if (!SetupInfo.IsVisibleSettings("AdministrationPage") || !SetupInfo.IsVisibleSettings("MailCommonDomain") ||
            _coreBaseSettings.Standalone)
        {
            throw new Exception("Common domain is not available");
        }

        var domain = _serverDomainEngine.GetCommonDomain();

        if (domain == null)
            throw new SecurityException("Domain not found.");

        var userInfo = _userManager.GetUsers(_securityContext.CurrentAccount.ID);

        return CreateMailbox(userInfo.DisplayUserName(_displayUserSettingsHelper), name, domain.Id, userInfo.Id.ToString());
    }

    public ServerMailboxData UpdateMailboxDisplayName(int mailboxId, string name)
    {
        var serverMailboxAddresses = _mailDaoFactory.GetServerAddressDao().GetList(mailboxId);

        if (!serverMailboxAddresses.Any())
            throw new ArgumentException("Mailbox not found");

        var serverMailboxAddress = serverMailboxAddresses.FirstOrDefault(a => !a.IsAlias && !a.IsMailGroup);

        if (serverMailboxAddress == null)
            throw new ArgumentException("Mailbox not found");

        var serverMailboxAliases = serverMailboxAddresses.Where(a => a.IsAlias).ToList();

        var serverDomain = _mailDaoFactory.GetServerDomainDao().GetDomain(serverMailboxAddress.DomainId);

        var isSharedDomain = serverDomain.Tenant == DefineConstants.SHARED_TENANT_ID;

        if (!IsAdmin && !isSharedDomain)
            throw new SecurityException("Need admin privileges.");

        var tenantQuota = _tenantManager.GetTenantQuota(Tenant);

        if (isSharedDomain
            && (tenantQuota.Trial
                || tenantQuota.Free))
        {
            throw new SecurityException("Not available in unpaid version");
        }

        if (name.Length > 255)
            throw new ArgumentException(@"Sender name exceed limitation of 64 characters.", "name");

        var serverMailbox =
            _mailDaoFactory.GetMailboxDao().GetMailBox(new ConcreteTenantServerMailboxExp(mailboxId, Tenant, false));

        serverMailbox.Name = name;

        _mailDaoFactory.GetMailboxDao().SaveMailBox(serverMailbox);

        _cacheEngine.Clear(serverMailbox.User);

        var address = ToServerDomainAddressData(serverMailboxAddress, serverDomain);

        var aliases = serverMailboxAliases.ConvertAll(a => ToServerDomainAddressData(a, serverDomain));

        var mailboxData = ToMailboxData(serverMailbox, address, aliases);

        return mailboxData;
    }

    public ServerDomainAddressData AddAlias(int mailboxId, string aliasName)
    {
        if (!IsAdmin)
            throw new SecurityException("Need admin privileges.");

        if (string.IsNullOrEmpty(aliasName))
            throw new ArgumentException(@"Invalid alias name.", "aliasName");

        if (mailboxId < 0)
            throw new ArgumentException(@"Invalid mailbox id.", "mailboxId");

        if (aliasName.Length > 64)
            throw new ArgumentException(@"Local part of mailbox alias exceed limitation of 64 characters.", "aliasName");

        if (!Parser.IsEmailLocalPartValid(aliasName))
            throw new ArgumentException("Incorrect mailbox alias.");

        var mailboxAliasName = aliasName.ToLowerInvariant();

        var mailbox = _mailDaoFactory.GetMailboxDao().GetMailBox(new ConcreteTenantMailboxExp(mailboxId, Tenant));

        if (mailbox == null)
            throw new ArgumentException("Mailbox not exists");

        if (!mailbox.IsTeamlabMailbox)
            throw new ArgumentException("Invalid mailbox type");

        if (mailbox.Tenant == DefineConstants.SHARED_TENANT_ID)
            throw new InvalidOperationException("Adding mailbox alias is not allowed for shared domain.");

        var mailAddress = new MailAddress(mailbox.Address);

        var serverDomain = _mailDaoFactory.GetServerDomainDao().GetDomains().FirstOrDefault(d => d.Name == mailAddress.Host);

        if (serverDomain == null)
            throw new ArgumentException("Domain not exists");

        var mailboxAddress = mailAddress.Address;

        if (_mailDaoFactory.GetServerAddressDao().IsAddressAlreadyRegistered(mailboxAliasName, serverDomain.Name))
        {
            throw new DuplicateNameException("You want to create a mailbox with already existing address.");
        }

        var utcNow = DateTime.UtcNow;

        var address = new ServerAddress
        {
            Id = 0,
            Tenant = Tenant,
            MailboxId = mailbox.Id,
            DomainId = serverDomain.Id,
            AddressName = mailboxAliasName,
            IsAlias = true,
            IsMailGroup = false,
            DateCreated = utcNow
        };

        var aliasEmail = string.Format("{0}@{1}", mailboxAliasName, serverDomain.Name);

        var server = _mailDaoFactory.GetServerDao().Get(Tenant);

        var engine = new Server.Core.ServerEngine(server.Id, server.ConnectionString);
        var strategy = _mailDaoFactory.GetContext().Database.CreateExecutionStrategy();

        strategy.Execute(() =>
        {
            using var tx = _mailDaoFactory.BeginTransaction(IsolationLevel.ReadUncommitted);

            address.Id = _mailDaoFactory.GetServerAddressDao().Save(address);

            var serverAddress = new Alias
            {
                Name = mailbox.Name,
                Address = aliasEmail,
                Goto = mailboxAddress,
                Domain = serverDomain.Name,
                Active = true,
                Islist = false,
                Modified = utcNow,
                Created = utcNow
            };

            engine.SaveAlias(serverAddress);

            tx.Commit();
        });

        _cacheEngine.Clear(mailbox.User);

        return new ServerDomainAddressData
        {
            Id = address.Id,
            DomainId = address.DomainId,
            Email = aliasEmail
        };
    }

    public void RemoveAlias(int mailboxId, int addressId)
    {
        if (!IsAdmin)
            throw new SecurityException("Need admin privileges.");

        if (mailboxId < 0)
            throw new ArgumentException(@"Invalid address id.", "mailboxId");


        var mailbox = _mailDaoFactory.GetMailboxDao().GetMailBox(new ConcreteTenantServerMailboxExp(mailboxId, Tenant, false));

        if (mailbox == null)
            throw new ArgumentException("Mailbox not exists");

        if (!mailbox.IsTeamlabMailbox)
            throw new ArgumentException("Invalid mailbox type");

        var alias = _mailDaoFactory.GetServerAddressDao().Get(addressId);

        if (!alias.IsAlias)
            throw new ArgumentException("Address is not alias");

        var serverDomain = _mailDaoFactory.GetServerDomainDao().GetDomain(alias.DomainId);

        if (serverDomain == null)
            throw new ArgumentException("Domain not exists");

        var aliasEmail = string.Format("{0}@{1}", alias.AddressName, serverDomain.Name);

        var server = _mailDaoFactory.GetServerDao().Get(Tenant);

        var engine = new Server.Core.ServerEngine(server.Id, server.ConnectionString);

        var strategy = _mailDaoFactory.GetContext().Database.CreateExecutionStrategy();

        strategy.Execute(() =>
        {
            using var tx = _mailDaoFactory.BeginTransaction(IsolationLevel.ReadUncommitted);

            _mailDaoFactory.GetServerAddressDao().Delete(addressId);
            engine.RemoveAlias(aliasEmail);

            tx.Commit();
        });

        _cacheEngine.Clear(mailbox.User);
    }

    public void RemoveMailbox(MailBoxData mailBox)
    {
        var serverMailboxAddresses = _mailDaoFactory.GetServerAddressDao().GetList(mailBox.MailBoxId);

        var serverMailboxAddress = serverMailboxAddresses.FirstOrDefault(a => !a.IsAlias && !a.IsMailGroup);

        if (serverMailboxAddress == null)
            throw new InvalidDataException("Mailbox address not found");

        var serverDomain = _mailDaoFactory.GetServerDomainDao().GetDomain(serverMailboxAddress.DomainId);

        if (serverDomain == null)
            throw new InvalidDataException("Domain not found");

        var serverGroups = _mailDaoFactory.GetServerGroupDao().GetList();

        var server = _mailDaoFactory.GetServerDao().Get(mailBox.TenantId);

        var serverEngine = new Server.Core.ServerEngine(server.Id, server.ConnectionString);

        var utcNow = DateTime.UtcNow;

        var strategy = _mailDaoFactory.GetContext().Database.CreateExecutionStrategy();

        strategy.Execute(() =>
        {
            using var tx = _mailDaoFactory.BeginTransaction(IsolationLevel.ReadUncommitted);

            foreach (var serverGroup in serverGroups)
            {
                var addresses = _mailDaoFactory.GetServerAddressDao().GetGroupAddresses(serverGroup.Id);

                var index = addresses.FindIndex(a => a.Id == serverMailboxAddress.Id);

                if (index < 0)
                    continue;

                addresses.RemoveAt(index);

                if (addresses.Count == 0)
                {
                    _mailDaoFactory.GetServerGroupDao().Delete(serverGroup.Id);

                    _mailDaoFactory.GetServerAddressDao().DeleteAddressesFromMailGroup(serverGroup.Id);

                    serverEngine.RemoveAlias(serverGroup.Address);
                }
                else
                {
                    _mailDaoFactory.GetServerAddressDao().DeleteAddressFromMailGroup(serverGroup.Id, serverMailboxAddress.Id);

                    var goTo = string.Join(",",
                        addresses.Select(m => string.Format("{0}@{1}", m.AddressName, serverDomain.Name)));

                    var serverAddress = new Alias
                    {
                        Name = "",
                        Address = serverGroup.Address,
                        Goto = goTo,
                        Domain = serverDomain.Name,
                        Active = true,
                        Islist = true,
                        Modified = utcNow,
                        Created = serverGroup.DateCreated
                    };

                    serverEngine.SaveAlias(serverAddress);
                }
            }

            _mailDaoFactory.GetServerAddressDao().Delete(serverMailboxAddresses.Select(a => a.Id).ToList());

            foreach (var mailboxAddress in serverMailboxAddresses)
            {
                serverEngine.RemoveAlias(string.Format("{0}@{1}", mailboxAddress.AddressName, serverDomain.Name));
            }

            _mailboxEngine.RemoveMailBox(mailBox, false);

            serverEngine.RemoveMailbox(string.Format("{0}@{1}", serverMailboxAddress.AddressName,
                serverDomain.Name));

            tx.Commit();
        });
    }

    public MailOperationStatus RemoveMailbox(int id)
    {
        if (id < 0)
            throw new ArgumentException(@"Invalid server mailbox id.", "id");

        var mailbox = _mailboxEngine.GetMailboxData(new ConcreteTenantServerMailboxExp(id, Tenant, false));

        if (mailbox == null)
            throw new ItemNotFoundException("Mailbox not found.");

        var isSharedDomain = mailbox.TenantId == DefineConstants.SHARED_TENANT_ID;

        if (!IsAdmin && !isSharedDomain)
            throw new SecurityException("Need admin privileges.");

        if (isSharedDomain && !IsAdmin &&
            !mailbox.UserId.Equals(_securityContext.CurrentAccount.ID.ToString(),
                StringComparison.InvariantCultureIgnoreCase))
        {
            throw new SecurityException(
                "Removing of a shared mailbox is allowed only for the current account if user is not admin.");
        }

        //TODO fix return OperationEngine.RemoveServerMailbox(mailbox);

        throw new NotImplementedException();
    }

    public void ChangePassword(int mailboxId, string password)
    {
        if (!_coreBaseSettings.Standalone)
            throw new SecurityException("Not allowed in this version");

        if (mailboxId < 0)
            throw new ArgumentException(@"Invalid mailbox id.", "mailboxId");

        var trimPwd = Parser.GetValidPassword(password, _settingsManager, _userManagerWrapper);


        var serverMailboxAddresses = _mailDaoFactory.GetServerAddressDao().GetList(mailboxId);

        if (!serverMailboxAddresses.Any())
            throw new ArgumentException("Mailbox not found");

        var serverMailboxAddress = serverMailboxAddresses.FirstOrDefault(a => !a.IsAlias && !a.IsMailGroup);

        if (serverMailboxAddress == null)
            throw new ArgumentException("Mailbox not found");

        var exp = IsAdmin
            ? (IMailboxExp)new ConcreteTenantMailboxExp(mailboxId, Tenant)
            : new ConcreteUserMailboxExp(mailboxId, Tenant, User);

        var mailbox =
                _mailDaoFactory.GetMailboxDao().GetMailBox(exp);

        if (mailbox == null) // Mailbox has been removed
            throw new ArgumentException("Mailbox not found");

        var server = _mailDaoFactory.GetServerDao().Get(Tenant);

        var strategy = _mailDaoFactory.GetContext().Database.CreateExecutionStrategy();

        strategy.Execute(() =>
        {
            using var tx = _mailDaoFactory.BeginTransaction(IsolationLevel.ReadUncommitted);

            var engine = new Server.Core.ServerEngine(server.Id, server.ConnectionString);

            engine.ChangePassword(mailbox.Address, trimPwd);

            mailbox.Password = trimPwd;
            mailbox.SmtpPassword = trimPwd;

            _mailDaoFactory.GetMailboxDao().SaveMailBox(mailbox);

            tx.Commit();
        });
    }

    public static ServerMailboxData ToMailboxData(Mailbox mailbox, ServerDomainAddressData address,
        List<ServerDomainAddressData> aliases)
    {
        return new ServerMailboxData
        {
            Id = mailbox.Id,
            UserId = mailbox.User,
            Address = address,
            Name = mailbox.Name,
            Aliases = aliases
        };
    }

    public static ServerDomainAddressData ToServerDomainAddressData(ServerAddress address, ServerDomain domain)
    {
        var result = new ServerDomainAddressData
        {
            Id = address.Id,
            DomainId = address.DomainId,
            Email = string.Format("{0}@{1}", address.AddressName, domain.Name)
        };

        return result;
    }

    public static ServerDomainAddressData ToServerDomainAddressData(ServerAddress address, ServerDomainData domain)
    {
        var result = new ServerDomainAddressData
        {
            Id = address.Id,
            DomainId = address.DomainId,
            Email = string.Format("{0}@{1}", address.AddressName, domain.Name)
        };

        return result;
    }

    public static ServerDomainAddressData ToServerDomainAddressData(ServerAddress address, string email)
    {
        var result = new ServerDomainAddressData
        {
            Id = address.Id,
            DomainId = address.DomainId,
            Email = email
        };

        return result;
    }
}
