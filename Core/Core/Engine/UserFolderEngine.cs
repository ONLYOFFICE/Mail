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


using ASC.Common;
using ASC.Common.Logging;
using ASC.Core;
using ASC.Mail.Core.Dao.Expressions.UserFolder;
using ASC.Mail.Core.Entities;
using ASC.Mail.Exceptions;
using ASC.Mail.Models;

using Microsoft.Extensions.Options;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ASC.Mail.Core.Engine
{
    [Scope]
    public class UserFolderEngine
    {
        private int Tenant => _tenantManager.GetCurrentTenant().TenantId;
        private string UserId => _securityContext.CurrentAccount.ID.ToString();

        private readonly ILog _log;
        private readonly IMailDaoFactory _mailDaoFactory;
        private readonly SecurityContext _securityContext;
        private readonly TenantManager _tenantManager;

        public UserFolderEngine(
            SecurityContext securityContext,
            TenantManager tenantManager,
            IMailDaoFactory mailDaoFactory,
            IOptionsMonitor<ILog> option)
        {
            _securityContext = securityContext;
            _tenantManager = tenantManager;
            _mailDaoFactory = mailDaoFactory;
            _log = option.Get("ASC.Mail.UserFolderEngine");
        }

        public MailUserFolderData Get(int id)
        {
            var userFolder = _mailDaoFactory.GetUserFolderDao().Get(id);

            return ToMailUserFolderData(userFolder);
        }

        public MailUserFolderData GetByMail(uint mailId)
        {
            var userFolder = _mailDaoFactory.GetUserFolderDao().GetByMail(mailId);

            return ToMailUserFolderData(userFolder);
        }

        public List<MailUserFolderData> GetList(List<int> ids = null, int? parentId = null)
        {
            var builder = SimpleUserFoldersExp.CreateBuilder(Tenant, UserId);

            if (ids != null && ids.Any())
            {
                builder.SetIds(ids);
            }

            if (parentId.HasValue)
            {
                builder.SetParent(parentId.Value);
            }

            var exp = builder.Build();

            var userFolderDataList = _mailDaoFactory.GetUserFolderDao().GetList(exp)
                .ConvertAll(ToMailUserFolderData);

            return userFolderDataList;
        }

        public MailUserFolderData Create(string name, int parentId = 0)
        {
            if (string.IsNullOrEmpty(name))
                throw new EmptyFolderException(@"Name is empty");

            var utsNow = DateTime.UtcNow;

            var newUserFolder = new UserFolder
            {
                Id = 0,
                ParentId = parentId,
                Name = name,
                User = UserId,
                Tenant = Tenant,
                TimeModified = utsNow
            };

            if (parentId > 0)
            {
                var parentUserFolder = _mailDaoFactory.GetUserFolderDao().Get(parentId);

                if (parentUserFolder == null)
                    throw new ArgumentException(@"Parent folder not found", "parentId");
            }

            if (IsFolderNameAlreadyExists(newUserFolder))
            {
                throw new AlreadyExistsFolderException(
                    string.Format("Folder with name \"{0}\" already exists", newUserFolder.Name));
            }

            using (var tx = _mailDaoFactory.BeginTransaction(IsolationLevel.ReadUncommitted))
            {
                newUserFolder.Id = _mailDaoFactory.GetUserFolderDao().Save(newUserFolder);

                if (newUserFolder.Id <= 0)
                    throw new Exception("Save user folder failed");

                var userFolderTreeItem = new UserFolderTreeItem
                {
                    FolderId = newUserFolder.Id,
                    ParentId = newUserFolder.Id,
                    Level = 0
                };

                //itself link
                _mailDaoFactory.GetUserFolderTreeDao().Save(userFolderTreeItem);

                //full path to root
                _mailDaoFactory.GetUserFolderTreeDao().InsertFullPathToRoot(newUserFolder.Id, newUserFolder.ParentId);

                tx.Commit();
            }

            _mailDaoFactory.GetUserFolderDao().RecalculateFoldersCount(newUserFolder.Id);

            return ToMailUserFolderData(newUserFolder);
        }

        public MailUserFolderData Update(int id, string name, int? parentId = null)
        {
            if (id < 0)
                throw new ArgumentException("id");

            if (string.IsNullOrEmpty(name))
                throw new EmptyFolderException(@"Name is empty");

            if (parentId.HasValue && id == parentId.Value)
                throw new ArgumentException(@"id equals to parentId", "parentId");

            var oldUserFolder = _mailDaoFactory.GetUserFolderDao().Get(id);

            if (oldUserFolder == null)
                throw new ArgumentException("Folder not found");

            var newUserFolder = new UserFolder
            {
                Id = id,
                ParentId = parentId ?? oldUserFolder.ParentId,
                Name = name,
                User = UserId,
                Tenant = Tenant,
                FolderCount = oldUserFolder.FolderCount,
                UnreadCount = oldUserFolder.UnreadCount,
                TotalCount = oldUserFolder.TotalCount,
                UnreadChainCount = oldUserFolder.UnreadChainCount,
                TotalChainCount = oldUserFolder.TotalChainCount,
                TimeModified = oldUserFolder.TimeModified
            };

            if (newUserFolder.Equals(oldUserFolder))
                return ToMailUserFolderData(oldUserFolder);

            if (IsFolderNameAlreadyExists(newUserFolder))
            {
                throw new AlreadyExistsFolderException(
                    string.Format("Folder with name \"{0}\" already exists", newUserFolder.Name));
            }

            var utsNow = DateTime.UtcNow;

            if (newUserFolder.ParentId != oldUserFolder.ParentId)
            {
                if (!CanMoveFolderTo(newUserFolder))
                {
                    throw new MoveFolderException(
                        string.Format("Can't move folder with id=\"{0}\" into the folder with id=\"{1}\"",
                            newUserFolder.Id, newUserFolder.ParentId));
                }
            }

            using (var tx = _mailDaoFactory.BeginTransaction(IsolationLevel.ReadUncommitted))
            {
                newUserFolder.TimeModified = utsNow;
                _mailDaoFactory.GetUserFolderDao().Save(newUserFolder);

                if (newUserFolder.ParentId != oldUserFolder.ParentId)
                {
                    _mailDaoFactory.GetUserFolderTreeDao().Move(newUserFolder.Id, newUserFolder.ParentId);
                }

                tx.Commit();
            }

            var recalcFolders = new List<int> { newUserFolder.ParentId };

            if (newUserFolder.ParentId > 0)
            {
                recalcFolders.Add(newUserFolder.Id);
            }

            if (oldUserFolder.ParentId != 0 && !recalcFolders.Contains(oldUserFolder.ParentId))
            {
                recalcFolders.Add(oldUserFolder.ParentId);
            }

            recalcFolders.ForEach(fid =>
            {
                _mailDaoFactory.GetUserFolderDao().RecalculateFoldersCount(fid);
            });

            return ToMailUserFolderData(newUserFolder);
        }

        public void SetFolderMessages(int userFolderId, List<int> ids)
        {
            _mailDaoFactory.GetUserFolderXMailDao().Remove(ids);

            _mailDaoFactory.GetUserFolderXMailDao().SetMessagesFolder(ids, userFolderId);
        }

        public void DeleteFolderMessages(IMailDaoFactory daoFactory, List<int> ids)
        {
            _mailDaoFactory.GetUserFolderXMailDao().Remove(ids);
        }

        public void RecalculateCounters(IMailDaoFactory mailDaoFactory, List<int> userFolderIds)
        {
            var totalUfMessList = _mailDaoFactory.GetMailInfoDao().GetMailUserFolderCount(userFolderIds);
            var unreadUfMessUfList = _mailDaoFactory.GetMailInfoDao().GetMailUserFolderCount(userFolderIds, true);
            var totalUfConvList = _mailDaoFactory.GetChainDao().GetChainUserFolderCount(userFolderIds);
            var unreadUfConvUfList = _mailDaoFactory.GetChainDao().GetChainUserFolderCount(userFolderIds, true);

            foreach (var id in userFolderIds)
            {
                totalUfMessList.TryGetValue(id, out int totalMess);

                unreadUfMessUfList.TryGetValue(id, out int unreadMess);

                totalUfConvList.TryGetValue(id, out int totalConv);

                unreadUfConvUfList.TryGetValue(id, out int unreadConv);

                SetFolderCounters(id,
                    unreadMess, totalMess, unreadConv, totalConv);
            }
        }

        public void SetFolderCounters(
            int userFolderId,
            int? unreadMess = null,
            int? totalMess = null,
            int? unreadConv = null,
            int? totalConv = null)
        {
            try
            {
                var res = _mailDaoFactory.GetUserFolderDao()
                    .SetFolderCounters(userFolderId, unreadMess, totalMess, unreadConv,
                        totalConv);

                //this action occurs during a transaction, therefore, saving changes is deferred until commit.
                //the result will always be 0.

                //if (res == 0)
                //throw new Exception("Need recalculation");
            }
            catch (Exception ex)
            {
                _log.ErrorFormat($"UserFolderEngine->SetFolderCounters() Exception: {ex}\nStack trace:\n{ex.StackTrace}");
                throw new Exception($"SetFolderCounters exception: folder id {userFolderId}", ex);
                //TODO: Think about recalculation
                //var engine = new EngineFactory(Tenant, User);
                //engine.OperationEngine.RecalculateFolders();
            }
        }

        public void ChangeFolderCounters(
            int userFolderId,
            int? unreadMessDiff = null,
            int? totalMessDiff = null,
            int? unreadConvDiff = null,
            int? totalConvDiff = null)
        {
            try
            {
                var res = _mailDaoFactory.GetUserFolderDao()
                    .ChangeFolderCounters(userFolderId, unreadMessDiff, totalMessDiff, unreadConvDiff,
                        totalConvDiff);

                if (res == 0)
                    throw new Exception("Need recalculation");
            }
            catch (Exception ex)
            {
                _log.ErrorFormat("UserFolderEngine->ChangeFolderCounters() Exception: {0}", ex.ToString());
                //TODO: Think about recalculation
                //var engine = new EngineFactory(Tenant, User);
                //engine.OperationEngine.RecalculateFolders();
            }
        }

        private bool IsFolderNameAlreadyExists(UserFolder newUserFolder)
        {
            //Find folder sub-folders
            var exp = SimpleUserFoldersExp.CreateBuilder(Tenant, UserId)
                .SetParent(newUserFolder.ParentId)
                .Build();

            var listExistinFolders = _mailDaoFactory.GetUserFolderDao().GetList(exp);

            return listExistinFolders.Any(existinFolder => existinFolder.Name.Equals(newUserFolder.Name,
                StringComparison.InvariantCultureIgnoreCase));
        }

        private bool CanMoveFolderTo(UserFolder newUserFolder)
        {
            //Find folder sub-folders
            var exp = SimpleUserFoldersExp.CreateBuilder(Tenant, UserId)
                .SetParent(newUserFolder.Id)
                .SetIds(new List<int> { newUserFolder.ParentId })
                .Build();

            var listExistinFolders = _mailDaoFactory.GetUserFolderDao().GetList(exp);

            return !listExistinFolders.Any();
        }

        // ReSharper disable once UnusedMember.Local
        private static UserFolder ToUserFolder(MailUserFolderData folder, int tenant, string user)
        {
            if (folder == null)
                return null;

            var utcNow = DateTime.UtcNow;

            var userFolder = new UserFolder
            {
                Tenant = tenant,
                User = user,
                Id = folder.Id,
                ParentId = folder.ParentId,
                Name = folder.Name,
                FolderCount = folder.FolderCount,
                UnreadCount = folder.UnreadCount,
                TotalCount = folder.TotalCount,
                UnreadChainCount = folder.UnreadChainCount,
                TotalChainCount = folder.TotalChainCount,
                TimeModified = utcNow
            };

            return userFolder;
        }

        private static MailUserFolderData ToMailUserFolderData(UserFolder folder)
        {
            if (folder == null)
                return null;

            var userFolderData = new MailUserFolderData
            {
                Id = folder.Id,
                ParentId = folder.ParentId,
                Name = folder.Name,
                FolderCount = folder.FolderCount,
                UnreadCount = folder.UnreadCount,
                TotalCount = folder.TotalCount,
                UnreadChainCount = folder.UnreadChainCount,
                TotalChainCount = folder.TotalChainCount
            };

            return userFolderData;
        }
    }
}
