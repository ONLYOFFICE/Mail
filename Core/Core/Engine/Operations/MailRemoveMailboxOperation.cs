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

namespace ASC.Mail.Core.Engine.Operations;

public class MailRemoveMailboxOperation : MailOperation
{
    public override MailOperationType OperationType
    {
        get { return MailOperationType.RemoveMailbox; }
    }

    private readonly MailBoxData _mailBoxData;
    private readonly MailboxEngine _mailboxEngine;
    private readonly QuotaEngine _quotaEngine;
    private readonly FolderEngine _folderEngine;
    private readonly CacheEngine _cacheEngine;
    private readonly IndexEngine _indexEngine;

    public MailRemoveMailboxOperation(
        TenantManager tenantManager,
        SecurityContext securityContext,
        MailboxEngine mailboxEngine,
        QuotaEngine quotaEngine,
        FolderEngine folderEngine,
        CacheEngine cacheEngine,
        IndexEngine indexEngine,
        IMailDaoFactory mailDaoFactory,
        CoreSettings coreSettings,
        StorageManager storageManager,
        ILoggerProvider logProvider,
        MailBoxData mailBoxData)
        : base(tenantManager, securityContext, mailDaoFactory, coreSettings, storageManager, logProvider)
    {
        _mailboxEngine = mailboxEngine;
        _quotaEngine = quotaEngine;
        _folderEngine = folderEngine;
        _cacheEngine = cacheEngine;
        _indexEngine = indexEngine;
        _mailBoxData = mailBoxData;

        SetSource(_mailBoxData.MailBoxId.ToString());
    }

    protected override void Do()
    {
        try
        {
            SetProgress((int?)MailOperationRemoveMailboxProgress.Init, "Setup tenant and user");

            TenantManager.SetCurrentTenant(CurrentTenant);

            SecurityContext.AuthenticateMe(CurrentUser);

            SetProgress((int?)MailOperationRemoveMailboxProgress.RemoveFromDb, "Remove mailbox from Db");

            var freedQuotaSize = _mailboxEngine.RemoveMailBoxInfo(_mailBoxData);

            SetProgress((int?)MailOperationRemoveMailboxProgress.FreeQuota, "Decrease newly freed quota space");

            _quotaEngine.QuotaUsedDelete(freedQuotaSize);

            SetProgress((int?)MailOperationRemoveMailboxProgress.RecalculateFolder, "Recalculate folders counters");

            _folderEngine.RecalculateFolders();

            SetProgress((int?)MailOperationRemoveMailboxProgress.ClearCache, "Clear accounts cache");

            _cacheEngine.Clear(_mailBoxData.UserId);

            SetProgress((int?)MailOperationRemoveMailboxProgress.RemoveIndex, "Remove Elastic Search index by messages");

            _indexEngine.Remove(_mailBoxData);

            SetProgress((int?)MailOperationRemoveMailboxProgress.Finished);
        }
        catch (Exception e)
        {
            base.Log.ErrorMailOperationRemoveMailbox(e.ToString());
            Error = "InternalServerError";
        }
    }


}
