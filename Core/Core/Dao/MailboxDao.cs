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

using IMailboxDao = ASC.Mail.Core.Dao.Interfaces.IMailboxDao;
using Mailbox = ASC.Mail.Core.Entities.Mailbox;
using SecurityContext = ASC.Core.SecurityContext;

namespace ASC.Mail.Core.Dao;

[Scope]
public class MailboxDao : BaseMailDao, IMailboxDao
{
    private readonly InstanceCrypto _instanceCrypto;

    private const int DELAY_AFTER_ERROR = 60;
    private readonly int delayAfterError = 0;
    private readonly int delay = 0;

    public MailboxDao(
         TenantManager tenantManager,
         SecurityContext securityContext,
         DbContextManager<MailDbContext> dbContext,
         InstanceCrypto instanceCrypto,
         MailSettings mailSettings)
        : base(tenantManager, securityContext, dbContext)
    {
        _instanceCrypto = instanceCrypto;
        delayAfterError = mailSettings.Defines.DefaultServerLoginDelayAfterError;
        delay = mailSettings.Defines.DefaultServerLoginDelay;
    }

    public Mailbox GetMailBox(IMailboxExp exp)
    {
        var mailbox = MailDbContext.MailMailboxes
            .AsNoTracking()
            .Where(exp.GetExpression())
            .Select(ToMailbox)
            .FirstOrDefault();

        return mailbox;
    }

    public List<Mailbox> GetMailBoxes(IMailboxesExp exp)
    {
        var query = MailDbContext.MailMailboxes
            .AsNoTracking()
            .Where(exp.GetExpression())
            .Select(ToMailbox);

        if (!string.IsNullOrEmpty(exp.OrderBy) && exp.OrderAsc.HasValue)
        {
            if ((bool)exp.OrderAsc)
            {
                query = query.OrderBy(b => b.DateChecked);
            }
            else
            {
                query = query.OrderByDescending(b => b.DateChecked);
            }
        }

        if (exp.Limit.HasValue)
        {
            query = query.Take(exp.Limit.Value);
        }

        return query.ToList();
    }

    public List<Mailbox> GetUniqueMailBoxes(IMailboxesExp exp)
    {
        var query = MailDbContext.MailMailboxes
            .AsNoTracking()
            .Where(exp.GetExpression())
            .Select(ToMailbox);

        if (!string.IsNullOrEmpty(exp.OrderBy) && exp.OrderAsc.HasValue)
        {
            if ((bool)exp.OrderAsc)
            {
                query = query.OrderBy(b => b.DateChecked);
            }
            else
            {
                query = query.OrderByDescending(b => b.DateChecked);
            }
        }

        if (exp.Limit.HasValue)
        {
            query = query.Take(exp.Limit.Value);
        }

        return query.ToList();
    }

    public Mailbox GetNextMailBox(IMailboxExp exp)
    {
        var mailbox = MailDbContext.MailMailboxes
            .AsNoTracking()
            .Where(exp.GetExpression())
            .OrderBy(mb => mb.Id)
            .Select(ToMailbox)
            .Take(1)
            .SingleOrDefault();

        return mailbox;
    }

    public Tuple<int, int> GetRangeMailboxes(IMailboxExp exp)
    {
        var mbIds = MailDbContext.MailMailboxes
            .AsNoTracking()
            .Where(exp.GetExpression())
            .OrderBy(mb => mb.Id)
            .Select(mb => (int)mb.Id)
            .ToList();

        var exists = mbIds.Any();

        var min = exists ? mbIds.First() : 0;
        var max = exists ? mbIds.Last() : 0;

        var result = new Tuple<int, int>(min, max);

        return result;
    }

    public List<Tuple<int, string>> GetMailUsers(IMailboxExp exp)
    {
        var list = MailDbContext.MailMailboxes
            .AsNoTracking()
            .Where(exp.GetExpression())
            .Select(mb => new Tuple<int, string>(mb.Tenant, mb.IdUser))
            .ToList();

        return list;
    }

