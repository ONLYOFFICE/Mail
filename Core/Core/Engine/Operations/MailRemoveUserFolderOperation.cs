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



using FolderType = ASC.Mail.Enums.FolderType;
using SecurityContext = ASC.Core.SecurityContext;

namespace ASC.Mail.Core.Engine.Operations;

public class MailRemoveUserFolderOperation : MailOperation
{
    public override MailOperationType OperationType
    {
        get { return MailOperationType.RemoveUserFolder; }
    }

    private readonly int _userFolderId;
    private readonly MessageEngine _messageEngine;
    private readonly IndexEngine _indexEngine;
    private readonly FactoryIndexer<MailMail> _factoryIndexer;
    private readonly IServiceProvider _serviceProvider;

    public MailRemoveUserFolderOperation(
        TenantManager tenantManager,
        SecurityContext securityContext,
        IMailDaoFactory mailDaoFactory,
        MessageEngine messageEngine,
        IndexEngine indexEngine,
        CoreSettings coreSettings,
        StorageManager storageManager,
        FactoryIndexer<MailMail> factoryIndexer,
        IServiceProvider serviceProvider,
        ILoggerProvider logProvider,
        int userFolderId)
        : base(tenantManager, securityContext, mailDaoFactory, coreSettings, storageManager, logProvider)
    {
        _messageEngine = messageEngine;
        _indexEngine = indexEngine;
        _factoryIndexer = factoryIndexer;
        _serviceProvider = serviceProvider;
        _userFolderId = userFolderId;

        SetSource(userFolderId.ToString());
    }

    protected override void Do()
    {
        try
        {
            SetProgress((int?)MailOperationRemoveUserFolderProgress.Init, "Setup tenant and user");

            TenantManager.SetCurrentTenant(CurrentTenant);

            SecurityContext.AuthenticateMe(CurrentUser);

            SetProgress((int?)MailOperationRemoveUserFolderProgress.DeleteFolders, "Delete folders");

            Delete(_userFolderId);

            SetProgress((int?)MailOperationRemoveUserFolderProgress.Finished);
        }
        catch (Exception e)
        {
            Log.ErrorMailOperationRemoveUserFolder(e.ToString());
            Error = "InternalServerError";
        }
    }

    public void Delete(int folderId)
    {
        var affectedIds = new List<int>();

        //TODO: Check or increase timeout for DB connection
        //using (var db = new DbManager(Defines.CONNECTION_STRING_NAME, Defines.RecalculateFoldersTimeout))

        var folder = MailDaoFactory.GetUserFolderDao().Get(folderId);
        if (folder == null)
            return;

        using (var tx = MailDaoFactory.BeginTransaction(IsolationLevel.ReadUncommitted))
        {
            //Find folder sub-folders
            var expTree = SimpleUserFoldersTreeExp.CreateBuilder()
                .SetParent(folder.Id)
                .Build();

            var removeFolderIds = MailDaoFactory.GetUserFolderTreeDao().Get(expTree)
                .ConvertAll(f => f.FolderId);

            if (!removeFolderIds.Contains(folderId))
                removeFolderIds.Add(folderId);

            //Remove folder with subfolders
            var expFolders = SimpleUserFoldersExp.CreateBuilder(CurrentTenant.Id, CurrentUser.ID.ToString())
                .SetIds(removeFolderIds)
                .Build();

            MailDaoFactory.GetUserFolderDao().Remove(expFolders);

            //Remove folder tree info
            expTree = SimpleUserFoldersTreeExp.CreateBuilder()
                .SetIds(removeFolderIds)
                .Build();

            MailDaoFactory.GetUserFolderTreeDao().Remove(expTree);

            //Move mails to trash
            foreach (var id in removeFolderIds)
            {
                var listMailIds = MailDaoFactory.GetUserFolderXMailDao().GetMailIds(id);

                if (!listMailIds.Any()) continue;

                affectedIds.AddRange(listMailIds);

                //Move mails to trash
                _messageEngine.SetFolder(MailDaoFactory, listMailIds, FolderType.Trash);

                //Remove listMailIds from 'mail_user_folder_x_mail'
                MailDaoFactory.GetUserFolderXMailDao().Remove(listMailIds);
            }

            tx.Commit();
        }

        MailDaoFactory.GetUserFolderDao().RecalculateFoldersCount(folder.ParentId);

        var t = _serviceProvider.GetService<MailMail>();
        if (!_factoryIndexer.Support(t) || !affectedIds.Any())
            return;

        var data = new MailMail
        {
            Folder = (byte)FolderType.Trash
        };

        _indexEngine.Update(data, s => s.In(m => m.Id, affectedIds.ToArray()), wrapper => wrapper.Unread);
    }
}
