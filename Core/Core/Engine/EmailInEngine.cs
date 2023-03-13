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

namespace ASC.Mail.Core.Engine;

[Scope]
public class EmailInEngine
{
    private readonly ILogger _log;
    private readonly AccountEngine _accountEngine;
    private readonly AlertEngine _alertEngine;
    private readonly MailStorageFactory _storageFactory;
    private readonly ApiHelper _apiHelper;

    public EmailInEngine(
        AccountEngine accountEngine,
        AlertEngine alertEngine,
        MailStorageFactory storageFactory,
        ApiHelper apiHelper,
        ILoggerProvider logProvider,
        MailTenantQuotaController mailTenantQuotaController
        )
    {
        _accountEngine = accountEngine;
        _alertEngine = alertEngine;
        _storageFactory = storageFactory;
        _apiHelper = apiHelper;
        _log = logProvider.CreateLogger("ASC.Mail.EmailInEngine");
    }

    public void SaveEmailInData(MailBoxData mailbox, MailMessageData message, string httpContextScheme = null)
    {
        if (string.IsNullOrEmpty(mailbox.EMailInFolder))
            return;

        try
        {
            foreach (var attachment in message.Attachments.Where(a => !a.isEmbedded))
            {
                if (attachment.dataStream != null)
                {
                    _log.DebugEmailInEngineUploadToDocuments(attachment.fileName, mailbox.EMailInFolder);

                    attachment.dataStream.Seek(0, SeekOrigin.Begin);

                    UploadToDocuments(attachment.dataStream, attachment.fileName, attachment.contentType, mailbox);
                }
                else
                {
                    var storage = _storageFactory.GetMailStorage(mailbox.TenantId);

                    using (var file = attachment.ToAttachmentStream(storage))
                    {
                        _log.DebugEmailInEngineUploadToDocuments(file.FileName, mailbox.EMailInFolder);

                        UploadToDocuments(file.FileStream, file.FileName, attachment.contentType, mailbox);
                    }
                }
            }

        }
        catch (Exception e)
        {
            _log.ErrorEmailInEngineSaveEmailInData(mailbox.TenantId, mailbox.UserId, message.Id, e.ToString());
        }
    }

    private void UploadToDocuments(Stream fileStream, string fileName, string contentType, MailBoxData mailbox)
    {
        try
        {
            var uploadedFileId = _apiHelper.UploadToDocuments(fileStream, fileName, contentType, mailbox.EMailInFolder, true);

            _log.InfoEmailInEngineFileUploaded(fileName, mailbox.EMailInFolder, uploadedFileId);
        }
        catch (ApiHelperException ex)
        {
            if (ex.StatusCode == HttpStatusCode.NotFound || ex.StatusCode == HttpStatusCode.Forbidden)
            {
                _log.InfoEmailInEngineEmailInIsUnreachable(mailbox.EMailInFolder);

                _accountEngine.SetAccountEmailInFolder(mailbox.MailBoxId, null);

                mailbox.EMailInFolder = null;

                _alertEngine.CreateUploadToDocumentsFailureAlert(mailbox.TenantId, mailbox.UserId,
                    mailbox.MailBoxId,
                    ex.StatusCode == HttpStatusCode.NotFound
                        ? UploadToDocumentsErrorType
                            .FolderNotFound
                        : UploadToDocumentsErrorType
                            .AccessDenied);

                throw;
            }

            _log.ErrorEmailInEngineUploadToDocuments(fileName, mailbox.EMailInFolder, ex.ToString());
        }
    }
}
