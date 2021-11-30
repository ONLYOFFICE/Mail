using ASC.Common;
using ASC.Common.Caching;
using ASC.Core.Users;
using ASC.Mail.Core.Search;

using Autofac;
using Autofac.Extensions.DependencyInjection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using System;
using System.Collections.Generic;
using System.IO;

namespace ASC.Mail.Tests
{
    public class BaseMailTests
    {
        protected UserInfo TestUser { get; set; }
        protected IServiceProvider ServiceProvider { get; set; }
        protected IHost TestHost { get; set; }
        protected IServiceScope serviceScope { get; set; }

        public virtual void Prepare()
        {
            var args = new string[] { };

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
                        .AddEnvironmentVariables();
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHttpContextAccessor();
                    services.AddMemoryCache();

                    var diHelper = new DIHelper(services);

                    diHelper.TryAdd<MailTestsScope>();
                    diHelper.TryAdd<FactoryIndexerMailMail>();
                    diHelper.TryAdd(typeof(ICacheNotify<>), typeof(KafkaCache<>));

                    var builder = new ContainerBuilder();
                    var container = builder.Build();

                    services.TryAddSingleton(container);

                    //diHelper.RegisterProducts(hostContext.Configuration, hostContext.HostingEnvironment.ContentRootPath);
                })
                //.UseConsoleLifetime()
                .Build();

            serviceScope = TestHost.Services.CreateScope();
            TestHost.Start();

            ServiceProvider = TestHost.Services;
        }
    }
}
