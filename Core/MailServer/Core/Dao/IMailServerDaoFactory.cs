using IMailboxDao = ASC.Mail.Server.Core.Dao.Interfaces.IMailboxDao;

namespace ASC.Mail.Core.MailServer.Core.Dao;

[Scope(typeof(MailServerDaoFactory), Additional = typeof(MailServerDaoFactoryExtension))]
public interface IMailServerDaoFactory
{
    MailServerDbContext GetContext();

    void SetServerDbConnectionString(string serverCs);

    IAliasDao GetAliasDao();

    IDkimDao GetDkimDao();

    IDomainDao GetDomainDao();

    IMailboxDao GetMailboxDao();

    public IDbContextTransaction BeginTransaction(System.Data.IsolationLevel? level = null);
}

