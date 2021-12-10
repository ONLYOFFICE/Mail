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


using ASC.Api.Core;
using ASC.Core;
using ASC.Mail.Aggregator.Tests.Common.Utils;
using ASC.Mail.Core.Dao.Entities;
using ASC.Mail.Core.Engine;
using ASC.Mail.Enums;
using ASC.Mail.Models;
using ASC.Mail.Utils;

using Autofac;

using Microsoft.Extensions.DependencyInjection;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ASC.Mail.Tests
{
    [TestFixture]
    internal class ChainEngineTests : BaseMailTests
    {
        private const string EML1_FILE_NAME = @"bad_encoding.eml";
        private static readonly string Eml1Path = TestFolderPath + EML1_FILE_NAME;
        private int MailId { get; set; }

        private MessageEngine MessageEngine { get; set; }
        private UserFolderEngine UserFolderEngine { get; set; }
        private TestEngine TestEngine { get; set; }
        private MailBoxSettingEngine MailBoxSettingEngine { get; set; }
        private MailboxEngine MailboxEngine { get; set; }
        private FolderEngine FolderEngine { get; set; }
        private TenantManager TenantManager { get; set; }
        private CoreSettings CoreSettings { get; set; }
        private ApiDateTimeHelper ApiDateTimeHelper { get; set; }

        [OneTimeSetUp]
        public override void Prepare()
        {
            base.Prepare();

            MessageEngine = serviceScope.ServiceProvider.GetService<MessageEngine>();
            UserFolderEngine = serviceScope.ServiceProvider.GetService<UserFolderEngine>();
            TestEngine = serviceScope.ServiceProvider.GetService<TestEngine>();
            MailBoxSettingEngine = serviceScope.ServiceProvider.GetService<MailBoxSettingEngine>();
            MailboxEngine = serviceScope.ServiceProvider.GetService<MailboxEngine>();
            FolderEngine = serviceScope.ServiceProvider.GetService<FolderEngine>();
            TenantManager = serviceScope.ServiceProvider.GetService<TenantManager>();
            CoreSettings = serviceScope.ServiceProvider.GetService<CoreSettings>();
            ApiDateTimeHelper = serviceScope.ServiceProvider.GetService<ApiDateTimeHelper>();
        }

        [Test]
        [Order(1)]
        public void RemoveConversationTest()
        {
            var folders = FolderEngine.GetFolders();

            Assert.AreEqual(true,
                folders.Any(f => f.totalMessages == 0 && f.unreadMessages == 0 && f.total == 0 && f.unread == 0));

            using (var fs = new FileStream(Eml1Path, FileMode.Open, FileAccess.Read))
            {
                var model = new TestMessageModel
                {
                    FolderId = (int)FolderType.Inbox,
                    UserFolderId = null,
                    MailboxId = TestMailbox.MailBoxId,
                    Unread = true,
                    EmlStream = fs
                };

                MailId = TestEngine.LoadSampleMessage(model, true);
            }

            folders = FolderEngine.GetFolders();

            Assert.AreEqual(true,
                folders.Any(f => f.totalMessages == 1 && f.unreadMessages == 1 && f.total == 1 && f.unread == 1));

            MessageEngine.SetRemoved(new List<int> { MailId });

            folders = FolderEngine.GetFolders();

            Assert.AreEqual(true,
                folders.Any(f => f.totalMessages == 0 && f.unreadMessages == 0 && f.total == 0 && f.unread == 0));
        }

        [Test]
        [Order(2)]
        public void ReadUnreadConvarsationsTest()
        {
            // Bug 34937
            var folders = FolderEngine.GetFolders();

            Assert.AreEqual(true,
                folders.Any(f => f.totalMessages == 0 && f.unreadMessages == 0 && f.total == 0 && f.unread == 0));

            var model = new TestMessageModel
            {
                FolderId = (int)FolderType.Inbox,
                UserFolderId = null,
                MailboxId = TestMailbox.MailBoxId,
                Unread = true,
                To = new List<string> { TestMailbox.EMailView },
                Cc = new List<string>(),
                Bcc = new List<string>(),
                Subject = "Test subject",
                Body = "Test body"
            };

            var id1 = TestEngine.CreateSampleMessage(model, add2Index: true);

            Assert.Greater(id1, 0);

            var id2 = TestEngine.CreateReplyToSampleMessage(id1, "Test Reply body", true);

            Assert.Greater(id2, 0);

            var chainMessages = MessageEngine.GetConversationMessages(TestMailbox.TenantId, TestMailbox.UserId, id1, false,
                false, false);

            Assert.AreEqual(2, chainMessages.Count);
            Assert.Contains(id1, chainMessages.Select(m => m.Id).ToArray());
            Assert.Contains(id2, chainMessages.Select(m => m.Id).ToArray());

            folders = FolderEngine.GetFolders();

            var inbox = folders.FirstOrDefault(f => f.id == FolderType.Inbox);

            Assert.IsNotNull(inbox);
            Assert.AreEqual(1, inbox.totalMessages);
            Assert.AreEqual(1, inbox.unreadMessages);
            Assert.AreEqual(1, inbox.total);
            Assert.AreEqual(1, inbox.unread);

            var sent = folders.FirstOrDefault(f => f.id == FolderType.Sent);

            Assert.IsNotNull(sent);
            Assert.AreEqual(1, sent.totalMessages);
            Assert.AreEqual(0, sent.unreadMessages);
            Assert.AreEqual(1, sent.total);
            Assert.AreEqual(0, sent.unread);

            //5) make all letters read in the inbox
            MessageEngine.SetUnread(new List<int> { id1 }, false, true);

            folders = FolderEngine.GetFolders();

            inbox = folders.FirstOrDefault(f => f.id == FolderType.Inbox);

            Assert.IsNotNull(inbox);
            Assert.AreEqual(1, inbox.totalMessages);
            Assert.AreEqual(0, inbox.unreadMessages);
            Assert.AreEqual(1, inbox.total);
            Assert.AreEqual(0, inbox.unread);

            sent = folders.FirstOrDefault(f => f.id == FolderType.Sent);

            Assert.IsNotNull(sent);
            Assert.AreEqual(1, sent.totalMessages);
            Assert.AreEqual(0, sent.unreadMessages);
            Assert.AreEqual(1, sent.total);
            Assert.AreEqual(0, sent.unread);

            //7) make all letters read in Sent
            MessageEngine.SetUnread(new List<int> { id2 }, false, true);

            folders = FolderEngine.GetFolders();

            inbox = folders.FirstOrDefault(f => f.id == FolderType.Inbox);

            Assert.IsNotNull(inbox);
            Assert.AreEqual(1, inbox.totalMessages);
            Assert.AreEqual(0, inbox.unreadMessages);
            Assert.AreEqual(1, inbox.total);
            Assert.AreEqual(0, inbox.unread);

            sent = folders.FirstOrDefault(f => f.id == FolderType.Sent);

            Assert.IsNotNull(sent);
            Assert.AreEqual(1, sent.totalMessages);
            Assert.AreEqual(0, sent.unreadMessages);
            Assert.AreEqual(1, sent.total);
            Assert.AreEqual(0, sent.unread);

            //8) make an unread letter in any chain (in Sent)
            MessageEngine.SetUnread(new List<int> { id2 }, true);

            folders = FolderEngine.GetFolders();

            inbox = folders.FirstOrDefault(f => f.id == FolderType.Inbox);

            Assert.IsNotNull(inbox);
            Assert.AreEqual(1, inbox.totalMessages);
            Assert.AreEqual(0, inbox.unreadMessages);
            Assert.AreEqual(1, inbox.total);
            Assert.AreEqual(0, inbox.unread);

            sent = folders.FirstOrDefault(f => f.id == FolderType.Sent);

            Assert.IsNotNull(sent);
            Assert.AreEqual(1, sent.totalMessages);
            Assert.AreEqual(1, sent.unreadMessages);
            Assert.AreEqual(1, sent.total);
            Assert.AreEqual(1, sent.unread);

            //10) click on the unread letter in the Inbox
            chainMessages = MessageEngine.GetConversationMessages(TestMailbox.TenantId, TestMailbox.UserId, id1, false,
                false, false, true); // last param is markRead = true - equals to open unread conversation

            Assert.IsNotEmpty(chainMessages);

            folders = FolderEngine.GetFolders();

            inbox = folders.FirstOrDefault(f => f.id == FolderType.Inbox);

            Assert.IsNotNull(inbox);
            Assert.AreEqual(1, inbox.totalMessages);
            Assert.AreEqual(0, inbox.unreadMessages);
            Assert.AreEqual(1, inbox.total);
            Assert.AreEqual(0, inbox.unread);

            sent = folders.FirstOrDefault(f => f.id == FolderType.Sent);

            Assert.IsNotNull(sent);
            Assert.AreEqual(1, sent.totalMessages);
            Assert.AreEqual(0, sent.unreadMessages);
            Assert.AreEqual(1, sent.total);
            Assert.AreEqual(0, sent.unread);
        }

        private void CreateFakeMails(int count, bool unread = false)
        {
            for (var i = 0; i < count; i++)
            {
                var text = string.Format("[TEST MAIL {0}]", i);

                var toAddress = string.Format("to{0}@to.com", i);

                var date = i == 0 ? DateTime.Now : DateTime.Now.AddMinutes(-i);

                var model = new TestMessageModel
                {
                    FolderId = (int)FolderType.Inbox,
                    MailboxId = TestMailbox.MailBoxId,
                    Unread = unread,
                    To = new List<string> { toAddress },
                    Cc = new List<string>(),
                    Bcc = new List<string>(),
                    Subject = text,
                    Body = text,
                    Date = date
                };

                var id = TestEngine.CreateSampleMessage(model, add2Index: true);

                Assert.Greater(id, 0);
            }

            // Wait for some time to index all new messages
            // Thread.Sleep(TimeSpan.FromSeconds(count));
        }

        [Test]
        [Order(3)]
        public void Paging25Total28Test()
        {
            const int page_size = 25;
            const int last_page_count = 3;

            const int n = page_size + last_page_count;

            CreateFakeMails(n);

            // Go to Inbox

            var filter = new MailSearchFilterData
            {
                PrimaryFolder = FolderType.Inbox,
                PageSize = page_size,
                Sort = DefineConstants.ORDER_BY_DATE_CHAIN,
                SortOrder = DefineConstants.DESCENDING,
                FromMessage = 0
            };

            var chains0 = MessageEngine.GetConversations(filter, out bool hasMore);

            Assert.IsNotEmpty(chains0);
            Assert.AreEqual(page_size, chains0.Count);

            // Go to next page

            var last = chains0.Last();

            filter.FromDate = ApiDateTimeHelper.Get(last.ChainDate);
            filter.FromMessage = last.Id;
            filter.PrevFlag = false;
            filter.PageSize = page_size;

            var chainsNext = MessageEngine.GetConversations(filter, out hasMore);

            Assert.IsNotEmpty(chainsNext);
            Assert.AreEqual(last_page_count, chainsNext.Count);

            var first = chainsNext.First();

            filter.FromDate = ApiDateTimeHelper.Get(first.ChainDate);
            filter.FromMessage = first.Id;
            filter.PrevFlag = true;
            filter.PageSize = page_size;

            var chainsPrev = MessageEngine.GetConversations(
                filter,
                out hasMore);

            Assert.IsNotEmpty(chainsPrev);
            Assert.AreEqual(page_size, chainsPrev.Count);
            Assert.AreEqual(chains0.First().Id, chainsPrev.First().Id);
            Assert.AreEqual(chains0.Last().Id, chainsPrev.Last().Id);
        }

        [Test]
        [Order(4)]
        public void Paging50Total57Test()
        {
            const int page_size = 50;
            const int last_page_count = 7;

            const int n = page_size + last_page_count;

            CreateFakeMails(n);

            // Go to Inbox

            var filter = new MailSearchFilterData
            {
                PrimaryFolder = FolderType.Inbox,
                PageSize = page_size,
                Sort = DefineConstants.ORDER_BY_DATE_CHAIN,
                SortOrder = DefineConstants.DESCENDING,
                FromMessage = 0
            };

            var chains0 = MessageEngine.GetConversations(filter, out bool hasMore);

            Assert.IsNotEmpty(chains0);
            Assert.AreEqual(page_size, chains0.Count);

            // Go to next page

            var last = chains0.Last();

            filter.FromDate = ApiDateTimeHelper.Get(last.ChainDate);
            filter.FromMessage = last.Id;
            filter.PrevFlag = false;
            filter.PageSize = page_size;

            var chainsNext = MessageEngine.GetConversations(filter, out hasMore);

            Assert.IsNotEmpty(chainsNext);
            Assert.AreEqual(last_page_count, chainsNext.Count);

            var first = chainsNext.First();

            filter.FromDate = ApiDateTimeHelper.Get(first.ChainDate);
            filter.FromMessage = first.Id;
            filter.PrevFlag = true;
            filter.PageSize = page_size;

            var chainsPrev = MessageEngine.GetConversations(filter, out hasMore);

            Assert.IsNotEmpty(chainsPrev);
            Assert.AreEqual(page_size, chainsPrev.Count);
            Assert.AreEqual(chains0.First().Id, chainsPrev.First().Id);
            Assert.AreEqual(chains0.Last().Id, chainsPrev.Last().Id);
        }

        [Test]
        [Order(5)]
        public void Paging75Total80Test()
        {
            const int page_size = 75;
            const int last_page_count = 5;

            const int n = page_size + last_page_count;

            CreateFakeMails(n);

            // Go to Inbox

            var filter = new MailSearchFilterData
            {
                PrimaryFolder = FolderType.Inbox,
                PageSize = page_size,
                Sort = DefineConstants.ORDER_BY_DATE_CHAIN,
                SortOrder = DefineConstants.DESCENDING,
                FromMessage = 0
            };

            var chains0 = MessageEngine.GetConversations(filter, out bool hasMore);

            Assert.IsNotEmpty(chains0);
            Assert.AreEqual(page_size, chains0.Count);

            // Go to next page

            var last = chains0.Last();

            filter.FromDate = ApiDateTimeHelper.Get(last.ChainDate);
            filter.FromMessage = last.Id;
            filter.PrevFlag = false;
            filter.PageSize = page_size;

            var chainsNext = MessageEngine.GetConversations(filter, out hasMore);

            Assert.IsNotEmpty(chainsNext);
            Assert.AreEqual(last_page_count, chainsNext.Count);

            var first = chainsNext.First();

            filter.FromDate = ApiDateTimeHelper.Get(first.ChainDate);
            filter.FromMessage = first.Id;
            filter.PrevFlag = true;
            filter.PageSize = page_size;

            var chainsPrev = MessageEngine.GetConversations(filter, out hasMore);

            Assert.IsNotEmpty(chainsPrev);
            Assert.AreEqual(page_size, chainsPrev.Count);
            Assert.AreEqual(chains0.First().Id, chainsPrev.First().Id);
            Assert.AreEqual(chains0.Last().Id, chainsPrev.Last().Id);
        }

        [Test]
        [Order(6)]
        public void Paging100Total113Test()
        {
            const int page_size = 100;
            const int last_page_count = 13;

            const int n = page_size + last_page_count;

            CreateFakeMails(n);

            // Go to Inbox

            var filter = new MailSearchFilterData
            {
                PrimaryFolder = FolderType.Inbox,
                PageSize = page_size,
                Sort = DefineConstants.ORDER_BY_DATE_CHAIN,
                SortOrder = DefineConstants.DESCENDING,
                FromMessage = 0
            };

            var chains0 = MessageEngine.GetConversations(filter, out bool hasMore);

            Assert.IsNotEmpty(chains0);
            Assert.AreEqual(page_size, chains0.Count);

            // Go to next page

            var last = chains0.Last();

            filter.FromDate = ApiDateTimeHelper.Get(last.ChainDate);
            filter.FromMessage = last.Id;
            filter.PrevFlag = false;
            filter.PageSize = page_size;

            var chainsNext = MessageEngine.GetConversations(filter, out hasMore);

            Assert.IsNotEmpty(chainsNext);
            Assert.AreEqual(last_page_count, chainsNext.Count);

            var first = chainsNext.First();

            filter.FromDate = ApiDateTimeHelper.Get(first.ChainDate);
            filter.FromMessage = first.Id;
            filter.PrevFlag = true;
            filter.PageSize = page_size;

            var chainsPrev = MessageEngine.GetConversations(filter, out hasMore);

            Assert.IsNotEmpty(chainsPrev);
            Assert.AreEqual(page_size, chainsPrev.Count);
            Assert.AreEqual(chains0.First().Id, chainsPrev.First().Id);
            Assert.AreEqual(chains0.Last().Id, chainsPrev.Last().Id);
        }

        [Test]
        [Order(7)]
        public void Paging25Total58SortDescTest()
        {
            const int page_size = 25;
            const int last_page_count = 8;

            const int n = page_size * 2 + last_page_count;

            CreateFakeMails(n);

            // Go to 1 page

            var filter = new MailSearchFilterData
            {
                PrimaryFolder = FolderType.Inbox,
                PageSize = page_size,
                Sort = DefineConstants.ORDER_BY_DATE_CHAIN,
                SortOrder = DefineConstants.DESCENDING
            };

            var chains1 = MessageEngine.GetConversations(filter, out bool hasMore);

            Assert.AreEqual(true, hasMore);
            Assert.IsNotEmpty(chains1);
            Assert.AreEqual(page_size, chains1.Count);

            var firstMessage1 = chains1.First();
            var lastMessage1 = chains1.Last();

            Assert.Greater(firstMessage1.ChainDate, lastMessage1.ChainDate);

            // Go to 2 page

            filter.FromDate = ApiDateTimeHelper.Get(lastMessage1.ChainDate);
            filter.FromMessage = lastMessage1.Id;
            filter.PrevFlag = false;
            filter.PageSize = page_size;

            var chains2 = MessageEngine.GetConversations(filter, out hasMore);

            Assert.AreEqual(true, hasMore);
            Assert.IsNotEmpty(chains2);
            Assert.AreEqual(page_size, chains2.Count);

            var firstMessage2 = chains2.First();
            var lastMessage2 = chains2.Last();

            Assert.Greater(firstMessage2.ChainDate, lastMessage2.ChainDate);

            // Go to 3 page

            filter.FromDate = ApiDateTimeHelper.Get(lastMessage2.ChainDate);
            filter.FromMessage = lastMessage2.Id;

            filter.PrevFlag = false;
            filter.PageSize = page_size;

            var chains3 = MessageEngine.GetConversations(filter, out hasMore);

            Assert.AreEqual(false, hasMore);
            Assert.IsNotEmpty(chains3);
            Assert.AreEqual(last_page_count, chains3.Count);

            var firstMessage3 = chains3.First();
            var lastMessage3 = chains3.Last();

            Assert.Greater(firstMessage3.ChainDate, lastMessage3.ChainDate);

            // Go back to 2 page

            filter.FromDate = ApiDateTimeHelper.Get(firstMessage3.ChainDate);
            filter.FromMessage = firstMessage3.Id;

            filter.PrevFlag = true;
            filter.PageSize = page_size;

            var chains2Prev = MessageEngine.GetConversations(filter, out hasMore);

            Assert.AreEqual(true, hasMore);
            Assert.IsNotEmpty(chains2Prev);
            Assert.AreEqual(page_size, chains2Prev.Count);

            var firstMessage2Prev = chains2Prev.First();
            var lastMessage2Prev = chains2Prev.Last();

            Assert.AreEqual(firstMessage2.Id, firstMessage2Prev.Id);
            Assert.AreEqual(lastMessage2.Id, lastMessage2Prev.Id);
            Assert.Greater(firstMessage2Prev.ChainDate, lastMessage2Prev.ChainDate);

            // Go back to 1 page

            filter.FromDate = ApiDateTimeHelper.Get(firstMessage2Prev.ChainDate);
            filter.FromMessage = firstMessage2Prev.Id;
            filter.PrevFlag = true;
            filter.PageSize = page_size;

            var chains1Prev = MessageEngine.GetConversations(filter, out hasMore);

            Assert.AreEqual(false, hasMore);
            Assert.IsNotEmpty(chains1Prev);
            Assert.AreEqual(page_size, chains1Prev.Count);

            var firstMessage1Prev = chains1Prev.First();
            var lastMessage1Prev = chains1Prev.Last();

            Assert.AreEqual(firstMessage1.Id, firstMessage1Prev.Id);
            Assert.AreEqual(lastMessage1.Id, lastMessage1Prev.Id);
            Assert.Greater(firstMessage1Prev.ChainDate, lastMessage1Prev.ChainDate);
        }

        [Test]
        [Order(8)]
        public void Paging25Total58SortAscTest()
        {
            const int page_size = 25;
            const int last_page_count = 8;

            const int n = page_size * 2 + last_page_count;

            CreateFakeMails(n);

            // Go to 1 page

            var filter = new MailSearchFilterData
            {
                PrimaryFolder = FolderType.Inbox,
                PageSize = page_size,
                Sort = DefineConstants.ORDER_BY_DATE_CHAIN,
                SortOrder = DefineConstants.ASCENDING
            };

            bool hasMore;

            var chains1 = MessageEngine.GetConversations(filter, out hasMore);

            Assert.AreEqual(true, hasMore);
            Assert.IsNotEmpty(chains1);
            Assert.AreEqual(page_size, chains1.Count);

            var firstMessage1 = chains1.First();
            var lastMessage1 = chains1.Last();

            Assert.Less(firstMessage1.ChainDate, lastMessage1.ChainDate);

            // Go to 2 page

            filter.FromDate = ApiDateTimeHelper.Get(lastMessage1.ChainDate);
            filter.FromMessage = lastMessage1.Id;
            filter.PrevFlag = false;
            filter.PageSize = page_size;

            var chains2 = MessageEngine.GetConversations(filter, out hasMore);

            Assert.AreEqual(true, hasMore);
            Assert.IsNotEmpty(chains2);
            Assert.AreEqual(page_size, chains2.Count);

            var firstMessage2 = chains2.First();
            var lastMessage2 = chains2.Last();

            Assert.Less(firstMessage2.ChainDate, lastMessage2.ChainDate);

            // Go to 3 page

            filter.FromDate = ApiDateTimeHelper.Get(lastMessage2.ChainDate);
            filter.FromMessage = lastMessage2.Id;
            filter.PrevFlag = false;
            filter.PageSize = page_size;

            var chains3 = MessageEngine.GetConversations(filter, out hasMore);

            Assert.AreEqual(false, hasMore);
            Assert.IsNotEmpty(chains3);
            Assert.AreEqual(last_page_count, chains3.Count);

            var firstMessage3 = chains3.First();
            var lastMessage3 = chains3.Last();

            Assert.Less(firstMessage3.ChainDate, lastMessage3.ChainDate);

            // Go back to 2 page

            filter.FromDate = ApiDateTimeHelper.Get(firstMessage3.ChainDate);
            filter.FromMessage = firstMessage3.Id;
            filter.PrevFlag = true;
            filter.PageSize = page_size;

            var chains2Prev = MessageEngine.GetConversations(filter, out hasMore);

            Assert.AreEqual(true, hasMore);
            Assert.IsNotEmpty(chains2Prev);
            Assert.AreEqual(page_size, chains2Prev.Count);

            var firstMessage2Prev = chains2Prev.First();
            var lastMessage2Prev = chains2Prev.Last();

            Assert.AreEqual(firstMessage2.Id, firstMessage2Prev.Id);
            Assert.AreEqual(lastMessage2.Id, lastMessage2Prev.Id);
            Assert.Less(firstMessage2Prev.ChainDate, lastMessage2Prev.ChainDate);

            // Go back to 1 page

            filter.FromDate = ApiDateTimeHelper.Get(firstMessage2Prev.ChainDate);
            filter.FromMessage = firstMessage2Prev.Id;
            filter.PrevFlag = true;
            filter.PageSize = page_size;

            var chains1Prev = MessageEngine.GetConversations(filter, out hasMore);

            Assert.AreEqual(false, hasMore);
            Assert.IsNotEmpty(chains1Prev);
            Assert.AreEqual(page_size, chains1Prev.Count);

            var firstMessage1Prev = chains1Prev.First();
            var lastMessage1Prev = chains1Prev.Last();

            Assert.AreEqual(firstMessage1.Id, firstMessage1Prev.Id);
            Assert.AreEqual(lastMessage1.Id, lastMessage1Prev.Id);
            Assert.Less(firstMessage1Prev.ChainDate, lastMessage1Prev.ChainDate);
        }

        [Test]
        [Order(9)]
        public void Paging25Total28UnreadTest()
        {
            if (!TestHelper.IgnoreIfFullTextSearch<MailMail>(false, serviceScope.ServiceProvider))
                return;

            const int page_size = 25;
            const int last_page_count = 3;

            const int n = page_size + last_page_count;

            CreateFakeMails(n, true);

            // Go to 1 page

            var filter = new MailSearchFilterData
            {
                PrimaryFolder = FolderType.Inbox,
                PageSize = page_size,
                Sort = DefineConstants.ORDER_BY_DATE_CHAIN,
                SortOrder = DefineConstants.DESCENDING,
                Unread = true
            };

            var chains0 = MessageEngine.GetConversations(filter, out bool hasMore);

            Assert.IsNotEmpty(chains0);
            Assert.AreEqual(page_size, chains0.Count);

            // Go to 2 page

            var last = chains0.Last();

            filter.FromDate = ApiDateTimeHelper.Get(last.ChainDate);
            filter.FromMessage = last.Id;
            filter.PrevFlag = false;
            filter.PageSize = page_size;

            var chainsNext = MessageEngine.GetConversations(filter, out hasMore);

            Assert.IsNotEmpty(chainsNext);
            Assert.AreEqual(last_page_count, chainsNext.Count);

            // Go to back to 1 page

            var first = chainsNext.First();

            filter.FromDate = ApiDateTimeHelper.Get(first.ChainDate);
            filter.FromMessage = first.Id;
            filter.PrevFlag = true;
            filter.PageSize = page_size;

            var chainsPrev = MessageEngine.GetConversations(filter, out hasMore);

            Assert.IsNotEmpty(chainsPrev);
            Assert.AreEqual(page_size, chainsPrev.Count);
            Assert.AreEqual(chains0.First().Id, chainsPrev.First().Id);
            Assert.AreEqual(chains0.Last().Id, chainsPrev.Last().Id);
        }

        [Test]
        [Order(10)]
        public void ReadUnreadSameChainInDifferentMailboxesTest()
        {
            if (!TestHelper.IgnoreIfFullTextSearch<MailMail>(false, ServiceProvider))
                return;

            var mailboxSettings = MailBoxSettingEngine.GetMailBoxSettings(DOMAIN);

            var testMailboxes = mailboxSettings.ToMailboxList("example@example.com", PASSWORD, CURRENT_TENANT, TestUser.ID.ToString());

            var testMailbox2 = testMailboxes.FirstOrDefault();

            if (testMailbox2 == null || !MailboxEngine.SaveMailBox(testMailbox2))
            {
                throw new Exception(string.Format("Can't create mailbox with email: {0}", TestUser.Email));
            }

            var folders = FolderEngine.GetFolders();

            Assert.AreEqual(true,
                folders.Any(f => f.totalMessages == 0 && f.unreadMessages == 0 && f.total == 0 && f.unread == 0));

            var date = DateTime.Now;
            var mimeMessageId = MailUtil.CreateMessageId(TenantManager, CoreSettings);

            var model1 = new TestMessageModel
            {
                FolderId = (int)FolderType.Inbox,
                UserFolderId = null,
                MailboxId = TestMailbox.MailBoxId,
                Unread = true,
                To = new List<string> { TestMailbox.EMail.Address },
                Cc = new List<string>(),
                Bcc = new List<string>(),
                Subject = "SOME TEXT",
                Body = "SOME TEXT",
                MimeMessageId = mimeMessageId,
                Date = date
            };

            var id1 = TestEngine.CreateSampleMessage(model1);

            Assert.Greater(id1, 0);

            var model2 = new TestMessageModel
            {
                FolderId = (int)FolderType.Inbox,
                UserFolderId = null,
                MailboxId = testMailbox2.MailBoxId,
                Unread = true,
                To = new List<string> { testMailbox2.EMail.Address },
                Cc = new List<string>(),
                Bcc = new List<string>(),
                Subject = "SOME TEXT",
                Body = "SOME TEXT",
                MimeMessageId = mimeMessageId,
                Date = date
            };

            var id2 = TestEngine.CreateSampleMessage(model2);

            Assert.Greater(id2, 0);

            folders = FolderEngine.GetFolders();

            var inbox = folders.First(f => f.id == FolderType.Inbox);

            Assert.AreEqual(true,
                inbox.totalMessages == 2 && inbox.unreadMessages == 2 && inbox.total == 2 && inbox.unread == 2);

            var ids = new List<int> { id1, id2 };

            MessageEngine.SetUnread(ids, false, true);

            folders = FolderEngine.GetFolders();

            inbox = folders.First(f => f.id == FolderType.Inbox);

            Assert.AreEqual(2, inbox.totalMessages);
            Assert.AreEqual(0, inbox.unreadMessages);
            Assert.AreEqual(2, inbox.total);
            Assert.AreEqual(0, inbox.unread);

            MessageEngine.SetUnread(new List<int> { id1 }, true, true);

            folders = FolderEngine.GetFolders();

            inbox = folders.First(f => f.id == FolderType.Inbox);

            Assert.AreEqual(2, inbox.totalMessages);
            Assert.AreEqual(1, inbox.unreadMessages);
            Assert.AreEqual(2, inbox.total);
            Assert.AreEqual(1, inbox.unread);

            MessageEngine.SetUnread(new List<int> { id2 }, true, true);

            folders = FolderEngine.GetFolders();

            inbox = folders.First(f => f.id == FolderType.Inbox);

            Assert.AreEqual(2, inbox.totalMessages);
            Assert.AreEqual(2, inbox.unreadMessages);
            Assert.AreEqual(2, inbox.total);
            Assert.AreEqual(2, inbox.unread);

            MessageEngine.SetUnread(ids, true, true);

            folders = FolderEngine.GetFolders();

            inbox = folders.First(f => f.id == FolderType.Inbox);

            Assert.AreEqual(2, inbox.totalMessages);
            Assert.AreEqual(2, inbox.unreadMessages);
            Assert.AreEqual(2, inbox.total);
            Assert.AreEqual(2, inbox.unread);
        }

        [Test]
        [Order(11)]
        public void MoveMessagesFromSameChainIntoDifferentUserFoldersTest()
        {
            var folders = FolderEngine.GetFolders();

            Assert.AreEqual(true,
                folders.Any(f => f.totalMessages == 0 && f.unreadMessages == 0 && f.total == 0 && f.unread == 0));

            var date = DateTime.Now;
            var mimeMessageId = MailUtil.CreateMessageId(TenantManager, CoreSettings);

            var model = new TestMessageModel
            {
                FolderId = (int)FolderType.Inbox,
                UserFolderId = null,
                MailboxId = TestMailbox.MailBoxId,
                Unread = true,
                To = new List<string> { TestMailbox.EMail.Address },
                Cc = new List<string>(),
                Bcc = new List<string>(),
                Subject = "SOME TEXT",
                Body = "SOME TEXT",
                MimeMessageId = mimeMessageId,
                Date = date
            };

            var id1 = TestEngine.CreateSampleMessage(model);

            Assert.Greater(id1, 0);

            var id2 = TestEngine.CreateReplyToSampleMessage(id1, "REPLY BODT TEST");

            Assert.Greater(id2, 0);

            Assert.AreNotEqual(id1, id2);

            folders = FolderEngine.GetFolders();

            var inbox = folders.First(f => f.id == FolderType.Inbox);

            Assert.AreEqual(1, inbox.totalMessages);
            Assert.AreEqual(1, inbox.unreadMessages);
            Assert.AreEqual(1, inbox.total);
            Assert.AreEqual(1, inbox.unread);

            var sent = folders.First(f => f.id == FolderType.Sent);

            Assert.AreEqual(1, sent.totalMessages);
            Assert.AreEqual(0, sent.unreadMessages);
            Assert.AreEqual(1, sent.total);
            Assert.AreEqual(0, sent.unread);

            var userFolder = folders.FirstOrDefault(f => f.id == FolderType.UserFolder);

            Assert.AreEqual(null, userFolder);

            var listUserFolders = UserFolderEngine.GetList();

            Assert.IsEmpty(listUserFolders);

            #region --> Create new UserFolder and move inbox message into it

            var uf1 = UserFolderEngine.Create("Folder 1");

            Assert.Greater(uf1.Id, 0);

            MessageEngine.SetFolder(new List<int> { id1 }, FolderType.UserFolder, uf1.Id);

            folders = FolderEngine.GetFolders();

            inbox = folders.First(f => f.id == FolderType.Inbox);

            Assert.AreEqual(0, inbox.totalMessages);
            Assert.AreEqual(0, inbox.unreadMessages);
            Assert.AreEqual(0, inbox.total);
            Assert.AreEqual(0, inbox.unread);

            sent = folders.First(f => f.id == FolderType.Sent);

            Assert.AreEqual(1, sent.totalMessages);
            Assert.AreEqual(0, sent.unreadMessages);
            Assert.AreEqual(1, sent.total);
            Assert.AreEqual(0, sent.unread);

            userFolder = folders.FirstOrDefault(f => f.id == FolderType.UserFolder);

            Assert.AreNotEqual(null, userFolder);

            Assert.AreEqual(1, userFolder.totalMessages);
            Assert.AreEqual(1, userFolder.unreadMessages);
            Assert.AreEqual(1, userFolder.total);
            Assert.AreEqual(1, userFolder.unread);

            listUserFolders = UserFolderEngine.GetList();

            Assert.IsNotEmpty(listUserFolders);

            var UFfolder1 = listUserFolders.Where(uf => uf.Id == uf1.Id).FirstOrDefault();

            Assert.AreEqual(1, UFfolder1.TotalCount);
            Assert.AreEqual(1, UFfolder1.UnreadCount);
            Assert.AreEqual(1, UFfolder1.TotalChainCount);
            Assert.AreEqual(1, UFfolder1.UnreadChainCount);

            #endregion

            #region --> Create new UserFolder and move sent message into it

            var uf2 = UserFolderEngine.Create("Folder 2");

            Assert.Greater(uf2.Id, 0);

            MessageEngine.SetFolder(new List<int> { id2 }, FolderType.UserFolder, uf2.Id);

            folders = FolderEngine.GetFolders();

            inbox = folders.First(f => f.id == FolderType.Inbox);

            Assert.AreEqual(0, inbox.totalMessages);
            Assert.AreEqual(0, inbox.unreadMessages);
            Assert.AreEqual(0, inbox.total);
            Assert.AreEqual(0, inbox.unread);

            sent = folders.First(f => f.id == FolderType.Sent);

            Assert.AreEqual(0, sent.totalMessages);
            Assert.AreEqual(0, sent.unreadMessages);
            Assert.AreEqual(0, sent.total);
            Assert.AreEqual(0, sent.unread);

            userFolder = folders.FirstOrDefault(f => f.id == FolderType.UserFolder);

            Assert.AreNotEqual(null, userFolder);

            Assert.AreEqual(2, userFolder.totalMessages);
            Assert.AreEqual(1, userFolder.unreadMessages);
            Assert.AreEqual(1, userFolder.total);
            Assert.AreEqual(1, userFolder.unread);

            listUserFolders = UserFolderEngine.GetList();

            Assert.IsNotEmpty(listUserFolders);

            var UFfolder2 = listUserFolders.Where(uf => uf.Id == uf2.Id).FirstOrDefault();

            Assert.AreEqual(1, UFfolder2.TotalCount);
            Assert.AreEqual(0, UFfolder2.UnreadCount);
            Assert.AreEqual(1, UFfolder2.TotalChainCount);
            Assert.AreEqual(0, UFfolder2.UnreadChainCount);

            #endregion
        }

        [Test]
        [Order(12)]
        public void SetAndUnsetImportanceByChainedMessagesTest()
        {
            var mailboxSettings = MailBoxSettingEngine.GetMailBoxSettings(DOMAIN);

            var testMailboxes = mailboxSettings.ToMailboxList("example@example.com", PASSWORD, CURRENT_TENANT, TestUser.ID.ToString());

            var testMailbox2 = testMailboxes.FirstOrDefault();

            if (testMailbox2 == null || !MailboxEngine.SaveMailBox(testMailbox2))
            {
                throw new Exception(string.Format("Can't create mailbox with email: {0}", TestUser.Email));
            }

            var folders = FolderEngine.GetFolders();

            Assert.AreEqual(true,
                folders.Any(f => f.totalMessages == 0 && f.unreadMessages == 0 && f.total == 0 && f.unread == 0));

            var date = DateTime.Now;
            var mimeMessageId = MailUtil.CreateMessageId(TenantManager, CoreSettings);

            var model = new TestMessageModel
            {
                FolderId = (int)FolderType.Inbox,
                UserFolderId = null,
                MailboxId = TestMailbox.MailBoxId,
                Unread = true,
                To = new List<string> { TestMailbox.EMail.Address },
                Cc = new List<string>(),
                Bcc = new List<string>(),
                Subject = "SOME TEXT",
                Body = "SOME TEXT",
                MimeMessageId = mimeMessageId,
                Date = date
            };

            var id1 = TestEngine.CreateSampleMessage(model);

            Assert.Greater(id1, 0);

            var id2 = TestEngine.CreateReplyToSampleMessage(id1, "SOME REPLY BODY");

            Assert.Greater(id2, 0);

            folders = FolderEngine.GetFolders();

            var inbox = folders.First(f => f.id == FolderType.Inbox);

            Assert.AreEqual(true,
                inbox.totalMessages == 1 && inbox.unreadMessages == 1 && inbox.total == 1 && inbox.unread == 1);

            var sent = folders.First(f => f.id == FolderType.Sent);

            Assert.AreEqual(true,
                sent.totalMessages == 1 && sent.unreadMessages == 0 && sent.total == 1 && sent.unread == 0);

            var message1 = MessageEngine.GetMessage(id1, new MailMessageData.Options());

            Assert.AreEqual(false, message1.Important);

            var message2 = MessageEngine.GetMessage(id2, new MailMessageData.Options());

            Assert.AreEqual(false, message2.Important);

            var chains = MessageEngine.GetChainsById(mimeMessageId);

            Assert.AreEqual(true, chains.All(c => !c.Importance));

            MessageEngine.SetConversationsImportanceFlags(CURRENT_TENANT, TestUser.ID.ToString(), true, new List<int> { id1 });

            message1 = MessageEngine.GetMessage(id1, new MailMessageData.Options());

            Assert.AreEqual(true, message1.Important);

            message2 = MessageEngine.GetMessage(id2, new MailMessageData.Options());

            Assert.AreEqual(true, message2.Important);

            chains = MessageEngine.GetChainsById(mimeMessageId);

            Assert.AreEqual(true, chains.All(c => c.Importance));

            MessageEngine.SetConversationsImportanceFlags(CURRENT_TENANT, TestUser.ID.ToString(), false, new List<int> { id2 });

            message1 = MessageEngine.GetMessage(id1, new MailMessageData.Options());

            Assert.AreEqual(false, message1.Important);

            message2 = MessageEngine.GetMessage(id2, new MailMessageData.Options());

            Assert.AreEqual(false, message2.Important);

            chains = MessageEngine.GetChainsById(mimeMessageId);

            Assert.AreEqual(true, chains.All(c => !c.Importance));

            MessageEngine.SetConversationsImportanceFlags(CURRENT_TENANT, TestUser.ID.ToString(), true, new List<int> { id2 });

            message1 = MessageEngine.GetMessage(id1, new MailMessageData.Options());

            Assert.AreEqual(true, message1.Important);

            message2 = MessageEngine.GetMessage(id2, new MailMessageData.Options());

            Assert.AreEqual(true, message2.Important);

            chains = MessageEngine.GetChainsById(mimeMessageId);

            Assert.AreEqual(true, chains.All(c => c.Importance));

            MessageEngine.SetConversationsImportanceFlags(CURRENT_TENANT, TestUser.ID.ToString(), false, new List<int> { id1 });

            message1 = MessageEngine.GetMessage(id1, new MailMessageData.Options());

            Assert.AreEqual(false, message1.Important);

            message2 = MessageEngine.GetMessage(id2, new MailMessageData.Options());

            Assert.AreEqual(false, message2.Important);

            chains = MessageEngine.GetChainsById(mimeMessageId);

            Assert.AreEqual(true, chains.All(c => !c.Importance));
        }
    }
}
