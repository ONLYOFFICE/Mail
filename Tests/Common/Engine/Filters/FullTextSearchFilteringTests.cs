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
using ASC.ElasticSearch;
using ASC.Mail.Aggregator.Tests.Common.Utils;
using ASC.Mail.Core.Dao.Entities;
using ASC.Mail.Core.Engine;
using ASC.Mail.Enums;
using ASC.Mail.Enums.Filter;
using ASC.Mail.Models;
using ASC.Mail.Utils;

using Microsoft.Extensions.DependencyInjection;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Linq;

namespace ASC.Mail.Tests
{
    [TestFixture]
    internal class FullTextSearchFilteringTests : BaseMailTests
    {
        private const int CURRENT_TENANT = 1;
        public const string PASSWORD = "123456";
        public const string DOMAIN = "gmail.com";
        public const string EMAIL_NAME = "Test User";

        public MailBoxData TestMailbox { get; set; }
        public UserInfo TestUser { get; set; }
        public int MailId { get; set; }

        private const int PAGE = 0;
        private const int LIMIT = 10;

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

            var testEngine = scope.ServiceProvider.GetService<TestEngine>();

            TestUser = UserManager.GetUsers(Guid.Parse("66faa6e4-f133-11ea-b126-00ffeec8b4ef"));
            TestUser.Email = TestHelper.GetTestEmailAddress(DOMAIN);

            //вынести
            securityContext.AuthenticateMe(TestUser.ID);

            var mailboxSettings = mailBoxSettingEngine.GetMailBoxSettings(DOMAIN);

            var testMailboxes = mailboxSettings.ToMailboxList(TestUser.Email, PASSWORD, CURRENT_TENANT, TestUser.ID.ToString());

            TestMailbox = testMailboxes.FirstOrDefault();

            if (TestMailbox != null)
                TestMailbox.Name = EMAIL_NAME;

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
                        factoryIndexer.Delete(s => s.Where(m => m.UserId, TestUser.ID.ToString()));


                    // Clear TestUser mail data
                    var mailGarbageEngine = scope.ServiceProvider.GetService<MailGarbageEngine>();
                    mailGarbageEngine.ClearUserMail(TestUser.ID, tenantManager.GetCurrentTenant());
                }*/

