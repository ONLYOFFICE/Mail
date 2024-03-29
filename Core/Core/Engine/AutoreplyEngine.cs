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
using SecurityContext = ASC.Core.SecurityContext;

namespace ASC.Mail.Core.Engine;

[Scope]
public class AutoreplyEngine
{
    private int Tenant => _tenantManager.GetCurrentTenant().Id;
    private string UserId => _securityContext.CurrentAccount.ID.ToString();

    private readonly SecurityContext _securityContext;
    private readonly TenantManager _tenantManager;
    private readonly IMailDaoFactory _mailDaoFactory;
    private readonly ServerEngine _serverEngine;
    private readonly CacheEngine _cacheEngine;
    private readonly ApiHelper _apiHelper;
    private readonly MailStorageManager _storageManager;
    private readonly MailSettings _mailSettings;
    private readonly int _autoreplyDaysInterval;
    private readonly ILogger _log;

    public AutoreplyEngine(
        SecurityContext securityContext,
        TenantManager tenantManager,
        IMailDaoFactory mailDaoFactory,
        ServerEngine serverEngine,
        CacheEngine cacheEngine,
        ApiHelper apiHelper,
        MailStorageManager storageManager,
        MailSettings mailSettings,
        ILoggerProvider logProvider)
    {
        _securityContext = securityContext;
        _tenantManager = tenantManager;
        _mailDaoFactory = mailDaoFactory;
        _serverEngine = serverEngine;
        _cacheEngine = cacheEngine;
        _apiHelper = apiHelper;
        _storageManager = storageManager;
        _mailSettings = mailSettings;

        _autoreplyDaysInterval = _mailSettings.Defines.AutoreplyDaysInterval;

        _log = logProvider.CreateLogger("ASC.Mail.AutoreplyEngine");
    }

    public MailAutoreplyData SaveAutoreply(int mailboxId, bool turnOn, bool onlyContacts,
        bool turnOnToDate, DateTime fromDate, DateTime toDate, string subject, string html)
    {
        if (fromDate == DateTime.MinValue)
            throw new ArgumentException(@"Invalid parameter", "fromDate");

        if (turnOnToDate && toDate == DateTime.MinValue)
            throw new ArgumentException(@"Invalid parameter", "toDate");

        if (turnOnToDate && toDate < fromDate)
            throw new ArgumentException(@"Wrong date interval, toDate < fromDate", "fromDate");

        if (string.IsNullOrEmpty(html))
            throw new ArgumentException(@"Invalid parameter", "html");

        html = _storageManager.ChangeEditorImagesLinks(html, mailboxId);

        var autoreply = new MailboxAutoreply
        {
            MailboxId = mailboxId,
            Tenant = Tenant,
            FromDate = fromDate,
            ToDate = toDate,
            Html = html,
            OnlyContacts = onlyContacts,
            TurnOnToDate = turnOnToDate,
            Subject = subject,
            TurnOn = turnOn
        };

        if (!_mailDaoFactory.GetMailboxDao().CanAccessTo(
            new ConcreteUserMailboxExp(mailboxId, Tenant, UserId)))
        {
            throw new AccessViolationException("Mailbox is not owned by user.");
        }

        _mailDaoFactory.GetMailboxAutoreplyDao().SaveAutoreply(autoreply);

        _cacheEngine.Clear(UserId);

        var resp = new MailAutoreplyData(autoreply.MailboxId, autoreply.Tenant, autoreply.TurnOn, autoreply.OnlyContacts,
            autoreply.TurnOnToDate, autoreply.FromDate, autoreply.ToDate, autoreply.Subject, autoreply.Html);

        return resp;
    }

