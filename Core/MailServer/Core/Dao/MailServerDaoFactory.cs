using IMailboxDao = ASC.Mail.Server.Core.Dao.Interfaces.IMailboxDao;
using MailboxDao = ASC.Mail.Server.Core.Dao.MailboxDao;

namespace ASC.Mail.Core.MailServer.Core.Dao;

[Scope]
public class MailServerDaoFactory : IMailServerDaoFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly MailServerDbContext _mailServerDbContext;

    public MailServerDaoFactory(
        IServiceProvider serviceProvider,
        DbContextManager<MailServerDbContext> dbContextManager)
    {
        _serviceProvider = serviceProvider;
        _mailServerDbContext = dbContextManager.Get("mailServer");
    }

    public IDbContextTransaction BeginTransaction(IsolationLevel? level = null)
    {
        return level.HasValue
            ? _mailServerDbContext.Database.BeginTransaction(level.Value)
            : _mailServerDbContext.Database.BeginTransaction();
    }

    public void SetServerDbConnectionString(string serverCs)
    {
        _mailServerDbContext.Database.SetConnectionString(serverCs);
    }

    public MailServerDbContext GetContext()
    {
        return _mailServerDbContext;
    }

    public IAliasDao GetAliasDao()
    {
        return _serviceProvider.GetService<IAliasDao>();
    }

    public IDkimDao GetDkimDao()
    {
        return _serviceProvider.GetService<IDkimDao>();
    }

    public IDomainDao GetDomainDao()
    {
        return _serviceProvider.GetService<IDomainDao>();
    }

    public IMailboxDao GetMailboxDao()
    {
        return _serviceProvider.GetService<IMailboxDao>();
    }
}
public class MailServerDaoFactoryExtension
{
    public static void Register(DIHelper services)
    {
        services.TryAdd<IAliasDao, AliasDao>();
        services.TryAdd<IDkimDao, DkimDao>();
        services.TryAdd<IDomainDao, DomainDao>();
        services.TryAdd<IMailboxDao, MailboxDao>();
    }
}
