/*
 *
 * (c) Copyright Ascensio System Limited 2010-2018
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

namespace ASC.Mail.Extensions;

public static class MimeMessageExtensions
{
    public static void FixEncodingIssues(this MimeMessage mimeMessage, ILogger log = null)
    {
        try
        {
            foreach (var mimeEntity in mimeMessage.BodyParts)
            {
                var textPart = mimeEntity as TextPart;

                if (textPart == null ||
                    textPart.Content == null ||
                    textPart.Content.Encoding != ContentEncoding.Default)
                {
                    continue;
                }

                try
                {
                    string charset;
                    using (var stream = new MemoryStream())
                    {
                        textPart.Content.DecodeTo(stream);
                        var bytes = stream.ToArray();
                        charset = EncodingTools.DetectCharset(bytes);
                    }

                    if (!string.IsNullOrEmpty(charset) &&
                        (textPart.ContentType == null ||
                         string.IsNullOrEmpty(textPart.ContentType.Charset) ||
                         textPart.ContentType.Charset != charset))
                    {
                        var encoding = EncodingTools.GetEncodingByCodepageName(charset);

                        if (encoding == null)
                            continue;

                        var newText = textPart.GetText(charset);

                        textPart.SetText(encoding, newText);
                    }
                }
                catch (Exception ex)
                {
                    log?.WarnMailExtensionsImproveBodyEncoding(ex.Message);
                }
            }

            if (mimeMessage.Headers.Contains(HeaderId.From))
            {
                var fromParsed = mimeMessage.From.FirstOrDefault();
                if (fromParsed != null && !string.IsNullOrEmpty(fromParsed.Name))
                {
                    var fromHeader = mimeMessage.Headers.FirstOrDefault(h => h.Id == HeaderId.From);
                    fromHeader.FixEncodingIssues(log);
                }
            }

            if (!mimeMessage.Headers.Contains(HeaderId.Subject))
                return;

            var subjectHeader = mimeMessage.Headers.FirstOrDefault(h => h.Id == HeaderId.Subject);
            subjectHeader.FixEncodingIssues(log);

        }
        catch (Exception ex)
        {
            log.WarnMailExtensionsFixEncodingIssues(ex.Message);
        }
    }

    public static void FixEncodingIssues(this Header header, ILogger log)
    {
        try
        {
            var rawValueString = Encoding.UTF8.GetString(header.RawValue).Trim();
            if (rawValueString.IndexOf("?q?", StringComparison.InvariantCultureIgnoreCase) > -1 ||
                rawValueString.IndexOf("?b?", StringComparison.InvariantCultureIgnoreCase) > -1)
            {
                return;
            }

            var charset = EncodingTools.DetectCharset(header.RawValue);

            if (string.IsNullOrEmpty(charset))
                return;

            var newValue = header.GetValue(charset);

            if (header.Value.Equals(newValue, StringComparison.InvariantCultureIgnoreCase))
                return;

            var encoding = EncodingTools.GetEncodingByCodepageName(charset);
            header.SetValue(encoding, newValue);
        }
        catch (Exception ex)
        {
            log.WarnMailExtensionsHeaderFixEncodingIssues(ex.Message);
        }
    }

    public static void FixDateIssues(this MimeMessage mimeMessage, ILogger log, DateTimeOffset? internalDate = null)
    {
        try
        {
            if (!mimeMessage.Headers.Contains(HeaderId.Date) || mimeMessage.Date > DateTimeOffset.UtcNow)
            {
                mimeMessage.Date = internalDate ?? DateTimeOffset.UtcNow;
            }
        }
        catch (Exception ex)
        {
            log.WarnMailExtensionsMimeFixEncodingIssues(ex.Message);
        }
    }

    public static MailMessageData CreateMailMessage(this MimeMessage message,
        TenantManager tenantManager, CoreSettings coreSettings, ILogger log,
        int mailboxId = -1,
        FolderType folder = FolderType.Inbox,
        bool unread = false,
        string chainId = "",
        DateTime? chainDate = null,
        string streamId = "")
    {
        var mail = new MailMessageData();

        if (message == null)
            throw new ArgumentNullException("message");

        mail.MailboxId = mailboxId;

        var now = DateTime.UtcNow;

        mail.Date = MailUtil.IsDateCorrect(message.Date.UtcDateTime) ? message.Date.UtcDateTime : now;

        mail.MimeMessageId = (string.IsNullOrEmpty(message.MessageId)
            ? MailUtil.CreateMessageId(tenantManager, coreSettings)
            : message.MessageId)
            .Trim('<', '>');

        mail.ChainId = string.IsNullOrEmpty(chainId) ? mail.MimeMessageId : chainId;

        mail.ChainDate = chainDate ?? now;

        mail.MimeReplyToId = mail.ChainId.Equals(mail.MimeMessageId) || string.IsNullOrEmpty(message.InReplyTo)
            ? null
            : message.InReplyTo.Trim('<', '>');

        mail.ReplyTo = message.ReplyTo.ToString();

        mail.From = message.From.ToString();

        mail.FromEmail = message.From != null && message.From.Mailboxes != null && message.From.Mailboxes.Any()
            ? message.From.Mailboxes.First().Address
            : "";

        mail.ToList = message.To.Mailboxes.Select(s => new MailAddress(s.Address, s.Name)).ToList();

        mail.To = string.Join(", ", message.To.Mailboxes.Select(s => s.ToString()));

        mail.CcList = message.Cc.Mailboxes.Select(s => new MailAddress(s.Address, s.Name)).ToList();

        mail.Cc = string.Join(", ", message.Cc.Mailboxes.Select(s => s.ToString()));

        mail.Bcc = string.Join(", ", message.Bcc.Mailboxes.Select(s => s.ToString()));

        mail.Subject = message.Subject ?? string.Empty;

        mail.Important = message.GetImportance(log);

        mail.TextBodyOnly = false;

        mail.Introduction = "";

        mail.Attachments = new List<MailAttachmentData>();

        mail.HtmlBodyStream = new MemoryStream();

        mail.ExtractMainParts(message);

        mail.Size = mail.HtmlBodyStream.Length > 0 ? mail.HtmlBodyStream.Length : mail.HtmlBody.Length;

        mail.HeaderFieldNames = new NameValueCollection();

        message.Headers
            .ToList()
            .ForEach(h => mail.HeaderFieldNames.Add(h.Field, h.Value));

        var headers = message.Headers.ToList();

        if (headers.Exists(h => h.Id == HeaderId.DispositionNotificationTo))
            mail.ReadRequestStatus = true;

        mail.Folder = folder;

        mail.IsNew = unread;

        mail.StreamId = string.IsNullOrEmpty(streamId) ? MailUtil.CreateStreamId() : streamId;

        mail.LoadCalendarInfo(message, log);

        return mail;
    }

    public static MailMessageData CreateCorruptedMesage(this MimeMessage message,
        TenantManager tenantManager, CoreSettings coreSettings,
        FolderType folder = FolderType.Inbox,
        bool unread = false,
        string chainId = "",
        string streamId = "")
    {
        var mailMessage = new MailMessageData
        {
            HasParseError = true
        };

        MailUtil.SkipErrors(() => mailMessage.Date = MailUtil.IsDateCorrect(message.Date.UtcDateTime)
            ? message.Date.UtcDateTime
            : DateTime.UtcNow);

        MailUtil.SkipErrors(() => mailMessage.MimeMessageId = (string.IsNullOrEmpty(message.MessageId)
            ? MailUtil.CreateMessageId(tenantManager, coreSettings)
            : message.MessageId)
                .Trim('<', '>'));

        MailUtil.SkipErrors(() => mailMessage.ChainId = string.IsNullOrEmpty(chainId) ? mailMessage.MimeMessageId : chainId);

        MailUtil.SkipErrors(() => mailMessage.MimeReplyToId = mailMessage.ChainId.Equals(mailMessage.MimeMessageId) ? null : message.InReplyTo.Trim('<', '>'));

        MailUtil.SkipErrors(() => mailMessage.ReplyTo = message.ReplyTo.ToString());

        MailUtil.SkipErrors(() => mailMessage.From = message.From.ToString());

        MailUtil.SkipErrors(() =>
            mailMessage.FromEmail =
                message.From != null && message.From.Mailboxes != null && message.From.Mailboxes.Any()
                    ? message.From.Mailboxes.First().Address
                    : "");

        MailUtil.SkipErrors(() => mailMessage.ToList = message.To.Mailboxes.Select(s => MailUtil.ExecuteSafe(() => new MailAddress(s.Address, s.Name))).ToList());

        MailUtil.SkipErrors(() => mailMessage.To = string.Join(", ", message.To.Mailboxes.Select(s => s.ToString())));

        MailUtil.SkipErrors(() => mailMessage.CcList = message.Cc.Mailboxes.Select(s => MailUtil.ExecuteSafe(() => new MailAddress(s.Address, s.Name))).ToList());

        MailUtil.SkipErrors(() => mailMessage.Cc = string.Join(", ", message.Cc.Mailboxes.Select(s => s.ToString())));

        MailUtil.SkipErrors(() => mailMessage.Bcc = string.Join(", ", message.Bcc.Mailboxes.Select(s => s.ToString())));

        MailUtil.SkipErrors(() => mailMessage.Subject = message.Subject ?? string.Empty);

        MailUtil.SkipErrors(() => mailMessage.Important = message.Importance == MessageImportance.High || message.Priority == MessagePriority.Urgent);

        mailMessage.HtmlBodyStream = new MemoryStream();

        using (var sw = new StreamWriter(mailMessage.HtmlBodyStream, Encoding.UTF8, 1024, true))
        {
            sw.Write("<body><pre>&nbsp;</pre></body>");
            sw.Flush();
        }

        mailMessage.Size = mailMessage.HtmlBodyStream.Length;

        mailMessage.HeaderFieldNames = new NameValueCollection();

        message.Headers
            .ToList()
            .ForEach(h => MailUtil.SkipErrors(() => mailMessage.HeaderFieldNames.Add(h.Field, h.Value)));

        mailMessage.Folder = folder;
        mailMessage.IsNew = unread;
        mailMessage.StreamId = string.IsNullOrEmpty(streamId) ? MailUtil.CreateStreamId() : streamId;
        mailMessage.TextBodyOnly = true;
        mailMessage.Introduction = "";
        mailMessage.Attachments = new List<MailAttachmentData>();

        MailUtil.SkipErrors(() =>
        {
            var mailAttach = new MailAttachmentData
            {
                contentId = null,
                fileName = "message.eml",
                contentType = "message/rfc822",
                contentLocation = null,
                dataStream = new MemoryStream()
            };

            message.WriteTo(mailAttach.dataStream);

            mailAttach.size = mailAttach.dataStream.Length;

            mailMessage.Attachments.Add(mailAttach);
        });

        return mailMessage;
    }

    public static bool GetImportance(this MimeMessage message, ILogger log)
    {
        var headerPriority = message.Headers.FirstOrDefault(x => x.Id == HeaderId.XPriority)?.Value ?? string.Empty;

        log.LogDebug($"GetImportance headerPriority is {headerPriority}");

        return message.Importance == MessageImportance.High
            || message.Priority == MessagePriority.Urgent
            || message.XPriority == XMessagePriority.Highest
            || !string.IsNullOrEmpty(headerPriority);
    }
}