    public void EnableAutoreply(MailBoxData account, bool enabled)
    {
        account.MailAutoreply.TurnOn = enabled;

        var autoreply = new MailboxAutoreply
        {
            MailboxId = account.MailBoxId,
            Tenant = account.TenantId,
            FromDate = account.MailAutoreply.FromDate,
            ToDate = account.MailAutoreply.ToDate,
            Html = account.MailAutoreply.Html,
            OnlyContacts = account.MailAutoreply.OnlyContacts,
            TurnOnToDate = account.MailAutoreply.TurnOnToDate,
            Subject = account.MailAutoreply.Subject,
            TurnOn = account.MailAutoreply.TurnOn
        };

        var result = _mailDaoFactory.GetMailboxAutoreplyDao().SaveAutoreply(autoreply);

        if (result <= 0)
            throw new InvalidOperationException();

        result = _mailDaoFactory.GetMailboxAutoreplyHistoryDao().DeleteAutoreplyHistory(account.MailBoxId);

        if (result <= 0)
            throw new InvalidOperationException();
    }

    public void SendAutoreply(MailBoxData account, MailMessageData message, string httpContextScheme)
    {
        try
        {
            if (message.Folder != FolderType.Inbox
                || account.MailAutoreply == null
                || !account.MailAutoreply.TurnOn)
            {
                return;
            }

            var utcNow = DateTime.UtcNow.Date;

            if (account.MailAutoreply.TurnOnToDate &&
                account.MailAutoreply.ToDate != DateTime.MinValue &&
                account.MailAutoreply.ToDate < utcNow)
            {
                _log.InfoAutoreplyEngineDisableAutoreply(account.MailBoxId);

                EnableAutoreply(account, false);

                return;
            }

            if (account.MailAutoreply.FromDate > utcNow)
            {
                _log.InfoAutoreplyEngineSkipAutoreplyUtc();
                return;
            }

            if (account.MailAutoreply.FromDate > message.Date)
            {
                _log.InfoAutoreplyEngineSkipAutoreplyDate();
                return;
            }

            if (MailUtil.IsMessageAutoGenerated(message))
            {
                _log.InfoAutoreplyEngineSkipAutoreplyAutoGeneratedHeader();
                return;
            }

            if (IsCurrentMailboxInFrom(account, message))
            {
                _log.InfoAutoreplyEngineSkipAutoreplyCurrentAccount();
                return;
            }

            var autoreplyEmail = GetAutoreplyEmailInTo(account, message);

            if (string.IsNullOrEmpty(autoreplyEmail))
            {
                _log.InfoAutoreplyEngineSkipAutoreplyNotFound();
                return;
            }

            if (HasGroupsInTo(account, message))
            {
                _log.InfoAutoreplyEngineSkipAutoreplyGroupAddress();
                return;
            }

            if (HasMailboxAutoreplyHistory(account, message.FromEmail))
            {
                _log.InfoAutoreplyEngineSkipAutoreplyAlreadySent();
                return;
            }

            if (account.MailAutoreply.OnlyContacts && !_apiHelper.SearchEmails(message.FromEmail).Any())
            {
                _log.InfoAutoreplyEngineSkipAutoreplyFromAddress();
                return;
            }

            _apiHelper.SendMessage(CreateAutoreply(account, message, autoreplyEmail), true);
            account.MailAutoreplyHistory.Add(message.FromEmail);

            _log.InfoAutoreplyEngineSend(autoreplyEmail);
        }
        catch (Exception ex)
        {
            _log.ErrorAutoreplyEngineSend(
                ex.ToString(), ex.InnerException != null ? ex.InnerException.ToString() : string.Empty,
                account.MailBoxId, account.UserId, account.TenantId);
        }
    }

    public void SaveAutoreplyHistory(MailBoxData account, MailMessageData messageItem)
    {
        var autoReplyHistory = new MailboxAutoreplyHistory
        {
            MailboxId = account.MailBoxId,
            SendingDate = DateTime.UtcNow,
            SendingEmail = new MailAddress(messageItem.To).Address,
            Tenant = account.TenantId
        };

        _mailDaoFactory.GetMailboxAutoreplyHistoryDao().SaveAutoreplyHistory(autoReplyHistory);
    }

    #region .Private

