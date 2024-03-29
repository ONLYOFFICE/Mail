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
 * Pursuant to Section 7 � 3(b) of the GNU GPL you must retain the original ONLYOFFICE logo which contains 
 * relevant author attributions when distributing the software. If the display of the logo in its graphic 
 * form is not reasonably feasible for technical reasons, you must include the words "Powered by ONLYOFFICE" 
 * in every copy of the program you distribute. 
 * Pursuant to Section 7 � 3(e) we decline to grant you any rights under trademark law for use of our trademarks.
 *
*/


using System.Security;

using ASC.Mail.Core.Storage;
using SecurityContext = ASC.Core.SecurityContext;

namespace ASC.Mail.Core.Engine;

[Scope]
public class CrmLinkEngine
{
    private int Tenant => _tenantManager.GetCurrentTenant().Id;
    private string User => _securityContext.CurrentAccount.ID.ToString();

    public readonly Common.Security.Authorizing.IAction _actionRead = new ASC.Common.Security.Authorizing.Action(new Guid("{6F05C382-8BCA-4469-9424-C807A98C40D7}"), "", administratorAlwaysAllow: true, conjunction: false);

    private readonly ILogger _log;
    private readonly SecurityContext _securityContext;
    private readonly TenantManager _tenantManager;
    private readonly ApiHelper _apiHelper;
    private readonly IMailDaoFactory _mailDaoFactory;
    private readonly MessageEngine _messageEngine;
    private readonly MailStorageFactory _storageFactory;
    //private readonly CrmSecurity _crmSecurity;
    private readonly IServiceProvider _serviceProvider;
    private readonly PermissionContext _permissionContext;
    private readonly WebItemSecurity _webItemSecurity;

    public CrmLinkEngine(
        SecurityContext securityContext,
        TenantManager tenantManager,
        ApiHelper apiHelper,
        IMailDaoFactory mailDaoFactory,
        MessageEngine messageEngine,
        MailStorageFactory storageFactory,
        ILoggerProvider logProvider,
        WebItemSecurity webItemSecurity,
        PermissionContext permissionContext,
        IServiceProvider serviceProvider)
    {
        _securityContext = securityContext;
        _tenantManager = tenantManager;
        _apiHelper = apiHelper;
        _mailDaoFactory = mailDaoFactory;
        _messageEngine = messageEngine;
        _storageFactory = storageFactory;
        _serviceProvider = serviceProvider;
        _permissionContext = permissionContext;
        _webItemSecurity = webItemSecurity;
        _log = logProvider.CreateLogger("ASC.Mail.CrmLinkEngine");
    }

    public List<CrmContactData> GetLinkedCrmEntitiesId(int messageId)
    {
        var mail = _mailDaoFactory.GetMailDao().GetMail(new ConcreteUserMessageExp(messageId, Tenant, User));

        return _mailDaoFactory.GetCrmLinkDao().GetLinkedCrmContactEntities(mail.ChainId, mail.MailboxId);
    }

    public void LinkChainToCrm(int messageId, List<CrmContactData> contactIds)
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var factory = scope.ServiceProvider.GetService<CrmContactDao>();

