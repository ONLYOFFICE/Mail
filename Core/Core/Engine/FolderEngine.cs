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
using ASC.Mail.Core.Dao.Expressions.Conversation;
using ASC.Mail.Core.Dao.Expressions.Message;
using ASC.Mail.Core.Dao.Expressions.UserFolder;
using ASC.Mail.Core.Engine.Operations.Base;
using ASC.Mail.Core.Entities;
using ASC.Mail.Enums;

using Microsoft.Extensions.Options;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ASC.Mail.Core.Engine
{
    [Scope]
    public class FolderEngine
    {
        private int Tenant => _tenantManager.GetCurrentTenant().TenantId;
        private string User => _securityContext.CurrentAccount.ID.ToString();

        private readonly SecurityContext _securityContext;
        private readonly TenantManager _tenantManager;
        private readonly IMailDaoFactory _mailDaoFactory;
        private readonly UserFolderEngine _userFolderEngine;
        private readonly ILog _log;

        public class MailFolderInfo
        {
            public FolderType id;
            public DateTime timeModified;
            public int unread;
            public int unreadMessages;
            public int total;
            public int totalMessages;
        }

        public FolderEngine(
            SecurityContext securityContext,
            TenantManager tenantManager,
            UserFolderEngine userFolderEngine,
            IOptionsMonitor<ILog> option,
            IMailDaoFactory mailDaoFactory)
        {
            _securityContext = securityContext;
            _tenantManager = tenantManager;
            _mailDaoFactory = mailDaoFactory;
            _userFolderEngine = userFolderEngine;
            _log = option.Get("ASC.Mail.FolderEngine");
        }

        public List<MailFolderInfo> GetFolders(string userId = null)
        {
            List<MailFolderInfo> folders;

            var needRecalculation = false;

            var folderList = userId == null
                ? _mailDaoFactory.GetFolderDao().GetFolders()
                : _mailDaoFactory.GetFolderDao().GetFolders(userId);

            foreach (var folder in DefaultFolders)
            {
                if (folderList.Exists(f => f.FolderType == folder))
                    continue;

                needRecalculation = true;

                var newFolder = new Folder
                {
                    FolderType = folder,
                    Tenant = Tenant,
                    UserId = User,
                    TotalCount = 0,
                    UnreadCount = 0,
                    UnreadChainCount = 0,
                    TotalChainCount = 0,
                    TimeModified = DateTime.UtcNow
                };

                _mailDaoFactory.GetFolderDao().Save(newFolder);

                folderList.Add(newFolder);
            }

            folders = folderList
                .ConvertAll(x => new MailFolderInfo
                {
                    id = x.FolderType,
                    timeModified = x.TimeModified,
                    unread = x.UnreadChainCount,
                    unreadMessages = x.UnreadCount,
                    total = x.TotalChainCount,
                    totalMessages = x.TotalCount
                });

            if (!needRecalculation)
                return folders;

            //TODO: Fix OperationEngine.RecalculateFolders();

            return folders;
        }

        public void ChangeFolderCounters(
            FolderType folder,
            int? userFolder = null,
            int? unreadMessDiff = null,
            int? totalMessDiff = null,
            int? unreadConvDiff = null,
            int? totalConvDiff = null)
        {
            if (folder == FolderType.UserFolder && !userFolder.HasValue)
                throw new ArgumentException(@"ChangeFolderCounters failed", "userFolder");

            try
            {
                var res = _mailDaoFactory.GetFolderDao()
                    .ChangeFolderCounters(folder, unreadMessDiff, totalMessDiff, unreadConvDiff, totalConvDiff);

                if (res == 0)
                {
                    var totalCount = totalMessDiff.GetValueOrDefault(0);
                    var unreadCount = unreadMessDiff.GetValueOrDefault(0);
                    var unreadChainCount = unreadConvDiff.GetValueOrDefault(0);
                    var totalChainCount = totalConvDiff.GetValueOrDefault(0);

                    if (totalCount < 0 || unreadCount < 0 || unreadChainCount < 0 || totalChainCount < 0)
                        throw new Exception("Need recalculation");

                    var f = _mailDaoFactory.GetFolderDao().GetFolder(folder);

                    if (f == null)
                    {
                        // Folder is not found

                        res = _mailDaoFactory.GetFolderDao().Save(new Folder
                        {
                            FolderType = folder,
                            Tenant = Tenant,
                            UserId = User,
                            TotalCount = totalCount,
                            UnreadCount = unreadCount,
                            UnreadChainCount = unreadChainCount,
                            TotalChainCount = totalChainCount,
                            TimeModified = DateTime.UtcNow
                        });

                        if (res == 0)
                            throw new Exception("Need recalculation");
                    }
                    else
                    {
                        throw new Exception("Need recalculation");
                    }

                }
            }
            catch (Exception ex)
            {
                _log.ErrorFormat("ChangeFolderCounters() Exception: {0}", ex.ToString());
                //TODO: Fix OperationEngine.RecalculateFolders();
            }

            if (!userFolder.HasValue)
                return;

            _userFolderEngine.ChangeFolderCounters(userFolder.Value, unreadMessDiff, totalMessDiff, unreadConvDiff, totalConvDiff);
        }

        public void RecalculateFolders(Action<MailOperationRecalculateMailboxProgress> callback = null)
        {
            using var tx = _mailDaoFactory.BeginTransaction(IsolationLevel.ReadUncommitted);

            var folderTypes = Enum.GetValues(typeof(FolderType)).Cast<int>();

            callback?.Invoke(MailOperationRecalculateMailboxProgress.CountUnreadMessages);

            var unreadMessagesCountByFolder =
                    _mailDaoFactory.GetMailInfoDao().GetMailCount(
                        SimpleMessagesExp.CreateBuilder(Tenant, User)
                            .SetUnread(true)
                            .Build());

            callback?.Invoke(MailOperationRecalculateMailboxProgress.CountTotalMessages);

            var totalMessagesCountByFolder = _mailDaoFactory.GetMailInfoDao().GetMailCount(
                    SimpleMessagesExp.CreateBuilder(Tenant, User)
                        .Build());

            callback?.Invoke(MailOperationRecalculateMailboxProgress.CountUreadConversation);

            var unreadConversationsCountByFolder =
                _mailDaoFactory.GetChainDao().GetChainCount(
                    SimpleConversationsExp.CreateBuilder(Tenant, User)
                        .SetUnread(true)
                        .Build());

            if (callback != null)
                callback(MailOperationRecalculateMailboxProgress.CountTotalConversation);

            var totalConversationsCountByFolder =
            _mailDaoFactory.GetChainDao().GetChainCount(
                SimpleConversationsExp.CreateBuilder(Tenant, User)
                    .Build());

            callback?.Invoke(MailOperationRecalculateMailboxProgress.UpdateFoldersCounters);

            var now = DateTime.UtcNow;

            var folders = (from folderId in folderTypes
                           let unreadMessCount =
                               unreadMessagesCountByFolder.ContainsKey(folderId)
                                   ? unreadMessagesCountByFolder[folderId]
                                   : 0
                           let totalMessCount =
                               totalMessagesCountByFolder.ContainsKey(folderId)
                                   ? totalMessagesCountByFolder[folderId]
                                   : 0
                           let unreadConvCount =
                               unreadConversationsCountByFolder.ContainsKey(folderId)
                                   ? unreadConversationsCountByFolder[folderId]
                                   : 0
                           let totalConvCount =
                               totalConversationsCountByFolder.ContainsKey(folderId)
                                   ? totalConversationsCountByFolder[folderId]
                                   : 0
                           select new Folder
                           {
                               FolderType = (FolderType)folderId,
                               Tenant = Tenant,
                               UserId = User,
                               UnreadCount = unreadMessCount,
                               UnreadChainCount = unreadConvCount,
                               TotalCount = totalMessCount,
                               TotalChainCount = totalConvCount,
                               TimeModified = now
                           })
                .ToList();

            foreach (var folder in folders)
            {
                _mailDaoFactory.GetFolderDao().Save(folder);
            }

            var userFolder = folders.FirstOrDefault(f => f.FolderType == FolderType.UserFolder);

            if (userFolder != null)
            {
                var userFolders =
                    _mailDaoFactory.GetUserFolderDao().GetList(
                        SimpleUserFoldersExp.CreateBuilder(Tenant, User)
                            .Build());

                if (userFolders.Any())
                {
                    var totalMessagesCountByUserFolder = _mailDaoFactory.GetMailInfoDao().GetMailUserFolderCount();

                    callback?.Invoke(MailOperationRecalculateMailboxProgress.CountTotalUserFolderMessages);

                    var unreadMessagesCountByUserFolder = _mailDaoFactory.GetMailInfoDao().GetMailUserFolderCount(true);

                    callback?.Invoke(MailOperationRecalculateMailboxProgress.CountUnreadUserFolderMessages);

                    var totalConversationsCountByUserFolder = _mailDaoFactory.GetChainDao().GetChainUserFolderCount();

                    callback?.Invoke(MailOperationRecalculateMailboxProgress.CountTotalUserFolderConversation);

                    var unreadConversationsCountByUserFolder = _mailDaoFactory.GetChainDao().GetChainUserFolderCount(true);

                    callback?.Invoke(MailOperationRecalculateMailboxProgress.CountUreadUserFolderConversation);

                    var newUserFolders = (from folder in userFolders
                                          let unreadMessCount =
                                              unreadMessagesCountByUserFolder.ContainsKey(folder.Id)
                                                  ? unreadMessagesCountByUserFolder[folder.Id]
                                                  : 0
                                          let totalMessCount =
                                              totalMessagesCountByUserFolder.ContainsKey(folder.Id)
                                                  ? totalMessagesCountByUserFolder[folder.Id]
                                                  : 0
                                          let unreadConvCount =
                                              unreadConversationsCountByUserFolder.ContainsKey(folder.Id)
                                                  ? unreadConversationsCountByUserFolder[folder.Id]
                                                  : 0
                                          let totalConvCount =
                                              totalConversationsCountByUserFolder.ContainsKey(folder.Id)
                                                  ? totalConversationsCountByUserFolder[folder.Id]
                                                  : 0
                                          select new UserFolder
                                          {
                                              Id = folder.Id,
                                              ParentId = folder.ParentId,
                                              Name = folder.Name,
                                              FolderCount = folder.FolderCount,
                                              Tenant = Tenant,
                                              User = User,
                                              UnreadCount = unreadMessCount,
                                              UnreadChainCount = unreadConvCount,
                                              TotalCount = totalMessCount,
                                              TotalChainCount = totalConvCount,
                                              TimeModified = now
                                          })
                        .ToList();

                    callback?.Invoke(MailOperationRecalculateMailboxProgress.UpdateUserFoldersCounters);

                    foreach (var folder in newUserFolders)
                    {
                        _mailDaoFactory.GetUserFolderDao().Save(folder);
                    }
                }
            }

            tx.Commit();
        }

        public static List<FolderType> DefaultFolders
        {
            get
            {
                return ((FolderType[])Enum.GetValues(typeof(FolderType)))
                    .Where(folderType => folderType != FolderType.Sending && folderType != FolderType.UserFolder)
                    .ToList();
            }
        }

    }
}
