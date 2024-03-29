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
using ASC.ElasticSearch;
using ASC.Mail.Aggregator.Tests.Common.Utils;
using ASC.Mail.Core.Dao.Entities;
using ASC.Mail.Core.Engine;
using ASC.Mail.Enums;

using Microsoft.Extensions.DependencyInjection;

using NUnit.Framework;

using System;
using System.Collections.Generic;

namespace ASC.Mail.Tests
{
    [TestFixture]
    internal class ContactSearchFilteringTests : BaseMailTests
    {
        private const int CURRENT_TENANT = 1;

        private const int CONTACT_ID_1 = 777;
        private const int CONTACT_ID_2 = 778;
        private const int CONTACT_ID_3 = 779;

        public UserInfo TestUser { get; set; }

        [OneTimeSetUp]
        public override void Prepare()
        {
            base.Prepare();

            //using var scope = ServiceProvider.CreateScope();

            var tenantManager = serviceScope.ServiceProvider.GetService<TenantManager>();
            var securityContext = serviceScope.ServiceProvider.GetService<SecurityContext>();

            var tenant = tenantManager.GetTenant(CURRENT_TENANT);
            tenantManager.SetCurrentTenant(tenant);

            securityContext.AuthenticateMe(ASC.Core.Configuration.Constants.CoreSystem);


            TestUser = UserManager.GetUsers(Guid.Parse("66faa6e4-f133-11ea-b126-00ffeec8b4ef"));
        }

        //[SetUp]
        //public void SetUp()
        //{
        //    using var scope = ServiceProvider.CreateScope();
        //    var userManager = scope.ServiceProvider.GetService<UserManager>();
        //    var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
        //    var securityContext = scope.ServiceProvider.GetService<SecurityContext>();
        //    var apiHelper = scope.ServiceProvider.GetService<ApiHelper>();

        //    tenantManager.SetCurrentTenant(CURRENT_TENANT);
        //    securityContext.AuthenticateMe(ASC.Core.Configuration.Constants.CoreSystem);

        //    var testEngine = scope.ServiceProvider.GetService<TestEngine>();

        //    TestUser = UserManager.GetUsers(Guid.Parse("66faa6e4-f133-11ea-b126-00ffeec8b4ef"));
        //}

        /*[TearDown]
        public void CleanUp()
        {
            using var scope = ServiceProvider.CreateScope();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            var indexEngine = scope.ServiceProvider.GetService<IndexEngine>();
            indexEngine.RemoveContacts(new List<int> { CONTACT_ID_1, CONTACT_ID_2, CONTACT_ID_3 }, CURRENT_TENANT, TestUser.ID);

            securityContext.AuthenticateMe(ASC.Core.Configuration.Constants.CoreSystem);

            // Remove TestUser profile
            var userManager = scope.ServiceProvider.GetService<UserManager>();
            userManager.DeleteUser(TestUser.ID);
        }*/

        [Test]
        [Order(1)]
        public void CheckContactSearchEmailLocalPartMatch()
        {
            using var scope = ServiceProvider.CreateScope();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailContact>(false, scope.ServiceProvider))
                return;

            var indexEngine = scope.ServiceProvider.GetService<IndexEngine>();
            var factoryIndexer = scope.ServiceProvider.GetService<FactoryIndexer<MailContact>>();

            var now = DateTime.UtcNow;

            var contact = new MailContact
            {
                Id = CONTACT_ID_1,
                TenantId = CURRENT_TENANT,
                IdUser = TestUser.ID.ToString(),
                Name = "Test Contact Name",
                Type = (int)ContactType.Personal,
                Description = "Some test description",
                InfoList = new List<MailContactInfo>
                {
                    new MailContactInfo
                    {
                        Id = 776,
                        TenantId = CURRENT_TENANT,
                        IdUser = TestUser.ID.ToString(),
                        IdContact = CONTACT_ID_1,
                        Type = (int)ContactInfoType.Email,
                        Data = "qqq@test.ru",
                        IsPrimary = true,
                        LastModified = now
                    }
                },
                LastModified = now
            };

            indexEngine.Add(contact);

