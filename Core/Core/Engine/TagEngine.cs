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

using ASC.Mail.Core.Log;

using CrmTag = ASC.Mail.Core.Entities.CrmTag;
using FolderType = ASC.Mail.Enums.FolderType;
using SecurityContext = ASC.Core.SecurityContext;
using Tag = ASC.Mail.Core.Entities.Tag;

namespace ASC.Mail.Core.Engine;

[Scope]
public class TagEngine
{
    private int Tenant => _tenantManager.GetCurrentTenant().Id;
    private string UserId => _securityContext.CurrentAccount.ID.ToString();

    private readonly TenantManager _tenantManager;
    private readonly SecurityContext _securityContext;
    private readonly ILogger<TagEngine> _log;
    private readonly IMailDaoFactory _mailDaoFactory;
    private readonly WebItemSecurity _webItemSecurity;

    public TagEngine(
        TenantManager tenantManager,
        SecurityContext securityContext,
        IMailDaoFactory mailDaoFactory,
        WebItemSecurity webItemSecurity,
        ILogger<TagEngine> log)
    {
        _tenantManager = tenantManager;
        _securityContext = securityContext;

        _mailDaoFactory = mailDaoFactory;

        _webItemSecurity = webItemSecurity;

        _log = log;
    }

    public Tag GetTag(int id)
    {
        return _mailDaoFactory.GetTagDao().GetTag(id);
    }

    public Tag GetTag(string name)
    {
        return _mailDaoFactory.GetTagDao().GetTag(name);
    }

    public List<Tag> GetTags()
    {
        var tagList = _mailDaoFactory.GetTagDao().GetTags();

        if (!_webItemSecurity.IsAvailableForMe(WebItemManager.CRMProductID))
        {
            return tagList
                .Where(p => p.TagName != "")
                .OrderByDescending(p => p.Id)
                .ToList();
        }

        var actualCrmTags = _mailDaoFactory.GetTagDao().GetCrmTags();

        var removedCrmTags =
            tagList.Where(t => t.Id < 0 && !actualCrmTags.Exists(ct => ct.Id == t.Id))
                .ToList();

        if (removedCrmTags.Any())
        {
            _mailDaoFactory.GetTagDao().DeleteTags(removedCrmTags.Select(t => t.Id).ToList());
            removedCrmTags.ForEach(t => tagList.Remove(t));
        }

        foreach (var crmTag in actualCrmTags)
        {
            var tag = tagList.FirstOrDefault(t => t.Id == crmTag.Id);
            if (tag != null)
                tag.TagName = crmTag.TagName;
            else
                tagList.Add(crmTag);
        }

        return tagList
            .Where(p => !string.IsNullOrEmpty(p.TagName))
            .OrderByDescending(p => p.Id)
            .ToList();
    }

    public List<CrmTag> GetCrmTags(string email)
    {
        var tags = new List<CrmTag>();

        var allowedContactIds = _mailDaoFactory.GetCrmContactDao().GetCrmContactIds(email);

        if (!allowedContactIds.Any())
            return tags;

        tags = _mailDaoFactory.GetTagDao().GetCrmTags(allowedContactIds);

        return tags
            .Where(p => !string.IsNullOrEmpty(p.TagTitle))
            .OrderByDescending(p => p.TagId)
            .ToList();
    }

    public bool IsTagExists(string name)
    {
        var tag = _mailDaoFactory.GetTagDao().GetTag(name);

        return tag != null;

    }

    public Tag CreateTag(string name, string style, IEnumerable<string> addresses)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentNullException("name");

        //TODO: Need transaction?

        var tag = _mailDaoFactory.GetTagDao().GetTag(name);

        if (tag != null)
            throw new ArgumentException("Tag name already exists");

        var emails = addresses as IList<string> ?? addresses.ToList();

        tag = new Tag
        {
            Id = 0,
            TagName = name,
            Tenant = Tenant,
            User = UserId,
            Addresses = string.Join(";", emails),
            Style = style,
            Count = 0,
            CrmId = 0
        };

        var id = _mailDaoFactory.GetTagDao().SaveTag(tag);

        if (id < 0)
            throw new Exception("Save failed");

        foreach (var email in emails)
        {
            _mailDaoFactory.GetTagAddressDao().Save(id, email);
        }

        tag.Id = id;

        //Commit transaction

