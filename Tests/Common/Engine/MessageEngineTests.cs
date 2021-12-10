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
using ASC.Mail.Extensions;
using ASC.Mail.Models;

using Microsoft.Extensions.DependencyInjection;

using NUnit.Framework;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ASC.Mail.Tests
{
    [TestFixture]
    internal class MessageEngineTests : BaseMailTests
    {
        private const string EML1_FILE_NAME = @"bad_encoding.eml";
        private static readonly string Eml1Path = TestFolderPath + EML1_FILE_NAME;

        public int MailId { get; set; }

        private FolderEngine FolderEngine { get; set; }
        private TestEngine TestEngine { get; set; }
        private MessageEngine MessageEngine { get; set; }

        [OneTimeSetUp]
        public override void Prepare()
        {
            base.Prepare();

            FolderEngine = serviceScope.ServiceProvider.GetService<FolderEngine>();
            TestEngine = serviceScope.ServiceProvider.GetService<TestEngine>();
            MessageEngine = serviceScope.ServiceProvider.GetService<MessageEngine>();

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

        [Test]
        [Order(1)]
        public void GetMessageStreamTest()
        {
            string htmlBody;
            using (var stream = MessageEngine.GetMessageStream(MailId))
            {
                htmlBody = Encoding.UTF8.GetString(stream.ReadToEnd());
            }

            Assert.IsNotNull(htmlBody);
            Assert.IsNotEmpty(htmlBody);
            Assert.AreEqual(83689, htmlBody.Length);
        }

        [Test]
        [Order(2)]
        public void RemoveMessageTest()
        {
            // Bug 34937

            var folders = FolderEngine.GetFolders();

            var inbox = folders.FirstOrDefault(f => f.id == FolderType.Inbox);

            Assert.IsNotNull(inbox);
            Assert.AreEqual(1, inbox.totalMessages);
            Assert.AreEqual(1, inbox.unreadMessages);
            Assert.AreEqual(1, inbox.total);
            Assert.AreEqual(1, inbox.unread);

            MessageEngine.SetRemoved(new List<int> { MailId });

            folders = FolderEngine.GetFolders();

            inbox = folders.FirstOrDefault(f => f.id == FolderType.Inbox);

            Assert.IsNotNull(inbox);
            Assert.AreEqual(0, inbox.totalMessages);
            Assert.AreEqual(0, inbox.unreadMessages);
            Assert.AreEqual(0, inbox.total);
            Assert.AreEqual(0, inbox.unread);
        }
    }
}
