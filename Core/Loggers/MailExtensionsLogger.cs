namespace ASC.Mail.Core.Loggers
{
    internal static partial class MailExtensionsLogger
    {
        [LoggerMessage(Level = LogLevel.Debug, Message = "IsUserTerminated(). Cannot detect user status. Exception:\n{error}\nreturn false.")]
        public static partial void DebugMailExtensionsCannotDetectUserStatus(this ILogger logger, string error);

        [LoggerMessage(Level = LogLevel.Debug, Message = "IsUserRemoved(). Cannot detect user remove status. Exception:\n{error}\nreturn false.")]
        public static partial void DebugMailExtensionsCannotDetectUserRemoveStatus(this ILogger logger, string error);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Attempt to set current tenant. Tenant {tenantId}...")]
        public static partial void DebugMailExtensionsAttemptSetTenant(this ILogger logger, int tenantId);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Attempt to get current tenant info.")]
        public static partial void DebugMailExtensionsGetCurrentTenantInfo(this ILogger logger);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Returned tenant status: {status}. TenantId: {id}. OwnerId: {ownerId}")]
        public static partial void DebugMailExtensionsReturnedTenantStatus(this ILogger logger, string status, int id, Guid ownerId);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Authentication attempt by OwnerId for tenant")]
        public static partial void DebugMailExtensionsAuthByOwnerIdTenant(this ILogger logger);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Authentication failed. Authentication attempt by mailbox UserId")]
        public static partial void DebugMailExtensionsAuthFailed(this ILogger logger);

        [LoggerMessage(Level = LogLevel.Error, Message = "GetTenantStatus(Tenant={tenantId}, User='{userId}') Exception: {error}")]
        public static partial void ErrorMailExtensionsGetTenantStatus(this ILogger logger, int tenantId, string userId, string error);

        [LoggerMessage(Level = LogLevel.Debug, Message = "IsTenantQuotaEnded: {quotaEnded} Tenant = {tenantId}. Tenant quota = {maxSize}Mb, used quota = {usedQuotaSize}Mb")]
        public static partial void DebugMailExtensionsIsTenantQuotaEnded(this ILogger logger, bool quotaEnded, int tenantId, double maxSize, double usedQuotaSize);

        [LoggerMessage(Level = LogLevel.Error, Message = "IsQuotaExhausted(Tenant = {tenantId}) Exception: {error} StackTrace: \n{stackTrace}")]
        public static partial void ErrorMailExtensionsIsQuotaExhausted(this ILogger logger, int tenantId, string error, string stackTrace);

        [LoggerMessage(Level = LogLevel.Error, Message = "Get portal settings failed (Tenant: {tenantId}, User: {userId}, Mailbox: {mailboxId}). Returned status code: {statusCode}")]
        public static partial void ErrorMailExtensionsGetPortalSettings(this ILogger logger, int tenantId, string userId, int mailboxId, string statusCode);

        [LoggerMessage(Level = LogLevel.Information, Message = "ChangeSmileLinks() Link to smile: {link}")]
        public static partial void InfoMailExtensionsLinkToSmile(this ILogger logger, string link);

        [LoggerMessage(Level = LogLevel.Information, Message = "ChangeSmileLinks() Embedded smile contentId: {contentId}")]
        public static partial void InfoMailExtensionsEmbeddedSmile(this ILogger logger, string contentId);

        [LoggerMessage(Level = LogLevel.Information, Message = "ChangeAttachedFileLinksImages() Link to file link: {link}")]
        public static partial void InfoMailExtensionsLinkToFile(this ILogger logger, string link);

        [LoggerMessage(Level = LogLevel.Information, Message = "ChangeAttachedFileLinksImages() Embedded file link contentId: {contentId}")]
        public static partial void InfoMailExtensionsEmbeddedFileLink(this ILogger logger, string contentId);

        [LoggerMessage(Level = LogLevel.Information, Message = "ChangeAllImagesLinksToEmbedded() Link to img link: {link}")]
        public static partial void InfoMailExtensionsLinkToImg(this ILogger logger, string link);

        [LoggerMessage(Level = LogLevel.Information, Message = "ChangeAllImagesLinksToEmbedded() Embedded img link contentId: {contentId}")]
        public static partial void InfoMailExtensionsEmbeddedImgLink(this ILogger logger, string contentId);

        [LoggerMessage(Level = LogLevel.Error, Message = "ChangeAllImagesLinksToEmbedded(): Exception: {error}")]
        public static partial void ErrorMailExtensionsImagesLinksToEmbedded(this ILogger logger, string error);

        [LoggerMessage(Level = LogLevel.Debug, Message = "LoadCalendarInfo found {count} calendars")]
        public static partial void DebugMailExtensionsFoundCalendars(this ILogger logger, int count);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Calendar UID: {calendarUid} Method: {method} ics: {eventIcs}")]
        public static partial void DebugMailExtensionsCalendarUid(this ILogger logger, string calendarUid, string method, string eventIcs);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Calendar exists as attachment")]
        public static partial void DebugMailExtensionsCalendarExists(this ILogger logger);

        [LoggerMessage(Level = LogLevel.Error, Message = "LoadCalendarInfo() \r\n Exception: \r\n{error}\r\n")]
        public static partial void ErrorMailExtensionsLoadCalendar(this ILogger logger, string error);

        [LoggerMessage(Level = LogLevel.Error, Message = "ReplaceEmbeddedImages() \r\n Exception: \r\n{error}\r\n")]
        public static partial void ErrorMailExtensionsReplaceEmbeddedImages(this ILogger logger, string error);

        [LoggerMessage(Level = LogLevel.Warning, Message = "MimeMessage.FixEncodingIssues -> ImproveBodyEncoding: {error}")]
        public static partial void WarnMailExtensionsImproveBodyEncoding(this ILogger logger, string error);

        [LoggerMessage(Level = LogLevel.Warning, Message = "MimeMessage.FixEncodingIssues: {error}")]
        public static partial void WarnMailExtensionsFixEncodingIssues(this ILogger logger, string error);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Header.FixEncodingIssues: {error}")]
        public static partial void WarnMailExtensionsHeaderFixEncodingIssues(this ILogger logger, string error);

        [LoggerMessage(Level = LogLevel.Warning, Message = "MimeMessage.FixEncodingIssues: {error}")]
        public static partial void WarnMailExtensionsMimeFixEncodingIssues(this ILogger logger, string error);

        [LoggerMessage(Level = LogLevel.Information, Message = "ChangeAttachedFileLinks() Change file link href: {fileId}")]
        public static partial void InfoMailExtensionsFileLinkHref(this ILogger logger, string fileId);

        [LoggerMessage(Level = LogLevel.Information, Message = "ChangeAttachedFileLinks() Set public accees to file: {fileId}")]
        public static partial void InfoMailExtensionsSetPublicAcceesToFile(this ILogger logger, string fileId);

        [LoggerMessage(Level = LogLevel.Information, Message = "ChangeAttachedFileLinks() Change file link href: {fileId}")]
        public static partial void InfoMailExtensionsChangeFileLinkHref(this ILogger logger, string fileId);

        [LoggerMessage(Level = LogLevel.Information, Message = "ChangeEmbededAttachmentLinks() Embeded attachment link for changing to cid: {fileId}")]
        public static partial void InfoMailExtensionsLinkForChangingToCid(this ILogger logger, string fileId);

        [LoggerMessage(Level = LogLevel.Information, Message = "ChangeEmbededAttachmentLinks() Attachment cid: {contentId}")]
        public static partial void InfoMailExtensionsAttachmentCid(this ILogger logger, string contentId);
    }
}
