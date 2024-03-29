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



using ASC.Common.Log;
using ASC.Mail.Core.Storage;
using SecurityContext = ASC.Core.SecurityContext;

namespace ASC.Mail.Storage;

[Scope]
public class MailStorageManager
{
    public const string CKEDITOR_IMAGES_DOMAIN = "mail";

    private int Tenant => _tenantManager.GetCurrentTenant().Id;
    private string User => _securityContext.CurrentAccount.ID.ToString();

    private readonly ILogger _log;
    private readonly SecurityContext _securityContext;
    private readonly MailStorageFactory _storageFactory;
    private readonly TenantManager _tenantManager;

    public MailStorageManager(
        SecurityContext securityContext,
        MailStorageFactory storageFactory,
        TenantManager tenantManager,
        ILoggerProvider logProvider)
    {
        _securityContext = securityContext;
        _storageFactory = storageFactory;
        _tenantManager = tenantManager;
        _log = logProvider.CreateLogger("ASC.Mail.StorageManager");
    }

    public IDataStore GetDataStoreForCkImages(int tenant)
    {
        return _storageFactory.GetStorage(tenant, "fckuploaders");
    }

    public IDataStore GetDataStoreForAttachments(int tenant)
    {
        return _storageFactory.GetStorage(tenant, "mailaggregator");
    }

    public static byte[] LoadLinkData(string link, ILogger log)
    {
        var data = new byte[] { };

        try
        {
            using (var webClient = new WebClient())
            {
                data = webClient.DownloadData(link);
            }
        }
        catch (Exception)
        {
            log.ErrorStorageManagerLoadLinkData(link);
        }

        return data;
    }

    public static byte[] LoadDataStoreItemData(string domain, string fileLink, IDataStore storage)
    {
        using var stream = storage.GetReadStreamAsync(domain, fileLink).Result;
        return stream.ReadToEnd();
    }

    public string ChangeEditorImagesLinks(string html, int mailboxId)
    {
        if (string.IsNullOrEmpty(html) || mailboxId < 1)
            return html;

        var newHtml = html;

        var ckStorage = GetDataStoreForCkImages(Tenant);
        var signatureStorage = GetDataStoreForAttachments(Tenant);
        //todo: replace selector
        var currentMailCkeditorUrl = ckStorage.GetUriAsync(CKEDITOR_IMAGES_DOMAIN, "").Result.ToString();

        var xpathQuery = GetXpathQueryForCkImagesToResaving(currentMailCkeditorUrl);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var linkNodes = doc.DocumentNode.SelectNodes(xpathQuery);

        if (linkNodes != null)
        {
            foreach (var linkNode in linkNodes)
            {
                try
                {
                    var link = linkNode.Attributes["src"].Value;

                    _log.InfoStorageManagerOriginalImageLink(link);

                    var fileLink = HttpUtility.UrlDecode(link.Substring(currentMailCkeditorUrl.Length));

                    var fileName = Path.GetFileName(fileLink);

                    var bytes = LoadDataStoreItemData(CKEDITOR_IMAGES_DOMAIN, fileLink, ckStorage);

                    var stableImageLink = StoreCKeditorImageWithoutQuota(mailboxId, fileName, bytes,
                                                           signatureStorage);

                    linkNode.SetAttributeValue("src", stableImageLink);
                }
                catch (Exception ex)
                {
                    _log.ErrorStorageManagerChangeSignature(ex.ToString());
                }
            }

            newHtml = doc.DocumentNode.OuterHtml;
        }

        return newHtml;
    }

    public string StoreCKeditorImageWithoutQuota(int mailboxId, string fileName, byte[] imageData, IDataStore storage)
    {
        try
        {
            if (imageData == null || imageData.Length == 0)
                throw new ArgumentNullException("imageData");

            var ext = string.IsNullOrEmpty(fileName) ? ".jpg" : Path.GetExtension(fileName);

            if (string.IsNullOrEmpty(ext))
                ext = ".jpg";

            var storeName = imageData.GetMd5();
            storeName = Path.ChangeExtension(storeName, ext);

            var contentDisposition = ContentDispositionUtil.GetHeaderValue(storeName);
            var contentType = MimeMapping.GetMimeMapping(ext);

            var signatureImagePath = MailStoragePathCombiner.GerStoredSignatureImagePath(User, mailboxId, storeName);

            using (var reader = new MemoryStream(imageData))
            {
                var uploadUrl = storage.SaveAsync(string.Empty, signatureImagePath, reader, contentType, contentDisposition).Result;
                return MailStoragePathCombiner.GetStoredUrl(uploadUrl);
            }
        }
        catch (Exception e)
        {
            _log.ErrorStorageManagerStoreCKeditor(fileName, e.ToString());

            throw;
        }
    }