    public int SaveMailBox(Mailbox mailbox)
    {
        var mailMailbox = new MailMailbox
        {
            Id = (uint)mailbox.Id,
            Tenant = mailbox.Tenant,
            IdUser = mailbox.User,
            Address = mailbox.Address,
            Name = mailbox.Name,
            Enabled = mailbox.Enabled,
            IsRemoved = mailbox.IsRemoved,
            IsProcessed = mailbox.IsProcessed,
            IsServerMailbox = mailbox.IsTeamlabMailbox,
            Imap = mailbox.Imap,
            UserOnline = mailbox.UserOnline,
            IsDefault = mailbox.IsDefault,
            MsgCountLast = mailbox.MsgCountLast,
            SizeLast = mailbox.SizeLast,
            LoginDelay = mailbox.LoginDelay,
            QuotaError = mailbox.QuotaError,
            ImapIntervals = mailbox.ImapIntervals,
            BeginDate = mailbox.BeginDate,
            EmailInFolder = mailbox.EmailInFolder,
            Pop3Password = _instanceCrypto.Encrypt(mailbox.Password),
            SmtpPassword = !string.IsNullOrEmpty(mailbox.SmtpPassword)
                    ? _instanceCrypto.Encrypt(mailbox.SmtpPassword)
                    : "",
            Token = !string.IsNullOrEmpty(mailbox.OAuthToken)
                    ? _instanceCrypto.Encrypt(mailbox.OAuthToken)
                    : "",
            TokenType = mailbox.OAuthType,
            IdSmtpServer = mailbox.SmtpServerId,
            IdInServer = mailbox.ServerId,
            DateChecked = mailbox.DateChecked,
            DateUserChecked = mailbox.DateUserChecked,
            DateLoginDelayExpires = mailbox.DateLoginDelayExpires,
            DateAuthError = mailbox.DateAuthError,
            DateCreated = mailbox.DateCreated
        };

        var result = MailDbContext.Entry(mailMailbox);
        result.State = mailMailbox.Id == 0
            ? EntityState.Added
            : EntityState.Modified;

        MailDbContext.SaveChanges();

        return (int)result.Entity.Id;
    }

    public bool SetMailboxRemoved(Mailbox mailbox)
    {
        var mailMailbox = new MailMailbox
        {
            Id = (uint)mailbox.Id,
            IsRemoved = true
        };

        MailDbContext.MailMailboxes.Attach(mailMailbox);
        MailDbContext.Entry(mailMailbox).Property(x => x.IsRemoved).IsModified = true;

        var result = MailDbContext.SaveChanges();

        return result > 0;
    }

    public bool RemoveMailbox(Mailbox mailbox, MailDbContext context)
    {
        var mailMailbox = new MailMailbox
        {
            Id = (uint)mailbox.Id
        };

        context.MailMailboxes.Remove(mailMailbox);

        var result = context.SaveChanges();

        return result > 0;
    }

    public bool Enable(IMailboxExp exp, bool enabled)
    {
        var mailboxes = MailDbContext.MailMailboxes.Where(exp.GetExpression()).ToList();

        if (!mailboxes.Any())
            return false;

        foreach (var mb in mailboxes)
        {
            mb.Enabled = enabled;
            if (enabled)
            {
                mb.DateAuthError = null;
            }
        }

        var result = MailDbContext.SaveChanges();

        return result > 0;
    }

    public bool SetNextLoginDelay(IMailboxExp exp, TimeSpan delay)
    {
        var mailboxes = MailDbContext.MailMailboxes
            .Where(exp.GetExpression());

        if (mailboxes == null)
            return false;

        foreach (var mailbox in mailboxes)
        {
            mailbox.IsProcessed = false;
            mailbox.DateLoginDelayExpires = DateTime.UtcNow.Add(delay);
        }

        var result = MailDbContext.SaveChanges();

        return result > 0;
    }

