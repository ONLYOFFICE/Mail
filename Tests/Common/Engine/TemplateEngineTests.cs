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
using ASC.Core.Users;
using ASC.Mail.Aggregator.Tests.Common.Utils;
using ASC.Mail.Core.Engine;
using ASC.Mail.Enums;
using ASC.Mail.Models;
using ASC.Mail.Utils;

using Microsoft.Extensions.DependencyInjection;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ASC.Mail.Tests
{
    [TestFixture]
    internal class TemplateEngineTests : BaseMailTests
    {
        private const int CURRENT_TENANT = 1;
        public const string PASSWORD = "123456";
        public const string DOMAIN = "gmail.com";

        private MailBoxData TestMailbox { get; set; }
        public UserInfo TestUser { get; set; }

        private static readonly string TestFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
           @"..\..\..\Data\");
        private const string EML1_FILE_NAME = @"bad_encoding.eml";
        private static readonly string Eml1Path = TestFolderPath + EML1_FILE_NAME;


        [OneTimeSetUp]
        public override void Prepare()
        {
            base.Prepare();
        }

        [SetUp]
        public void SetUp()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();
            var mailBoxSettingEngine = scope.ServiceProvider.GetService<MailBoxSettingEngine>();
            var mailboxEngine = scope.ServiceProvider.GetService<MailboxEngine>();
            var apiHelper = scope.ServiceProvider.GetService<ApiHelper>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(ASC.Core.Configuration.Constants.CoreSystem);

            TestUser = UserManager.GetUsers(Guid.Parse("66faa6e4-f133-11ea-b126-00ffeec8b4ef"));
            TestUser.Email = TestHelper.GetTestEmailAddress(DOMAIN);

            //вынести
            var mailboxSettings = mailBoxSettingEngine.GetMailBoxSettings(DOMAIN);

            var testMailboxes = mailboxSettings.ToMailboxList(TestUser.Email, PASSWORD, CURRENT_TENANT, TestUser.ID.ToString());

            TestMailbox = testMailboxes.FirstOrDefault();

            if (TestMailbox == null || !mailboxEngine.SaveMailBox(TestMailbox))
            {
                throw new Exception(string.Format("Can't create mailbox with email: {0}", TestUser.Email));
            }
        }

        /*[TearDown]
        public void CleanUp()
        {
            if (TestUser == null || TestUser.ID == Guid.Empty)
                return;

            using var scope = ServiceProvider.CreateScope();

            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(ASC.Core.Configuration.Constants.CoreSystem);

            // Remove TestUser profile
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            userManager.DeleteUser(TestUser.ID);

            // Clear TestUser mail index
            var factoryIndexer = scope.ServiceProvider.GetService<FactoryIndexer<MailMail>>();

            var t = scope.ServiceProvider.GetService<MailMail>();
            if (factoryIndexer.Support(t))
                factoryIndexer.DeleteAsync(s => s.Where(m => m.UserId, TestUser.ID.ToString())).Wait();

            // Clear TestUser mail data
            var mailGarbageEngine = scope.ServiceProvider.GetService<MailGarbageEngine>();
            mailGarbageEngine.ClearUserMail(TestUser.ID, tenantManager.GetCurrentTenant());
        }*/

        [Test]
        public void CreateTemplate()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            var folderEngine = scope.ServiceProvider.GetService<FolderEngine>();
            var coreSettings = scope.ServiceProvider.GetService<CoreSettings>();
            var templateEngine = scope.ServiceProvider.GetService<TemplateEngine>();
            var messageEngine = scope.ServiceProvider.GetService<MessageEngine>();

            var folders = folderEngine.GetFolders();

            Assert.IsNotEmpty(folders);

            var templateItem = new MailTemplateData(0, TestMailbox, "test@gmail.com", new List<string>(),
                new List<string>(), new List<string>(), "subject",
                MailUtil.CreateMessageId(tenantManager, coreSettings), null, false,
                null, "Test body", MailUtil.CreateStreamId(), new List<MailAttachmentData>());

            var data = templateEngine.Save(templateItem);

            Assert.Greater(data.Id, 0);

            folders = folderEngine.GetFolders();

            var templateFolder = folders.FirstOrDefault(f => f.id == FolderType.Templates);

            Assert.IsNotNull(templateFolder);
            Assert.AreEqual(1, templateFolder.totalMessages);
            Assert.AreEqual(0, templateFolder.unreadMessages);
            Assert.AreEqual(0, templateFolder.total);
            Assert.AreEqual(0, templateFolder.unread);

            var savedTemplateData = messageEngine.GetMessage(data.Id, new MailMessageData.Options());

            Assert.AreEqual("subject", savedTemplateData.Subject);
            Assert.AreEqual("test@gmail.com", savedTemplateData.From);
        }
    }
}
