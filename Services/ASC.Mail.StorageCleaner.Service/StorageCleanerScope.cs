namespace ASC.Mail.StorageCleaner.Service;

[Scope]
public class StorageCleanerScope
{
    private readonly Server.Core.ServerEngine _serverEngine;
    private readonly SecurityContext _securityContext;
    private readonly UserManager _userManager;
    private readonly IMailDaoFactory _mailDaoFactory;
    private readonly ApiHelper _apiHelper;
    private readonly MailboxEngine _mailboxEngine;
    private readonly TenantManager _tenantManager;
    private readonly ServerMailboxEngine _serverMailboxEngine;
    private readonly ServerDomainEngine _serverDomainEngine;
    private readonly UserFolderEngine _userFolderEngine;
    private readonly OperationEngine _operationEngine;
    private readonly StorageFactory _storageFactory;

    public StorageCleanerScope(
        Server.Core.ServerEngine serverEngine,
        SecurityContext securityContext,
        UserManager userManager,
        IMailDaoFactory mailDaoFactory,
        ApiHelper apiHelper,
        MailboxEngine mailboxEngine,
        TenantManager tenantManager,
        ServerMailboxEngine serverMailboxEngine,
        ServerDomainEngine serverDomainEngine,
        UserFolderEngine userFolderEngine,
        OperationEngine operationEngine,
        StorageFactory storageFactory)
    {
        _serverEngine = serverEngine;
        _securityContext = securityContext;
        _userManager = userManager;
        _mailDaoFactory = mailDaoFactory;
        _apiHelper = apiHelper;
        _mailboxEngine = mailboxEngine;
        _tenantManager = tenantManager;
        _serverMailboxEngine = serverMailboxEngine;
        _serverDomainEngine = serverDomainEngine;
        _userFolderEngine = userFolderEngine;
        _operationEngine = operationEngine;
        _storageFactory = storageFactory;
    }
}
