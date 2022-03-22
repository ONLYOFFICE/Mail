namespace ASC.Mail.ImapSync;

[Scope]
public class MailClientScope
{
    private readonly RedisFactory _redisFactory;
    private readonly TenantManager _tenantManager;
    private readonly CoreBaseSettings _coreBaseSettings;
    private readonly StorageFactory _storageFactory;
    private readonly MailEnginesFactory _mailEnginesFactory;
    private readonly SecurityContext _securityContext;
    private readonly ApiHelper _apiHelper;
    private readonly IMailDaoFactory _mailDaoFactory;

    private readonly MailboxEngine _mailboxEngine;
    private readonly FolderEngine _folderEngine;
    private readonly ServiceProvider _serviceProvider;

    public MailClientScope(
        RedisFactory redisFactory,
        ServiceProvider serviceProvider,
        TenantManager tenantManager,
        CoreBaseSettings coreBaseSettings,
        StorageFactory storageFactory,
        MailEnginesFactory mailEnginesFactory,
        SecurityContext securityContext,
        ApiHelper apiHelper,
        IMailDaoFactory mailDaoFactory,
        MailboxEngine mailboxEngine,
        FolderEngine folderEngine)
    {
        _serviceProvider = serviceProvider;
        _coreBaseSettings = coreBaseSettings;
        _tenantManager = tenantManager;
        _storageFactory = storageFactory;
        _mailEnginesFactory = mailEnginesFactory;
        _securityContext = securityContext;
        _apiHelper = apiHelper;
        _mailDaoFactory = mailDaoFactory;
        _mailboxEngine = mailboxEngine;
        _folderEngine = folderEngine;
        _redisFactory = redisFactory;
    }
}