            contactIds.ForEach(x => CheckCRMPermisions(x, factory));
        }

        var mail = _mailDaoFactory.GetMailDao().GetMail(new ConcreteUserMessageExp(messageId, Tenant, User));

        var chainedMessages = _mailDaoFactory.GetMailInfoDao().GetMailInfoList(
            SimpleMessagesExp.CreateBuilder(Tenant, User)
                .SetChainId(mail.ChainId)
                .Build());

        if (!chainedMessages.Any())
            return;

        var linkingMessages = new List<MailMessageData>();

        foreach (var chainedMessage in chainedMessages)
        {
            var message = _messageEngine.GetMessage(chainedMessage.Id,
                new MailMessageData.Options
                {
                    LoadImages = true,
                    LoadBody = true,
                    NeedProxyHttp = false
                });

            message.LinkedCrmEntityIds = contactIds;

            linkingMessages.Add(message);

        }

        var strategy = _mailDaoFactory.GetContext().Database.CreateExecutionStrategy();

        strategy.Execute(() =>
        {
            using var tx = _mailDaoFactory.BeginTransaction(IsolationLevel.ReadUncommitted);

            _mailDaoFactory.GetCrmLinkDao().SaveCrmLinks(mail.ChainId, mail.MailboxId, contactIds);

            foreach (var message in linkingMessages)
            {
                try
                {
                    AddRelationshipEvents(message);
                }
                catch (ApiHelperException ex)
                {
                    if (!ex.Message.Equals("Already exists"))
                        throw;
                }
            }

            tx.Commit();
        });
    }

    public void MarkChainAsCrmLinked(int messageId, List<CrmContactData> contactIds)
    {
        var strategy = _mailDaoFactory.GetContext().Database.CreateExecutionStrategy();

        strategy.Execute(() =>
        {
            using var tx = _mailDaoFactory.BeginTransaction(IsolationLevel.ReadUncommitted);

            var mail = _mailDaoFactory.GetMailDao().GetMail(new ConcreteUserMessageExp(messageId, Tenant, User));

            _mailDaoFactory.GetCrmLinkDao().SaveCrmLinks(mail.ChainId, mail.MailboxId, contactIds);

            tx.Commit();
        });
    }

    public void UnmarkChainAsCrmLinked(int messageId, IEnumerable<CrmContactData> contactIds)
    {
        var strategy = _mailDaoFactory.GetContext().Database.CreateExecutionStrategy();

        strategy.Execute(() =>
        {
            using var tx = _mailDaoFactory.BeginTransaction(IsolationLevel.ReadUncommitted);

            var mail = _mailDaoFactory.GetMailDao().GetMail(new ConcreteUserMessageExp(messageId, Tenant, User));

            _mailDaoFactory.GetCrmLinkDao().RemoveCrmLinks(mail.ChainId, mail.MailboxId, contactIds);

            tx.Commit();
        });
    }

    public void ExportMessageToCrm(int messageId, IEnumerable<CrmContactData> crmContactIds)
    {
        if (messageId < 0)
            throw new ArgumentException(@"Invalid message id", nameof(messageId));
        if (crmContactIds == null)
            throw new ArgumentException(@"Invalid contact ids list", nameof(crmContactIds));

        var messageItem = _messageEngine.GetMessage(messageId, new MailMessageData.Options
        {
            LoadImages = true,
            LoadBody = true,
            NeedProxyHttp = false
        });

        messageItem.LinkedCrmEntityIds = crmContactIds.ToList();

        AddRelationshipEvents(messageItem);
    }

    public void AddRelationshipEventForLinkedAccounts(MailBoxData mailbox, MailMessageData messageItem)
    {
        try
        {
            messageItem.LinkedCrmEntityIds = _mailDaoFactory.GetCrmLinkDao()
                .GetLinkedCrmContactEntities(messageItem.ChainId, mailbox.MailBoxId);

            if (!messageItem.LinkedCrmEntityIds.Any()) return;

            AddRelationshipEvents(messageItem, mailbox);
        }
        catch (Exception ex)
        {
            _log.WarnCrmLinkEngineAddingHistoryEvent(messageItem.Id, ex);
        }
    }

    public void AddRelationshipEvents(MailMessageData message, MailBoxData mailbox = null)
    {
        using var scope = _serviceProvider.CreateScope();

        if (mailbox != null)
        {
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(mailbox.TenantId);
            securityContext.AuthenticateMe(new Guid(mailbox.UserId));
        }

        var factory = scope.ServiceProvider.GetService<CrmContactDao>();

        foreach (var contactEntity in message.LinkedCrmEntityIds)
        {
            CheckCRMPermisions(contactEntity, factory);

            var fileIds = new List<object>();

            foreach (var attachment in message.Attachments.FindAll(attach => !attach.isEmbedded))
            {
                if (attachment.dataStream != null)
                {
                    attachment.dataStream.Seek(0, SeekOrigin.Begin);

                    var uploadedFileId = _apiHelper.UploadToCrm(attachment.dataStream, attachment.fileName,
                        attachment.contentType, contactEntity);

                    if (uploadedFileId != null)
                    {
                        fileIds.Add(uploadedFileId);
                    }
                }
                else
                {
                    var dataStore = _storageFactory.GetMailStorage(Tenant);

                    using var file = attachment.ToAttachmentStream(dataStore);

                    var uploadedFileId = _apiHelper.UploadToCrm(file.FileStream, file.FileName,
                        attachment.contentType, contactEntity);

                    if (uploadedFileId != null)
                    {
                        fileIds.Add(uploadedFileId);
                    }
                }
            }

            _apiHelper.AddToCrmHistory(message, contactEntity, fileIds);

            _log.InfoCrmLinkEngineAddRelationshipEvents(message.Id, contactEntity.Id);
        }
    }

    private void CheckCRMPermisions(CrmContactData crmContactEntity, CrmContactDao factory)
    {
        switch (crmContactEntity.Type)
        {
            case CrmContactData.EntityTypes.Contact:
                if (!(_webItemSecurity.IsProductAdministrator(WebItemManager.CRMProductID, _securityContext.CurrentAccount.ID)
                    || _permissionContext.CheckPermissions(_actionRead)))
                {
                    throw new SecurityException();
                }

                break;
            case CrmContactData.EntityTypes.Case:
                if (!(_webItemSecurity.IsProductAdministrator(WebItemManager.CRMProductID, _securityContext.CurrentAccount.ID)
                    || _permissionContext.CheckPermissions(_actionRead)))
                {
                    throw new SecurityException();
                }

                break;
            case CrmContactData.EntityTypes.Opportunity:
                if (!(_webItemSecurity.IsProductAdministrator(WebItemManager.CRMProductID, _securityContext.CurrentAccount.ID)
                    || _permissionContext.CheckPermissions(_actionRead)))
                {
                    throw new SecurityException();
                }

                break;
        }
    }
}
