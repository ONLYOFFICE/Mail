using ASC.Common;
using ASC.Common.Caching;
using ASC.Common.Mapping;
using ASC.Core;
using ASC.Core.Common.EF;
using ASC.Core.Common.EF.Context;
using ASC.Core.Tenants;
using ASC.Core.Users;
using ASC.Mail.Aggregator.Tests.Common.Utils;
using ASC.Mail.Core.Engine;
using ASC.Mail.Core.Search;
using ASC.Mail.Models;
using ASC.Mail.Utils;

using Autofac.Extensions.DependencyInjection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ASC.Mail.Tests
{
    [SetUpFixture]
    public class MySetUpClass
    {
        protected IServiceScope Scope { get; set; }

        [OneTimeSetUp]
        public void CreateDb()
        {
            var args = new string[] {
                "--pathToConf", Path.Combine("..", "..", "..", "..", "config"),
                "--ConnectionStrings:default:connectionString", BaseMailTests.TestConnection,
                "--migration:enabled", "true" };

            var host = Host.CreateDefaultBuilder(args)
                .UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .ConfigureAppConfiguration((hostContext, config) =>
                {
                    var buided = config.Build();

                    var path = buided["pathToConf"];

                    if (!Path.IsPathRooted(path))
                    {
                        path = Path.GetFullPath(Path.Combine(hostContext.HostingEnvironment.ContentRootPath, path));
                    }

                    config.SetBasePath(path);

                    var env = hostContext.Configuration.GetValue("ENVIRONMENT", "Production");
                    var env2 = hostContext.HostingEnvironment.EnvironmentName;
                    config
                        .AddInMemoryCollection(new Dictionary<string, string>
                        {
                            {"pathToConf", path}
                        })
                        .AddJsonFile("appsettings.json")
                        .AddJsonFile($"appsettings.{env}.json", true)
                        .AddJsonFile("storage.json")
                        .AddJsonFile("kafka.json")
                        .AddJsonFile($"kafka.{env}.json", true)
                        .AddJsonFile("mail.json")
                        .AddCommandLine(args)
                        .AddEnvironmentVariables();
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHttpContextAccessor();
                    services.AddMemoryCache();

                    var diHelper = new DIHelper(services);

                    diHelper.TryAdd<MailTestsScope>();
                    diHelper.TryAdd<FactoryIndexerMailMail>();
                    diHelper.TryAdd<FactoryIndexerMailContact>();
                    diHelper.TryAdd(typeof(ICacheNotify<>), typeof(KafkaCache<>));
                    services.AddAutoMapper(Assembly.GetAssembly(typeof(MappingProfile)));
                })
                .Build();

            Scope = host.Services.CreateScope();
            var configuration = Scope.ServiceProvider.GetService<IConfiguration>();

            Migrate(host.Services);
            Migrate(host.Services, Assembly.GetExecutingAssembly().GetName().Name);


        }

        [OneTimeTearDown]
        public void DropDb()
        {
            var context = Scope.ServiceProvider.GetService<DbContextManager<TenantDbContext>>();
            context.Value.Database.EnsureDeleted();
        }

        private void Migrate(IServiceProvider serviceProvider, string testAssembly = null)
        {
            using var scope = serviceProvider.CreateScope();
            var c = scope.ServiceProvider.GetService<IConfiguration>();

            if (!string.IsNullOrEmpty(testAssembly))
            {
                var configuration = scope.ServiceProvider.GetService<IConfiguration>();
                configuration["testAssembly"] = testAssembly;
            }

            using var db = scope.ServiceProvider.GetService<DbContextManager<UserDbContext>>();
            db.Value.Migrate();
        }
    }

    public class BaseMailTests
    {
        protected IServiceProvider ServiceProvider { get; set; }
        protected IHost TestHost { get; set; }
        protected IServiceScope serviceScope { get; set; }
        protected Tenant CurrentTenant { get; set; }
        protected SecurityContext SecurityContext { get; set; }
        protected UserManager UserManager { get; set; }

        protected MailBoxData TestMailbox { get; set; }
        protected UserInfo TestUser { get; set; }

        protected const int CURRENT_TENANT = 1;
        public const string PASSWORD = "123456";
        public const string DOMAIN = "gmail.com";

        protected static readonly string TestFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
           @"..\..\..\Data\");

        public const string TestConnection = "Server=localhost;Database=onlyoffice_test;User ID=root;Password=root;Pooling=true;Character Set=utf8;AutoEnlist=false;SSL Mode=none;AllowPublicKeyRetrieval=True";

        public virtual void Prepare()
        {
            var args = new string[] {
                "--pathToConf" , Path.Combine("..", "..","..", "..", "config"),
                "--ConnectionStrings:default:connectionString", TestConnection,
                 "--migration:enabled", "true" };

            TestHost = Host.CreateDefaultBuilder(args)
                .UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .ConfigureAppConfiguration((hostContext, config) =>
                {
                    var buided = config.Build();

                    var path = buided["pathToConf"];

                    if (!Path.IsPathRooted(path))
                    {
                        path = Path.GetFullPath(Path.Combine(hostContext.HostingEnvironment.ContentRootPath, path));
                    }

                    config.SetBasePath(path);

                    var env = hostContext.Configuration.GetValue("ENVIRONMENT", "Production");

                    config
                        .AddInMemoryCollection(new Dictionary<string, string>
                        {
                            {"pathToConf", path}
                        })
                        .AddJsonFile("appsettings.json")
                        .AddJsonFile($"appsettings.{env}.json", true)
                        .AddJsonFile("storage.json")
                        .AddJsonFile("kafka.json")
                        .AddJsonFile($"kafka.{env}.json", true)
                        .AddJsonFile("mail.json")
                        .AddCommandLine(args)
                        .AddEnvironmentVariables();
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHttpContextAccessor();
                    services.AddMemoryCache();

                    var diHelper = new DIHelper(services);

                    diHelper.TryAdd<MailTestsScope>();
                    diHelper.TryAdd<FactoryIndexerMailMail>();
                    diHelper.TryAdd<FactoryIndexerMailContact>();
                    diHelper.TryAdd(typeof(ICacheNotify<>), typeof(KafkaCache<>));
                    services.AddAutoMapper(Assembly.GetAssembly(typeof(MappingProfile)));
                })
                .Build();

            serviceScope = TestHost.Services.CreateScope();

            var tenantManager = serviceScope.ServiceProvider.GetService<TenantManager>();
            var tenant = tenantManager.GetTenant(CURRENT_TENANT);

            var mailBoxSettingEngine = serviceScope.ServiceProvider.GetService<MailBoxSettingEngine>();
            var mailboxEngine = serviceScope.ServiceProvider.GetService<MailboxEngine>();

            tenantManager.SetCurrentTenant(tenant);
            CurrentTenant = tenant;

            UserManager = serviceScope.ServiceProvider.GetService<UserManager>();

            SecurityContext = serviceScope.ServiceProvider.GetService<SecurityContext>();
            SecurityContext.AuthenticateMe(CurrentTenant.OwnerId);
            TestHost.Start();

            TestUser = UserManager.GetUsers(Guid.Parse("66faa6e4-f133-11ea-b126-00ffeec8b4ef"));
            TestUser.Email = TestHelper.GetTestEmailAddress(DOMAIN);

            var mailboxSettings = mailBoxSettingEngine.GetMailBoxSettings(DOMAIN);
            var testMailboxes = mailboxSettings.ToMailboxList(TestUser.Email, PASSWORD, CURRENT_TENANT, TestUser.ID.ToString());

            TestMailbox = testMailboxes.FirstOrDefault();

            if (TestMailbox == null || !mailboxEngine.SaveMailBox(TestMailbox))
            {
                throw new Exception(string.Format("Can't create mailbox with email: {0}", TestUser.Email));
            }

            ServiceProvider = TestHost.Services;
        }
    }
}