    public bool SetMailboxEmailIn(Mailbox mailbox, string emailInFolder)
    {
        var mailMailbox = MailDbContext.MailMailboxes
            .Where(mb => mb.Id == mailbox.Id
                && mb.Tenant == mailbox.Tenant
                && mb.IdUser == mailbox.User
                && mb.IsRemoved == false)
            .FirstOrDefault();

        if (mailMailbox == null)
            return false;

        mailMailbox.EmailInFolder = "" != emailInFolder ? emailInFolder : null;

        var result = MailDbContext.SaveChanges();

        return result > 0;
    }

    public bool SetMailboxesActivity(int tenant, string user, bool userOnline = true)
    {
        var mailMailbox = MailDbContext.MailMailboxes
            .Where(mb => mb.Tenant == tenant
                && mb.IdUser == user
                && mb.IsRemoved == false)
            .FirstOrDefault();

        if (mailMailbox == null)
            return false;

        mailMailbox.DateUserChecked = DateTime.UtcNow;
        mailMailbox.UserOnline = userOnline;

        var result = MailDbContext.SaveChanges();

        return result > 0;
    }

    public bool SetMailboxInProcess(int id)
    {
        int result = 0;
        try
        {
            var box = MailDbContext.MailMailboxes
                .Where(b => b.Id == id && b.IsProcessed == false && b.IsRemoved == false)
                .FirstOrDefault();

            if (box == null) return false;

            box.IsProcessed = true;
            box.DateChecked = DateTime.UtcNow;

            result = MailDbContext.SaveChanges();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            return false;
        }

        return (result > 0);
    }

    public bool ReleaseMailbox(Mailbox mailbox, MailboxReleasedOptions rOptions)
    {
        if (rOptions.ServerLoginDelay < delay)
            rOptions.ServerLoginDelay = delay;

        var rBox = MailDbContext.MailMailboxes.FirstOrDefault(b => b.Id == mailbox.Id);

        rBox.IsProcessed = false;
        rBox.DateChecked = DateTime.UtcNow;

        if (mailbox.DateAuthError.HasValue && mailbox.Enabled)
        {
            rBox.DateLoginDelayExpires = delayAfterError < DELAY_AFTER_ERROR
                ? DateTime.UtcNow.AddSeconds(DELAY_AFTER_ERROR)
                : DateTime.UtcNow.AddSeconds(delayAfterError); ;
        }
        else if (!mailbox.DateAuthError.HasValue)
        {
            rBox.DateLoginDelayExpires = rOptions.ServerLoginDelay > delay
                ? DateTime.UtcNow.AddSeconds(rOptions.ServerLoginDelay)
                : DateTime.UtcNow.AddSeconds(delay);
        }

        if (rOptions.Enabled.HasValue) rBox.Enabled = rOptions.Enabled.Value;
        if (rOptions.MessageCount.HasValue) rBox.MsgCountLast = rOptions.MessageCount.Value;
        if (rOptions.Size.HasValue) rBox.SizeLast = rOptions.Size.Value;
        if (rOptions.QuotaError.HasValue) rBox.QuotaError = rOptions.QuotaError.Value;
        if (!string.IsNullOrEmpty(rOptions.OAuthToken)) rBox.Token = _instanceCrypto.Encrypt(rOptions.OAuthToken);
        if (rOptions.ResetImapIntervals.HasValue)
        {
            rBox.ImapIntervals = null;
        }
        else
        {
            if (!string.IsNullOrEmpty(rOptions.ImapIntervalsJson))
            {
                rBox.ImapIntervals = rOptions.ImapIntervalsJson;
            }
        }

        var result = MailDbContext.SaveChanges();

        return result > 0;
    }