    public void StoreAttachmentWithoutQuota(MailAttachmentData mailAttachmentData)
    {
        try
        {
            if ((mailAttachmentData.dataStream == null || mailAttachmentData.dataStream.Length == 0)
                && (mailAttachmentData.data == null || mailAttachmentData.data.Length == 0))
            {
                return;
            }

            if (string.IsNullOrEmpty(mailAttachmentData.fileName))
                mailAttachmentData.fileName = "attachment.ext";

            var storage = _storageFactory.GetMailStorage(Tenant);

            storage.QuotaController = null;

            if (string.IsNullOrEmpty(mailAttachmentData.storedName))
            {
                mailAttachmentData.storedName = MailUtil.CreateStreamId();

                var ext = Path.GetExtension(mailAttachmentData.fileName);

                if (!string.IsNullOrEmpty(ext))
                    mailAttachmentData.storedName = Path.ChangeExtension(mailAttachmentData.storedName, ext);
            }

            mailAttachmentData.fileNumber =
                !string.IsNullOrEmpty(mailAttachmentData.contentId) //Upload hack: embedded attachment have to be saved in 0 folder
                    ? 0
                    : mailAttachmentData.fileNumber;

            var attachmentPath = MailStoragePathCombiner.GerStoredFilePath(mailAttachmentData);

            if (mailAttachmentData.data != null)
            {
                using (var reader = new MemoryStream(mailAttachmentData.data))
                {
                    var uploadUrl = (mailAttachmentData.needSaveToTemp)
                        ? storage.SaveAsync("attachments_temp", attachmentPath, reader, mailAttachmentData.fileName).Result
                        : storage.SaveAsync(attachmentPath, reader, mailAttachmentData.fileName).Result;

                    mailAttachmentData.storedFileUrl = MailStoragePathCombiner.GetStoredUrl(uploadUrl);
                }
            }
            else
            {
                var uploadUrl = (mailAttachmentData.needSaveToTemp)
                    ? storage.SaveAsync("attachments_temp", attachmentPath, mailAttachmentData.dataStream, mailAttachmentData.fileName).Result
                    : storage.SaveAsync(attachmentPath, mailAttachmentData.dataStream, mailAttachmentData.fileName).Result;

                mailAttachmentData.storedFileUrl = MailStoragePathCombiner.GetStoredUrl(uploadUrl);
            }

            if (mailAttachmentData.needSaveToTemp)
            {
                mailAttachmentData.tempStoredUrl = mailAttachmentData.storedFileUrl;
            }
        }
        catch (Exception e)
        {
            _log.ErrorStorageManagerStoreAttachment(mailAttachmentData.fileName, mailAttachmentData.contentType, e.ToString());

            throw;
        }
    }

    public void MailQuotaUsedAdd(long usedQuota)
    {
        var quotaController = _storageFactory.GetMailQuotaContriller(Tenant);

        try
        {
            quotaController.QuotaUsedAdd(DefineConstants.MODULE_NAME,
                string.Empty,
                DefineConstants.MAIL_QUOTA_TAG,
                usedQuota,
                _securityContext.CurrentAccount.ID,
                true);
        }
        catch (Exception ex)
        {
            _log.Error($"MailQuotaUsedAdd: {_securityContext.CurrentAccount.ID}, size={usedQuota}, ex={ex}");

            throw;
        }
    }

    public void MailQuotaUsedDelete(long usedQuota)
    {
        var quotaController = _storageFactory.GetMailQuotaContriller(Tenant);

        try
        {
            quotaController.QuotaUsedDelete(DefineConstants.MODULE_NAME,
                string.Empty,
                DefineConstants.MAIL_QUOTA_TAG,
                usedQuota,
                _securityContext.CurrentAccount.ID);
        }
        catch (Exception ex)
        {
            _log.Error($"MailQuotaUsedDelete: {_securityContext.CurrentAccount.ID}, size={usedQuota}, ex={ex}");

            throw;
        }
    }

    public static string GetXpathQueryForAttachmentsToResaving(string thisMailFckeditorUrl,
                                                                string thisMailAttachmentFolderUrl,
                                                                string thisUserStorageUrl)
    {
        const string src_condition_format = "contains(@src,'{0}')";
        var addedByUserToFck = string.Format(src_condition_format, thisMailFckeditorUrl);
        var addedToThisMail = string.Format(src_condition_format, thisMailAttachmentFolderUrl);
        var addedToThisUserMails = string.Format(src_condition_format, thisUserStorageUrl);
        var xpathQuery = string.Format("//img[@src and ({0} or {1}) and not({2})]", addedByUserToFck,
                                        addedToThisUserMails, addedToThisMail);
        return xpathQuery;
    }

    public static string GetXpathQueryForCkImagesToResaving(string thisMailCkeditorUrl)
    {
        const string src_condition_format = "contains(@src,'{0}')";
        var addedByUserToFck = string.Format(src_condition_format, thisMailCkeditorUrl);
        var xpathQuery = string.Format("//img[@src and {0}]", addedByUserToFck);
        return xpathQuery;
    }
}
