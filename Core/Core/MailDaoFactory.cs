using IMailboxDao = ASC.Mail.Core.Dao.Interfaces.IMailboxDao;
using MailboxDao = ASC.Mail.Core.Dao.MailboxDao;

namespace ASC.Mail.Core;

[Scope]
public class MailDaoFactory : IMailDaoFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly MailDbContext _mailDbContext;

    public MailDaoFactory(
        IServiceProvider serviceProvider,
        MailDbContext dbContextManager)
    {
        _serviceProvider = serviceProvider;
        _mailDbContext = dbContextManager;
    }

    public MailDbContext GetContext()
    {
        return _mailDbContext;
    }

    public IAccountDao GetAccountDao()
    {
        return _serviceProvider.GetService<IAccountDao>();
    }

    public IAlertDao GetAlertDao()
    {
        return _serviceProvider.GetService<IAlertDao>();
    }

    public IAttachmentDao GetAttachmentDao()
    {
        return _serviceProvider.GetService<IAttachmentDao>();
    }

    public IChainDao GetChainDao()
    {
        return _serviceProvider.GetService<IChainDao>();
    }

    public IContactCardDao GetContactCardDao()
    {
        return _serviceProvider.GetService<IContactCardDao>();
    }

    public IContactDao GetContactDao()
    {
        return _serviceProvider.GetService<IContactDao>();
    }

    public IContactInfoDao GetContactInfoDao()
    {
        return _serviceProvider.GetService<IContactInfoDao>();
    }

    public ICrmContactDao GetCrmContactDao()
    {
        return _serviceProvider.GetService<ICrmContactDao>();
    }

    public ICrmLinkDao GetCrmLinkDao()
    {
        return _serviceProvider.GetService<ICrmLinkDao>();
    }

    public IDisplayImagesAddressDao GetDisplayImagesAddressDao()
    {
        return _serviceProvider.GetService<IDisplayImagesAddressDao>();
    }

    public IFilterDao GetFilterDao()
    {
        return _serviceProvider.GetService<IFilterDao>();
    }

    public IFolderDao GetFolderDao()
    {
        return _serviceProvider.GetService<IFolderDao>();
    }

    public IImapFlagsDao GetImapFlagsDao()
    {
        return _serviceProvider.GetService<IImapFlagsDao>();
    }

    public IImapSpecialMailboxDao GetImapSpecialMailboxDao()
    {
        return _serviceProvider.GetService<IImapSpecialMailboxDao>();
    }

    public IMailboxAutoreplyDao GetMailboxAutoreplyDao()
    {
        return _serviceProvider.GetService<IMailboxAutoreplyDao>();
    }

    public IMailboxAutoreplyHistoryDao GetMailboxAutoreplyHistoryDao()
    {
        return _serviceProvider.GetService<IMailboxAutoreplyHistoryDao>();
    }

    public IMailboxDao GetMailboxDao()
    {
        return _serviceProvider.GetService<IMailboxDao>();
    }

    public IMailboxDomainDao GetMailboxDomainDao()
    {
        return _serviceProvider.GetService<IMailboxDomainDao>();
    }

    public IMailboxProviderDao GetMailboxProviderDao()
    {
        return _serviceProvider.GetService<IMailboxProviderDao>();
    }

    public IMailboxServerDao GetMailboxServerDao()
    {
        return _serviceProvider.GetService<IMailboxServerDao>();
    }

    public IMailboxSignatureDao GetMailboxSignatureDao()
    {
        return _serviceProvider.GetService<IMailboxSignatureDao>();
    }

    public IMailDao GetMailDao()
    {
        return _serviceProvider.GetService<IMailDao>();
    }

    public IMailGarbageDao GetMailGarbageDao()
    {
        return _serviceProvider.GetService<IMailGarbageDao>();
    }

    public IMailInfoDao GetMailInfoDao()
    {
        return _serviceProvider.GetService<IMailInfoDao>();
    }

    public IServerAddressDao GetServerAddressDao()
    {
        return _serviceProvider.GetService<IServerAddressDao>();
    }

    public IServerDao GetServerDao()
    {
        return _serviceProvider.GetService<IServerDao>();
    }

    public IServerDnsDao GetServerDnsDao()
    {
        return _serviceProvider.GetService<IServerDnsDao>();
    }

    public IServerDomainDao GetServerDomainDao()
    {
        return _serviceProvider.GetService<IServerDomainDao>();
    }

    public IServerGroupDao GetServerGroupDao()
    {
        return _serviceProvider.GetService<IServerGroupDao>();
    }

    public ITagAddressDao GetTagAddressDao()
    {
        return _serviceProvider.GetService<ITagAddressDao>();
    }

    public ITagDao GetTagDao()
    {
        return _serviceProvider.GetService<ITagDao>();
    }

    public ITagMailDao GetTagMailDao()
    {
        return _serviceProvider.GetService<ITagMailDao>();
    }

    public IUserFolderDao GetUserFolderDao()
    {
        return _serviceProvider.GetService<IUserFolderDao>();
    }

    public IUserFolderTreeDao GetUserFolderTreeDao()
    {
        return _serviceProvider.GetService<IUserFolderTreeDao>();
    }

    public IUserFolderXMailDao GetUserFolderXMailDao()
    {
        return _serviceProvider.GetService<IUserFolderXMailDao>();
    }

    public IDbContextTransaction BeginTransaction(System.Data.IsolationLevel? level = null)
    {
        return level.HasValue ? _mailDbContext.Database.BeginTransaction(level.Value) : _mailDbContext.Database.BeginTransaction();
    }
}
public class MailDaoFactoryExtension
{
    public static void Register(DIHelper services)
    {
        services.TryAdd<IAccountDao, AccountDao>();
        services.TryAdd<IAlertDao, AlertDao>();
        services.TryAdd<IAttachmentDao, AttachmentDao>();
        services.TryAdd<IChainDao, ChainDao>();
        services.TryAdd<IContactCardDao, ContactCardDao>();
        services.TryAdd<IContactDao, ContactDao>();
        services.TryAdd<IContactInfoDao, ContactInfoDao>();
        services.TryAdd<ICrmContactDao, CrmContactDao>();
        services.TryAdd<ICrmLinkDao, CrmLinkDao>();
        services.TryAdd<IDisplayImagesAddressDao, DisplayImagesAddressDao>();
        services.TryAdd<IFilterDao, FilterDao>();
        services.TryAdd<IFolderDao, FolderDao>();
        services.TryAdd<IImapFlagsDao, ImapFlagsDao>();
        services.TryAdd<IImapSpecialMailboxDao, ImapSpecialMailboxDao>();
        services.TryAdd<IMailboxAutoreplyDao, MailboxAutoreplyDao>();
        services.TryAdd<IMailboxAutoreplyHistoryDao, MailboxAutoreplyHistoryDao>();
        services.TryAdd<IMailboxDao, MailboxDao>();
        services.TryAdd<IMailboxDomainDao, MailboxDomainDao>();
        services.TryAdd<IMailboxProviderDao, MailboxProviderDao>();
        services.TryAdd<IMailboxServerDao, MailboxServerDao>();
        services.TryAdd<IMailboxSignatureDao, MailboxSignatureDao>();
        services.TryAdd<IMailDao, MailDao>();
        services.TryAdd<IMailGarbageDao, MailGarbageDao>();
        services.TryAdd<IMailInfoDao, MailInfoDao>();
        services.TryAdd<IServerAddressDao, ServerAddressDao>();
        services.TryAdd<IServerDao, ServerDao>();
        services.TryAdd<IServerDnsDao, ServerDnsDao>();
        services.TryAdd<IServerDomainDao, ServerDomainDao>();
        services.TryAdd<IServerGroupDao, ServerGroupDao>();
        services.TryAdd<ITagAddressDao, TagAddressDao>();
        services.TryAdd<ITagDao, TagDao>();
        services.TryAdd<ITagMailDao, TagMailDao>();
        services.TryAdd<IUserFolderDao, UserFolderDao>();
        services.TryAdd<IUserFolderTreeDao, UserFolderTreeDao>();
        services.TryAdd<IUserFolderXMailDao, UserFolderXMailDao>();
    }
}
