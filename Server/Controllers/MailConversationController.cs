﻿using ASC.Api.Core;
using ASC.Mail.Enums;
using ASC.Mail.Models;

using Microsoft.AspNetCore.Mvc;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ASC.Mail.Controllers
{
    public partial class MailController : ControllerBase
    {
        /// <summary>
        ///    Returns filtered conversations, if were changes since last check date
        /// </summary>
        /// <param optional="true" name="folder">Folder ID - integer. 1 - inbox, 2 - sent, 3 - drafts, 4 - trash, 5 - spam.</param>
        /// <param optional="true" name="unread">Message unread status. bool flag. Search in unread(true), read(false) or all(null) messages.</param>
        /// <param optional="true" name="attachments">Message attachments status. bool flag. Search messages with attachments(true), without attachments(false) or all(null) messages.</param>
        /// <param optional="true" name="period_from">Begin search period date</param>
        /// <param optional="true" name="period_to">End search period date</param>
        /// <param optional="true" name="important">Message has importance flag. bool flag.</param>
        /// <param optional="true" name="from_address">Address to find 'From' field</param>
        /// <param optional="true" name="to_address">Address to find 'To' field</param>
        /// <param optional="true" name="mailbox_id">Recipient mailbox id.</param>
        /// <param optional="true" name="tags">Messages tags. Id of tags linked with target messages.</param>
        /// <param optional="true" name="search">Text to search in messages body and subject.</param>
        /// <param optional="true" name="page_size">Count of messages on page</param>
        /// <param name="sortorder">Sort order by date. String parameter: "ascending" - ascended, "descending" - descended.</param> 
        /// <param optional="true" name="from_date">Date from wich conversations search performed</param>
        /// <param optional="true" name="from_message">Message from wich conversations search performed</param>
        /// <param optional="true" name="with_calendar">Message has calendar flag. bool flag.</param>
        /// <param optional="true" name="user_folder_id">id of user's folder</param>
        /// <param name="prev_flag"></param>
        /// <returns>List of filtered chains</returns>
        /// <short>Gets filtered conversations</short>
        /// <category>Conversations</category>
        [HttpGet(@"conversations")]
        public IEnumerable<MailMessageData> GetFilteredConversations(int? folder,
            bool? unread,
            bool? attachments,
            long? period_from,
            long? period_to,
            bool? important,
            string from_address,
            string to_address,
            int? mailbox_id,
            IEnumerable<int> tags,
            string search,
            int? page_size,
            string sortorder,
            ApiDateTime from_date,
            int? from_message,
            bool? prev_flag,
            bool? with_calendar,
            int? user_folder_id)
        {
            var primaryFolder = user_folder_id.HasValue
                ? FolderType.UserFolder
                : folder.HasValue ? (FolderType)folder.Value : FolderType.Inbox;

            var filter = new MailSearchFilterData
            {
                PrimaryFolder = primaryFolder,
                Unread = unread,
                Attachments = attachments,
                PeriodFrom = period_from,
                PeriodTo = period_to,
                Important = important,
                FromAddress = from_address,
                ToAddress = to_address,
                MailboxId = mailbox_id,
                CustomLabels = new List<int>(tags),
                SearchText = search,
                PageSize = page_size.GetValueOrDefault(25),
                Sort = DefineConstants.ORDER_BY_DATE_CHAIN,
                SortOrder = sortorder,
                WithCalendar = with_calendar,
                UserFolderId = user_folder_id,
                FromDate = from_date,
                FromMessage = from_message.GetValueOrDefault(0),
                PrevFlag = prev_flag.GetValueOrDefault(false)
            };

            var conversations = _messageEngine.GetConversations(filter, out bool hasMore);

            if (hasMore)
            {
                _apiContext.SetTotalCount(page_size.GetValueOrDefault(25) + 1);
            }
            else
            {
                _apiContext.SetTotalCount(conversations.Count);
            }

            return conversations;
        }

        /// <summary>
        /// Get list of messages linked into one chain (conversation)
        /// </summary>
        /// <param name="id">ID of any message in the chain</param>
        /// <param name="loadAll">Load content of all messages</param>
        /// <param optional="true" name="markRead">Mark conversation as read</param>
        /// <param optional="true" name="needSanitize">Flag specifies is needed to prepare html for FCKeditor</param>
        /// <returns>List messages linked in one chain</returns>
        /// <category>Conversations</category>
        /// <exception cref="ArgumentException">Exception happens when in parameters is invalid. Text description contains parameter name and text description.</exception>
        [HttpGet(@"conversation/{id}")]
        public IEnumerable<MailMessageData> GetConversation(int id, bool? loadAll, bool? markRead, bool? needSanitize)
        {
            var list = _messageEngine.GetConversation(id, loadAll, markRead, needSanitize);

            return list;
        }

        /// <summary>
        /// Get previous or next conversation id.
        /// </summary>
        /// <param name="id">Head message id of current conversation.</param>
        /// <param name="direction">String parameter for determine prev or next conversation needed. "prev" for previous, "next" for next.</param>
        /// <param optional="true" name="folder">Folder ID - integer. 1 - inbox, 2 - sent, 5 - spam.</param>
        /// <param optional="true" name="unread">Message unread status. bool flag. Search in unread(true), read(false) or all(null) messages.</param>
        /// <param optional="true" name="attachments">Message attachments status. bool flag. Search messages with attachments(true), without attachments(false) or all(null) messages.</param>
        /// <param optional="true" name="period_from">Begin search period date</param>
        /// <param optional="true" name="period_to">End search period date</param>
        /// <param optional="true" name="important">Message has importance flag. bool flag.</param>
        /// <param optional="true" name="from_address">Address to find 'From' field</param>
        /// <param optional="true" name="to_address">Address to find 'To' field</param>
        /// <param optional="true" name="mailbox_id">Recipient mailbox id.</param>
        /// <param optional="true" name="tags">Messages tags. Id of tags linked with target messages.</param>
        /// <param optional="true" name="search">Text to search in messages body and subject.</param>
        /// <param optional="true" name="page_size">Count on messages on page</param>
        /// <param name="sortorder">Sort order by date. String parameter: "ascending" - ascended, "descending" - descended.</param>
        /// <param optional="true" name="with_calendar">Message has with_calendar flag. bool flag.</param> 
        /// <param optional="true" name="user_folder_id">id of user's folder</param>
        /// <returns>Head message id of previous or next conversation.</returns>
        /// <category>Conversations</category>
        [HttpGet(@"conversation/{id}/{direction}")]
        public long GetPrevNextConversationId(int id,
            string direction,
            int? folder,
            bool? unread,
            bool? attachments,
            long? period_from,
            long? period_to,
            bool? important,
            string from_address,
            string to_address,
            int? mailbox_id,
            IEnumerable<int> tags,
            string search,
            int? page_size,
            string sortorder,
            bool? with_calendar,
            int? user_folder_id)
        {
            // inverse sort order if prev message require
            if ("prev" == direction)
                sortorder = DefineConstants.ASCENDING == sortorder ? DefineConstants.DESCENDING : DefineConstants.ASCENDING;

            var primaryFolder = folder.HasValue ? (FolderType)folder.Value : FolderType.Inbox;

            var filter = new MailSearchFilterData
            {
                PrimaryFolder = primaryFolder,
                Unread = unread,
                Attachments = attachments,
                PeriodFrom = period_from,
                PeriodTo = period_to,
                Important = important,
                FromAddress = from_address,
                ToAddress = to_address,
                MailboxId = mailbox_id,
                CustomLabels = new List<int>(tags),
                SearchText = search,
                PageSize = page_size.GetValueOrDefault(25),
                Sort = DefineConstants.ORDER_BY_DATE_CHAIN,
                SortOrder = sortorder,
                WithCalendar = with_calendar,
                UserFolderId = user_folder_id
            };

            return _messageEngine.GetNextConversationId(id, filter);
        }

        /// <summary>
        ///    Moves conversation specified in ids to the folder.
        /// </summary>
        /// <param name="ids">List of mesasges ids from conversations.</param>
        /// <param name="folder">Folder ID - integer. 1 - inbox, 2 - sent, 3 - drafts, 4 - trash, 5 - spam.</param>
        /// <param optional="true" name="userFolderId">User Folder Id</param>
        /// <returns>List of mesasges ids from conversations.</returns>
        /// <short>Move conversations to folder</short>
        /// <category>Conversations</category>
        [HttpPut(@"conversations/move")]
        public IEnumerable<int> MoveConversations(List<int> ids, int folder, int? userFolderId = null)
        {
            if (!ids.Any())
                throw new ArgumentException(@"Empty ids collection", "ids");

            var toFolder = (FolderType)folder;

            if (!MailFolder.IsIdOk(toFolder))
                throw new ArgumentException(@"Invalid folder id", "folder");

            _messageEngine.SetConversationsFolder(ids, toFolder, userFolderId);

            if (toFolder != FolderType.Spam)
                return ids;

            //TODO: Try to move message (IMAP only) to spam folder on original server (need new separated operation)

            var scheme = HttpContext == null ? Uri.UriSchemeHttp : HttpContext.Request.GetUrlRewriter().Scheme;
            _spamEngine.SendConversationsToSpamTrainer(TenantId, UserId, ids, true, scheme);

            return ids;
        }

        /// <summary>
        ///    Restores all the conversations previously moved to specific folders to their original folders.
        /// </summary>
        /// <param name="ids">List of conversation ids for restore.</param>
        /// <param optional="true" name="learnSpamTrainer">send messages tp spam training</param>
        /// <returns>List of restored conversations ids</returns>
        /// <short>Restore conversations to original folders</short>
        /// <category>Conversations</category>
        [HttpPut(@"conversations/restore")]
        public IEnumerable<int> RestoreConversations(List<int> ids, bool learnSpamTrainer = false)
        {
            if (!ids.Any())
                throw new ArgumentException(@"Empty ids collection", "ids");

            _messageEngine.RestoreConversations(TenantId, UserId, ids);

            if (learnSpamTrainer)
            {
                var scheme = HttpContext == null ? Uri.UriSchemeHttp : HttpContext.Request.GetUrlRewriter().Scheme;
                _spamEngine.SendConversationsToSpamTrainer(TenantId, UserId, ids, false, scheme);
            }

            return ids;
        }

        /// <summary>
        ///    Removes conversations from folders
        /// </summary>
        /// <param name="ids">List of conversation ids for remove.</param>
        /// <returns>List of removed conversation ids</returns>
        /// <short>Remove conversations</short>
        /// <category>Conversations</category>
        [HttpPut(@"conversations/remove")]
        public IEnumerable<int> RemoveConversations(List<int> ids)
        {
            if (!ids.Any())
                throw new ArgumentException(@"Empty ids collection", "ids");

            _messageEngine.DeleteConversations(TenantId, UserId, ids);

            return ids;
        }

        /// <summary>
        ///    Sets the status for the conversations specified by ids.
        /// </summary>
        /// <param name="ids">List of conversation ids for status changing.</param>
        /// <param name="status">String parameter specifies status for changing. Values: "read", "unread", "important" and "normal"</param>
        /// <returns>List of status changed conversations.</returns>
        /// <short>Set conversations status</short>
        /// <category>Conversations</category>
        [HttpPut(@"conversations/mark")]
        public IEnumerable<int> MarkConversations(List<int> ids, string status)
        {
            if (!ids.Any())
                throw new ArgumentException(@"Empty ids collection", "ids");

            switch (status)
            {
                case "read":
                    _messageEngine.SetUnread(ids, false, true);
                    break;

                case "unread":
                    _messageEngine.SetUnread(ids, true, true);
                    break;

                case "important":
                    _messageEngine.SetConversationsImportanceFlags(TenantId, UserId, true, ids);
                    break;

                case "normal":
                    _messageEngine.SetConversationsImportanceFlags(TenantId, UserId, false, ids);
                    break;
            }
            return ids;
        }

        /// <summary>
        ///    Add the specified tag to conversations.
        /// </summary>
        /// <param name="tag_id">Tag id for adding.</param>
        /// <param name="messages">List of conversation ids for tag adding.</param>
        /// <returns>Added tag_id</returns>
        /// <short>Add tag to conversations</short> 
        /// <category>Conversations</category>
        ///<exception cref="ArgumentException">Exception happens when in parameters is invalid. Text description contains parameter name and text description.</exception>
        [HttpPut(@"conversations/tag/{tag_id}/set")]
        public int SetConversationsTag(int tag_id, List<int> messages)
        {
            if (!messages.Any())
                throw new ArgumentException(@"Message ids are empty", "messages");

            _tagEngine.SetConversationsTag(messages, tag_id);

            return tag_id;
        }

        /// <summary>
        ///    Removes the specified tag from conversations.
        /// </summary>
        /// <param name="tag_id">Tag id to removing.</param>
        /// <param name="messages">List of conversation ids for tag removing.</param>
        /// <returns>Removed tag_id</returns>
        /// <short>Remove tag from conversations</short> 
        /// <category>Conversations</category>
        ///<exception cref="ArgumentException">Exception happens when in parameters is invalid. Text description contains parameter name and text description.</exception>
        [HttpPut(@"conversations/tag/{tag_id}/unset")]
        public int UnsetConversationsTag(int tag_id, List<int> messages)
        {
            if (!messages.Any())
                throw new ArgumentException(@"Message ids are empty", "messages");

            _tagEngine.UnsetConversationsTag(messages, tag_id);

            return tag_id;
        }

        /// <summary>
        /// Marks conversation as CRM linked. All new mail will be added to CRM history.
        /// </summary>
        /// <param name="id_message">Id of any messages from the chain</param>
        /// <param name="crm_contact_ids">List of CrmContactEntity. List item format: {entity_id: 0, entity_type: 0}.
        /// Entity types: 1 - Contact, 2 - Case, 3 - Opportunity.
        /// </param>
        /// <returns>none</returns>
        /// <category>Conversations</category>
        /// <exception cref="ArgumentException">Exception happens when in parameters is invalid. Text description contains parameter name and text description.</exception>
        [HttpPut(@"conversations/crm/link")]
        public void LinkConversationToCrm(int id_message, IEnumerable<CrmContactData> crm_contact_ids)
        {
            if (id_message < 0)
                throw new ArgumentException(@"Invalid message id", "id_message");
            if (crm_contact_ids == null)
                throw new ArgumentException(@"Invalid contact ids list", "crm_contact_ids");

            var scheme = HttpContext == null
                ? Uri.UriSchemeHttp
                : HttpContext.Request.GetUrlRewriter().Scheme;

            _crmLinkEngine.LinkChainToCrm(id_message, crm_contact_ids.ToList());
        }

        /// <summary>
        /// Marks conversation as CRM linked. All new mail will be added to CRM history.
        /// </summary>
        /// <param name="id_message">Id of any messages from the chain</param>
        /// <param name="crm_contact_ids">List of CrmContactEntity. List item format: {entity_id: 0, entity_type: 0}.
        /// Entity types: 1 - Contact, 2 - Case, 3 - Opportunity.
        /// </param>
        /// <returns>none</returns>
        /// <category>Conversations</category>
        /// <exception cref="ArgumentException">Exception happens when in parameters is invalid. Text description contains parameter name and text description.</exception>
        [HttpPut(@"conversations/crm/mark")]
        public void MarkConversationAsCrmLinked(int id_message, IEnumerable<CrmContactData> crm_contact_ids)
        {
            if (id_message < 0)
                throw new ArgumentException(@"Invalid message id", "id_message");
            if (crm_contact_ids == null)
                throw new ArgumentException(@"Invalid contact ids list", "crm_contact_ids");

            _crmLinkEngine.MarkChainAsCrmLinked(id_message, crm_contact_ids.ToList());
        }

        /// <summary>
        /// Method tears conversation link with crm.
        /// </summary>
        /// <param name="id_message">Id of any messages from the chain</param>
        /// <param name="crm_contact_ids">List of CrmContactEntity. List item format: {entity_id: 0, entity_type: 0}.
        /// Entity types: 1 - Contact, 2 - Case, 3 - Opportunity.
        /// </param>
        /// <returns>none</returns>
        /// <category>Conversations</category>
        /// <exception cref="ArgumentException">Exception happens when in parameters is invalid. Text description contains parameter name and text description.</exception>
        [HttpPut(@"conversations/crm/unmark")]
        public void UnmarkConversationAsCrmLinked(int id_message, IEnumerable<CrmContactData> crm_contact_ids)
        {
            if (id_message < 0)
                throw new ArgumentException(@"Invalid message id", "id_message");
            if (crm_contact_ids == null)
                throw new ArgumentException(@"Invalid contact ids list", "crm_contact_ids");

            _crmLinkEngine.UnmarkChainAsCrmLinked(id_message, crm_contact_ids);
        }

        /// <summary>
        /// Method checks is chain crm linked by message_id.
        /// </summary>
        /// <param name="message_id">Id of any messages from the chain</param>
        /// <returns>MailCrmStatus</returns>
        /// <category>Conversations</category>
        /// <exception cref="ArgumentException">Exception happens when in parameters is invalid. Text description contains parameter name and text description.</exception>
        [HttpGet(@"conversations/link/crm/status")]
        public MailCrmStatus IsConversationLinkedWithCrm(int message_id)
        {
            if (message_id < 0)
                throw new ArgumentException(@"Invalid message id", "message_id");

            var entities = GetLinkedCrmEntitiesInfo(message_id);

            var result = new MailCrmStatus(message_id, entities.Any());

            return result;
        }
    }
}
