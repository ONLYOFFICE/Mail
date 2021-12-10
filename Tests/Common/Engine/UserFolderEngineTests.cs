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
using ASC.Mail.Exceptions;
using ASC.Mail.Models;

using Microsoft.Extensions.DependencyInjection;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.IO;

namespace ASC.Mail.Tests
{
    [TestFixture]
    internal class UserFoldersTests : BaseMailTests
    {
        private const string EML1_FILE_NAME = @"bad_encoding.eml";
        private const string EML2_FILE_NAME = @"embed_image.eml";
        private const string EML3_FILE_NAME = @"exo__with_javascript.eml";
        private const string EML4_FILE_NAME = @"icloud_ics.eml";
        private const string EML5_FILE_NAME = @"medium_sample.eml";
        private const string EML6_FILE_NAME = @"message_mailru.eml";

        private static readonly string Eml1Path = TestFolderPath + EML1_FILE_NAME;
        private static readonly string Eml2Path = TestFolderPath + EML2_FILE_NAME;
        private static readonly string Eml3Path = TestFolderPath + EML3_FILE_NAME;
        private static readonly string Eml4Path = TestFolderPath + EML4_FILE_NAME;
        private static readonly string Eml5Path = TestFolderPath + EML5_FILE_NAME;
        private static readonly string Eml6Path = TestFolderPath + EML6_FILE_NAME;

        public int MailId { get; set; }

        private MessageEngine MessageEngine { get; set; }
        private UserFolderEngine UserFolderEngine { get; set; }
        private TestEngine TestEngine { get; set; }

        [OneTimeSetUp]
        public override void Prepare()
        {
            base.Prepare();

            MessageEngine = serviceScope.ServiceProvider.GetService<MessageEngine>();
            UserFolderEngine = serviceScope.ServiceProvider.GetService<UserFolderEngine>();
            TestEngine = serviceScope.ServiceProvider.GetService<TestEngine>();
        }

        [Test]
        [Order(1)]
        public void CreateFolderTest()
        {
            var folder = UserFolderEngine.Create("CreateFolderTest");

            Assert.Greater(folder.Id, 0);
        }

        [Test]
        [Order(2)]
        public void CreateFolderWithAlreadyExistingNameTest()
        {
            const string name = "CreateFolderWithAlreadyExistingNameTest";

            var folder = UserFolderEngine.Create(name);

            Assert.Greater(folder.Id, 0);

            Assert.Throws<AlreadyExistsFolderException>(() =>
            {
                UserFolderEngine.Create(name);
            });
        }

