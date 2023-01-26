namespace ASC.Mail.Core.Dao.Interfaces;

[Scope(typeof(ServerDao))]
public interface IServerDao
{
    MailServerServer Get(int tenant);
    List<MailServerServer> GetList();
    int Link(MailServerServer server, int tenant);
    int UnLink(MailServerServer server, int tenant);
    int Save(MailServerServer server);
    int Delete(int id);
}
