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

using ASC.Mail.Core.Log;

namespace ASC.Mail.Core.Engine;

[Scope]
public class IndexEngine
{
    private readonly FactoryIndexerMailMail _factoryIndexerMailMail;
    private readonly FactoryIndexer _factoryIndexerCommon;
    private readonly IServiceProvider _serviceProvider;
    private readonly IMailDaoFactory _mailDaoFactory;
    private readonly ILogger<IndexEngine> _log;

    public IndexEngine(
        FactoryIndexerMailMail factoryIndexerMailMail,
        FactoryIndexer factoryIndexerCommon,
        IServiceProvider serviceProvider,
        IMailDaoFactory mailDaoFactory,
        ILogger<IndexEngine> log)
    {
        _factoryIndexerMailMail = factoryIndexerMailMail;
        _factoryIndexerCommon = factoryIndexerCommon;
        _serviceProvider = serviceProvider;
        _mailDaoFactory = mailDaoFactory;
        _log = log;
    }

    public bool IsIndexAvailable()
    {
        var service = _serviceProvider.GetService<MailMail>();

        if (!_factoryIndexerMailMail.Support(service))
        {
            _log.InfoIndexEngineSupportFalse(service.IndexName);
            return false;
        }

        if (!_factoryIndexerCommon.CheckState(false))
        {
            _log.InfoIndexEngineCheckStateFalse();
            return false;
        }

        return true;
    }