        return tag;
    }

    public Tag UpdateTag(int id, string name, string style, IEnumerable<string> addresses)
    {
        var tag = _mailDaoFactory.GetTagDao().GetTag(id);

        if (tag == null)
            throw new ArgumentException(@"Tag not found");

        if (!tag.TagName.Equals(name))
        {
            var tagByName = _mailDaoFactory.GetTagDao().GetTag(name);

            if (tagByName != null && tagByName.Id != id)
                throw new ArgumentException(@"Tag name already exists");

            tag.TagName = name;
            tag.Style = style;
        }

        //Start transaction
        var oldAddresses = _mailDaoFactory.GetTagAddressDao().GetTagAddresses(tag.Id);

        var newAddresses = addresses as IList<string> ?? addresses.ToList();
        tag.Addresses = string.Join(";", newAddresses);

        _mailDaoFactory.GetTagDao().SaveTag(tag);

        if (!newAddresses.Any())
        {
            if (oldAddresses.Any())
                _mailDaoFactory.GetTagAddressDao().Delete(tag.Id);
        }
        else
        {
            foreach (var oldAddress in oldAddresses)
            {
                if (!newAddresses.Contains(oldAddress))
                {
                    _mailDaoFactory.GetTagAddressDao().Delete(tag.Id, oldAddress);
                }
            }

            foreach (var newAddress in newAddresses)
            {
                if (!oldAddresses.Contains(newAddress))
                {
                    _mailDaoFactory.GetTagAddressDao().Save(tag.Id, newAddress);
                }
            }
        }

        //Commit transaction

        return tag;
    }

    public bool DeleteTag(int id)
    {
        //Begin transaction

        _mailDaoFactory.GetTagDao().DeleteTag(id);

        _mailDaoFactory.GetTagAddressDao().Delete(id);

        _mailDaoFactory.GetTagMailDao().DeleteByTagId(id);

        //Commit transaction

        return true;
    }

    public List<int> GetOrCreateTags(int tenant, string user, string[] names)
    {
        var tagIds = new List<int>();

        if (!names.Any())
            return tagIds;


        var tags = _mailDaoFactory.GetTagDao().GetTags();

        foreach (var name in names)
        {
            var tag =
                tags.FirstOrDefault(t => t.TagName.Equals(name, StringComparison.InvariantCultureIgnoreCase));

            if (tag != null)
            {
                tagIds.Add(tag.Id);
                continue;
            }

            tag = new Tag
            {
                Id = 0,
                TagName = name,
                Addresses = "",
                Count = 0,
                CrmId = 0,
                Style = (Math.Abs(name.GetHashCode() % 16) + 1).ToString(CultureInfo.InvariantCulture),
                Tenant = tenant,
                User = user
            };

            var id = _mailDaoFactory.GetTagDao().SaveTag(tag);

            if (id > 0)
            {
                _log.InfoTagEngineTagCreated(name, id);

                tagIds.Add(id);
            }
        }

        return tagIds;
    }

    public void SetMessagesTag(List<int> messageIds, int tagId)
    {
        using (var tx = _mailDaoFactory.BeginTransaction())
        {
            if (!SetMessagesTag(_mailDaoFactory, messageIds, tagId))
            {
                tx.Rollback();
                return;
            }

            tx.Commit();
        }

        UpdateIndexerTags(messageIds, UpdateAction.Add, tagId);

        var ids = string.Join(",", messageIds);

        _log.InfoTagEngineTagAdded(tagId, ids);
    }

    public bool SetMessagesTag(IMailDaoFactory daoFactory, List<int> messageIds, int tagId)
    {
        var tag = _mailDaoFactory.GetTagDao().GetTag(tagId);

        if (tag == null)
        {
            return false;
        }

        GetValidForUserMessages(messageIds, out List<int> validIds, out List<ChainInfo> chains);

        _mailDaoFactory.GetTagMailDao().SetMessagesTag(validIds, tag.Id);

        UpdateTagsCount(tag);

        foreach (var chain in chains)
        {
            UpdateChainTags(chain.Id, chain.Folder, chain.MailboxId);
        }

        // Change time_modified for index
        _mailDaoFactory.GetMailDao().SetMessagesChanged(validIds);

        return true;
    }

    public void UpdateChainTags(string chainId, FolderType folder, int mailboxId)
    {
        var tags = _mailDaoFactory.GetTagMailDao().GetChainTags(chainId, folder, mailboxId);

        var updateQuery = SimpleConversationsExp.CreateBuilder(Tenant, UserId)
                .SetChainId(chainId)
                .SetMailboxId(mailboxId)
                .SetFolder((int)folder)
                .Build();

        _mailDaoFactory.GetChainDao().SetFieldValue(
            updateQuery,
            "Tags",
            tags);
    }

    public void UnsetMessagesTag(List<int> messageIds, int tagId)
    {
        List<int> validIds;

        using (var tx = _mailDaoFactory.BeginTransaction())
        {
            GetValidForUserMessages(messageIds, out validIds, out List<ChainInfo> chains);

            _mailDaoFactory.GetTagMailDao().Delete(tagId, validIds);

            var tag = _mailDaoFactory.GetTagDao().GetTag(tagId);

            if (tag != null)
                UpdateTagsCount(tag);

            foreach (var chain in chains)
            {
                UpdateChainTags(chain.Id, chain.Folder, chain.MailboxId);
            }

            // Change time_modified for index
            _mailDaoFactory.GetMailDao().SetMessagesChanged(validIds);

            tx.Commit();
        }

        UpdateIndexerTags(validIds, UpdateAction.Remove, tagId);
    }

    public void SetConversationsTag(IEnumerable<int> messagesIds, int tagId)
    {
        var ids = messagesIds as IList<int> ?? messagesIds.ToList();

        if (!ids.Any()) return;

        List<int> validIds;

        using (var tx = _mailDaoFactory.BeginTransaction())
        {
            var tag = _mailDaoFactory.GetTagDao().GetTag(tagId);

            if (tag == null)
            {
                tx.Rollback();
                return;
            }

            var foundedChains = _mailDaoFactory.GetMailInfoDao().GetChainedMessagesInfo(messagesIds.ToList());

            if (!foundedChains.Any())
            {
                tx.Rollback();
                return;
            }

            validIds = foundedChains.Select(r => r.Id).ToList();
            var chains =
                foundedChains.GroupBy(r => new { r.ChainId, r.Folder, r.MailboxId })
                    .Select(
                        r =>
                            new ChainInfo
                            {
                                Id = r.Key.ChainId,
                                Folder = r.Key.Folder,
                                MailboxId = r.Key.MailboxId
                            });

            _mailDaoFactory.GetTagMailDao().SetMessagesTag(validIds, tag.Id);

            UpdateTagsCount(tag);

            foreach (var chain in chains)
            {
                UpdateChainTags(chain.Id, chain.Folder, chain.MailboxId);
            }

            // Change time_modified for index
            _mailDaoFactory.GetMailDao().SetMessagesChanged(validIds);

            tx.Commit();
        }

        UpdateIndexerTags(validIds, UpdateAction.Add, tagId);
    }

    public void UnsetConversationsTag(IEnumerable<int> messagesIds, int tagId)
    {
        var ids = messagesIds as IList<int> ?? messagesIds.ToList();

        if (!ids.Any()) return;

        List<int> validIds;

        using (var tx = _mailDaoFactory.BeginTransaction())
        {
            var foundedChains = _mailDaoFactory.GetMailInfoDao().GetChainedMessagesInfo(messagesIds.ToList());

            if (!foundedChains.Any())
            {
                tx.Rollback();
                return;
            }

            validIds = foundedChains.Select(r => r.Id).ToList();

            var chains =
                foundedChains.GroupBy(r => new { r.ChainId, r.Folder, r.MailboxId })
                    .Select(
                        r =>
                            new ChainInfo
                            {
                                Id = r.Key.ChainId,
                                Folder = r.Key.Folder,
                                MailboxId = r.Key.MailboxId
                            });

            _mailDaoFactory.GetTagMailDao().Delete(tagId, validIds);

            var tag = _mailDaoFactory.GetTagDao().GetTag(tagId);

            if (tag != null)
                UpdateTagsCount(tag);

            foreach (var chain in chains)
            {
                UpdateChainTags(chain.Id, chain.Folder, chain.MailboxId);
            }

            // Change time_modified for index
            _mailDaoFactory.GetMailDao().SetMessagesChanged(validIds);

            tx.Commit();
        }

        UpdateIndexerTags(validIds, UpdateAction.Remove, tagId);
    }

    private void UpdateIndexerTags(List<int> ids, UpdateAction action, int tagId)
    {
        //TODO: because error when query

        /*
         * Type: script_exception Reason: "runtime error" 
         * CausedBy: "Type: illegal_argument_exception 
         * Reason: "dynamic method [java.util.HashMap, contains/1] not found""
         */

        //var t = ServiceProvider.GetService<MailMail>();
        //if (!FactoryIndexer.Support(t) || !FactoryIndexerCommon.CheckState(false))
        return;

        /*if (ids == null || !ids.Any())
            return;

        var data = new MailMail
        {
            Tags = new List<MailTag>
                {
                    new MailTag
                    {
                        Id = tagId
                    }
                }
        };

        Expression<Func<Selector<MailMail>, Selector<MailMail>>> exp =
            s => s.In(m => m.Id, ids.ToArray());

        IndexEngine.Update(data, exp, action, s => s.Tags.ToList());*/
    }

    private void UpdateTagsCount(Tag tag)
    {
        var count = _mailDaoFactory.GetTagMailDao().CalculateTagCount(tag.Id);

        tag.Count = count;

        _mailDaoFactory.GetTagDao().SaveTag(tag);
    }

    private void GetValidForUserMessages(List<int> messagesIds, out List<int> validIds,
        out List<ChainInfo> chains)
    {
        var mailInfoList = _mailDaoFactory.GetMailInfoDao().GetMailInfoList(
            SimpleMessagesExp.CreateBuilder(Tenant, UserId)
                .SetMessageIds(messagesIds)
                .Build());

        validIds = new List<int>();
        chains = new List<ChainInfo>();

        foreach (var mailInfo in mailInfoList)
        {
            validIds.Add(mailInfo.Id);
            chains.Add(new ChainInfo
            {
                Id = mailInfo.ChainId,
                Folder = mailInfo.Folder,
                MailboxId = mailInfo.MailboxId
            });
        }
    }
}
