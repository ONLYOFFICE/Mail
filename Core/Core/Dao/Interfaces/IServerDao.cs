namespace ASC.Mail.Core.Dao.Interfaces;

[Scope(typeof(ServerDao))]
public interface IServerDao
{
    ASC.Core.Common.EF.Model.Mail.ServerServer Get(int tenant);
    List<ASC.Core.Common.EF.Model.Mail.ServerServer> GetList();
    int Link(ASC.Core.Common.EF.Model.Mail.ServerServer server, int tenant);
    int UnLink(ASC.Core.Common.EF.Model.Mail.ServerServer server, int tenant);
    int Save(ASC.Core.Common.EF.Model.Mail.ServerServer server);
    int Delete(int id);
}