    public void Add<T>(T data) where T : class, ISearchItem, new()
    {
        try
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            if (!IsIndexAvailable()) return;

            var entityType = data.GetType();

            if (entityType == typeof(MailMail))
            {
                var indexer = _serviceProvider.GetService<FactoryIndexerMailMail>();
                var mail = data as MailMail;
                indexer.Index(InitMailDocument(mail));
            }
            else if (entityType == typeof(MailContact))
            {
                var indexer = _serviceProvider.GetService<FactoryIndexerMailContact>();
                var contact = data as MailContact;
                indexer.Index(contact);
            }
            else
            {
                //?? some other entities with index
            }

            _log.InfoIndexEngineAddSuccess(typeof(T), data == null ? -1 : data.Id);
        }
        catch (Exception ex)
        {
            _log.ErrorIndexEngineAdd(typeof(T), data == null ? -1 : data.Id, ex.ToString());
        }
    }

    private MailMail InitMailDocument(MailMail mail)
    {
        using var scope = _serviceProvider.CreateScope();
        var tenantManager = scope.ServiceProvider.GetService<TenantManager>();

        tenantManager.SetCurrentTenant(mail.TenantId);

        mail.Document = new Document
        {
            Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(""))
        };

        if (!_factoryIndexerMailMail.CanIndexByContent(mail)) return mail;

        try
        {
            var data = _mailDaoFactory.GetMailDao().GetDocumentData(mail);

            if (!string.IsNullOrEmpty(data))
            {
                mail.Document.Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(data));
                return mail;
            }

            using (var stream = _mailDaoFactory.GetMailDao().GetDocumentStream(mail))
            {
                if (stream == null) return mail;

                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    mail.Document.Data = Convert.ToBase64String(ms.GetBuffer());
                }
            }

            return mail;
        }
        catch (FileNotFoundException e)
        {
            _log.ErrorIndexEngineFileNotFound(e.ToString());
        }
        catch (Exception e)
        {
            _log.ErrorIndexEngine(e.ToString());
        }

        return mail;
    }

    public void Update(List<MailMail> mails, UpdateAction action, Expression<Func<MailMail, IList>> fields)
    {
        try
        {
            if (mails == null || !mails.Any())
                throw new ArgumentNullException("mails");

            if (!IsIndexAvailable())
                return;

            var indexer = _serviceProvider.GetService<FactoryIndexer<MailMail>>();

            mails.ForEach(x => indexer.Update(x, action, fields));
        }
        catch (Exception ex)
        {
            _log.ErrorIndexEngineUpdate(mails == null ? 0 : mails.Count, ex.ToString());
        }
    }

    public void Update(MailMail data, Expression<Func<Selector<MailMail>, Selector<MailMail>>> expression,
        UpdateAction action, Expression<Func<MailMail, IList>> fields)
    {
        try
        {
            if (data == null)
                throw new ArgumentNullException("data");

            if (expression == null)
                throw new ArgumentNullException("expression");

            if (!IsIndexAvailable())
                return;

            var indexer = _serviceProvider.GetService<FactoryIndexer<MailMail>>();

            indexer.Update(data, expression, action, fields);
        }
        catch (Exception ex)
        {
            _log.ErrorIndexEngineUpdate(ex.ToString());
        }
    }

    public void Update(MailMail data, Expression<Func<Selector<MailMail>, Selector<MailMail>>> expression,
        params Expression<Func<MailMail, object>>[] fields)
    {
        try
        {
            if (data == null)
                throw new ArgumentNullException("data");

            if (expression == null)
                throw new ArgumentNullException("expression");

            if (!IsIndexAvailable())
                return;

            var indexer = _serviceProvider.GetService<FactoryIndexer<MailMail>>();

            indexer.Update(data, expression, true, fields);
        }
        catch (Exception ex)
        {
            _log.ErrorIndexEngineUpdate(ex.ToString());
        }
    }

    public void Update<T>(List<T> list, params Expression<Func<T, object>>[] fields) where T : class, ISearchItem, new()
    {
        try
        {
            if (list == null || !list.Any())
                throw new ArgumentNullException("list");

            if (!IsIndexAvailable())
                return;

            var indexer = _serviceProvider.GetService<FactoryIndexer<T>>();

            list.ForEach(x => indexer.Update(x, true, fields));
        }
        catch (Exception ex)
        {
            var typeParameterType = typeof(T);

            _log.ErrorIndexEngineUpdate(typeParameterType, list == null ? 0 : list.Count, ex.ToString());
        }
    }

    public void Remove(List<int> ids, int tenant, Guid user)
    {
        try
        {
            if (ids == null || !ids.Any())
                throw new ArgumentNullException("ids");

            if (!IsIndexAvailable())
                return;

            var indexer = _serviceProvider.GetService<FactoryIndexer<MailMail>>();

            ids.ForEach(id =>
                indexer.Delete(
                    r => new Selector<MailMail>(_serviceProvider)
                        .Where(m => m.Id, id)
                        .Where(e => e.UserId, user.ToString())
                        .Where(e => e.TenantId, tenant)));
        }
        catch (Exception ex)
        {
            _log.ErrorIndexEngineRemoveIds(ids == null ? 0 : ids.Count, ex.ToString());
        }
    }

    public void Remove(MailBoxData mailBox)
    {
        try
        {
            if (mailBox == null)
                throw new ArgumentNullException("mailBox");

            if (!IsIndexAvailable())
                return;

            var selector = new Selector<MailMail>(_serviceProvider)
                .Where(m => m.MailboxId, mailBox.MailBoxId)
                .Where(e => e.UserId, mailBox.UserId)
                .Where(e => e.TenantId, mailBox.TenantId);

            var indexer = _serviceProvider.GetService<FactoryIndexer<MailMail>>();

            indexer.Delete(r => selector);
        }
        catch (Exception ex)
        {
            _log.ErrorIndexEngineRemoveId(mailBox == null ? -1 : mailBox.MailBoxId, ex.ToString());
        }
    }

    public void RemoveContacts(List<int> ids, int tenant, Guid user)
    {
        try
        {
            if (ids == null || !ids.Any())
                throw new ArgumentNullException("ids");

            if (!IsIndexAvailable())
                return;

            var indexer = _serviceProvider.GetService<FactoryIndexer<MailContact>>();

            indexer.Delete(
                r => new Selector<MailContact>(_serviceProvider)
                    .In(s => s.Id, ids.ToArray())
                    .Where(e => e.IdUser, user.ToString())
                    .Where(e => e.TenantId, tenant));
        }
        catch (Exception ex)
        {
            _log.ErrorIndexEngineRemoveContacts(ids == null ? 0 : ids.Count, ex.ToString());
        }
    }
}
