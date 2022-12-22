using ASC.Data.Storage;
using SecurityContext = ASC.Core.SecurityContext;

namespace ASC.Mail.Core.Engine;

[Scope]
public class MailEnginesFactory
{
    private TenantManager _tenantManager;
    private SecurityContext _securityContext;

    public int Tenant => _tenantManager.GetCurrentTenant().Id;
    public string UserId => _securityContext.CurrentAccount.ID.ToString();

    private AutoreplyEngine _autoreplyEngine;
    private CalendarEngine _calendarEngine;
    private IndexEngine _indexEngine;
    private TagEngine _tagEngine;
    private CrmLinkEngine _crmLinkEngine;
    private EmailInEngine _emailInEngine;
    private FilterEngine _filterEngine;
    private MailboxEngine _mailboxEngine;
    private MessageEngine _messageEngine;
    private StorageFactory _storageFactory;
    private StorageManager _storageManager;
    private FolderEngine _folderEngine;
    private UserFolderEngine _userFolderEngine;
    private ApiHelper _apiHelper;
    private MailInfoDao _mailInfoDao;

    public AutoreplyEngine AutoreplyEngine => _autoreplyEngine;
    public CalendarEngine CalendarEngine => _calendarEngine;
    public IndexEngine IndexEngine => _indexEngine;
    public TagEngine TagEngine => _tagEngine;
    public CrmLinkEngine CrmLinkEngine => _crmLinkEngine;
    public EmailInEngine EmailInEngine => _emailInEngine;
    public FilterEngine FilterEngine => _filterEngine;
    public MailboxEngine MailboxEngine => _mailboxEngine;
    public MessageEngine MessageEngine => _messageEngine;
    public StorageFactory StorageFactory => _storageFactory;
    public StorageManager StorageManager => _storageManager;
    public FolderEngine FolderEngine => _folderEngine;
    public UserFolderEngine UserFolderEngine => _userFolderEngine;
    public ApiHelper ApiHelper => _apiHelper;
    public MailInfoDao MailInfoDao => _mailInfoDao;

    public MailEnginesFactory(
        AutoreplyEngine autoreplyEngine,
        CalendarEngine calendarEngine,
        IndexEngine indexEngine,
        TagEngine tagEngine,
        CrmLinkEngine crmLinkEngine,
        EmailInEngine emailInEngine,
        FilterEngine filterEngine,
        MailboxEngine mailboxEngine,
        MessageEngine messageEngine,
        TenantManager tenantManager,
        SecurityContext securityContext,
        StorageFactory storageFactory,
        StorageManager storageManager,
        FolderEngine folderEngine,
        UserFolderEngine userFolderEngine,
        ApiHelper apiHelper,
        MailInfoDao mailInfoDao)
    {
        _autoreplyEngine = autoreplyEngine;
        _calendarEngine = calendarEngine;
        _indexEngine = indexEngine;
        _tagEngine = tagEngine;
        _crmLinkEngine = crmLinkEngine;
        _emailInEngine = emailInEngine;
        _filterEngine = filterEngine;
        _mailboxEngine = mailboxEngine;
        _messageEngine = messageEngine;

        _tenantManager = tenantManager;
        _securityContext = securityContext;
        _storageFactory = storageFactory;
        _folderEngine = folderEngine;
        _storageManager = storageManager;
        _userFolderEngine = userFolderEngine;
        _apiHelper = apiHelper;
        _mailInfoDao = mailInfoDao;
    }

    public void SetTenantAndUser(int tenant, string username)
    {
        _tenantManager.SetCurrentTenant(tenant);
        _securityContext.AuthenticateMe(new Guid(username));
    }
}