    public bool SetMailboxAuthError(int id, DateTime? authErrorDate)
    {
        var query = MailDbContext.MailMailboxes
            .Where(mb => mb.Id == id);

        if (authErrorDate.HasValue)
        {
            query.Where(mb => mb.DateAuthError == null);
        }

        var mailMailbox = query.FirstOrDefault();

        if (mailMailbox == null)
            return false;

        mailMailbox.DateAuthError = authErrorDate;
        mailMailbox.DateChecked = DateTime.UtcNow;

        var result = MailDbContext.SaveChanges();

        return result > 0;
    }

    public List<int> SetMailboxesProcessed(int timeout)
    {
        var mailboxes = MailDbContext.MailMailboxes
            .Where(mb => mb.IsProcessed == true
                && mb.DateChecked != null
                && EF.Functions.DateDiffMinute(mb.DateChecked, DateTime.UtcNow) > timeout)
            .ToList();

        if (!mailboxes.Any()) return new List<int>();

        mailboxes.ForEach(mb => mb.IsProcessed = false);

        var result = MailDbContext.SaveChanges();

        MailDbContext.ChangeTracker.Clear();

        return mailboxes.Select(mb => (int)mb.Id).ToList();
    }

    public bool CanAccessTo(IMailboxExp exp)
    {
        var foundIds = MailDbContext.MailMailboxes
            .AsNoTracking()
            .Where(exp.GetExpression())
            .Select(mb => mb.Id)
            .ToList();

        return foundIds.Any();
    }

    public MailboxStatus GetMailBoxStatus(IMailboxExp exp)
    {
        var status = MailDbContext.MailMailboxes
            .AsNoTracking()
            .Where(exp.GetExpression())
            .Select(ToMailboxStatus)
            .FirstOrDefault();

        return status;
    }

    protected MailboxStatus ToMailboxStatus(MailMailbox r)
    {
        var status = new MailboxStatus
        {
            Id = (int)r.Id,
            IsRemoved = r.IsRemoved,
            Enabled = r.Enabled,
            BeginDate = r.BeginDate
        };

        return status;
    }

    protected Mailbox ToMailbox(MailMailbox r)
    {
        var mb = new Mailbox
        {
            Id = (int)r.Id,
            User = r.IdUser,
            Tenant = r.Tenant,
            Address = r.Address,
            Enabled = r.Enabled,

            MsgCountLast = r.MsgCountLast,
            SizeLast = r.SizeLast,

            Name = r.Name,
            LoginDelay = r.LoginDelay,
            IsProcessed = r.IsProcessed,
            IsRemoved = r.IsRemoved,
            IsDefault = r.IsDefault,
            QuotaError = r.QuotaError,
            Imap = r.Imap,
            BeginDate = r.BeginDate,
            OAuthType = r.TokenType,

            ImapIntervals = r.ImapIntervals,
            SmtpServerId = r.IdSmtpServer,
            ServerId = r.IdInServer,
            EmailInFolder = r.EmailInFolder,
            IsTeamlabMailbox = r.IsServerMailbox,
            DateCreated = r.DateCreated.GetValueOrDefault(),
            DateChecked = r.DateChecked.GetValueOrDefault(),
            DateUserChecked = r.DateUserChecked.GetValueOrDefault(),
            UserOnline = r.UserOnline,
            DateLoginDelayExpires = r.DateLoginDelayExpires,
            DateAuthError = r.DateAuthError
        };

        string password = r.Pop3Password,
            smtpPassword = r.SmtpPassword,
            oAuthToken = r.Token;

        TryDecryptPassword(password, out password);

        mb.Password = password;

        if (!string.IsNullOrEmpty(smtpPassword))
        {
            TryDecryptPassword(smtpPassword, out smtpPassword);
        }

        mb.SmtpPassword = smtpPassword ?? "";

        TryDecryptPassword(oAuthToken, out oAuthToken);

        mb.OAuthToken = oAuthToken;

        return mb;
    }

    public bool TryDecryptPassword(string encryptedPassword, out string password)
    {
        password = "";
        try
        {
            if (string.IsNullOrEmpty(encryptedPassword))
                return false;

            password = _instanceCrypto.Decrypt(encryptedPassword);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
