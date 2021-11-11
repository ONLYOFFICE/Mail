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


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ASC.Core;
using ASC.Core.Users;
using ASC.Mail.Aggregator.Tests.Common.Utils;
using ASC.Mail.Models;
using ASC.Mail.Enums;
using ASC.Mail.Exceptions;
using ASC.Mail.Utils;
using NUnit.Framework;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ASC.Common;
using ASC.Api.Core.Auth;
using ASC.Api.Core.Middleware;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Autofac;
using ASC.Mail.Core.Engine;
using ASC.ElasticSearch;
using ASC.Common.Logging;
using ASC.Api.Core;
using ASC.Mail.Core.Dao.Entities;

namespace ASC.Mail.Aggregator.Tests.Common.Engine
{
    [TestFixture]
    internal class DraftEngineTests
    {
        private const int CURRENT_TENANT = 0;
        public const string PASSWORD = "123456";
        public const string DOMAIN = "gmail.com";

        public UserInfo TestUser { get; private set; }
        private MailBoxData TestMailbox { get; set; }

        private static readonly string TestFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
           @"..\..\..\Data\");
        private const string EML1_FILE_NAME = @"bad_encoding.eml";
        private static readonly string Eml1Path = TestFolderPath + EML1_FILE_NAME;

        IServiceProvider ServiceProvider { get; set; }
        IHost TestHost { get; set; }

        [OneTimeSetUp]
        public void Prepare()
        {
            var args = new string[] { };

            TestHost = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostContext, config) =>
                {
                    var buided = config.Build();
                    var path = buided["pathToConf"];
                    if (!Path.IsPathRooted(path))
                    {
                        path = Path.GetFullPath(Path.Combine(hostContext.HostingEnvironment.ContentRootPath, path));
                    }

                    config.SetBasePath(path);

                    config
                        .AddInMemoryCollection(new Dictionary<string, string>
                        {
                        {"pathToConf", path}
                        })
                        .AddJsonFile("appsettings.json")
                        .AddJsonFile($"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json", true)
                        .AddJsonFile("storage.json")
                        .AddJsonFile("kafka.json")
                        .AddJsonFile($"kafka.{hostContext.HostingEnvironment.EnvironmentName}.json", true)
                        .AddEnvironmentVariables();

                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHttpContextAccessor();

                    var diHelper = new DIHelper(services);

                    diHelper
                        .AddCookieAuthHandler()
                        .AddCultureMiddleware()
                        .AddIpSecurityFilter()
                        .AddPaymentFilter()
                        .AddProductSecurityFilter()
                        .AddTenantStatusFilter();

                    diHelper.AddNLogManager("ASC.Api", "ASC.Web");

                    diHelper
                        .AddTenantManagerService()
                        .AddUserManagerService()
                        .AddSecurityContextService()
                        .AddMailBoxSettingEngineService()
                        .AddMailboxEngineService()
                        .AddApiContextService()
                        .AddApiHelperService()
                        .AddFolderEngineService()
                        .AddUserFolderEngineService()
                        .AddFactoryIndexerService()
                        .AddFactoryIndexerService<MailMail>()
                        .AddMailGarbageEngineService()
                        .AddTestEngineService()
                        .AddDraftEngineService()
                        .AddMessageEngineService()
                        .AddCoreSettingsService();

                    var builder = new ContainerBuilder();
                    var container = builder.Build();

                    services.TryAddSingleton(container);

                    //services.AddAutofac(hostContext.Configuration, hostContext.HostingEnvironment.ContentRootPath);
                })
                .UseConsoleLifetime()
                .Build();

            TestHost.Start();

            ServiceProvider = TestHost.Services;
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

            TestUser = TestHelper.CreateNewRandomEmployee(userManager, securityContext, tenantManager, apiHelper);

            var mailboxSettings = mailBoxSettingEngine.GetMailBoxSettings(DOMAIN);

            var testMailboxes = mailboxSettings.ToMailboxList(TestUser.Email, PASSWORD, CURRENT_TENANT, TestUser.ID.ToString());

            TestMailbox = testMailboxes.FirstOrDefault();

