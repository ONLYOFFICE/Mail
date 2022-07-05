

namespace ASC.Mail.Aggregator.Service.Service;

[Scope]
public class AggregatorServiceScope
{
    #region AggSrv

    private readonly TenantManager _tenantManager;
    private readonly CoreBaseSettings _coreBaseSettings;
    private readonly MailQueueItemSettings _mailQueueItemSettings;
    private readonly StorageFactory _storageFactory;
    private readonly MailEnginesFactory _mailEnginesFactory;
    private readonly SecurityContext _securityContext;
    private readonly ApiHelper _apiHelper;
    private readonly IMailDaoFactory _mailDaoFactory;

    #endregion

    #region QueueManagerScope

    private readonly UserManager _userManager;
    private readonly MailboxEngine _mailboxEngine;
    private readonly AlertEngine _alertEngine;

    #endregion

    #region SignalrWorkerScope

    private readonly FolderEngine _folderEngine;

    #endregion

    private readonly ServiceProvider _serviceProvider;

    public AggregatorServiceScope(
        ServiceProvider serviceProvider,
        TenantManager tenantManager,
        CoreBaseSettings coreBaseSettings,
        MailQueueItemSettings mailQueueItemSettings,
        StorageFactory storageFactory,
        MailEnginesFactory mailEnginesFactory,
        SecurityContext securityContext,
        ApiHelper apiHelper,
        IMailDaoFactory mailDaoFactory,
        UserManager userManager,
        MailboxEngine mailboxEngine,
        AlertEngine alertEngine,
        FolderEngine folderEngine)
    {
        _serviceProvider = serviceProvider;

        _tenantManager = tenantManager;
        _mailQueueItemSettings = mailQueueItemSettings;
        _coreBaseSettings = coreBaseSettings;
        _storageFactory = storageFactory;
        _mailEnginesFactory = mailEnginesFactory;
        _securityContext = securityContext;
        _apiHelper = apiHelper;
        _mailDaoFactory = mailDaoFactory;
        _userManager = userManager;
        _mailboxEngine = mailboxEngine;
        _alertEngine = alertEngine;
        _folderEngine = folderEngine;
    }

    public TenantManager GetTenantManager() => _tenantManager;
}
