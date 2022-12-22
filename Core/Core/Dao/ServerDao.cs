/*
 *
 * (c) Copyright Ascensio System Limited 2010-2018
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

using SecurityContext = ASC.Core.SecurityContext;

namespace ASC.Mail.Core.Dao;

[Scope]
public class ServerDao : BaseMailDao, IServerDao
{
    public ServerDao(
         TenantManager tenantManager,
         SecurityContext securityContext,
         MailDbContext dbContext)
        : base(tenantManager, securityContext, dbContext)
    {
    }

    private const string SERVER_ALIAS = "ms";
    private const string SERVER_X_TENANT_ALIAS = "st";

    public MailServerServer Get(int tenant)
    {
        var server = MailDbContext.MailServerServers
            .AsNoTracking()
            .Join(MailDbContext.MailServerServerXTenants, s => s.Id, x => x.IdServer,
                (s, x) => new
                {
                    Server = s,
                    Xtenant = x
                })
            .Where(o => o.Xtenant.IdTenant == tenant)
            .Select(o => ToServer(o.Server))
            .FirstOrDefault();

        return server;
    }

    public List<MailServerServer> GetList()
    {
        var list = MailDbContext.MailServerServers
            .AsNoTracking()
            .Select(ToServer)
            .ToList();

        return list;
    }

    public int Link(MailServerServer server, int tenant)
    {
        var xItem = new MailServerServerXTenant
        {
            IdServer = server.Id,
            IdTenant = tenant
        };

        MailDbContext.AddOrUpdate(t => t.MailServerServerXTenants, xItem);

        var result = MailDbContext.SaveChanges();

        return result;
    }

    public int UnLink(MailServerServer server, int tenant)
    {
        var deleteItem = new MailServerServerXTenant
        {
            IdServer = server.Id,
            IdTenant = tenant
        };

        MailDbContext.MailServerServerXTenants.Remove(deleteItem);

        var result = MailDbContext.SaveChanges();

        return result;
    }

    public int Save(MailServerServer server)
    {
        var mailServer = new MailServerServer
        {
            Id = server.Id,
            MxRecord = server.MxRecord,
            ConnectionString = server.ConnectionString,
            ServerType = server.ServerType,
            SmtpSettingsId = server.SmtpSettingsId,
            ImapSettingsId = server.ImapSettingsId
        };

        var entry = MailDbContext.AddOrUpdate(t => t.MailServerServers, mailServer);

        MailDbContext.SaveChanges();

        return entry.Id;
    }

    public int Delete(int id)
    {
        var deleteItem = new MailServerServerXTenant
        {
            IdServer = id
        };

        MailDbContext.MailServerServerXTenants.Remove(deleteItem);

        MailDbContext.SaveChanges();

        var mailServer = new MailServerServer
        {
            Id = id
        };

        MailDbContext.MailServerServers.Remove(mailServer);

        var result = MailDbContext.SaveChanges();

        return result;
    }

    protected static MailServerServer ToServer(MailServerServer r)
    {
        var s = new MailServerServer
        {
            Id = r.Id,
            MxRecord = r.MxRecord,
            ConnectionString = r.ConnectionString,
            ServerType = r.ServerType,
            SmtpSettingsId = r.SmtpSettingsId,
            ImapSettingsId = r.ImapSettingsId
        };

        return s;
    }
}
