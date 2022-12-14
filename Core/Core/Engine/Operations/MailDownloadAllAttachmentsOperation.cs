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

public sealed class MailDownloadAllAttachmentsOperation : MailOperation
{
    private readonly MessageEngine _messageEngine;
    private readonly TempStream _tempStream;
    private readonly int _messageId;

    public override MailOperationType OperationType
    {
        get { return MailOperationType.DownloadAllAttachments; }
    }

    public MailDownloadAllAttachmentsOperation(
        TenantManager tenantManager,
        SecurityContext securityContext,
        IMailDaoFactory mailDaoFactory,
        MessageEngine messageEngine,
        CoreSettings coreSettings,
        StorageManager storageManager,
        StorageFactory storageFactory,
        ILoggerProvider logProvider,
        TempStream tempStream,
        int messageId)
        : base(tenantManager, securityContext, mailDaoFactory, coreSettings, storageManager, logProvider, storageFactory)
    {
        _messageEngine = messageEngine;
        _messageId = messageId;
        _tempStream = tempStream;
    }

    protected override void Do()
    {
        try
        {
            SetProgress((int?)MailOperationDownloadAllAttachmentsProgress.Init);

            TenantManager.SetCurrentTenant(CurrentTenant);

            try
            {
                SecurityContext.AuthenticateMe(CurrentUser);
            }
            catch
            {
                Error = "Error";//Resource.SsoSettingsNotValidToken;
                Log.ErrorMailOperation(Error);
            }

            SetProgress((int?)MailOperationDownloadAllAttachmentsProgress.GetAttachments);

            var attachments =
                _messageEngine.GetAttachments(new ConcreteMessageAttachmentsExp(_messageId,
                    CurrentTenant.TenantId, CurrentUser.ID.ToString()));

            if (!attachments.Any())
            {
                Error = MailCoreResource.NoAttachmentsInMessage;

                throw new Exception(Error);
            }

            SetProgress((int?)MailOperationDownloadAllAttachmentsProgress.Zipping);

            var damagedAttachments = 0;

            var mailStorage = StorageFactory.GetMailStorage(CurrentTenant.TenantId);

            using (var stream = _tempStream.Create())
            {
                using (var zip = new ZipOutputStream(stream))
                {
                    zip.IsStreamOwner = false;
                    zip.SetLevel(3);
                    ZipStrings.UseUnicode = true;

                    var attachmentsCount = attachments.Count;
                    var progressMaxValue = (int)MailOperationDownloadAllAttachmentsProgress.ArchivePreparation;
                    var progressMinValue = (int)MailOperationDownloadAllAttachmentsProgress.Zipping;
                    var progresslength = progressMaxValue - progressMinValue;
                    var progressStep = (double)progresslength / attachmentsCount;
                    var zippingProgress = 0.0;

                    foreach (var attachment in attachments)
                    {
                        try
                        {
                            using (var file = attachment.ToAttachmentStream(mailStorage))
                            {
                                ZipFile(zip, file.FileName, file.FileStream);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.ErrorMailOperation(ex.ToString());

                            Error = string.Format(MailCoreResource.FileNotFoundOrDamaged, attachment.fileName);

                            damagedAttachments++;

                            ZipFile(zip, attachment.fileName); // Zip empty file
                        }

                        zippingProgress += progressStep;

                        SetProgress(progressMinValue + (int?)zippingProgress);
                    }
                }

                SetProgress((int?)MailOperationDownloadAllAttachmentsProgress.ArchivePreparation);

                if (stream.Length == 0)
                {
                    Error = "File stream is empty";

                    throw new Exception(Error);
                }

                stream.Position = 0;

                var path = mailStorage.SaveAsync(
                    FileConstant.StorageDomainTmp,
                    string.Format(@"{0}\{1}", ((IAccount)Thread.CurrentPrincipal.Identity).ID, DefineConstants.ARCHIVE_NAME),
                    stream,
                    "application/zip",
                    "attachment; filename=\"" + DefineConstants.ARCHIVE_NAME + "\"").Result;

                Log.DebugMailOperationArchiveStored(path);
            }

            SetProgress((int?)MailOperationDownloadAllAttachmentsProgress.CreateLink);

            var baseDomain = CoreSettings.BaseDomain;

            var source = string.Format("{0}?{1}=bulk",
                "/products/files/httphandlers/filehandler.ashx",
                FilesLinkUtility.Action);

            if (damagedAttachments > 1)
                Error = string.Format(MailCoreResource.FilesNotFound, damagedAttachments);

            SetProgress((int?)MailOperationDownloadAllAttachmentsProgress.Finished, null, source);
        }
        catch (Exception ex)
        {
            Log.ErrorMailOperationDownloadAttachments(ex.ToString());
            Error = string.IsNullOrEmpty(Error)
                ? "InternalServerError"
                : Error;
        }
    }

    private static void ZipFile(ZipOutputStream zip, string filename, Stream fileStream = null)
    {
        filename = filename ?? "file";
        var entry = new ZipEntry(Path.GetFileName(filename));
        entry.Size = fileStream.Length;
        zip.PutNextEntry(entry);
        fileStream.CopyTo(zip);
        zip.CloseEntry();
    }
}
