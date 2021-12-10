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
    internal class FilterEngineTests : BaseMailTests
    {
        private const int CURRENT_TENANT = 1;
        public const string PASSWORD = "123456";
        public const string DOMAIN = "gmail.com";

        public MailBoxData TestMailbox { get; set; }
        public UserInfo TestUser { get; set; }
        public int MailId { get; set; }

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

            var t = scope.ServiceProvider.GetService<MailWrapper>();
            if (factoryIndexerHelper.Support(t))
                factoryIndexer.DeleteAsync(s => s.Where(m => m.UserId, TestUser.ID)).Wait();

            // Clear TestUser mail data
            var mailGarbageEngine = scope.ServiceProvider.GetService<MailGarbageEngine>();
            mailGarbageEngine.ClearUserMail(TestUser.ID, tenantManager.GetCurrentTenant());
        }*/

        [Test]
        [Order(1)]
        public void CreateBaseFilterTest()
        {
            using var scope = ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            var filterEngine = scope.ServiceProvider.GetService<FilterEngine>();

            CreateBaseFilter(filterEngine);
        }

        [Test]
        [Order(2)]
        public void CreateFullFilterTest()
        {
            using var scope = ServiceProvider.CreateScope();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            var filterEngine = scope.ServiceProvider.GetService<FilterEngine>();

            var filter = new MailSieveFilterData
            {
                Conditions = new List<MailSieveFilterConditionData>
                {
                    new MailSieveFilterConditionData
                    {
                        Key = ConditionKeyType.From,
                        Operation = ConditionOperationType.Contains,
                        Value = "support@example.com"
                    },
                    new MailSieveFilterConditionData
                    {
                        Key = ConditionKeyType.ToOrCc,
                        Operation = ConditionOperationType.NotContains,
                        Value = "toOrcc@example.com"
                    },
                    new MailSieveFilterConditionData
                    {
                        Key = ConditionKeyType.To,
                        Operation = ConditionOperationType.Matches,
                        Value = "to@example.com"
                    },
                    new MailSieveFilterConditionData
                    {
                        Key = ConditionKeyType.Cc,
                        Operation = ConditionOperationType.NotMatches,
                        Value = "cc@example.com"
                    },
                    new MailSieveFilterConditionData
                    {
                        Key = ConditionKeyType.Subject,
                        Operation = ConditionOperationType.Contains,
                        Value = "[TEST]"
                    }
                },
                Actions = new List<MailSieveFilterActionData>
                {
                    new MailSieveFilterActionData
                    {
                        Action = ActionType.MarkAsRead
                    },
                    new MailSieveFilterActionData
                    {
                        Action = ActionType.DeleteForever
                    },
                    new MailSieveFilterActionData
                    {
                        Action = ActionType.MoveTo,
                        Data = "5" // Spam default folder id
                    },
                    new MailSieveFilterActionData
                    {
                        Action = ActionType.MarkTag,
                        Data = "111" // Fake tag Id
                    }
                },
                Options = new MailSieveFilterOptionsData
                {
                    MatchMultiConditions = MatchMultiConditionsType.MatchAtLeastOne,
                    ApplyTo = new MailSieveFilterOptionsApplyToData
                    {
                        WithAttachments = ApplyToAttachmentsType.WithAndWithoutAttachments
                    }
                }
            };

            var id = filterEngine.Create(filter);

            Assert.Greater(id, 0);
        }

        [Test]
        [Order(3)]
        public void GetFilterTest()
        {
            using var scope = ServiceProvider.CreateScope();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            var filterEngine = scope.ServiceProvider.GetService<FilterEngine>();

            var id = CreateBaseFilter(filterEngine);

            var rFilter = filterEngine.Get(id);

            Assert.IsNotNull(rFilter);

            Assert.AreEqual(id, rFilter.Id);
        }

        [Test]
        [Order(4)]
        public void GetFiltersTest()
        {
            using var scope = ServiceProvider.CreateScope();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            var filterEngine = scope.ServiceProvider.GetService<FilterEngine>();

            CreateBaseFilter(filterEngine);
            CreateBaseFilter(filterEngine);

            var rFilters = filterEngine.GetList();

            Assert.AreEqual(rFilters.Count, 2);
        }

        [Test]
        [Order(5)]
        public void UpdateFilterTest()
        {
            using var scope = ServiceProvider.CreateScope();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            var filterEngine = scope.ServiceProvider.GetService<FilterEngine>();

            var id = CreateBaseFilter(filterEngine);

            var rFilter = filterEngine.Get(id);

            Assert.IsNotNull(rFilter);

            Assert.AreEqual(id, rFilter.Id);

            Assert.AreEqual(rFilter.Conditions.First().Key, ConditionKeyType.From);
            Assert.AreEqual(rFilter.Actions.First().Action, ActionType.MarkAsRead);

            rFilter.Conditions.First().Key = ConditionKeyType.To;
            rFilter.Actions.First().Action = ActionType.DeleteForever;

            filterEngine.Update(rFilter);

            rFilter = filterEngine.Get(id);

            Assert.AreEqual(rFilter.Conditions.First().Key, ConditionKeyType.To);
            Assert.AreEqual(rFilter.Actions.First().Action, ActionType.DeleteForever);
        }

        [Test]
        [Order(6)]
        public void DeleteFilterTest()
        {
            using var scope = ServiceProvider.CreateScope();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            var filterEngine = scope.ServiceProvider.GetService<FilterEngine>();

            var id = CreateBaseFilter(filterEngine);

            var success = filterEngine.Delete(id);

            Assert.AreEqual(true, success);

            var rFilter = filterEngine.Get(id);

            Assert.AreEqual(null, rFilter);
        }

        [Test]
        [Order(7)]
        public void CreateDisabledFilterTest()
        {
            using var scope = ServiceProvider.CreateScope();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            var filterEngine = scope.ServiceProvider.GetService<FilterEngine>();

            var id = CreateBaseFilter(filterEngine, false);

            var rFilter = filterEngine.Get(id);

            Assert.AreEqual(false, rFilter.Enabled);
        }

        [Test]
        [Order(8)]
        public void EnabledFilterTest()
        {
            using var scope = ServiceProvider.CreateScope();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            var filterEngine = scope.ServiceProvider.GetService<FilterEngine>();

            var id = CreateBaseFilter(filterEngine, false);

            var rFilter = filterEngine.Get(id);

            rFilter.Enabled = true;

            var success = filterEngine.Update(rFilter);

            Assert.AreEqual(true, success);

            rFilter = filterEngine.Get(rFilter.Id);

            Assert.AreEqual(true, rFilter.Enabled);
        }

        private static int CreateBaseFilter(FilterEngine engine, bool enabled = true)
        {
            var filter = new MailSieveFilterData
            {
                Enabled = enabled,

                Conditions = new List<MailSieveFilterConditionData>
                {
                    new MailSieveFilterConditionData
                    {
                        Key = ConditionKeyType.From,
                        Operation = ConditionOperationType.Contains,
                        Value = "support@example.com"
                    }
                },
                Actions = new List<MailSieveFilterActionData>
                {
                    new MailSieveFilterActionData
                    {
                        Action = ActionType.MarkAsRead
                    }
                }
            };

            var id = engine.Create(filter);

            Assert.Greater(id, 0);

            return id;
        }
    }
}