        [Test]
        [Order(3)]
        public void CreateFolderWithoutParentTest()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                UserFolderEngine.Create("CreateFolderWithoutParentTest", 777);
            });
        }

        [Test]
        [Order(4)]
        public void CreateFolderWithoutNameTest()
        {
            Assert.Throws<EmptyFolderException>(() =>
            {
                UserFolderEngine.Create("");
            });
        }

        [Test]
        [Order(5)]
        public void CreateSubFolderTest()
        {
            var folder = UserFolderEngine.Create("CreateSubFolderTest");

            Assert.Greater(folder.Id, 0);

            var subFolder = UserFolderEngine.Create("CreateSubFolderTestSUB", folder.Id);

            Assert.Greater(subFolder.Id, 0);

            var rootFolder = UserFolderEngine.Get(folder.Id);

            Assert.IsNotNull(rootFolder);

            Assert.AreEqual(1, rootFolder.FolderCount);
        }

        //TODO: fix userFolderEngine.Delete
        /*[Test]
        public void RemoveSubFolderTest()
        {
            using var scope = ServiceProvider.CreateScope();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            var userFolderEngine = scope.ServiceProvider.GetService<UserFolderEngine>();

            var folder = userFolderEngine.Create("Test folder");

            Assert.Greater(folder.Id, 0);

            var subFolder = userFolderEngine.Create("Test sub folder", folder.Id);

            Assert.Greater(subFolder.Id, 0);

            var rootFolder = userFolderEngine.Get(folder.Id);

            Assert.IsNotNull(rootFolder);

            Assert.AreEqual(1, rootFolder.FolderCount);

            userFolderEngine.Delete(subFolder.Id);

            rootFolder = userFolderEngine.Get(rootFolder.Id);

            Assert.IsNotNull(rootFolder);

            Assert.AreEqual(0, rootFolder.FolderCount);
        }*/

        [Test]
        [Order(6)]
        public void ChangeNameTest()
        {
            const string name = "ChangeNameTest";

            var folder = UserFolderEngine.Create(name);

            Assert.Greater(folder.Id, 0);

            Assert.AreEqual(name, folder.Name);

            const string new_name = "New ChangeNameTest";

            var resultFolder = UserFolderEngine.Update(folder.Id, new_name);

            Assert.IsNotNull(resultFolder);

            Assert.Greater(resultFolder.Id, 0);

            Assert.AreEqual(0, resultFolder.FolderCount);

            Assert.AreEqual(new_name, resultFolder.Name);

            Assert.AreNotEqual(folder.Name, resultFolder.Name);
        }

        [Test]
        [Order(7)]
        public void ChangeNameToExistingTest()
        {
            const string name1 = "ChangeNameToExistingTest";

            var folder1 = UserFolderEngine.Create(name1);

            Assert.Greater(folder1.Id, 0);

            Assert.AreEqual(name1, folder1.Name);

            const string name2 = "New Folder Name ChangeNameToExistingTest";

            var folder2 = UserFolderEngine.Create(name2);

            Assert.Greater(folder2.Id, 0);

            Assert.AreEqual(name2, folder2.Name);

            Assert.Throws<AlreadyExistsFolderException>(() =>
            {
                UserFolderEngine.Update(folder2.Id, name1);
            });
        }

        [Test]
        [Order(8)]
        public void MoveToBaseFolderTest()
        {
            var baseFolder = UserFolderEngine.Create("MoveToBaseFolderTest 1");

            Assert.Greater(baseFolder.Id, 0);

            var folder = UserFolderEngine.Create("MoveToBaseFolderTest 1.1");

            Assert.Greater(folder.Id, 0);

            UserFolderEngine.Update(folder.Id, folder.Name, baseFolder.Id);

            var resultBaseFolder = UserFolderEngine.Get(baseFolder.Id);

            Assert.IsNotNull(resultBaseFolder);

            Assert.AreEqual(0, resultBaseFolder.ParentId);

            Assert.AreEqual(1, resultBaseFolder.FolderCount);

            var resultFolder = UserFolderEngine.Get(folder.Id);

            Assert.Greater(resultFolder.Id, 0);

            Assert.AreEqual(baseFolder.Id, resultFolder.ParentId);
        }

        [Test]
        [Order(9)]
        public void MoveFromBaseFolderTest()
        {
            var baseFolder = UserFolderEngine.Create("MoveFromBaseFolderTest 1");

            Assert.Greater(baseFolder.Id, 0);

            var folder = UserFolderEngine.Create("MoveFromBaseFolderTest 1.1", baseFolder.Id);

            Assert.Greater(folder.Id, 0);

            Assert.AreEqual(baseFolder.Id, folder.ParentId);

            baseFolder = UserFolderEngine.Get(baseFolder.Id);

            Assert.AreEqual(1, baseFolder.FolderCount);

            UserFolderEngine.Update(folder.Id, folder.Name, 0);

            var resultBaseFolder = UserFolderEngine.Get(baseFolder.Id);

            Assert.IsNotNull(resultBaseFolder);

            Assert.AreEqual(0, resultBaseFolder.FolderCount);

            var resultFolder = UserFolderEngine.Get(folder.Id);

            Assert.Greater(resultFolder.Id, 0);
        }

        [Test]
        [Order(10)]
        public void WrongMoveFolderTest()
        {
            var baseFolder = UserFolderEngine.Create("WrongMoveFolderTest 1");

            Assert.Greater(baseFolder.Id, 0);

            var folder = UserFolderEngine.Create("WrongMoveFolderTest 1.1", baseFolder.Id);

            Assert.Greater(folder.Id, 0);

            Assert.AreEqual(baseFolder.Id, folder.ParentId);

            Assert.Throws<MoveFolderException>(() =>
            {
                UserFolderEngine.Update(baseFolder.Id, baseFolder.Name, folder.Id);
            });
        }

        [Test]
        [Order(11)]
        public void WrongChangeParentToCurrentTest()
        {
            var baseFolder = UserFolderEngine.Create("WrongChangeParentToCurrentTest 1");

            Assert.Greater(baseFolder.Id, 0);

            var folder = UserFolderEngine.Create("WrongChangeParentToCurrentTest 1.1", baseFolder.Id);

            Assert.Greater(folder.Id, 0);

            Assert.AreEqual(baseFolder.Id, folder.ParentId);

            Assert.Throws<ArgumentException>(() =>
            {
                UserFolderEngine.Update(folder.Id, folder.Name, folder.Id);
            });
        }

        [Test]
        [Order(12)]
        public void WrongChangeParentToChildTest()
        {
            var root = UserFolderEngine.Create("WrongChangeParentToChildTest");

            Assert.Greater(root.Id, 0);

            var f1 = UserFolderEngine.Create("WrongChangeParentToChildTest 1", root.Id);

            Assert.Greater(f1.Id, 0);

            Assert.AreEqual(root.Id, f1.ParentId);

            root = UserFolderEngine.Get(root.Id);

            Assert.AreEqual(1, root.FolderCount);

            var f11 = UserFolderEngine.Create("WrongChangeParentToChildTest 1.1", f1.Id);

            Assert.Greater(f11.Id, 0);

            Assert.AreEqual(f1.Id, f11.ParentId);

            f1 = UserFolderEngine.Get(f1.Id);

            Assert.AreEqual(1, f1.FolderCount);

            var f111 = UserFolderEngine.Create("WrongChangeParentToChildTest 1.1.1", f11.Id);

            Assert.Greater(f111.Id, 0);

            Assert.AreEqual(f11.Id, f111.ParentId);

            Assert.Throws<MoveFolderException>(() =>
            {
                UserFolderEngine.Update(f11.Id, f11.Name, f111.Id);
            });
        }

        //TODO: fix userFolderEngine.Delete
        /*[Test]
        public void DeleteFolderInTheMiddleTest()
        {
            using var scope = ServiceProvider.CreateScope();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            var userFolderEngine = scope.ServiceProvider.GetService<UserFolderEngine>();

            var root = userFolderEngine.Create("Root");

            Assert.Greater(root.Id, 0);

            var f1 = userFolderEngine.Create("1", root.Id);

            Assert.Greater(f1.Id, 0);

            Assert.AreEqual(root.Id, f1.ParentId);

            root = userFolderEngine.Get(root.Id);

            Assert.AreEqual(1, root.FolderCount);

            var f11 = userFolderEngine.Create("1.1", f1.Id);

            Assert.Greater(f11.Id, 0);

            Assert.AreEqual(f1.Id, f11.ParentId);

            f1 = userFolderEngine.Get(f1.Id);

            Assert.AreEqual(1, f1.FolderCount);

            var f111 = userFolderEngine.Create("1.1.1", f11.Id);

            Assert.Greater(f111.Id, 0);

            Assert.AreEqual(f11.Id, f111.ParentId);

            f11 = userFolderEngine.Get(f11.Id);

            Assert.AreEqual(1, f11.FolderCount);

            var f2 = userFolderEngine.Create("2", root.Id);

            Assert.Greater(f2.Id, 0);

            Assert.AreEqual(root.Id, f2.ParentId);

            f1 = userFolderEngine.Get(root.Id);

            Assert.AreEqual(4, f1.FolderCount);

            userFolderEngine.Delete(f11.Id);

            f1 = userFolderEngine.Get(root.Id);

            Assert.AreEqual(2, f1.FolderCount);

            userFolderEngine.Delete(root.Id);

            var list = userFolderEngine.GetList();

            Assert.IsEmpty(list);
        }*/

        [Test]
        [Order(13)]
        public void LoadMessagesToUserFolderTest()
        {
            var baseFolder = UserFolderEngine.Create("LoadMessagesToUserFolderTest 1");

            Assert.Greater(baseFolder.Id, 0);

            var resultBaseFolder = UserFolderEngine.Get(baseFolder.Id);

            Assert.IsNotNull(resultBaseFolder);
            Assert.AreEqual(0, resultBaseFolder.ParentId);
            Assert.AreEqual(0, resultBaseFolder.FolderCount);
            Assert.AreEqual(0, resultBaseFolder.UnreadCount);
            Assert.AreEqual(0, resultBaseFolder.TotalCount);
            Assert.AreEqual(0, resultBaseFolder.UnreadChainCount);
            Assert.AreEqual(0, resultBaseFolder.TotalChainCount);

            int mailId1;

            using (var fs = new FileStream(Eml1Path, FileMode.Open, FileAccess.Read))
            {
                var model = new TestMessageModel
                {
                    FolderId = (int)FolderType.UserFolder,
                    UserFolderId = baseFolder.Id,
                    MailboxId = TestMailbox.MailBoxId,
                    Unread = true,
                    EmlStream = fs
                };

                mailId1 = TestEngine.LoadSampleMessage(model);
            }

            Assert.Greater(mailId1, 0);

            resultBaseFolder = UserFolderEngine.Get(baseFolder.Id);

            Assert.IsNotNull(resultBaseFolder);
            Assert.AreEqual(0, resultBaseFolder.ParentId);
            Assert.AreEqual(0, resultBaseFolder.FolderCount);
            Assert.AreEqual(1, resultBaseFolder.UnreadCount);
            Assert.AreEqual(1, resultBaseFolder.TotalCount);
            Assert.AreEqual(1, resultBaseFolder.UnreadChainCount);
            Assert.AreEqual(1, resultBaseFolder.TotalChainCount);

            int mailId2;

            using (var fs = new FileStream(Eml2Path, FileMode.Open, FileAccess.Read))
            {
                var model = new TestMessageModel
                {
                    FolderId = (int)FolderType.UserFolder,
                    UserFolderId = baseFolder.Id,
                    MailboxId = TestMailbox.MailBoxId,
                    Unread = false,
                    EmlStream = fs
                };

                mailId2 = TestEngine.LoadSampleMessage(model);
            }

            Assert.Greater(mailId2, 0);

            resultBaseFolder = UserFolderEngine.Get(baseFolder.Id);

            Assert.IsNotNull(resultBaseFolder);
            Assert.AreEqual(0, resultBaseFolder.ParentId);
            Assert.AreEqual(0, resultBaseFolder.FolderCount);
            Assert.AreEqual(1, resultBaseFolder.UnreadCount);
            Assert.AreEqual(2, resultBaseFolder.TotalCount);
            Assert.AreEqual(1, resultBaseFolder.UnreadChainCount);
            Assert.AreEqual(2, resultBaseFolder.TotalChainCount);
        }

        [Test]
        [Order(14)]
        public void MoveMessagesFromDefaulFolderToUserFolderTest()
        {
            var baseFolder = UserFolderEngine.Create("MoveMessagesFromDefaulFolderToUserFolderTest 1");

            Assert.Greater(baseFolder.Id, 0);

            var resultBaseFolder = UserFolderEngine.Get(baseFolder.Id);

            Assert.IsNotNull(resultBaseFolder);
            Assert.AreEqual(0, resultBaseFolder.ParentId);
            Assert.AreEqual(0, resultBaseFolder.FolderCount);
            Assert.AreEqual(0, resultBaseFolder.UnreadCount);
            Assert.AreEqual(0, resultBaseFolder.TotalCount);
            Assert.AreEqual(0, resultBaseFolder.UnreadChainCount);
            Assert.AreEqual(0, resultBaseFolder.TotalChainCount);

            int mailId1;
            int mailId2;

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

                mailId1 = TestEngine.LoadSampleMessage(model);
            }

            using (var fs = new FileStream(Eml2Path, FileMode.Open, FileAccess.Read))
            {
                var model = new TestMessageModel
                {
                    FolderId = (int)FolderType.Inbox,
                    UserFolderId = null,
                    MailboxId = TestMailbox.MailBoxId,
                    Unread = false,
                    EmlStream = fs
                };

                mailId2 = TestEngine.LoadSampleMessage(model);
            }

            Assert.Greater(mailId1, 0);
            Assert.Greater(mailId2, 0);

            MessageEngine.SetFolder(new List<int> { mailId1, mailId2 }, FolderType.UserFolder,
                baseFolder.Id);

            resultBaseFolder = UserFolderEngine.Get(baseFolder.Id);

            Assert.IsNotNull(resultBaseFolder);
            Assert.AreEqual(0, resultBaseFolder.ParentId);
            Assert.AreEqual(0, resultBaseFolder.FolderCount);
            Assert.AreEqual(1, resultBaseFolder.UnreadCount);
            Assert.AreEqual(2, resultBaseFolder.TotalCount);
            Assert.AreEqual(1, resultBaseFolder.UnreadChainCount);
            Assert.AreEqual(2, resultBaseFolder.TotalChainCount);
        }

        [Test]
        [Order(15)]
        public void MoveMessagesFromUserFolderToDefaulFolderTest()
        {
            var baseFolder = UserFolderEngine.Create("MoveMessagesFromToDefaul 1");

            Assert.Greater(baseFolder.Id, 0);

            var resultBaseFolder = UserFolderEngine.Get(baseFolder.Id);

            Assert.IsNotNull(resultBaseFolder);
            Assert.AreEqual(0, resultBaseFolder.ParentId);
            Assert.AreEqual(0, resultBaseFolder.FolderCount);
            Assert.AreEqual(0, resultBaseFolder.UnreadCount);
            Assert.AreEqual(0, resultBaseFolder.TotalCount);
            Assert.AreEqual(0, resultBaseFolder.UnreadChainCount);
            Assert.AreEqual(0, resultBaseFolder.TotalChainCount);

            int mailId1;
            int mailId2;

            using (var fs = new FileStream(Eml5Path, FileMode.Open, FileAccess.Read))
            {
                var model = new TestMessageModel
                {
                    FolderId = (int)FolderType.UserFolder,
                    UserFolderId = baseFolder.Id,
                    MailboxId = TestMailbox.MailBoxId,
                    Unread = true,
                    EmlStream = fs
                };

                mailId1 = TestEngine.LoadSampleMessage(model);
            }

            using (var fs = new FileStream(Eml6Path, FileMode.Open, FileAccess.Read))
            {
                var model = new TestMessageModel
                {
                    FolderId = (int)FolderType.UserFolder,
                    UserFolderId = baseFolder.Id,
                    MailboxId = TestMailbox.MailBoxId,
                    Unread = false,
                    EmlStream = fs
                };

                mailId2 = TestEngine.LoadSampleMessage(model);
            }

            Assert.Greater(mailId1, 0);
            Assert.Greater(mailId2, 0);

            resultBaseFolder = UserFolderEngine.Get(baseFolder.Id);

            Assert.IsNotNull(resultBaseFolder);
            Assert.AreEqual(0, resultBaseFolder.ParentId);
            Assert.AreEqual(0, resultBaseFolder.FolderCount);
            Assert.AreEqual(1, resultBaseFolder.UnreadCount);
            Assert.AreEqual(2, resultBaseFolder.TotalCount);
            Assert.AreEqual(1, resultBaseFolder.UnreadChainCount);
            Assert.AreEqual(2, resultBaseFolder.TotalChainCount);

            MessageEngine.SetFolder(new List<int> { mailId1, mailId2 }, FolderType.Inbox);

            resultBaseFolder = UserFolderEngine.Get(baseFolder.Id);

            Assert.IsNotNull(resultBaseFolder);
            Assert.AreEqual(0, resultBaseFolder.ParentId);
            Assert.AreEqual(0, resultBaseFolder.FolderCount);
            Assert.AreEqual(0, resultBaseFolder.UnreadCount);
            Assert.AreEqual(0, resultBaseFolder.TotalCount);
            Assert.AreEqual(0, resultBaseFolder.UnreadChainCount);
            Assert.AreEqual(0, resultBaseFolder.TotalChainCount);
        }

        [Test]
        [Order(16)]
        public void MoveMessagesFromUserFolderToAnotherUserFolderTest()
        {
            var folder1 = UserFolderEngine.Create("MoveMessagesFromToAnother 1");

            Assert.Greater(folder1.Id, 0);

            var resultFolder1 = UserFolderEngine.Get(folder1.Id);

            Assert.IsNotNull(resultFolder1);
            Assert.AreEqual(0, resultFolder1.ParentId);
            Assert.AreEqual(0, resultFolder1.FolderCount);
            Assert.AreEqual(0, resultFolder1.UnreadCount);
            Assert.AreEqual(0, resultFolder1.TotalCount);
            Assert.AreEqual(0, resultFolder1.UnreadChainCount);
            Assert.AreEqual(0, resultFolder1.TotalChainCount);

            var folder2 = UserFolderEngine.Create("MoveMessagesFromToAnother 2");

            Assert.Greater(folder2.Id, 0);

            var resultFolder2 = UserFolderEngine.Get(folder2.Id);

            Assert.IsNotNull(resultFolder2);
            Assert.AreEqual(0, resultFolder2.ParentId);
            Assert.AreEqual(0, resultFolder2.FolderCount);
            Assert.AreEqual(0, resultFolder2.UnreadCount);
            Assert.AreEqual(0, resultFolder2.TotalCount);
            Assert.AreEqual(0, resultFolder2.UnreadChainCount);
            Assert.AreEqual(0, resultFolder2.TotalChainCount);

            int mailId1;
            int mailId2;

            using (var fs = new FileStream(Eml3Path, FileMode.Open, FileAccess.Read))
            {
                var model = new TestMessageModel
                {
                    FolderId = (int)FolderType.UserFolder,
                    UserFolderId = folder1.Id,
                    MailboxId = TestMailbox.MailBoxId,
                    Unread = true,
                    EmlStream = fs
                };

                mailId1 = TestEngine.LoadSampleMessage(model);
            }

            using (var fs = new FileStream(Eml4Path, FileMode.Open, FileAccess.Read))
            {
                var model = new TestMessageModel
                {
                    FolderId = (int)FolderType.UserFolder,
                    UserFolderId = folder1.Id,
                    MailboxId = TestMailbox.MailBoxId,
                    Unread = false,
                    EmlStream = fs
                };

                mailId2 = TestEngine.LoadSampleMessage(model);
            }

            Assert.Greater(mailId1, 0);
            Assert.Greater(mailId2, 0);

            resultFolder1 = UserFolderEngine.Get(folder1.Id);

            Assert.IsNotNull(resultFolder1);
            Assert.AreEqual(0, resultFolder1.ParentId);
            Assert.AreEqual(0, resultFolder1.FolderCount);
            Assert.AreEqual(1, resultFolder1.UnreadCount);
            Assert.AreEqual(2, resultFolder1.TotalCount);
            Assert.AreEqual(1, resultFolder1.UnreadChainCount);
            Assert.AreEqual(2, resultFolder1.TotalChainCount);

            MessageEngine.SetFolder(new List<int> { mailId1, mailId2 }, FolderType.UserFolder, folder2.Id);

            resultFolder1 = UserFolderEngine.Get(folder1.Id);

            Assert.IsNotNull(resultFolder1);
            Assert.AreEqual(0, resultFolder1.ParentId);
            Assert.AreEqual(0, resultFolder1.FolderCount);
            Assert.AreEqual(0, resultFolder1.UnreadCount);
            Assert.AreEqual(0, resultFolder1.TotalCount);
            Assert.AreEqual(0, resultFolder1.UnreadChainCount);
            Assert.AreEqual(0, resultFolder1.TotalChainCount);

            resultFolder2 = UserFolderEngine.Get(folder2.Id);

            Assert.IsNotNull(resultFolder2);
            Assert.AreEqual(0, resultFolder2.ParentId);
            Assert.AreEqual(0, resultFolder2.FolderCount);
            Assert.AreEqual(1, resultFolder2.UnreadCount);
            Assert.AreEqual(2, resultFolder2.TotalCount);
            Assert.AreEqual(1, resultFolder2.UnreadChainCount);
            Assert.AreEqual(2, resultFolder2.TotalChainCount);
        }
    }
}
