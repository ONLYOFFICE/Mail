﻿/*
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
    internal class SqlFilteringTests : BaseMailTests
    {
        private const int CURRENT_TENANT = 1;
        public const string PASSWORD = "123456";
        public const string DOMAIN = "gmail.com";

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
            var factoryIndexerHelper = scope.ServiceProvider.GetService<FactoryIndexerHelper>();

            // Clear TestUser mail data
            var mailGarbageEngine = scope.ServiceProvider.GetService<MailGarbageEngine>();
            mailGarbageEngine.ClearUserMail(TestUser.ID, tenantManager.GetCurrentTenant());
        }*/

        [Test]
        [Order(1)]
        public void CheckSimpleFilterFromMatch()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailMail>(true, scope.ServiceProvider))
                return;

            var testEngine = scope.ServiceProvider.GetService<TestEngine>();
            var messageEngine = scope.ServiceProvider.GetService<MessageEngine>();

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
                }
            };

            var messages = messageEngine.GetFilteredMessages(filter, PAGE, LIMIT, out long total);

            Assert.AreEqual(1, total);
            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual(TestMailbox.EMail.Address, messages[0].From);
        }

        [Test]
        [Order(2)]
        public void CheckSimpleFilterFromContains()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailMail>(true, scope.ServiceProvider))
                return;

            var testEngine = scope.ServiceProvider.GetService<TestEngine>();
            var messageEngine = scope.ServiceProvider.GetService<MessageEngine>();

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

            var filter = new MailSieveFilterData
            {
                Conditions = new List<MailSieveFilterConditionData>
                {
                    new MailSieveFilterConditionData
                    {
                        Key = ConditionKeyType.From,
                        Operation = ConditionOperationType.Contains,
                        Value = TestMailbox.EMail.Host
                    }
                }
            };

            var messages = messageEngine.GetFilteredMessages(filter, PAGE, LIMIT, out long total);

            Assert.AreEqual(1, total);
            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual(TestMailbox.EMail.Address, messages[0].From);
        }

        [Test]
        [Order(3)]
        public void CheckSimpleFilterToMatch()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailMail>(true, scope.ServiceProvider))
                return;

            var testEngine = scope.ServiceProvider.GetService<TestEngine>();
            var messageEngine = scope.ServiceProvider.GetService<MessageEngine>();

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

            var filter = new MailSieveFilterData
            {
                Conditions = new List<MailSieveFilterConditionData>
                {
                    new MailSieveFilterConditionData
                    {
                        Key = ConditionKeyType.To,
                        Operation = ConditionOperationType.Matches,
                        Value = "to@to.com"
                    }
                }
            };

            var messages = messageEngine.GetFilteredMessages(filter, PAGE, LIMIT, out long total);

            Assert.AreEqual(1, total);
            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual("to@to.com", messages[0].To);
        }

        [Test]
        [Order(4)]
        public void CheckSimpleFilterToContains()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailMail>(true, scope.ServiceProvider))
                return;

            var testEngine = scope.ServiceProvider.GetService<TestEngine>();
            var messageEngine = scope.ServiceProvider.GetService<MessageEngine>();

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

            var filter = new MailSieveFilterData
            {
                Conditions = new List<MailSieveFilterConditionData>
                {
                    new MailSieveFilterConditionData
                    {
                        Key = ConditionKeyType.To,
                        Operation = ConditionOperationType.Contains,
                        Value = "@to.com"
                    }
                }
            };

            var messages = messageEngine.GetFilteredMessages(filter, PAGE, LIMIT, out long total);

            Assert.AreEqual(1, total);
            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual("to@to.com", messages[0].To);
        }

        [Test]
        [Order(5)]
        public void CheckSimpleFilterCcMatch()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailMail>(true, scope.ServiceProvider))
                return;

            var testEngine = scope.ServiceProvider.GetService<TestEngine>();
            var messageEngine = scope.ServiceProvider.GetService<MessageEngine>();

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

            var filter = new MailSieveFilterData
            {
                Conditions = new List<MailSieveFilterConditionData>
                {
                    new MailSieveFilterConditionData
                    {
                        Key = ConditionKeyType.Cc,
                        Operation = ConditionOperationType.Matches,
                        Value = "cc@cc.com"
                    }
                }
            };

            var messages = messageEngine.GetFilteredMessages(filter, PAGE, LIMIT, out long total);

            Assert.AreEqual(1, total);
            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual("cc@cc.com", messages[0].Cc);
        }

        [Test]
        [Order(6)]
        public void CheckSimpleFilterCcContains()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailMail>(true, scope.ServiceProvider))
                return;

            var testEngine = scope.ServiceProvider.GetService<TestEngine>();
            var messageEngine = scope.ServiceProvider.GetService<MessageEngine>();

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

            var filter = new MailSieveFilterData
            {
                Conditions = new List<MailSieveFilterConditionData>
                {
                    new MailSieveFilterConditionData
                    {
                        Key = ConditionKeyType.Cc,
                        Operation = ConditionOperationType.Contains,
                        Value = "cc"
                    }
                }
            };

            var messages = messageEngine.GetFilteredMessages(filter, PAGE, LIMIT, out long total);

            Assert.AreEqual(1, total);
            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual("cc@cc.com", messages[0].Cc);
        }

        [Test]
        [Order(7)]
        public void CheckSimpleFilterToOrCcMatch()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailMail>(true, scope.ServiceProvider))
                return;

            var testEngine = scope.ServiceProvider.GetService<TestEngine>();
            var messageEngine = scope.ServiceProvider.GetService<MessageEngine>();

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
        }

        [Test]
        [Order(8)]
        public void CheckSimpleFilterToOrCcContains()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailMail>(true, scope.ServiceProvider))
                return;

            var testEngine = scope.ServiceProvider.GetService<TestEngine>();
            var messageEngine = scope.ServiceProvider.GetService<MessageEngine>();

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

            var filter = new MailSieveFilterData
            {
                Conditions = new List<MailSieveFilterConditionData>
                {
                    new MailSieveFilterConditionData
                    {
                        Key = ConditionKeyType.ToOrCc,
                        Operation = ConditionOperationType.Contains,
                        Value = "to"
                    }
                }
            };

            var messages = messageEngine.GetFilteredMessages(filter, PAGE, LIMIT, out long total);

            Assert.AreEqual(1, total);
            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual("to@to.com", messages[0].To);
            Assert.AreEqual("cc@cc.com", messages[0].Cc);
        }

        [Test]
        [Order(8)]
        public void CheckSimpleFilterSubjectMatch()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailMail>(true, scope.ServiceProvider))
                return;

            var testEngine = scope.ServiceProvider.GetService<TestEngine>();
            var messageEngine = scope.ServiceProvider.GetService<MessageEngine>();

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

            var filter = new MailSieveFilterData
            {
                Conditions = new List<MailSieveFilterConditionData>
                {
                    new MailSieveFilterConditionData
                    {
                        Key = ConditionKeyType.Subject,
                        Operation = ConditionOperationType.Matches,
                        Value = "[TEST SUBJECT]"
                    }
                }
            };

            var messages = messageEngine.GetFilteredMessages(filter, PAGE, LIMIT, out long total);

            Assert.AreEqual(1, total);
            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual("[TEST SUBJECT]", messages[0].Subject);
        }

        [Test]
        [Order(10)]
        public void CheckSimpleFilterSubjectContains()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailMail>(true, scope.ServiceProvider))
                return;

            var testEngine = scope.ServiceProvider.GetService<TestEngine>();
            var messageEngine = scope.ServiceProvider.GetService<MessageEngine>();

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

            var filter = new MailSieveFilterData
            {
                Conditions = new List<MailSieveFilterConditionData>
                {
                    new MailSieveFilterConditionData
                    {
                        Key = ConditionKeyType.Subject,
                        Operation = ConditionOperationType.Contains,
                        Value = "SUBJECT"
                    }
                }
            };

            var messages = messageEngine.GetFilteredMessages(filter, PAGE, LIMIT, out long total);

            Assert.AreEqual(1, total);
            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual("[TEST SUBJECT]", messages[0].Subject);
        }

        [Test]
        [Order(11)]
        public void CheckComplexFilterFromAndSubjectMatchAll()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailMail>(true, scope.ServiceProvider))
                return;

            var testEngine = scope.ServiceProvider.GetService<TestEngine>();
            var messageEngine = scope.ServiceProvider.GetService<MessageEngine>();

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
                        Value = "[TEST SUBJECT]"
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
            Assert.AreEqual(TestMailbox.EMail.Address, messages[0].From);
            Assert.AreEqual("[TEST SUBJECT]", messages[0].Subject);
        }

        [Test]
        [Order(12)]
        public void CheckComplexFilterFromAndSubjectMatchAtLeastOne()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailMail>(true, scope.ServiceProvider))
                return;

            var testEngine = scope.ServiceProvider.GetService<TestEngine>();
            var messageEngine = scope.ServiceProvider.GetService<MessageEngine>();

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
                        Value = "[TEST SUBJECT1]"
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
            Assert.AreEqual(TestMailbox.EMail.Address, messages[0].From);
            Assert.AreEqual("[TEST SUBJECT]", messages[0].Subject);
        }
    }
}