            var term = "qqq";

            var selector = new Selector<MailContact>(ServiceProvider)
                        .MatchAll(term)
                        .Where(s => s.IdUser, TestUser.ID.ToString());


            var success = factoryIndexer.TrySelectIds(s => selector, out List<int> ids);

            Assert.AreEqual(true, success);
            Assert.IsNotEmpty(ids);
            Assert.AreEqual(1, ids.Count);
            Assert.Contains(CONTACT_ID_1, ids);
        }

        [Test]
        [Order(2)]
        public void CheckContactSearchEmailDomainMatch()
        {
            using var scope = ServiceProvider.CreateScope();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailContact>(false, scope.ServiceProvider))
                return;

            var indexEngine = scope.ServiceProvider.GetService<IndexEngine>();
            var factoryIndexer = scope.ServiceProvider.GetService<FactoryIndexer<MailContact>>();

            var now = DateTime.UtcNow;

            var contact = new MailContact
            {
                Id = CONTACT_ID_1,
                TenantId = CURRENT_TENANT,
                IdUser = TestUser.ID.ToString(),
                Name = "Test Contact Name",
                Type = (int)ContactType.Personal,
                Description = "Some test description",
                InfoList = new List<MailContactInfo>
                {
                    new MailContactInfo
                    {
                        Id = 776,
                        TenantId = CURRENT_TENANT,
                        IdUser = TestUser.ID.ToString(),
                        IdContact = CONTACT_ID_1,
                        Type = (int)ContactInfoType.Email,
                        Data = "qqq@test.ru",
                        IsPrimary = true,
                        LastModified = now
                    }
                },
                LastModified = now
            };

            indexEngine.Add(contact);

            var term = "test.ru";

            var selector = new Selector<MailContact>(ServiceProvider)
                        .MatchAll(term)
                        .Where(s => s.IdUser, TestUser.ID.ToString());

            var success = factoryIndexer.TrySelectIds(s => selector, out List<int> ids);

