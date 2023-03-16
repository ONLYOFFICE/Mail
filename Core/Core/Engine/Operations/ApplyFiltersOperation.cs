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
using MailFolder = ASC.Mail.Models.MailFolder;
using SecurityContext = ASC.Core.SecurityContext;

namespace ASC.Mail.Core.Engine.Operations;

public sealed class ApplyFiltersOperation : MailOperation
{
    private readonly FilterEngine _filterEngine;
    private readonly MessageEngine _messageEngine;
    private readonly MailboxEngine _mailboxEngine;
    private readonly List<int> _ids;

    public override MailOperationType OperationType
    {
        get { return MailOperationType.ApplyAnyFilters; }
    }

    public ApplyFiltersOperation(
        TenantManager tenantManager,
        SecurityContext securityContext,
        IMailDaoFactory mailDaoFactory,
        FilterEngine filterEngine,
        MessageEngine messageEngine,
        MailboxEngine mailboxEngine,
        CoreSettings coreSettings,
        MailStorageManager storageManager,
        ILoggerProvider logProvider,
        List<int> ids)
        : base(tenantManager, securityContext, mailDaoFactory, coreSettings, storageManager, logProvider)
    {
        _filterEngine = filterEngine;
        _messageEngine = messageEngine;
        _mailboxEngine = mailboxEngine;
        _ids = ids;

        if (ids == null || !ids.Any())
            throw new ArgumentException("No ids");
    }

    protected override void Do()
    {
        try
        {
            SetProgress((int?)MailOperationApplyFilterProgress.Init, "Setup tenant and user");

            TenantManager.SetCurrentTenant(CurrentTenant);

            SecurityContext.AuthenticateMe(CurrentUser);

            SetProgress((int?)MailOperationApplyFilterProgress.Filtering, "Filtering");

            var filters = _filterEngine.GetList();

            if (!filters.Any())
            {
                SetProgress((int?)MailOperationApplyFilterProgress.Finished);

                return;
            }

            SetProgress((int?)MailOperationApplyFilterProgress.FilteringAndApplying, "Filtering and applying action");

            var mailboxes = new List<MailBoxData>();

            var index = 0;
            var max = _ids.Count;

            foreach (var id in _ids)
            {
                var progressState = string.Format("Message id = {0} ({1}/{2})", id, ++index, max);

                try
                {
                    SetSource(progressState);

                    var message = _messageEngine.GetMessage(id, new MailMessageData.Options());

                    if (message.Folder != FolderType.Spam && message.Folder != FolderType.Sent && message.Folder != FolderType.Inbox)
                        continue;

                    var mailbox = mailboxes.FirstOrDefault(mb => mb.MailBoxId == message.MailboxId);

                    if (mailbox == null)
                    {
                        mailbox =
                            _mailboxEngine.GetMailboxData(new ConcreteSimpleMailboxExp(message.MailboxId));

                        if (mailbox == null)
                            continue;

                        mailboxes.Add(mailbox);
                    }

                    _filterEngine.ApplyFilters(message, mailbox, new MailFolder(message.Folder, ""), filters);

                }
                catch (Exception ex)
                {
                    Log.ErrorMailOperationProcessing(progressState, ex.ToString());
                }
            }

            SetProgress((int?)MailOperationApplyFilterProgress.Finished);
        }
        catch (Exception e)
        {
            Log.ErrorMailOperationRemoveUserFolder(e.ToString());
            Error = "InternalServerError";
        }
    }
}