    private MailMessageData CreateAutoreply(MailBoxData account, MailMessageData messageItem, string autoreplyEmail)
    {
        var mailMessage = new MailMessageData();
        var stringBuilder = new StringBuilder(account.MailAutoreply.Subject);

        if (!string.IsNullOrEmpty(account.MailAutoreply.Subject))
            stringBuilder.Append(" ");

        mailMessage.Subject = stringBuilder.AppendFormat("Re: {0}", messageItem.Subject).ToString();
        mailMessage.HtmlBody = account.MailAutoreply.Html;
        mailMessage.MimeReplyToId = messageItem.MimeMessageId;
        mailMessage.To = messageItem.FromEmail;
        mailMessage.From = autoreplyEmail ?? account.EMail.ToString();

        if (account.MailSignature == null)
        {
            var signature = _mailDaoFactory.GetMailboxSignatureDao().GetSignature(account.MailBoxId);

            if (signature != null)
            {
                account.MailSignature = new MailSignatureData(signature.MailboxId, signature.Tenant, signature.Html,
                    signature.IsActive);
            }
            else
            {
                account.MailSignature = new MailSignatureData(account.MailBoxId, account.TenantId, "", false);
            }
        }

        if (account.MailSignature != null && account.MailSignature.IsActive)
        {
            mailMessage.HtmlBody = new StringBuilder(mailMessage.HtmlBody).AppendFormat(
                @"<div class='tlmail_signature' mailbox_id='{0}'
                         style='font-family:open sans,sans-serif; font-size:12px; margin:0px;'>
                         <div>{1}</div>
                      </div>",
                account.MailBoxId, account.MailSignature.Html).ToString();
        }
        return mailMessage;
    }

    private bool HasMailboxAutoreplyHistory(MailBoxData account, string email)
    {
        if (account.MailAutoreplyHistory == null)
            account.MailAutoreplyHistory = new List<string>();

        if (account.MailAutoreplyHistory.Contains(email))
            return true;

        var emails = _mailDaoFactory.GetMailboxAutoreplyHistoryDao()
            .GetAutoreplyHistorySentEmails(account.MailBoxId, email, _autoreplyDaysInterval);

        if (!emails.Any())
            return false;

        account.MailAutoreplyHistory.Add(email);

        return true;
    }

    private bool HasGroupsInTo(MailBoxData account, MailMessageData messageItem)
    {
        if (!account.IsTeamlab)
            return false;

        if (account.Groups == null)
        {
            account.Groups = _serverEngine.GetGroups(account.MailBoxId);
        }

        foreach (var group in account.Groups)
        {
            if (messageItem.ToList.Any(
                address =>
                    string.Equals(address.Address, group.Email, StringComparison.InvariantCultureIgnoreCase)))
            {
                return true;
            }

            if (messageItem.CcList.Any(
                address =>
                    string.Equals(address.Address, group.Email, StringComparison.InvariantCultureIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private string GetAutoreplyEmailInTo(MailBoxData account, MailMessageData messageItem)
    {
        var autoreplyAddress = GetAddressInList(account, messageItem.ToList) ??
                               GetAddressInList(account, messageItem.CcList);
        return autoreplyAddress;
    }

    private string GetAddressInList(MailBoxData account, List<MailAddress> list)
    {
        if (list.Any(address =>
                string.Equals(address.Address, account.EMail.Address, StringComparison.InvariantCultureIgnoreCase)))
        {
            return account.EMail.ToString();
        }

        if (!account.IsTeamlab)
            return null;

        if (account.Aliases == null)
        {
            account.Aliases = _serverEngine.GetAliases(account.MailBoxId);
        }

        var result = (from address in list
                      from alias in account.Aliases
                      where string.Equals(address.Address, alias.Email, StringComparison.InvariantCultureIgnoreCase)
                      select alias.Email)
            .FirstOrDefault();

        return result;
    }

    private bool IsCurrentMailboxInFrom(MailBoxData account, MailMessageData messageItem)
    {
        if (string.Equals(messageItem.FromEmail, account.EMail.Address, StringComparison.InvariantCultureIgnoreCase))
            return true;

        if (!account.IsTeamlab)
            return false;

        if (account.Aliases == null)
        {
            account.Aliases = _serverEngine.GetAliases(account.MailBoxId);
        }

        return
            account.Aliases.Any(
                alias =>
                    string.Equals(messageItem.FromEmail, alias.Email, StringComparison.InvariantCultureIgnoreCase));
    }

    #endregion

}
