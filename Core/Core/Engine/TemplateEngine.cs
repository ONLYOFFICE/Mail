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

using ASC.Mail.Core.Storage;
using FolderType = ASC.Mail.Enums.FolderType;
using MailMessage = ASC.Mail.Models.MailMessageData;
using SecurityContext = ASC.Core.SecurityContext;

namespace ASC.Mail.Core.Engine;

[Scope]
public sealed class TemplateEngine : ComposeEngineBase
{
    public TemplateEngine(
        SecurityContext securityContext,
        TenantManager tenantManager,
        IMailDaoFactory mailDaoFactory,
        AccountEngine accountEngine,
        MailboxEngine mailboxEngine,
        MessageEngine messageEngine,
        QuotaEngine quotaEngine,
        IndexEngine indexEngine,
        MailStorageManager storageManager,
        CoreSettings coreSettings,
        MailStorageFactory storageFactory,
        SocketServiceClient signalrServiceClient,
        ILoggerProvider logProvider,
        SocketServiceClient socketServiceClient,
        MailSettings mailSettings,
        DeliveryFailureMessageTranslates daemonLabels = null)
        : base(
        accountEngine,
        mailboxEngine,
        messageEngine,
        quotaEngine,
        indexEngine,
        mailDaoFactory,
        storageManager,
        securityContext,
        tenantManager,
        coreSettings,
        storageFactory,
        socketServiceClient,
        logProvider,
        mailSettings,
        daemonLabels)
    {
    }

    public override MailMessage Save(MessageModel model, DeliveryFailureMessageTranslates translates = null)
    {
        var mailAddress = new MailAddress(model.From);

        var accounts = _accountEngine.GetAccountInfoList().ToAccountData();

        var account = accounts.FirstOrDefault(a => a.Email.ToLower().Equals(mailAddress.Address));

        if (account == null)
            throw new ArgumentException("Mailbox not found");

        if (account.IsGroup)
            throw new InvalidOperationException("Saving emails from a group address is forbidden");

        var mbox = _mailboxEngine.GetMailboxData(
            new ConcreteUserMailboxExp(account.MailboxId, Tenant, User));

        if (mbox == null)
            throw new ArgumentException("No such mailbox");

        string mimeMessageId, streamId;

        var previousMailboxId = mbox.MailBoxId;

        if (model.Id > 0)
        {
            var message = _messageEngine.GetMessage(model.Id, new MailMessage.Options
            {
                LoadImages = false,
                LoadBody = true,
                NeedProxyHttp = _mailSettings.NeedProxyHttp,
                NeedSanitizer = false
            });

            if (message.Folder != FolderType.Templates)
            {
                throw new InvalidOperationException("Saving emails is permitted only in the Templates folder");
            }

            if (message.HtmlBody.Length > _mailSettings.Defines.MaximumMessageBodySize)
            {
                throw new InvalidOperationException("Message body exceeded limit (" + _mailSettings.Defines.MaximumMessageBodySize / 1024 + " KB)");
            }

            mimeMessageId = message.MimeMessageId;

            streamId = message.StreamId;

            /*
            if (attachments != null && attachments.Any())
            {
                foreach (var attachment in attachments)
                {
                    attachment.streamId = streamId;
                }
            }
             */

            previousMailboxId = message.MailboxId;
        }
        else
        {
            mimeMessageId = MailUtil.CreateMessageId(_tenantManager, _coreSettings);
            streamId = MailUtil.CreateStreamId();
        }

        var fromAddress = MailUtil.CreateFullEmail(mbox.Name, mbox.EMail.Address);

        var template = new MailTemplateData(model.Id, mbox, fromAddress, model.To, model.Cc, model.Bcc, model.Subject,
                mimeMessageId, model.MimeReplyToId, model.Importance,
                model.Tags, model.Body, streamId, model.Attachments, model.CalendarIcs)
        {
            PreviousMailboxId = previousMailboxId
        };

        DaemonLabels = translates ?? DeliveryFailureMessageTranslates.Defauilt;

        return Save(template);
    }
}