        [Test]
        [Order(1)]
        public void CheckFtSearchSubjectMatch()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailMail>(false, scope.ServiceProvider))
                return;

            var testEngine = scope.ServiceProvider.GetService<TestEngine>();
            var messageEngine = scope.ServiceProvider.GetService<MessageEngine>();
            var factoryIndexer = scope.ServiceProvider.GetService<FactoryIndexer<MailMail>>();

            const string subj = "[TEST SUBJECT]";

            var model = new TestMessageModel
            {
                FolderId = (int)FolderType.Inbox,
                UserFolderId = null,
                MailboxId = TestMailbox.MailBoxId,
                Unread = true,
                To = new List<string> { "to@to.com" },
                Cc = new List<string> { "cc@cc.com" },
                Bcc = new List<string>(),
                Subject = subj,
                Body = "This is SPARTA"
            };

            var id = testEngine.CreateSampleMessage(model);

            var message = messageEngine.GetMessage(id, new MailMessageData.Options());

            var MailMail = message.ToMailMail(CURRENT_TENANT, TestUser.ID);

            factoryIndexer.Index(MailMail);

            Assert.Greater(id, 0);

            var filter = new MailSieveFilterData
            {
                Conditions = new List<MailSieveFilterConditionData>
                {
                    new MailSieveFilterConditionData
                    {
                        Key = ConditionKeyType.Subject,
                        Operation = ConditionOperationType.Matches,
                        Value = subj
                    }
                },
                Options = new MailSieveFilterOptionsData
                {
                    MatchMultiConditions = MatchMultiConditionsType.MatchAtLeastOne
                }
            };

            var messages = messageEngine.GetFilteredMessages(filter, PAGE, LIMIT, out long total);

            Assert.AreEqual(1, total);
            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual(subj, messages[0].Subject);
        }

        [Test]
        [Order(2)]
        public void CheckFtSearchSubjectContains()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailMail>(false, scope.ServiceProvider))
                return;

            var testEngine = scope.ServiceProvider.GetService<TestEngine>();
            var messageEngine = scope.ServiceProvider.GetService<MessageEngine>();
            var factoryIndexer = scope.ServiceProvider.GetService<FactoryIndexer<MailMail>>();

            var rndGuid = Guid.NewGuid().ToString("N");
            var subj = string.Format("[TEST SUBJECT] {0} zzz", rndGuid);

            var model = new TestMessageModel
            {
                FolderId = (int)FolderType.Inbox,
                UserFolderId = null,
                MailboxId = TestMailbox.MailBoxId,
                Unread = true,
                To = new List<string> { "to@to.com" },
                Cc = new List<string> { "cc@cc.com" },
                Bcc = new List<string>(),
                Subject = subj,
                Body = "This is SPARTA"
            };

            var id = testEngine.CreateSampleMessage(model);

            var message = messageEngine.GetMessage(id, new MailMessageData.Options());

            var MailMail = message.ToMailMail(CURRENT_TENANT, TestUser.ID);

            factoryIndexer.Index(MailMail);

            Assert.Greater(id, 0);

            var filter = new MailSieveFilterData
            {
                Conditions = new List<MailSieveFilterConditionData>
                {
                    new MailSieveFilterConditionData
                    {
                        Key = ConditionKeyType.Subject,
                        Operation = ConditionOperationType.Contains,
                        Value = rndGuid
                    }
                },
                Options = new MailSieveFilterOptionsData
                {
                    MatchMultiConditions = MatchMultiConditionsType.MatchAtLeastOne
                }
            };

            var messages = messageEngine.GetFilteredMessages(filter, PAGE, LIMIT, out long total);

            Assert.AreEqual(1, total);
            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual(subj, messages[0].Subject);
        }

        [Test]
        [Order(3)]
        public void CheckFtSearchFromMatch()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailMail>(false, scope.ServiceProvider))
                return;

            var testEngine = scope.ServiceProvider.GetService<TestEngine>();
            var messageEngine = scope.ServiceProvider.GetService<MessageEngine>();
            var factoryIndexer = scope.ServiceProvider.GetService<FactoryIndexer<MailMail>>();

            var model = new TestMessageModel
            {
                FolderId = (int)FolderType.Inbox,
                UserFolderId = null,
                MailboxId = TestMailbox.MailBoxId,
                Unread = true,
                To = new List<string> { "to@to.com" },
                Cc = new List<string> { "cc@cc.com" },
                Bcc = new List<string>(),
                Subject = "[TEST SUBJECT]",
                Body = "This is SPARTA"
            };

            var id = testEngine.CreateSampleMessage(model);

            var message = messageEngine.GetMessage(id, new MailMessageData.Options());

            var MailMail = message.ToMailMail(CURRENT_TENANT, TestUser.ID);

            factoryIndexer.Index(MailMail);

            Assert.Greater(id, 0);

            var filter = new MailSieveFilterData
            {
                Conditions = new List<MailSieveFilterConditionData>
                {
                    new MailSieveFilterConditionData
                    {
                        Key = ConditionKeyType.From,
                        Operation = ConditionOperationType.Matches,
                        Value = TestMailbox.EMail.Address
                    }
                },
                Options = new MailSieveFilterOptionsData
                {
                    MatchMultiConditions = MatchMultiConditionsType.MatchAtLeastOne
                }
            };

            var messages = messageEngine.GetFilteredMessages(filter, PAGE, LIMIT, out long total);

            Assert.AreEqual(1, total);
            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual(message.From, messages[0].From);
        }

        [Test]
        [Order(4)]
        public void CheckFtSearchFromContains()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailMail>(false, scope.ServiceProvider))
                return;

            var testEngine = scope.ServiceProvider.GetService<TestEngine>();
            var messageEngine = scope.ServiceProvider.GetService<MessageEngine>();
            var factoryIndexer = scope.ServiceProvider.GetService<FactoryIndexer<MailMail>>();

            var model = new TestMessageModel
            {
                FolderId = (int)FolderType.Inbox,
                UserFolderId = null,
                MailboxId = TestMailbox.MailBoxId,
                Unread = true,
                To = new List<string> { "to@to.com" },
                Cc = new List<string> { "cc@cc.com" },
                Bcc = new List<string>(),
                Subject = "[TEST SUBJECT]",
                Body = "This is SPARTA"
            };

            var id = testEngine.CreateSampleMessage(model);

            var message = messageEngine.GetMessage(id, new MailMessageData.Options());

            var MailMail = message.ToMailMail(CURRENT_TENANT, TestUser.ID);

            factoryIndexer.Index(MailMail);

            Assert.Greater(id, 0);

            var filter = new MailSieveFilterData
            {
                Conditions = new List<MailSieveFilterConditionData>
                {
                    new MailSieveFilterConditionData
                    {
                        Key = ConditionKeyType.From,
                        Operation = ConditionOperationType.Contains,
                        Value = EMAIL_NAME
                    }
                },
                Options = new MailSieveFilterOptionsData
                {
                    MatchMultiConditions = MatchMultiConditionsType.MatchAtLeastOne
                }
            };

            var messages = messageEngine.GetFilteredMessages(filter, PAGE, LIMIT, out long total);

            Assert.AreEqual(1, total);
            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual(message.From, messages[0].From);
        }

        [Test]
        [Order(5)]
        public void CheckFtSearchToOrCcMatch()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailMail>(false, scope.ServiceProvider))
                return;

            var testEngine = scope.ServiceProvider.GetService<TestEngine>();
            var messageEngine = scope.ServiceProvider.GetService<MessageEngine>();
            var factoryIndexer = scope.ServiceProvider.GetService<FactoryIndexer<MailMail>>();

            var model = new TestMessageModel
            {
                FolderId = (int)FolderType.Inbox,
                UserFolderId = null,
                MailboxId = TestMailbox.MailBoxId,
                Unread = true,
                To = new List<string> { "to@to.com" },
                Cc = new List<string> { "cc@cc.com" },
                Bcc = new List<string>(),
                Subject = "[TEST SUBJECT]",
                Body = "This is SPARTA"
            };

            var id = testEngine.CreateSampleMessage(model);

            var message = messageEngine.GetMessage(id, new MailMessageData.Options());

            var MailMail = message.ToMailMail(CURRENT_TENANT, TestUser.ID);

            factoryIndexer.Index(MailMail);

            Assert.Greater(id, 0);

            var filter = new MailSieveFilterData
            {
                Conditions = new List<MailSieveFilterConditionData>
                {
                    new MailSieveFilterConditionData
                    {
                        Key = ConditionKeyType.ToOrCc,
                        Operation = ConditionOperationType.Matches,
                        Value = "cc@cc.com"
                    }
                }
            };

            var messages = messageEngine.GetFilteredMessages(filter, PAGE, LIMIT, out long total);

            Assert.AreEqual(1, total);
            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual("to@to.com", messages[0].To);
            Assert.AreEqual("cc@cc.com", messages[0].Cc);

            filter = new MailSieveFilterData
            {
                Conditions = new List<MailSieveFilterConditionData>
                {
                    new MailSieveFilterConditionData
                    {
                        Key = ConditionKeyType.ToOrCc,
                        Operation = ConditionOperationType.Matches,
                        Value = "to@to.com"
                    }
                }
            };

            messages = messageEngine.GetFilteredMessages(filter, PAGE, LIMIT, out total);

            Assert.AreEqual(1, total);
            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual("to@to.com", messages[0].To);
            Assert.AreEqual("cc@cc.com", messages[0].Cc);
        }

        [Test]
        [Order(6)]
        public void CheckFtSearchToOrCcContains()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailMail>(false, scope.ServiceProvider))
                return;

            var testEngine = scope.ServiceProvider.GetService<TestEngine>();
            var messageEngine = scope.ServiceProvider.GetService<MessageEngine>();
            var factoryIndexer = scope.ServiceProvider.GetService<FactoryIndexer<MailMail>>();

            var model = new TestMessageModel
            {
                FolderId = (int)FolderType.Inbox,
                UserFolderId = null,
                MailboxId = TestMailbox.MailBoxId,
                Unread = true,
                To = new List<string> { "\"to to\" <to@to.com>" },
                Cc = new List<string> { "\"cc cc\" <cc@cc.com>" },
                Bcc = new List<string>(),
                Subject = "[TEST SUBJECT]",
                Body = "This is SPARTA"
            };

            var id = testEngine.CreateSampleMessage(model);

            var message = messageEngine.GetMessage(id, new MailMessageData.Options());

            var MailMail = message.ToMailMail(CURRENT_TENANT, TestUser.ID);

            factoryIndexer.Index(MailMail);

            Assert.Greater(id, 0);

            var filter = new MailSieveFilterData
            {
                Conditions = new List<MailSieveFilterConditionData>
                {
                    new MailSieveFilterConditionData
                    {
                        Key = ConditionKeyType.ToOrCc,
                        Operation = ConditionOperationType.Contains,
                        Value = "cc@cc.com"
                    }
                }
            };

            var messages = messageEngine.GetFilteredMessages(filter, PAGE, LIMIT, out long total);

            Assert.AreEqual(1, total);
            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual("\"to to\" <to@to.com>", messages[0].To);
            Assert.AreEqual("\"cc cc\" <cc@cc.com>", messages[0].Cc);

            filter = new MailSieveFilterData
            {
                Conditions = new List<MailSieveFilterConditionData>
                {
                    new MailSieveFilterConditionData
                    {
                        Key = ConditionKeyType.ToOrCc,
                        Operation = ConditionOperationType.Contains,
                        Value = "to@to.com"
                    }
                }
            };

            messages = messageEngine.GetFilteredMessages(filter, PAGE, LIMIT, out total);

            Assert.AreEqual(1, total);
            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual("\"to to\" <to@to.com>", messages[0].To);
            Assert.AreEqual("\"cc cc\" <cc@cc.com>", messages[0].Cc);
        }

        [Test]
        [Order(7)]
        public void CheckFtSearchByFromEmailOnly()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailMail>(false, scope.ServiceProvider))
                return;

            var testEngine = scope.ServiceProvider.GetService<TestEngine>();
            var messageEngine = scope.ServiceProvider.GetService<MessageEngine>();
            var factoryIndexer = scope.ServiceProvider.GetService<FactoryIndexer<MailMail>>();

            var model = new TestMessageModel
            {
                FolderId = (int)FolderType.Inbox,
                UserFolderId = null,
                MailboxId = TestMailbox.MailBoxId,
                Unread = true,
                FromAddress = "\"Twitter\" <info@twitter.com>",
                To = new List<string> { "\"mono\" <mono.mail.4test@gmail.com>" },
                Cc = new List<string>(),
                Bcc = new List<string>(),
                Subject = "[TEST SUBJECT]",
                Body = "This is SPARTA"
            };

            var id = testEngine.CreateSampleMessage(model);

            var message = messageEngine.GetMessage(id, new MailMessageData.Options());

            var MailMail = message.ToMailMail(CURRENT_TENANT, TestUser.ID);

            factoryIndexer.Index(MailMail);

            Assert.Greater(id, 0);

            var filter = new MailSearchFilterData // With page
            {
                PrimaryFolder = FolderType.Inbox,
                FromAddress = "info@twitter.com",
                Page = 0,
                PageSize = 10
            };

            var messages = messageEngine.GetFilteredMessages(filter, out long total);

            Assert.AreEqual(1, total);
            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual(message.From, messages[0].From);
        }

        [Test]
        [Order(8)]
        public void CheckFtSearchFromAndSubjectMatchAll()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailMail>(false, scope.ServiceProvider))
                return;

            var testEngine = scope.ServiceProvider.GetService<TestEngine>();
            var messageEngine = scope.ServiceProvider.GetService<MessageEngine>();
            var factoryIndexer = scope.ServiceProvider.GetService<FactoryIndexer<MailMail>>();

            var subject = "[TEST SUBJECT]";

            var model = new TestMessageModel
            {
                FolderId = (int)FolderType.Inbox,
                UserFolderId = null,
                MailboxId = TestMailbox.MailBoxId,
                Unread = true,
                To = new List<string> { "to@to.com" },
                Cc = new List<string> { "cc@cc.com" },
                Bcc = new List<string>(),
                Subject = subject,
                Body = "This is SPARTA"
            };

            var id = testEngine.CreateSampleMessage(model);

            var message = messageEngine.GetMessage(id, new MailMessageData.Options());

            var MailMail = message.ToMailMail(CURRENT_TENANT, TestUser.ID);

            factoryIndexer.Index(MailMail);

            Assert.Greater(id, 0);

            var filter = new MailSieveFilterData
            {
                Conditions = new List<MailSieveFilterConditionData>
                {
                    new MailSieveFilterConditionData
                    {
                        Key = ConditionKeyType.From,
                        Operation = ConditionOperationType.Matches,
                        Value = TestMailbox.EMail.Address
                    },
                    new MailSieveFilterConditionData
                    {
                        Key = ConditionKeyType.Subject,
                        Operation = ConditionOperationType.Matches,
                        Value = subject
                    }
                },
                Options = new MailSieveFilterOptionsData
                {
                    MatchMultiConditions = MatchMultiConditionsType.MatchAll
                }
            };

            var messages = messageEngine.GetFilteredMessages(filter, PAGE, LIMIT, out long total);

            Assert.AreEqual(1, total);
            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual(message.From, messages[0].From);
            Assert.AreEqual(message.Subject, messages[0].Subject);
        }

        [Test]
        [Order(9)]
        public void CheckFtSearchFromAndSubjectMatchAtLeastOne()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailMail>(false, scope.ServiceProvider))
                return;

            var testEngine = scope.ServiceProvider.GetService<TestEngine>();
            var messageEngine = scope.ServiceProvider.GetService<MessageEngine>();
            var factoryIndexer = scope.ServiceProvider.GetService<FactoryIndexer<MailMail>>();

            var subject = "[TEST SUBJECT]";

            var model = new TestMessageModel
            {
                FolderId = (int)FolderType.Inbox,
                UserFolderId = null,
                MailboxId = TestMailbox.MailBoxId,
                Unread = true,
                To = new List<string> { "to@to.com" },
                Cc = new List<string> { "cc@cc.com" },
                Bcc = new List<string>(),
                Subject = subject,
                Body = "This is SPARTA"
            };

            var id = testEngine.CreateSampleMessage(model);

            var message = messageEngine.GetMessage(id, new MailMessageData.Options());

            var MailMail = message.ToMailMail(CURRENT_TENANT, TestUser.ID);

            factoryIndexer.Index(MailMail);

            Assert.Greater(id, 0);

            var filter = new MailSieveFilterData
            {
                Conditions = new List<MailSieveFilterConditionData>
                {
                    new MailSieveFilterConditionData
                    {
                        Key = ConditionKeyType.From,
                        Operation = ConditionOperationType.Matches,
                        Value = TestMailbox.EMail.Address
                    },
                    new MailSieveFilterConditionData
                    {
                        Key = ConditionKeyType.Subject,
                        Operation = ConditionOperationType.Matches,
                        Value = subject + "1"
                    }
                },
                Options = new MailSieveFilterOptionsData
                {
                    MatchMultiConditions = MatchMultiConditionsType.MatchAtLeastOne
                }
            };

            var messages = messageEngine.GetFilteredMessages(filter, PAGE, LIMIT, out long total);

            Assert.AreEqual(1, total);
            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual(message.From, messages[0].From);
            Assert.AreEqual(message.Subject, messages[0].Subject);
        }

        [Test]
        [Order(10)]
        public void CheckFtSearchToWithDiffMatchAll()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailMail>(false, scope.ServiceProvider))
                return;

            var testEngine = scope.ServiceProvider.GetService<TestEngine>();
            var messageEngine = scope.ServiceProvider.GetService<MessageEngine>();
            var factoryIndexer = scope.ServiceProvider.GetService<FactoryIndexer<MailMail>>();

            var to = "to@to.com";

            var model = new TestMessageModel
            {
                FolderId = (int)FolderType.Inbox,
                UserFolderId = null,
                MailboxId = TestMailbox.MailBoxId,
                Unread = true,
                To = new List<string> { to },
                Cc = new List<string> { "cc@cc.com" },
                Bcc = new List<string>(),
                Subject = "[TEST SUBJECT]",
                Body = "This is SPARTA"
            };

            var id = testEngine.CreateSampleMessage(model);

            Assert.Greater(id, 0);

            var model2 = new TestMessageModel
            {
                FolderId = (int)FolderType.Inbox,
                UserFolderId = null,
                MailboxId = TestMailbox.MailBoxId,
                Unread = true,
                To = new List<string> { "to@todo.com" },
                Cc = new List<string> { "cc@ccdo.com" },
                Bcc = new List<string>(),
                Subject = "[TEST 2 SUBJECT]",
                Body = "This is TROY"
            };

            var id2 = testEngine.CreateSampleMessage(model2);

            Assert.Greater(id2, 0);

            var message = messageEngine.GetMessage(id, new MailMessageData.Options());
            var MailMail = message.ToMailMail(CURRENT_TENANT, TestUser.ID);

            factoryIndexer.Index(MailMail);

            var message2 = messageEngine.GetMessage(id2, new MailMessageData.Options());
            var MailMail2 = message2.ToMailMail(CURRENT_TENANT, TestUser.ID);

            factoryIndexer.Index(MailMail2);

            var filter = new MailSieveFilterData
            {
                Conditions = new List<MailSieveFilterConditionData>
                {
                    new MailSieveFilterConditionData
                    {
                        Key = ConditionKeyType.To,
                        Operation = ConditionOperationType.Matches,
                        Value = to
                    }
                }
            };

            var messages = messageEngine.GetFilteredMessages(filter, PAGE, LIMIT, out long total);

            Assert.AreEqual(1, total);
            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual(message.To, messages[0].To);
        }

        [Test]
        [Order(11)]
        public void CheckFtSearchWithCalendar()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailMail>(false, scope.ServiceProvider))
                return;

            var testEngine = scope.ServiceProvider.GetService<TestEngine>();
            var messageEngine = scope.ServiceProvider.GetService<MessageEngine>();
            var factoryIndexer = scope.ServiceProvider.GetService<FactoryIndexer<MailMail>>();

            var model = new TestMessageModel
            {
                FolderId = (int)FolderType.Inbox,
                UserFolderId = null,
                MailboxId = TestMailbox.MailBoxId,
                Unread = true,
                To = new List<string> { "to@to.com" },
                Cc = new List<string> { "cc@cc.com" },
                Bcc = new List<string>(),
                Subject = "[TEST SUBJECT]",
                Body = "This is SPARTA"
            };

            var id = testEngine.CreateSampleMessage(model);

            Assert.Greater(id, 0);

            var model2 = new TestMessageModel
            {
                FolderId = (int)FolderType.Inbox,
                UserFolderId = null,
                MailboxId = TestMailbox.MailBoxId,
                Unread = true,
                To = new List<string> { "to@todo.com" },
                Cc = new List<string> { "cc@ccdo.com" },
                Bcc = new List<string>(),
                Subject = "[TEST 2 SUBJECT]",
                Body = "This is TROY",
                CalendarUid = "5FE291F6-64D0-4751-938F-53033FCD9225"
            };

            var id2 = testEngine.CreateSampleMessage(model2);

            Assert.Greater(id2, 0);

            var message = messageEngine.GetMessage(id, new MailMessageData.Options());
            var MailMail = message.ToMailMail(CURRENT_TENANT, TestUser.ID);

            factoryIndexer.Index(MailMail);

            var message2 = messageEngine.GetMessage(id2, new MailMessageData.Options());
            var MailMail2 = message2.ToMailMail(CURRENT_TENANT, TestUser.ID);

            factoryIndexer.Index(MailMail2);

            var selector = new Selector<MailMail>(ServiceProvider)
                .Where(m => m.WithCalendar, true)
                .Where(r => r.UserId, TestUser.ID.ToString())
                .Sort(r => r.DateSent, true);

            var success = factoryIndexer.TrySelectIds(m => selector, out List<int> mailIds);

            Assert.AreEqual(true, success);
            Assert.AreEqual(1, mailIds.Count);
            Assert.AreEqual(id2, mailIds.First());
        }

        [Test]
        [Order(12)]
        public void CheckFtSortByDateSend()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailMail>(false, scope.ServiceProvider))
                return;

            var testEngine = scope.ServiceProvider.GetService<TestEngine>();
            var messageEngine = scope.ServiceProvider.GetService<MessageEngine>();
            var factoryIndexer = scope.ServiceProvider.GetService<FactoryIndexer<MailMail>>();

            const string sort_order = DefineConstants.DESCENDING;

            var model = new TestMessageModel
            {
                FolderId = (int)FolderType.Inbox,
                UserFolderId = null,
                MailboxId = TestMailbox.MailBoxId,
                Unread = true,
                To = new List<string> { "to@to.com" },
                Cc = new List<string> { "cc@cc.com" },
                Bcc = new List<string>(),
                Subject = "[TEST SUBJECT]",
                Body = "This is SPARTA",
                Date = new DateTime(2018, 09, 18, 9, 2, 33)
            };

            var id1 = testEngine.CreateSampleMessage(model);

            Assert.Greater(id1, 0);

            var model2 = new TestMessageModel
            {
                FolderId = (int)FolderType.Inbox,
                UserFolderId = null,
                MailboxId = TestMailbox.MailBoxId,
                Unread = true,
                To = new List<string> { "to@todo.com" },
                Cc = new List<string> { "cc@ccdo.com" },
                Bcc = new List<string>(),
                Subject = "[TEST 2 SUBJECT]",
                Body = "This is TROY",
                Date = new DateTime(2018, 09, 14, 16, 36, 30)
            };

            var id2 = testEngine.CreateSampleMessage(model2);

            Assert.Greater(id2, 0);

            var model3 = new TestMessageModel
            {
                FolderId = (int)FolderType.Inbox,
                UserFolderId = null,
                MailboxId = TestMailbox.MailBoxId,
                Unread = true,
                To = new List<string> { "to@todo.com" },
                Cc = new List<string> { "cc@ccdo.com" },
                Bcc = new List<string>(),
                Subject = "[TEST 3 SUBJECT]",
                Body = "This is ROMA",
                Date = new DateTime(2018, 09, 15, 7, 51, 12)
            };

            var id3 = testEngine.CreateSampleMessage(model3);

            Assert.Greater(id3, 0);

            var ids = new[] { id1, id2, id3 };

            foreach (var id in ids)
            {
                var message = messageEngine.GetMessage(id, new MailMessageData.Options());

                var MailMail = message.ToMailMail(CURRENT_TENANT, TestUser.ID);

                factoryIndexer.Index(MailMail);
            }

            var selector = new Selector<MailMail>(ServiceProvider)
                .Where(r => r.Folder, (int)FolderType.Inbox)
                .Where(r => r.UserId, TestUser.ID.ToString())
                .Sort(r => r.DateSent, sort_order == DefineConstants.ASCENDING);

            var success = factoryIndexer.TrySelectIds(m => selector, out List<int> mailIds);

            Assert.AreEqual(true, success);
            Assert.AreEqual(3, mailIds.Count);
            Assert.AreEqual(id1, mailIds[0]);
            Assert.AreEqual(id3, mailIds[1]);
            Assert.AreEqual(id2, mailIds[2]);

            var selector1 = new Selector<MailMail>(ServiceProvider);

            selector1.Where(r => r.Folder, (int)FolderType.Inbox);

            selector1.Where(r => r.UserId, TestUser.ID.ToString())
                .Sort(r => r.DateSent, sort_order == DefineConstants.ASCENDING);

            success = factoryIndexer.TrySelectIds(m => selector1, out mailIds);

            Assert.AreEqual(true, success);
            Assert.AreEqual(3, mailIds.Count);
            Assert.AreEqual(id1, mailIds[0]);
            Assert.AreEqual(id3, mailIds[1]);
            Assert.AreEqual(id2, mailIds[2]);

            var index = 0;
            const int max = 2;

            var selector2 = new Selector<MailMail>(ServiceProvider)
                .Where(r => r.Folder, (int)FolderType.Inbox)
                .Limit(index, max)
                .Where(r => r.UserId, TestUser.ID.ToString())
                .Sort(r => r.DateSent, sort_order == DefineConstants.ASCENDING);

            success = factoryIndexer.TrySelectIds(m => selector2, out mailIds);

            Assert.AreEqual(true, success);
            Assert.AreEqual(2, mailIds.Count);
            Assert.AreEqual(id1, mailIds[0]);
            Assert.AreEqual(id3, mailIds[1]);

            index += mailIds.Count;

            var selector3 = new Selector<MailMail>(ServiceProvider)
                .Where(r => r.Folder, (int)FolderType.Inbox)
                .Limit(index, max)
                .Where(r => r.UserId, TestUser.ID.ToString())
                .Sort(r => r.DateSent, sort_order == DefineConstants.ASCENDING);

            success = factoryIndexer.TrySelectIds(m => selector3, out mailIds);

            Assert.AreEqual(true, success);
            Assert.AreEqual(1, mailIds.Count);
            Assert.AreEqual(id2, mailIds[0]);

            var filter = new MailSearchFilterData
            {
                PrimaryFolder = FolderType.Inbox,
                PageSize = 25,
                Sort = DefineConstants.ORDER_BY_DATE_SENT,
                SortOrder = sort_order
            };

            var messages = messageEngine.GetFilteredMessages(filter, out long totalMessages);

            Assert.AreEqual(true, success);
            Assert.AreEqual(3, messages.Count);
            Assert.AreEqual(id1, messages[0].Id);
            Assert.AreEqual(id3, messages[1].Id);
            Assert.AreEqual(id2, messages[2].Id);
        }

        [Test]
        [Order(13)]
        public void CheckFtPrevOrNextMessage()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailMail>(false, scope.ServiceProvider))
                return;

            var testEngine = scope.ServiceProvider.GetService<TestEngine>();
            var messageEngine = scope.ServiceProvider.GetService<MessageEngine>();
            var factoryIndexer = scope.ServiceProvider.GetService<FactoryIndexer<MailMail>>();

            var model = new TestMessageModel
            {
                FolderId = (int)FolderType.Inbox,
                UserFolderId = null,
                MailboxId = TestMailbox.MailBoxId,
                Unread = true,
                To = new List<string> { "to@to.com" },
                Cc = new List<string> { "cc@cc.com" },
                Bcc = new List<string>(),
                Subject = "[TEST SUBJECT]",
                Body = "This is SPARTA",
                Date = new DateTime(2018, 09, 18, 9, 2, 33)
            };

            var id1 = testEngine.CreateSampleMessage(model);

            Assert.Greater(id1, 0);

            var model2 = new TestMessageModel
            {
                FolderId = (int)FolderType.Inbox,
                UserFolderId = null,
                MailboxId = TestMailbox.MailBoxId,
                Unread = true,
                To = new List<string> { "to@todo.com" },
                Cc = new List<string> { "cc@ccdo.com" },
                Bcc = new List<string>(),
                Subject = "[TEST 2 SUBJECT]",
                Body = "This is TROY",
                Date = new DateTime(2018, 09, 14, 16, 36, 30)
            };

            var id2 = testEngine.CreateSampleMessage(model2);

            Assert.Greater(id2, 0);

            var model3 = new TestMessageModel
            {
                FolderId = (int)FolderType.Inbox,
                UserFolderId = null,
                MailboxId = TestMailbox.MailBoxId,
                Unread = true,
                To = new List<string> { "to@todo.com" },
                Cc = new List<string> { "cc@ccdo.com" },
                Bcc = new List<string>(),
                Subject = "[TEST 3 SUBJECT]",
                Body = "This is ROMA",
                Date = new DateTime(2018, 09, 15, 7, 51, 12)
            };

            var id3 = testEngine.CreateSampleMessage(model3);

            Assert.Greater(id3, 0);

            var ids = new[] { id1, id2, id3 };

            foreach (var id in ids)
            {
                var message = messageEngine.GetMessage(id, new MailMessageData.Options());

                var MailMail = message.ToMailMail(CURRENT_TENANT, TestUser.ID);

                factoryIndexer.Index(MailMail);
            }

            var filter = new MailSearchFilterData
            {
                PrimaryFolder = FolderType.Inbox,
                PageSize = 25,
                Sort = DefineConstants.ORDER_BY_DATE_SENT,
                SortOrder = DefineConstants.DESCENDING
            };

            var nextId = messageEngine.GetNextFilteredMessageId(id1, filter);

            Assert.AreEqual(id3, nextId);

            nextId = messageEngine.GetNextFilteredMessageId(id2, filter);

            Assert.AreEqual(-1, nextId);

            filter = new MailSearchFilterData
            {
                PrimaryFolder = FolderType.Inbox,
                PageSize = 25,
                Sort = DefineConstants.ORDER_BY_DATE_SENT,
                SortOrder = DefineConstants.ASCENDING
            };

            var prevId = messageEngine.GetNextFilteredMessageId(id2, filter);

            Assert.AreEqual(id3, prevId);

            prevId = messageEngine.GetNextFilteredMessageId(id1, filter);

            Assert.AreEqual(-1, prevId);
        }

        [Test]
        [Order(14)]
        public void CheckFtSearchWithTags()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailMail>(false, scope.ServiceProvider))
                return;

            var testEngine = scope.ServiceProvider.GetService<TestEngine>();
            var messageEngine = scope.ServiceProvider.GetService<MessageEngine>();
            var tagEngine = scope.ServiceProvider.GetService<TagEngine>();
            var factoryIndexer = scope.ServiceProvider.GetService<FactoryIndexer<MailMail>>();

            var tag1 = tagEngine.CreateTag("Tag1", "4", new List<string>());

            Assert.Greater(tag1.Id, 0);

            var tag2 = tagEngine.CreateTag("Tag2", "3", new List<string>());

            Assert.Greater(tag2.Id, 0);

            var tagIds1 = new List<int> { tag2.Id, tag1.Id };
            var tagIds2 = new List<int> { tag2.Id };

            var model = new TestMessageModel
            {
                FolderId = (int)FolderType.Inbox,
                UserFolderId = null,
                MailboxId = TestMailbox.MailBoxId,
                Unread = true,
                To = new List<string> { "to@to.com" },
                Cc = new List<string> { "cc@cc.com" },
                Bcc = new List<string>(),
                Subject = "[TEST SUBJECT]",
                Body = "This is SPARTA",
                TagIds = tagIds1
            };

            var id1 = testEngine.CreateSampleMessage(model);

            Assert.Greater(id1, 0);

            var model2 = new TestMessageModel
            {
                FolderId = (int)FolderType.Inbox,
                UserFolderId = null,
                MailboxId = TestMailbox.MailBoxId,
                Unread = true,
                To = new List<string> { "to@todo.com" },
                Cc = new List<string> { "cc@ccdo.com" },
                Bcc = new List<string>(),
                Subject = "[TEST 2 SUBJECT]",
                Body = "This is TROY",
                TagIds = tagIds2
            };

            var id2 = testEngine.CreateSampleMessage(model2);

            Assert.Greater(id2, 0);

            var model3 = new TestMessageModel
            {
                FolderId = (int)FolderType.Inbox,
                UserFolderId = null,
                MailboxId = TestMailbox.MailBoxId,
                Unread = true,
                To = new List<string> { "to@todo.com" },
                Cc = new List<string> { "cc@ccdo.com" },
                Bcc = new List<string>(),
                Subject = "[TEST 3 SUBJECT]",
                Body = "This is ROMA"
            };

            var id3 = testEngine.CreateSampleMessage(model3);

            Assert.Greater(id3, 0);

            var message = messageEngine.GetMessage(id1, new MailMessageData.Options());
            var MailMail = message.ToMailMail(CURRENT_TENANT, TestUser.ID);

            factoryIndexer.Index(MailMail);

            var message2 = messageEngine.GetMessage(id2, new MailMessageData.Options());
            var MailMail2 = message2.ToMailMail(CURRENT_TENANT, TestUser.ID);

            factoryIndexer.Index(MailMail2);

            var message3 = messageEngine.GetMessage(id3, new MailMessageData.Options());
            var MailMail3 = message3.ToMailMail(CURRENT_TENANT, TestUser.ID);

            factoryIndexer.Index(MailMail3);

            var selectorOneTag = new Selector<MailMail>(ServiceProvider)
                .InAll(r => r.Tags.Select(t => t.Id), tagIds2.ToArray())
                .Where(r => r.UserId, TestUser.ID.ToString())
                .Sort(r => r.DateSent, true);

            var success = factoryIndexer.TrySelectIds(m => selectorOneTag, out List<int> mailIds);

            Assert.AreEqual(true, success);
            Assert.AreEqual(2, mailIds.Count);

            var ids = new[] { id1, id2 };
            Assert.AreEqual(true, mailIds.All(id => ids.Contains(id)));

            var selectorTwoTags = new Selector<MailMail>(ServiceProvider)
                .InAll(r => r.Tags.Select(t => t.Id), tagIds1.ToArray())
                .Where(r => r.UserId, TestUser.ID.ToString())
                .Sort(r => r.DateSent, true);

            success = factoryIndexer.TrySelectIds(m => selectorTwoTags, out mailIds);

            Assert.AreEqual(true, success);
            Assert.AreEqual(1, mailIds.Count);
            Assert.AreEqual(id1, mailIds.First());
        }

        private List<int> GenerateIndexedMessages(int count)
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            var testEngine = scope.ServiceProvider.GetService<TestEngine>();

            var idList = new List<int>(count);

            var i = 0;

            var date = DateTime.Now;

            while (i < count)
            {
                var model = new TestMessageModel
                {
                    FolderId = (int)FolderType.Inbox,
                    UserFolderId = null,
                    MailboxId = TestMailbox.MailBoxId,
                    Unread = true,
                    To = new List<string> { "to@to.com" },
                    Cc = new List<string> { "cc@cc.com" },
                    Bcc = new List<string>(),
                    Subject = string.Format("{0} [TEST SUBJECT]", i),
                    Body = "This is SPARTA",
                    Date = date
                };

                var id = testEngine.CreateSampleMessage(model, true);

                idList.Add(id);

                i++;
                date = date.AddHours(-i);
            }

            return idList;
        }


        [Test]
        public void CheckFtUnreadMessagesPaging()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailMail>(false, scope.ServiceProvider))
                return;

            var testEngine = scope.ServiceProvider.GetService<TestEngine>();
            var messageEngine = scope.ServiceProvider.GetService<MessageEngine>();
            var factoryIndexer = scope.ServiceProvider.GetService<FactoryIndexer<MailMail>>();

            const int TOTAL_COUNT = 27;
            const int PAGE_SIZE = 25;

            // control message
            var ids = GenerateIndexedMessages(TOTAL_COUNT);

            var model = new TestMessageModel
            {
                FolderId = (int)FolderType.Inbox,
                UserFolderId = null,
                MailboxId = TestMailbox.MailBoxId,
                Unread = false,
                To = new List<string> { "to@to.com" },
                Cc = new List<string> { "cc@cc.com" },
                Bcc = new List<string>(),
                Subject = "[TEST CONTROL SUBJECT]",
                Body = "This is SPARTA",
                Date = new DateTime(2018, 09, 18, 9, 2, 33)
            };

            var id = testEngine.CreateSampleMessage(model, true);

            Assert.Greater(id, 0);

            Assert.AreEqual(TOTAL_COUNT, ids.Count);

            var filter = new MailSearchFilterData
            {
                PrimaryFolder = FolderType.Inbox,
                Unread = true,
                Page = 0,
                PageSize = PAGE_SIZE,
                Sort = DefineConstants.ORDER_BY_DATE_SENT,
                SortOrder = DefineConstants.ASCENDING
            };

            var messages = messageEngine.GetFilteredMessages(filter, out long totalMessagesCount); // FIRST PAGE

            Assert.AreEqual(PAGE_SIZE, messages.Count);
            Assert.AreEqual(TOTAL_COUNT, totalMessagesCount);

            filter.Page++;

            messages = messageEngine.GetFilteredMessages(filter, out totalMessagesCount); // SECOND AND LAST PAGE

            Assert.AreEqual(TOTAL_COUNT - PAGE_SIZE, messages.Count);
            Assert.AreEqual(TOTAL_COUNT - PAGE_SIZE, totalMessagesCount);

            filter.Page--;

            messages = messageEngine.GetFilteredMessages(filter, out totalMessagesCount); // BACK TO FIRST PAGE

            Assert.AreEqual(PAGE_SIZE, messages.Count);
            Assert.AreEqual(TOTAL_COUNT, totalMessagesCount);

            filter.Page = -1;

            messages = messageEngine.GetFilteredMessages(filter, out totalMessagesCount); // NEGATIVE PAGE TEST

            Assert.AreEqual(0, messages.Count);
            Assert.AreEqual(0, totalMessagesCount);

            filter.Page = 2;

            messages = messageEngine.GetFilteredMessages(filter, out totalMessagesCount); // OVERFLOW PAGE TEST

            Assert.AreEqual(0, messages.Count);
            Assert.AreEqual(0, totalMessagesCount);
        }
    }
}
