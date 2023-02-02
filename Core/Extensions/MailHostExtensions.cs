using ASC.Common.Logging;
using ASC.Core.Common.EF.Context;
using ASC.MessagingSystem.EF.Context;
using Microsoft.AspNetCore.Hosting;
using System.Runtime.InteropServices;

namespace ASC.Mail.Core.Extensions
{
    public static class MailHostExtensions
    {
        public static Microsoft.Extensions.Configuration.ConfigurationManager AddMailJsonFiles(
            this Microsoft.Extensions.Configuration.ConfigurationManager configurationManager,
            string env)
        {

            configurationManager
                    .AddJsonFile("appsettings.json")
                    .AddJsonFile($"appsettings.{env}.json", true)
                    .AddJsonFile("storage.json")
                    .AddJsonFile($"storage.{env}.json", true)
                    .AddJsonFile("mail.json")
                    .AddJsonFile($"mail.{env}.json", true)
                    .AddJsonFile("elastic.json")
                    .AddJsonFile($"elastic.{env}.json", true);


            return configurationManager;
        }

        public static void AddMailServices(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            services.AddScoped<EFLoggerFactory>();
            services.AddBaseDbContextPool<CoreDbContext>();
            services.AddBaseDbContextPool<TenantDbContext>();
            services.AddBaseDbContextPool<UserDbContext>();
            services.AddBaseDbContextPool<CustomDbContext>();
            services.AddBaseDbContextPool<WebstudioDbContext>();
            services.AddBaseDbContextPool<MessagesContext>();

            services.AddAutoMapper(GetAutoMapperProfileAssemblies());
        }

        public static void MailConfigureKestrel(this IWebHostBuilder builder)
        {
            builder.ConfigureKestrel((hostingContext, serverOptions) =>
            {
                var kestrelConfig = hostingContext.Configuration.GetSection("Kestrel");

                if (!kestrelConfig.Exists()) return;

                var unixSocket = kestrelConfig.GetValue<string>("ListenUnixSocket");

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    if (!string.IsNullOrWhiteSpace(unixSocket))
                    {
                        unixSocket = string.Format(unixSocket, hostingContext.HostingEnvironment.ApplicationName.Replace("ASC.", "").Replace(".", ""));

                        serverOptions.ListenUnixSocket(unixSocket);
                    }
                }
            });
        }

        public static IEnumerable<Assembly> GetAutoMapperProfileAssemblies()
        {
            return from x in AppDomain.CurrentDomain.GetAssemblies()
                   where x.GetName().Name!.StartsWith("ASC.")
                   select x;
        }
    }


}
