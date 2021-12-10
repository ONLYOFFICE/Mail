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


using ASC.Mail.Core.Engine;
using ASC.Mail.Enums;
using ASC.Mail.Models;

using Microsoft.Extensions.DependencyInjection;

using NUnit.Framework;

using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ASC.Mail.Tests
{
    [TestFixture]
    internal class TagEngineTests : BaseMailTests
    {
        private const string EML1_FILE_NAME = @"bad_encoding.eml";
        private static readonly string Eml1Path = TestFolderPath + EML1_FILE_NAME;

        public int MailId { get; set; }

        private TestEngine TestEngine { get; set; }
        private MessageEngine MessageEngine { get; set; }
        private TagEngine TagEngine { get; set; }


        [OneTimeSetUp]
        public override void Prepare()
        {
            base.Prepare();

            TestEngine = serviceScope.ServiceProvider.GetService<TestEngine>();
            MessageEngine = serviceScope.ServiceProvider.GetService<MessageEngine>();
            TagEngine = serviceScope.ServiceProvider.GetService<TagEngine>();

            using var fs = new FileStream(Eml1Path, FileMode.Open, FileAccess.Read);

            var model = new TestMessageModel
            {
                FolderId = (int)FolderType.Inbox,
                UserFolderId = null,
                MailboxId = TestMailbox.MailBoxId,
                Unread = true,
                EmlStream = fs
            };

            MailId = TestEngine.LoadSampleMessage(model);
        }

        [TearDown]
        public void DeleteTagsForNextTest()
        {
            var tags = TagEngine.GetTags();

            foreach (var tag in tags)
            {
                TagEngine.DeleteTag(tag.Id);
            }
        }

        private List<Core.Entities.Tag> CreateTagsOnMessage()
        {
            var tag1 = TagEngine.CreateTag("Tag1", "11", new List<string>());

            Assert.IsNotNull(tag1);
            Assert.Greater(tag1.Id, 0);

            TagEngine.SetMessagesTag(new List<int> { MailId }, tag1.Id);

            var tag2 = TagEngine.CreateTag("Tag2", "10", new List<string>());

            Assert.IsNotNull(tag1);
            Assert.Greater(tag1.Id, 0);

            TagEngine.SetMessagesTag(new List<int> { MailId }, tag2.Id);

            var tags = TagEngine.GetTags();

            Assert.IsNotEmpty(tags);
            Assert.AreEqual(2, tags.Count);
            Assert.Contains(tag1.Id, tags.Select(m => m.Id).ToArray());
            Assert.Contains(tag2.Id, tags.Select(m => m.Id).ToArray());

            var message = MessageEngine.GetMessage(MailId, new MailMessageData.Options());

            Assert.IsNotEmpty(message.TagIds);
            Assert.AreEqual(2, message.TagIds.Count);
            Assert.Contains(tag1.Id, message.TagIds);
            Assert.Contains(tag2.Id, message.TagIds);

            return tags;
        }

        private List<Core.Entities.Tag> CreateTagsOnConversation()
        {
            var tag1 = TagEngine.CreateTag("Tag1", "11", new List<string>());

            Assert.IsNotNull(tag1);
            Assert.Greater(tag1.Id, 0);

            TagEngine.SetConversationsTag(new List<int> { MailId }, tag1.Id);

            var tag2 = TagEngine.CreateTag("Tag2", "10", new List<string>());

            Assert.IsNotNull(tag1);
            Assert.Greater(tag1.Id, 0);

            TagEngine.SetConversationsTag(new List<int> { MailId }, tag2.Id);

            var tags = TagEngine.GetTags();

            Assert.IsNotEmpty(tags);
            Assert.AreEqual(2, tags.Count);
            Assert.Contains(tag1.Id, tags.Select(m => m.Id).ToArray());
            Assert.Contains(tag2.Id, tags.Select(m => m.Id).ToArray());

            var message = MessageEngine.GetMessage(MailId, new MailMessageData.Options());

            Assert.IsNotEmpty(message.TagIds);
            Assert.AreEqual(2, message.TagIds.Count);
            Assert.Contains(tag1.Id, message.TagIds);
            Assert.Contains(tag2.Id, message.TagIds);

            return tags;
        }

        [Test]
        [Order(1)]
        public void SetMessageNewTagsTest()
        {
            CreateTagsOnMessage();
        }

        [Test()]
        [Order(2)]
        public void SetConversationNewTagsTest()
        {
            CreateTagsOnConversation();
        }

        [Test]
        [Order(3)]
        public void UnsetMessageFirstTagTest()
        {
            var tags = CreateTagsOnMessage();

            var tag1 = tags[0];
            var tag2 = tags[1];

            TagEngine.UnsetMessagesTag(new List<int> { MailId }, tag1.Id);

            var message = MessageEngine.GetMessage(MailId, new MailMessageData.Options());

            Assert.IsNotEmpty(message.TagIds);
            Assert.AreEqual(1, message.TagIds.Count);

            Assert.Contains(tag2.Id, message.TagIds);
        }

        [Test]
        [Order(4)]
        public void UnsetConversationFirstTagTest()
        {
            var tags = CreateTagsOnConversation();

            var tag1 = tags[0];
            var tag2 = tags[1];

            TagEngine.UnsetConversationsTag(new List<int> { MailId }, tag1.Id);

            var message = MessageEngine.GetMessage(MailId, new MailMessageData.Options());

            Assert.IsNotEmpty(message.TagIds);
            Assert.AreEqual(1, message.TagIds.Count);

            Assert.Contains(tag2.Id, message.TagIds);
        }

        [Test]
        [Order(5)]
        public void UnsetMessageSecondTagTest()
        {
            var tags = CreateTagsOnMessage();

            var tag1 = tags[0];
            var tag2 = tags[1];

            TagEngine.UnsetMessagesTag(new List<int> { MailId }, tag2.Id);

            var message = MessageEngine.GetMessage(MailId, new MailMessageData.Options());

            Assert.IsNotEmpty(message.TagIds);
            Assert.AreEqual(1, message.TagIds.Count);

            Assert.Contains(tag1.Id, message.TagIds);
        }

        [Test]
        [Order(6)]
        public void UnsetConversationSecondTagTest()
        {
            var tags = CreateTagsOnConversation();

            var tag1 = tags[0];
            var tag2 = tags[1];

            TagEngine.UnsetConversationsTag(new List<int> { MailId }, tag2.Id);

            var message = MessageEngine.GetMessage(MailId, new MailMessageData.Options());

            Assert.IsNotEmpty(message.TagIds);
            Assert.AreEqual(1, message.TagIds.Count);

            Assert.Contains(tag1.Id, message.TagIds);
        }
    }
}
