/*
 *
 * (c) Copyright Ascensio System Limited 2010-2020
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
public class UserFolderDao : BaseMailDao, IUserFolderDao
{
    public UserFolderDao(
         TenantManager tenantManager,
         SecurityContext securityContext,
         MailDbContext dbContext)
        : base(tenantManager, securityContext, dbContext)
    {
    }

    public UserFolder Get(int id)
    {
        var userFolder = MailDbContext.MailUserFolders
            .AsNoTracking()
            .Where(f => f.TenantId == Tenant && f.IdUser == UserId && f.Id == id)
            .Select(ToUserFolder)
            .SingleOrDefault();

        return userFolder;
    }

    public UserFolder GetByName(string name, int parentId=0)
    {
        var userFolder = MailDbContext.MailUserFolders
            .AsNoTracking()
            .Where(f => f.TenantId == Tenant && f.IdUser == UserId && f.Name == name && f.ParentId == parentId)
            .Select(ToUserFolder)
            .SingleOrDefault();

        return userFolder;
    }

    public UserFolder GetByMail(uint mailId)
    {
        var folderId = MailDbContext.MailUserFolderXMails
            .AsNoTracking()
            .Where(ufxm => ufxm.IdMail == mailId)
            .Select(ufxm => ufxm.IdFolder)
            .Distinct()
            .SingleOrDefault();

        if (folderId == 0)
            return null;

        var userFolder = MailDbContext.MailUserFolders
            .Where(f => f.Id == folderId)
            .Select(ToUserFolder)
            .SingleOrDefault();

        return userFolder;
    }

    public List<UserFolder> GetList(IUserFoldersExp exp)
    {
        var query = MailDbContext.MailUserFolders
            .AsNoTracking()
            .Where(exp.GetExpression())
            .Select(ToUserFolder);

        if (exp.StartIndex.HasValue)
        {
            query.Skip(exp.StartIndex.Value);
        }

        if (exp.Limit.HasValue)
        {
            query.Take(exp.Limit.Value);
        }

        if (!string.IsNullOrEmpty(exp.OrderBy))
        {
            if (exp.OrderAsc != null && exp.OrderAsc.Value)
            {
                if (exp.OrderBy == "timeModified")
                    query.OrderBy(uf => uf.TimeModified);
                else
                    query.OrderBy(uf => uf.Name);
            }
            else
            {
                if (exp.OrderBy == "timeModified")
                    query.OrderByDescending(uf => uf.TimeModified);
                else
                    query.OrderByDescending(uf => uf.Name);
            }
        }

        var list = query.ToList();

        return list;
    }

    public UserFolder GetRootFolder(int folderId)
    {
        var parentId = MailDbContext.MailUserFolderTrees
            .AsNoTracking()
            .Where(t => t.FolderId == folderId)
            .OrderByDescending(t => t.Level)
            .Select(t => t.ParentId)
            .FirstOrDefault();

        if (parentId == 0)
            return null;

        var userFolder = MailDbContext.MailUserFolders
            .Where(f => f.Id == parentId)
            .Select(ToUserFolder)
            .SingleOrDefault();

        return userFolder;
    }

    public int Save(UserFolder folder)
    {
        var mailUserFolder = new MailUserFolder
        {
            Id = folder.Id,
            ParentId = folder.ParentId,
            TenantId = folder.Tenant,
            IdUser = folder.User,
            Name = folder.Name,
            FoldersCount = (uint)folder.FolderCount,
            UnreadMessagesCount = (uint)folder.UnreadCount,
            TotalMessagesCount = (uint)folder.TotalCount,
            UnreadConversationsCount = (uint)folder.UnreadChainCount,
            TotalConversationsCount = (uint)folder.TotalCount,
            ModifiedOn = folder.TimeModified
        };

        var entry = MailDbContext.AddOrUpdate(MailDbContext.MailUserFolders, mailUserFolder);

        MailDbContext.SaveChanges();

        return entry.Id;
    }

    public int Remove(int id)
    {
        var mailUserFolder = new MailUserFolder
        {
            Id = id,
            TenantId = Tenant,
            IdUser = UserId,
        };

        MailDbContext.MailUserFolders.Remove(mailUserFolder);

        var count = MailDbContext.SaveChanges();

        return count;
    }

    public int Remove(IUserFoldersExp exp)
    {
        var deleteQuery = MailDbContext.MailUserFolders.Where(exp.GetExpression());

        MailDbContext.MailUserFolders.RemoveRange(deleteQuery);

        var count = MailDbContext.SaveChanges();

        return count;
    }

    public void RecalculateFoldersCount(int id)
    {
        var toUpdate = MailDbContext.MailUserFolders
            .Where(uf => MailDbContext.MailUserFolderTrees
                .Where(t => t.FolderId == id)
                .Select(t => t.ParentId)
                .Any(pId => pId == uf.Id)
            )
            .ToList();

        foreach (var f in toUpdate)
        {
            var count = MailDbContext.MailUserFolderTrees
                .Where(r => r.ParentId == f.Id)
                .Count() - 1;

            f.FoldersCount = (uint)count;
        }

        var result = MailDbContext.SaveChanges();
    }

    public int SetFolderCounters(int folderId, int? unreadMess = null, int? totalMess = null,
        int? unreadConv = null, int? totalConv = null)
    {
        if (!unreadMess.HasValue
            && !totalMess.HasValue
            && !unreadConv.HasValue
            && !totalConv.HasValue)
        {
            return -1;
        }

        var userFolder = MailDbContext.MailUserFolders
            .Where(uf => uf.TenantId == Tenant && uf.IdUser == UserId && uf.Id == folderId)
            .SingleOrDefault();

        if (userFolder == null)
            return -1;

        if (unreadMess.HasValue)
            userFolder.UnreadMessagesCount = (uint)unreadMess.Value;

        if (totalMess.HasValue)
            userFolder.TotalMessagesCount = (uint)totalMess.Value;

        if (unreadConv.HasValue)
            userFolder.UnreadConversationsCount = (uint)unreadConv.Value;

        if (totalConv.HasValue)
            userFolder.TotalConversationsCount = (uint)totalConv.Value;

        var result = MailDbContext.SaveChanges();

        return result;
    }

    public int ChangeFolderCounters(int folderId, int? unreadMessDiff = null, int? totalMessDiff = null,
        int? unreadConvDiff = null, int? totalConvDiff = null)
    {
        if (!unreadMessDiff.HasValue
            && !totalMessDiff.HasValue
            && !unreadConvDiff.HasValue
            && !totalConvDiff.HasValue)
        {
            return -1;
        }

        var userFolder = MailDbContext.MailUserFolders
            .Where(uf => uf.TenantId == Tenant && uf.IdUser == UserId && uf.Id == folderId)
            .SingleOrDefault();

        if (userFolder == null)
            return -1;

        if (unreadMessDiff.HasValue)
        {
            if (unreadMessDiff.Value == 0)
                userFolder.UnreadMessagesCount = (uint)unreadMessDiff.Value;
            else
                userFolder.UnreadMessagesCount += (uint)unreadMessDiff.Value;
        }

        if (totalMessDiff.HasValue)
        {
            if (totalMessDiff.Value == 0)
                userFolder.TotalMessagesCount = (uint)totalMessDiff.Value;
            else
                userFolder.TotalMessagesCount += (uint)totalMessDiff.Value;
        }

        if (unreadConvDiff.HasValue)
        {
            if (unreadConvDiff.Value == 0)
                userFolder.UnreadConversationsCount = (uint)unreadConvDiff.Value;
            else
                userFolder.UnreadConversationsCount += (uint)unreadConvDiff.Value;
        }

        if (totalConvDiff.HasValue)
        {
            if (totalConvDiff.Value == 0)
                userFolder.TotalConversationsCount = (uint)totalConvDiff.Value;
            else
                userFolder.TotalConversationsCount += (uint)totalConvDiff.Value;
        }

        var result = MailDbContext.SaveChanges();

        return result;
    }

    protected UserFolder ToUserFolder(MailUserFolder r)
    {
        var folder = new UserFolder
        {
            Id = r.Id,
            ParentId = r.ParentId,

            Tenant = r.TenantId,
            User = r.IdUser,

            Name = r.Name,
            FolderCount = (int)r.FoldersCount,

            UnreadCount = (int)r.UnreadMessagesCount,
            TotalCount = (int)r.TotalMessagesCount,

            UnreadChainCount = (int)r.UnreadConversationsCount,
            TotalChainCount = (int)r.TotalConversationsCount,

            TimeModified = r.ModifiedOn
        };

        return folder;
    }
}