            if (TestMailbox == null || !mailboxEngine.SaveMailBox(TestMailbox))
            {
                throw new Exception(string.Format("Can't create mailbox with email: {0}", TestUser.Email));
            }
        }

        [TearDown]
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
            var factoryIndexerHelper = scope.ServiceProvider.GetService<FactoryIndexer<MailMail>>();

            var t = scope.ServiceProvider.GetService<MailMail>();
            if (factoryIndexer.Support(t))
                factoryIndexer.DeleteAsync(s => s.Where(m => m.IdUser, TestUser.ID.ToString())).Wait();

            // Clear TestUser mail data
            var mailGarbageEngine = scope.ServiceProvider.GetService<MailGarbageEngine>();
            mailGarbageEngine.ClearUserMail(TestUser.ID, tenantManager.GetCurrentTenant());
        }

        [Test]
        public void CreateDraftTest()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            var folderEngine = scope.ServiceProvider.GetService<FolderEngine>();
            var coreSettings = scope.ServiceProvider.GetService<CoreSettings>();
            var draftEngine = scope.ServiceProvider.GetService<DraftEngine>();
            var messageEngine = scope.ServiceProvider.GetService<MessageEngine>();

            var folders = folderEngine.GetFolders();

            Assert.IsNotEmpty(folders);

            Assert.AreEqual(true,
                folders.Any(f => f.total == 0 && f.unread == 0 && f.totalMessages == 0 && f.unreadMessages == 0));

            var draftItem = new MailDraftData(0, TestMailbox, "test@gmail.com", new List<string>(), new List<string>(), new List<string>(), "subject", 
                MailUtil.CreateMessageId(tenantManager, coreSettings), null, false, null, "Test body", MailUtil.CreateStreamId(), new List<MailAttachmentData>());

            var data = draftEngine.Save(draftItem);

            Assert.Greater(data.Id, 0);

            folders = folderEngine.GetFolders();

            var draft = folders.FirstOrDefault(f => f.id == FolderType.Draft);

            Assert.IsNotNull(draft);
            Assert.AreEqual(1, draft.totalMessages);
            Assert.AreEqual(0, draft.unreadMessages);
            Assert.AreEqual(0, draft.total);
            Assert.AreEqual(0, draft.unread);

            var savedDraftData = messageEngine.GetMessage(data.Id, new MailMessageData.Options());

            Assert.AreEqual("subject", savedDraftData.Subject);
            Assert.AreEqual("test@gmail.com", savedDraftData.From);
        }

        [Test]
        public void CreateForwardDraftTest()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            var folderEngine = scope.ServiceProvider.GetService<FolderEngine>();
            var coreSettings = scope.ServiceProvider.GetService<CoreSettings>();
            var draftEngine = scope.ServiceProvider.GetService<DraftEngine>();
            var testEngine = scope.ServiceProvider.GetService<TestEngine>();
            var messageEngine = scope.ServiceProvider.GetService<MessageEngine>();

            var folders = folderEngine.GetFolders();

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

            var id1 = testEngine.CreateSampleMessage(model);

            Assert.Greater(id1, 0);

            MailAttachmentData attachData;

            using (var fs = new FileStream(Eml1Path, FileMode.Open, FileAccess.Read))
            {
                var attachmentModel = new TestAttachmentModel {
                    ContentType = "message/eml",
                    Filename = EML1_FILE_NAME,
                    Stream = fs
                };

                attachData = testEngine.AppendAttachmentsToSampleMessage(id1, attachmentModel);
            }

            Assert.IsNotNull(attachData);
            Assert.Greater(attachData.fileId, 0);

            var message = messageEngine.GetMessage(id1, new MailMessageData.Options());

            Assert.AreEqual(1, message.Attachments.Count);

            var draftItem = new MailDraftData(0, TestMailbox, "test@gmail.com", new List<string>(), new List<string>(), new List<string>(), 
                "subject", MailUtil.CreateMessageId(tenantManager, coreSettings), null, false, null, "Test body", 
                MailUtil.CreateStreamId(), message.Attachments);

            var data = draftEngine.Save(draftItem);

            Assert.Greater(data.Id, 0);

            folders = folderEngine.GetFolders();

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

            var savedDraftData = messageEngine.GetMessage(data.Id, new MailMessageData.Options());

            Assert.AreEqual("subject", savedDraftData.Subject);
            Assert.AreEqual("test@gmail.com", savedDraftData.From);
            Assert.AreEqual(1, savedDraftData.Attachments.Count);

            message = messageEngine.GetMessage(id1, new MailMessageData.Options());

            Assert.AreEqual(1, message.Attachments.Count);
        }

        [Test]
        public void CreateDraftWithClonedAttachmentTest()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            var folderEngine = scope.ServiceProvider.GetService<FolderEngine>();
            var coreSettings = scope.ServiceProvider.GetService<CoreSettings>();
            var draftEngine = scope.ServiceProvider.GetService<DraftEngine>();
            var testEngine = scope.ServiceProvider.GetService<TestEngine>();
            var messageEngine = scope.ServiceProvider.GetService<MessageEngine>();

            var folders = folderEngine.GetFolders();

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

            var id1 = testEngine.CreateSampleMessage(model);

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

                attachData = testEngine.AppendAttachmentsToSampleMessage(id1, attachmentModel);
            }

            Assert.IsNotNull(attachData);
            Assert.Greater(attachData.fileId, 0);

            var message = messageEngine.GetMessage(id1, new MailMessageData.Options());

            Assert.AreEqual(1, message.Attachments.Count);

            var clonedAttachments = new List<MailAttachmentData>();
            do
            {
                var clone = attachData.Clone() as MailAttachmentData;
                if (clone == null)
                    break;

                clonedAttachments.Add(clone);

            } while (clonedAttachments.Sum(a => a.size) < Defines.ATTACHMENTS_TOTAL_SIZE_LIMIT);

            Assert.Greater(clonedAttachments.Count, 1);

            var draftItem = new MailDraftData(0, TestMailbox, "test@gmail.com", new List<string>(), new List<string>(),
                new List<string>(), "subject", MailUtil.CreateMessageId(tenantManager, coreSettings), null, false, null, "Test body",
                MailUtil.CreateStreamId(), clonedAttachments);

            Assert.AreEqual(1, draftItem.Attachments.Count);

            var data = draftEngine.Save(draftItem);

            Assert.Greater(data.Id, 0);
            Assert.AreEqual(1, data.Attachments.Count);

            var savedDraftData = messageEngine.GetMessage(data.Id, new MailMessageData.Options());

            Assert.AreEqual("subject", savedDraftData.Subject);
            Assert.AreEqual("test@gmail.com", savedDraftData.From);
            Assert.AreEqual(1, savedDraftData.Attachments.Count);

            message = messageEngine.GetMessage(id1, new MailMessageData.Options());

            Assert.AreEqual(1, message.Attachments.Count);
        }

        [Test]
        public void CreateDraftWithAttachmentsTotalExceededTest()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            var coreSettings = scope.ServiceProvider.GetService<CoreSettings>();

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

            } while (attachments.Sum(a => a.size) < Defines.ATTACHMENTS_TOTAL_SIZE_LIMIT);

            Assert.Throws<DraftException>(
                () => new MailDraftData(0, TestMailbox, "test@gmail.com", new List<string>(), new List<string>(),
                    new List<string>(), "subject", MailUtil.CreateMessageId(tenantManager, coreSettings), null, false, null, "Test body",
                    MailUtil.CreateStreamId(), attachments), "Total size of all files exceeds limit!");
        }

        [Test]
        public void CreateDraftWithAttachAndOpenIt()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            var folderEngine = scope.ServiceProvider.GetService<FolderEngine>();
            var coreSettings = scope.ServiceProvider.GetService<CoreSettings>();
            var draftEngine = scope.ServiceProvider.GetService<DraftEngine>();
            var testEngine = scope.ServiceProvider.GetService<TestEngine>();
            var messageEngine = scope.ServiceProvider.GetService<MessageEngine>();

            var folders = folderEngine.GetFolders();

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

            var id1 = testEngine.CreateSampleMessage(model);

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

                attachData = testEngine.AppendAttachmentsToSampleMessage(id1, attachmentModel);
            }

            Assert.IsNotNull(attachData);
            Assert.Greater(attachData.fileId, 0);

            var message = messageEngine.GetMessage(id1, new MailMessageData.Options());

            Assert.AreEqual(1, message.Attachments.Count);

            var draftItem = new MailDraftData(0, TestMailbox, "test@gmail.com", 
                new List<string>(), new List<string>(), new List<string>(), "subject", 
                MailUtil.CreateMessageId(tenantManager, coreSettings), null, false, null, "Test body", 
                MailUtil.CreateStreamId(), message.Attachments);

            var data = draftEngine.Save(draftItem);

            Assert.AreEqual(1, data.Attachments.Count);

            data = draftEngine.Save(draftItem);

            Assert.AreEqual(1, data.Attachments.Count);

            data = draftEngine.Save(draftItem);

            Assert.AreEqual(1, data.Attachments.Count);
        }
    }
}