            Assert.AreEqual(true, success);
            Assert.IsNotEmpty(ids);
            Assert.AreEqual(1, ids.Count);
            Assert.Contains(CONTACT_ID_1, ids);
        }

        [Test]
        [Order(3)]
        public void CheckContactSearchFullEmailMatch()
        {
            using var scope = ServiceProvider.CreateScope();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailContact>(false, scope.ServiceProvider))
                return;

            var indexEngine = scope.ServiceProvider.GetService<IndexEngine>();
            var factoryIndexer = scope.ServiceProvider.GetService<FactoryIndexer<MailContact>>();

            var now = DateTime.UtcNow;

            var contact = new MailContact
            {
                Id = CONTACT_ID_1,
                TenantId = CURRENT_TENANT,
                IdUser = TestUser.ID.ToString(),
                Name = "Test Contact Name",
                Type = (int)ContactType.Personal,
                Description = "Some test description",
                InfoList = new List<MailContactInfo>
                {
                    new MailContactInfo
                    {
                        Id = 776,
                        TenantId = CURRENT_TENANT,
                        IdUser = TestUser.ID.ToString(),
                        IdContact = CONTACT_ID_1,
                        Type = (int)ContactInfoType.Email,
                        Data = "qqq@test.ru",
                        IsPrimary = true,
                        LastModified = now
                    }
                },
                LastModified = now
            };

            indexEngine.Add(contact);

            var term = "qqq@test.ru";

            var selector = new Selector<MailContact>(ServiceProvider)
                        .MatchAll(term)
                        .Where(s => s.IdUser, TestUser.ID.ToString());

            var success = factoryIndexer.TrySelectIds(s => selector, out List<int> ids);

            Assert.AreEqual(true, success);
            Assert.IsNotEmpty(ids);
            Assert.AreEqual(1, ids.Count);
            Assert.Contains(CONTACT_ID_1, ids);
        }

        [Test]
        [Order(4)]
        public void CheckContactSearchNameMatch()
        {
            using var scope = ServiceProvider.CreateScope();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailContact>(false, scope.ServiceProvider))
                return;

            var indexEngine = scope.ServiceProvider.GetService<IndexEngine>();
            var factoryIndexer = scope.ServiceProvider.GetService<FactoryIndexer<MailContact>>();

            var now = DateTime.UtcNow;

            var contact1 = new MailContact
            {
                Id = CONTACT_ID_1,
                TenantId = CURRENT_TENANT,
                IdUser = TestUser.ID.ToString(),
                Name = "Test Contact Name",
                Type = (int)ContactType.Personal,
                Description = "Some test description",
                InfoList = new List<MailContactInfo>
                {
                    new MailContactInfo
                    {
                        Id = 1,
                        TenantId = CURRENT_TENANT,
                        IdUser = TestUser.ID.ToString(),
                        IdContact = CONTACT_ID_1,
                        Type = (int)ContactInfoType.Email,
                        Data = "qqq@test.ru",
                        IsPrimary = true,
                        LastModified = now
                    }
                },
                LastModified = now
            };

            indexEngine.Add(contact1);

            var contact2 = new MailContact
            {
                Id = CONTACT_ID_2,
                TenantId = CURRENT_TENANT,
                IdUser = TestUser.ID.ToString(),
                Name = "This is SPARTA",
                Type = (int)ContactType.Personal,
                Description = "Some test description",
                InfoList = new List<MailContactInfo>
                {
                    new MailContactInfo
                    {
                        Id = 2,
                        TenantId = CURRENT_TENANT,
                        IdUser = TestUser.ID.ToString(),
                        IdContact = CONTACT_ID_2,
                        Type = (int)ContactInfoType.Email,
                        Data = "qqq2@test.ru",
                        IsPrimary = true,
                        LastModified = now
                    }
                },
                LastModified = now
            };

            indexEngine.Add(contact2);

            var term = "SPARTA";

            var selector = new Selector<MailContact>(ServiceProvider)
                        .MatchAll(term)
                        .Where(s => s.IdUser, TestUser.ID.ToString());

            var success = factoryIndexer.TrySelectIds(s => selector, out List<int> ids);

            Assert.AreEqual(true, success);
            Assert.IsNotEmpty(ids);
            Assert.AreEqual(1, ids.Count);
            Assert.Contains(CONTACT_ID_2, ids);
        }

        [Test]
        [Order(5)]
        public void CheckContactSearchDescriptionMatch()
        {
            using var scope = ServiceProvider.CreateScope();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailContact>(false, scope.ServiceProvider))
                return;

            var indexEngine = scope.ServiceProvider.GetService<IndexEngine>();
            var factoryIndexer = scope.ServiceProvider.GetService<FactoryIndexer<MailContact>>();

            var now = DateTime.UtcNow;

            var contact1 = new MailContact
            {
                Id = CONTACT_ID_1,
                TenantId = CURRENT_TENANT,
                IdUser = TestUser.ID.ToString(),
                Name = "Test Contact Name",
                Type = (int)ContactType.Personal,
                Description = "Some test description",
                InfoList = new List<MailContactInfo>
                {
                    new MailContactInfo
                    {
                        Id = 1,
                        TenantId = CURRENT_TENANT,
                        IdUser = TestUser.ID.ToString(),
                        IdContact = CONTACT_ID_1,
                        Type = (int)ContactInfoType.Email,
                        Data = "qqq@test.ru",
                        IsPrimary = true,
                        LastModified = now
                    }
                },
                LastModified = now
            };

            indexEngine.Add(contact1);

            var contact2 = new MailContact
            {
                Id = CONTACT_ID_2,
                TenantId = CURRENT_TENANT,
                IdUser = TestUser.ID.ToString(),
                Name = "This is SPARTA",
                Type = (int)ContactType.Personal,
                Description = "Some test description",
                InfoList = new List<MailContactInfo>
                {
                    new MailContactInfo
                    {
                        Id = 2,
                        TenantId = CURRENT_TENANT,
                        IdUser = TestUser.ID.ToString(),
                        IdContact = CONTACT_ID_2,
                        Type = (int)ContactInfoType.Email,
                        Data = "qqq2@test.ru",
                        IsPrimary = true,
                        LastModified = now
                    }
                },
                LastModified = now
            };

            indexEngine.Add(contact2);

            var contact3 = new MailContact
            {
                Id = CONTACT_ID_3,
                TenantId = CURRENT_TENANT,
                IdUser = TestUser.ID.ToString(),
                Name = "This is TROY",
                Type = (int)ContactType.Personal,
                Description = "Troy is the best",
                InfoList = new List<MailContactInfo>
                {
                    new MailContactInfo
                    {
                        Id = 2,
                        TenantId = CURRENT_TENANT,
                        IdUser = TestUser.ID.ToString(),
                        IdContact = CONTACT_ID_3,
                        Type = (int)ContactInfoType.Email,
                        Data = "qqq3@test.ru",
                        IsPrimary = true,
                        LastModified = now
                    }
                },
                LastModified = now
            };

            indexEngine.Add(contact3);

            var term = "description";

            var selector = new Selector<MailContact>(ServiceProvider)
                        .MatchAll(term)
                        .Where(s => s.IdUser, TestUser.ID.ToString());

            var success = factoryIndexer.TrySelectIds(s => selector, out List<int> ids);

            Assert.AreEqual(true, success);
            Assert.IsNotEmpty(ids);
            Assert.AreEqual(2, ids.Count);
            Assert.Contains(CONTACT_ID_1, ids);
            Assert.Contains(CONTACT_ID_2, ids);
        }

        [Test]
        [Order(6)]
        public void CheckContactSearchPartPhoneMatch()
        {
            using var scope = ServiceProvider.CreateScope();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailContact>(false, scope.ServiceProvider))
                return;

            var indexEngine = scope.ServiceProvider.GetService<IndexEngine>();
            var factoryIndexer = scope.ServiceProvider.GetService<FactoryIndexer<MailContact>>();

            var now = DateTime.UtcNow;

            var contact1 = new MailContact
            {
                Id = CONTACT_ID_1,
                TenantId = CURRENT_TENANT,
                IdUser = TestUser.ID.ToString(),
                Name = "Test Contact Name",
                Type = (int)ContactType.Personal,
                Description = "Some test description",
                InfoList = new List<MailContactInfo>
                {
                    new MailContactInfo
                    {
                        Id = 1,
                        TenantId = CURRENT_TENANT,
                        IdUser = TestUser.ID.ToString(),
                        IdContact = CONTACT_ID_1,
                        Type = (int)ContactInfoType.Phone,
                        Data = "+7999999997",
                        IsPrimary = true,
                        LastModified = now
                    }
                },
                LastModified = now
            };

            indexEngine.Add(contact1);

            var contact2 = new MailContact
            {
                Id = CONTACT_ID_2,
                TenantId = CURRENT_TENANT,
                IdUser = TestUser.ID.ToString(),
                Name = "This is SPARTA",
                Type = (int)ContactType.Personal,
                Description = "Some test description",
                InfoList = new List<MailContactInfo>
                {
                    new MailContactInfo
                    {
                        Id = 2,
                        TenantId = CURRENT_TENANT,
                        IdUser = TestUser.ID.ToString(),
                        IdContact = CONTACT_ID_2,
                        Type = (int)ContactInfoType.Phone,
                        Data = "+7999999998",
                        IsPrimary = true,
                        LastModified = now
                    }
                },
                LastModified = now
            };

            indexEngine.Add(contact2);

            var contact3 = new MailContact
            {
                Id = CONTACT_ID_3,
                TenantId = CURRENT_TENANT,
                IdUser = TestUser.ID.ToString(),
                Name = "This is TROY",
                Type = (int)ContactType.Personal,
                Description = "Troy is the best",
                InfoList = new List<MailContactInfo>
                {
                    new MailContactInfo
                    {
                        Id = 2,
                        TenantId = CURRENT_TENANT,
                        IdUser = TestUser.ID.ToString(),
                        IdContact = CONTACT_ID_3,
                        Type = (int)ContactInfoType.Phone,
                        Data = "+7999999999",
                        IsPrimary = true,
                        LastModified = now
                    }
                },
                LastModified = now
            };

            indexEngine.Add(contact3);

            var term = "799";

            var selector = new Selector<MailContact>(ServiceProvider)
                        .MatchAll(term)
                        .Where(s => s.IdUser, TestUser.ID.ToString());

            var success = factoryIndexer.TrySelectIds(s => selector, out List<int> ids);

            Assert.AreEqual(true, success);
            Assert.IsNotEmpty(ids);
            Assert.AreEqual(3, ids.Count);
            Assert.Contains(CONTACT_ID_1, ids);
            Assert.Contains(CONTACT_ID_2, ids);
            Assert.Contains(CONTACT_ID_3, ids);
        }

        [Test]
        [Order(7)]
        public void CheckContactSearchFullPhoneMatch()
        {
            using var scope = ServiceProvider.CreateScope();
            var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
            var securityContext = scope.ServiceProvider.GetService<SecurityContext>();

            tenantManager.SetCurrentTenant(CURRENT_TENANT);
            securityContext.AuthenticateMe(TestUser.ID);

            if (!TestHelper.IgnoreIfFullTextSearch<MailContact>(false, scope.ServiceProvider))
                return;

            var indexEngine = scope.ServiceProvider.GetService<IndexEngine>();
            var factoryIndexer = scope.ServiceProvider.GetService<FactoryIndexer<MailContact>>();

            var now = DateTime.UtcNow;

            var contact1 = new MailContact
            {
                Id = CONTACT_ID_1,
                TenantId = CURRENT_TENANT,
                IdUser = TestUser.ID.ToString(),
                Name = "Test Contact Name",
                Type = (int)ContactType.Personal,
                Description = "Some test description",
                InfoList = new List<MailContactInfo>
                {
                    new MailContactInfo
                    {
                        Id = 1,
                        TenantId = CURRENT_TENANT,
                        IdUser = TestUser.ID.ToString(),
                        IdContact = CONTACT_ID_1,
                        Type = (int)ContactInfoType.Phone,
                        Data = "+7999999997",
                        IsPrimary = true,
                        LastModified = now
                    }
                },
                LastModified = now
            };

            indexEngine.Add(contact1);

            var contact2 = new MailContact
            {
                Id = CONTACT_ID_2,
                TenantId = CURRENT_TENANT,
                IdUser = TestUser.ID.ToString(),
                Name = "This is SPARTA",
                Type = (int)ContactType.Personal,
                Description = "Some test description",
                InfoList = new List<MailContactInfo>
                {
                    new MailContactInfo
                    {
                        Id = 2,
                        TenantId = CURRENT_TENANT,
                        IdUser = TestUser.ID.ToString(),
                        IdContact = CONTACT_ID_2,
                        Type = (int)ContactInfoType.Phone,
                        Data = "+7999999998",
                        IsPrimary = true,
                        LastModified = now
                    }
                },
                LastModified = now
            };

            indexEngine.Add(contact2);

            var contact3 = new MailContact
            {
                Id = CONTACT_ID_3,
                TenantId = CURRENT_TENANT,
                IdUser = TestUser.ID.ToString(),
                Name = "This is TROY",
                Type = (int)ContactType.Personal,
                Description = "Troy is the best",
                InfoList = new List<MailContactInfo>
                {
                    new MailContactInfo
                    {
                        Id = 2,
                        TenantId = CURRENT_TENANT,
                        IdUser = TestUser.ID.ToString(),
                        IdContact = CONTACT_ID_3,
                        Type = (int)ContactInfoType.Phone,
                        Data = "+7999999999",
                        IsPrimary = true,
                        LastModified = now
                    }
                },
                LastModified = now
            };

            indexEngine.Add(contact3);

            var term = "+7999999999";

            var selector = new Selector<MailContact>(ServiceProvider)
                        .MatchAll(term)
                        .Where(s => s.IdUser, TestUser.ID.ToString());

            var success = factoryIndexer.TrySelectIds(s => selector, out List<int> ids);

            Assert.AreEqual(true, success);
            Assert.IsNotEmpty(ids);
            Assert.AreEqual(1, ids.Count);
            Assert.Contains(CONTACT_ID_3, ids);
        }
    }
}
