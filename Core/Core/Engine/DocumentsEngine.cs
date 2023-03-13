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
using SecurityContext = ASC.Core.SecurityContext;

namespace ASC.Mail.Core.Engine;

[Scope]
public class DocumentsEngine
{
    public const string MY_DOCS_FOLDER_ID = "@my";

    private int Tenant => _tenantManager.GetCurrentTenant().Id;
    private string User => _securityContext.CurrentAccount.ID.ToString();

    private readonly SecurityContext _securityContext;
    private readonly TenantManager _tenantManager;
    private readonly ApiHelper _apiHelper;
    private readonly MessageEngine _messageEngine;
    private readonly MailStorageFactory _storageFactory;

    public DocumentsEngine(
        SecurityContext securityContext,
        TenantManager tenantManager,
        ApiHelper apiHelper,
        MessageEngine messageEngine,
        MailStorageFactory storageFactory)
    {
        _securityContext = securityContext;
        _tenantManager = tenantManager;
        _apiHelper = apiHelper;
        _messageEngine = messageEngine;
        _storageFactory = storageFactory;
    }

    public List<object> StoreAttachmentsToMyDocuments(int messageId)
    {
        return StoreAttachmentsToDocuments(messageId, MY_DOCS_FOLDER_ID);
    }

    public object StoreAttachmentToMyDocuments(int attachmentId)
    {
        return StoreAttachmentToDocuments(attachmentId, MY_DOCS_FOLDER_ID);
    }

    public List<object> StoreAttachmentsToDocuments(int messageId, string folderId)
    {
        var attachments =
            _messageEngine.GetAttachments(new ConcreteMessageAttachmentsExp(messageId, Tenant, User));

        return
            attachments.Select(attachment => StoreAttachmentToDocuments(attachment, folderId))
                .Where(uploadedFileId => uploadedFileId != null)
                .ToList();
    }

    public object StoreAttachmentToDocuments(int attachmentId, string folderId)
    {
        var attachment = _messageEngine.GetAttachment(
            new ConcreteUserAttachmentExp(attachmentId, Tenant, User));

        if (attachment == null)
            return -1;

        return StoreAttachmentToDocuments(attachment, folderId);
    }

    public object StoreAttachmentToDocuments(MailAttachmentData mailAttachmentData, string folderId)
    {
        if (mailAttachmentData == null)
            return -1;

        var dataStore = _storageFactory.GetMailStorage(Tenant);

        using var file = mailAttachmentData.ToAttachmentStream(dataStore);

        var uploadedFileId = _apiHelper.UploadToDocuments(file.FileStream, file.FileName,
            mailAttachmentData.contentType, folderId, true);

        return uploadedFileId;
    }
}
