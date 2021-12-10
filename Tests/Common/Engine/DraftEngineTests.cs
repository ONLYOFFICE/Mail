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


using ASC.Core;
using ASC.Mail.Core.Engine;
using ASC.Mail.Enums;
using ASC.Mail.Exceptions;
using ASC.Mail.Models;
using ASC.Mail.Utils;

using Microsoft.Extensions.DependencyInjection;

using NUnit.Framework;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ASC.Mail.Tests
{
    [TestFixture]
    internal class DraftEngineTests : BaseMailTests
    {
        private const string EML1_FILE_NAME = @"bad_encoding.eml";
        private static readonly string Eml1Path = TestFolderPath + EML1_FILE_NAME;

        private FolderEngine FolderEngine { get; set; }
        private TestEngine TestEngine { get; set; }
        private MessageEngine MessageEngine { get; set; }
        private TenantManager TenantManager { get; set; }
        private CoreSettings CoreSettings { get; set; }
        private DraftEngine DraftEngine { get; set; }

        [OneTimeSetUp]
        public override void Prepare()
        {
            base.Prepare();

            FolderEngine = serviceScope.ServiceProvider.GetService<FolderEngine>();
            TestEngine = serviceScope.ServiceProvider.GetService<TestEngine>();
            MessageEngine = serviceScope.ServiceProvider.GetService<MessageEngine>();
            TenantManager = serviceScope.ServiceProvider.GetService<TenantManager>();
            CoreSettings = serviceScope.ServiceProvider.GetService<CoreSettings>();
            DraftEngine = serviceScope.ServiceProvider.GetService<DraftEngine>();
        }

        [Test]
        [Order(1)]
        public void CreateDraftTest()
        {
            var folders = FolderEngine.GetFolders();

            Assert.IsNotEmpty(folders);

            Assert.AreEqual(true,
                folders.Any(f => f.total == 0 && f.unread == 0 && f.totalMessages == 0 && f.unreadMessages == 0));

            var draftItem = new MailDraftData(0, TestMailbox, "test@gmail.com", new List<string>(), new List<string>(), new List<string>(), "subject",
                MailUtil.CreateMessageId(TenantManager, CoreSettings), null, false, null, "Test body", MailUtil.CreateStreamId(), new List<MailAttachmentData>());

            var data = DraftEngine.Save(draftItem);

            Assert.Greater(data.Id, 0);

            folders = FolderEngine.GetFolders();

            var draft = folders.FirstOrDefault(f => f.id == FolderType.Draft);

            Assert.IsNotNull(draft);
            Assert.AreEqual(1, draft.totalMessages);
            Assert.AreEqual(0, draft.unreadMessages);
            Assert.AreEqual(0, draft.total);
            Assert.AreEqual(0, draft.unread);

            var savedDraftData = MessageEngine.GetMessage(data.Id, new MailMessageData.Options());

            Assert.AreEqual("subject", savedDraftData.Subject);
            Assert.AreEqual("test@gmail.com", savedDraftData.From);
        }

        [Test]
        [Order(2)]
        public void CreateForwardDraftTest()
        {
            var folders = FolderEngine.GetFolders();

            Assert.IsNotEmpty(folders);

            Assert.AreEqual(true,
                folders.Any(f => f.total == 0 && f.unread == 0 && f.totalMessages == 0 && f.unreadMessages == 0));

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

            var id1 = TestEngine.CreateSampleMessage(model);

            Assert.Greater(id1, 0);

            MailAttachmentData attachData;

            using (var fs = new FileStream(Eml1Path, FileMode.Open, FileAccess.Read))
            {
                var attachmentModel = new TestAttachmentModel
                {
                    ContentType = "message/eml",
                    Filename = EML1_FILE_NAME,
                    Stream = fs
                };

                attachData = TestEngine.AppendAttachmentsToSampleMessage(id1, attachmentModel);
            }

            Assert.IsNotNull(attachData);
            Assert.Greater(attachData.fileId, 0);

            var message = MessageEngine.GetMessage(id1, new MailMessageData.Options());

            Assert.AreEqual(1, message.Attachments.Count);

            var draftItem = new MailDraftData(0, TestMailbox, "test@gmail.com", new List<string>(), new List<string>(), new List<string>(),
                "subject", MailUtil.CreateMessageId(TenantManager, CoreSettings), null, false, null, "Test body",
                MailUtil.CreateStreamId(), message.Attachments);

            var data = DraftEngine.Save(draftItem);

            Assert.Greater(data.Id, 0);

            folders = FolderEngine.GetFolders();

            var inbox = folders.FirstOrDefault(f => f.id == FolderType.Inbox);

            Assert.IsNotNull(inbox);
            Assert.AreEqual(1, inbox.totalMessages);
            Assert.AreEqual(1, inbox.unreadMessages);
            Assert.AreEqual(1, inbox.total);
            Assert.AreEqual(1, inbox.unread);

            var draft = folders.FirstOrDefault(f => f.id == FolderType.Draft);

            Assert.IsNotNull(draft);
            Assert.AreEqual(1, draft.totalMessages);
            Assert.AreEqual(0, draft.unreadMessages);
            Assert.AreEqual(0, draft.total);
            Assert.AreEqual(0, draft.unread);

            var savedDraftData = MessageEngine.GetMessage(data.Id, new MailMessageData.Options());

            Assert.AreEqual("subject", savedDraftData.Subject);
            Assert.AreEqual("test@gmail.com", savedDraftData.From);
            Assert.AreEqual(1, savedDraftData.Attachments.Count);

            message = MessageEngine.GetMessage(id1, new MailMessageData.Options());

            Assert.AreEqual(1, message.Attachments.Count);
        }

        [Test]
        [Order(3)]
        public void CreateDraftWithClonedAttachmentTest()
        {
            var folders = FolderEngine.GetFolders();

            Assert.IsNotEmpty(folders);

            Assert.AreEqual(true,
                folders.Any(f => f.total == 0 && f.unread == 0 && f.totalMessages == 0 && f.unreadMessages == 0));

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

            var id1 = TestEngine.CreateSampleMessage(model);

            Assert.Greater(id1, 0);

            MailAttachmentData attachData;

            using (var fs = new FileStream(Eml1Path, FileMode.Open, FileAccess.Read))
            {
                var attachmentModel = new TestAttachmentModel
                {
                    ContentType = "message/eml",
                    Filename = EML1_FILE_NAME,
                    Stream = fs
                };

                attachData = TestEngine.AppendAttachmentsToSampleMessage(id1, attachmentModel);
            }

            Assert.IsNotNull(attachData);
            Assert.Greater(attachData.fileId, 0);

            var message = MessageEngine.GetMessage(id1, new MailMessageData.Options());

            Assert.AreEqual(1, message.Attachments.Count);

            var clonedAttachments = new List<MailAttachmentData>();
            do
            {
                var clone = attachData.Clone() as MailAttachmentData;
                if (clone == null)
                    break;

                clonedAttachments.Add(clone);

            } while (clonedAttachments.Sum(a => a.size) < DefineConstants.ATTACHMENTS_TOTAL_SIZE_LIMIT);

            Assert.Greater(clonedAttachments.Count, 1);

            var draftItem = new MailDraftData(0, TestMailbox, "test@gmail.com", new List<string>(), new List<string>(),
                new List<string>(), "subject", MailUtil.CreateMessageId(TenantManager, CoreSettings), null, false, null, "Test body",
                MailUtil.CreateStreamId(), clonedAttachments);

            Assert.AreEqual(1, draftItem.Attachments.Count);

            var data = DraftEngine.Save(draftItem);

            Assert.Greater(data.Id, 0);
            Assert.AreEqual(1, data.Attachments.Count);

            var savedDraftData = MessageEngine.GetMessage(data.Id, new MailMessageData.Options());

            Assert.AreEqual("subject", savedDraftData.Subject);
            Assert.AreEqual("test@gmail.com", savedDraftData.From);
            Assert.AreEqual(1, savedDraftData.Attachments.Count);

            message = MessageEngine.GetMessage(id1, new MailMessageData.Options());

            Assert.AreEqual(1, message.Attachments.Count);
        }

        [Test]
        [Order(4)]
        public void CreateDraftWithAttachmentsTotalExceededTest()
        {
            var attachments = new List<MailAttachmentData>();

            var index = 0;

            do
            {
                ++index;

                var attachData = new MailAttachmentData()
                {
                    fileId = index,
                    fileNumber = index,
                    tenant = CURRENT_TENANT,
                    user = TestUser.ID.ToString(),
                    mailboxId = TestMailbox.MailBoxId,
                    data = Encoding.UTF8.GetBytes("Test"),
                    size = 100000,
                    contentId = "Content" + index,
                    streamId = MailUtil.CreateStreamId(),
                    storedName = MailUtil.CreateStreamId() + ".txt",
                    fileName = "Test_DATA.txt"
                };

                attachments.Add(attachData);

            } while (attachments.Sum(a => a.size) < DefineConstants.ATTACHMENTS_TOTAL_SIZE_LIMIT);

            Assert.Throws<DraftException>(
                () => new MailDraftData(0, TestMailbox, "test@gmail.com", new List<string>(), new List<string>(),
                    new List<string>(), "subject", MailUtil.CreateMessageId(TenantManager, CoreSettings), null, false, null, "Test body",
                    MailUtil.CreateStreamId(), attachments), "Total size of all files exceeds limit!");
        }

        [Test]
        [Order(5)]
        public void CreateDraftWithAttachAndOpenIt()
        {
            var folders = FolderEngine.GetFolders();

            Assert.IsNotEmpty(folders);

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

            var id1 = TestEngine.CreateSampleMessage(model);

            Assert.Greater(id1, 0);

            MailAttachmentData attachData;

            using (var fs = new FileStream(Eml1Path, FileMode.Open, FileAccess.Read))
            {
                var attachmentModel = new TestAttachmentModel
                {
                    ContentType = "message/eml",
                    Filename = EML1_FILE_NAME,
                    Stream = fs
                };

                attachData = TestEngine.AppendAttachmentsToSampleMessage(id1, attachmentModel);
            }

            Assert.IsNotNull(attachData);
            Assert.Greater(attachData.fileId, 0);

            var message = MessageEngine.GetMessage(id1, new MailMessageData.Options());

            Assert.AreEqual(1, message.Attachments.Count);

            var draftItem = new MailDraftData(0, TestMailbox, "test@gmail.com",
                new List<string>(), new List<string>(), new List<string>(), "subject",
                MailUtil.CreateMessageId(TenantManager, CoreSettings), null, false, null, "Test body",
                MailUtil.CreateStreamId(), message.Attachments);

            var data = DraftEngine.Save(draftItem);

            Assert.AreEqual(1, data.Attachments.Count);

            data = DraftEngine.Save(draftItem);

            Assert.AreEqual(1, data.Attachments.Count);

            data = DraftEngine.Save(draftItem);

            Assert.AreEqual(1, data.Attachments.Count);
        }
    }
}
