using ASC.Common.Logging;
using ASC.Core.Common.EF.Context;
using ASC.MessagingSystem.EF.Context;
using Microsoft.AspNetCore.Hosting;
using System.Runtime.InteropServices;
using IMailboxDao = ASC.Mail.Core.Dao.Interfaces.IMailboxDao;

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

            services.AddMailScoppedServices();
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


        public static void AddMailScoppedServices(this IServiceCollection services)
        {
            services.AddScoped<IAccountDao, AccountDao>();
            services.AddScoped<IAlertDao, AlertDao>();
            services.AddScoped<IAttachmentDao, AttachmentDao>();
            services.AddScoped<IChainDao, ChainDao>();
            services.AddScoped<IContactCardDao, ContactCardDao>();
            services.AddScoped<IContactDao, ContactDao>();
            services.AddScoped<IContactInfoDao, ContactInfoDao>();
            services.AddScoped<ICrmContactDao, CrmContactDao>();
            services.AddScoped<ICrmLinkDao, CrmLinkDao>();
            services.AddScoped<IDisplayImagesAddressDao, DisplayImagesAddressDao>();
            services.AddScoped<IFilterDao, FilterDao>();
            services.AddScoped<IFolderDao, FolderDao>();
            services.AddScoped<IImapFlagsDao, ImapFlagsDao>();
            services.AddScoped<IImapSpecialMailboxDao, ImapSpecialMailboxDao>();
            services.AddScoped<IMailboxAutoreplyDao, MailboxAutoreplyDao>();
            services.AddScoped<IMailboxAutoreplyHistoryDao, MailboxAutoreplyHistoryDao>();
            services.AddScoped<IMailboxDao, ASC.Mail.Core.Dao.MailboxDao>();
            services.AddScoped<IMailDaoFactory, MailDaoFactory>();
            services.AddScoped<IMailboxDomainDao, MailboxDomainDao>();
            services.AddScoped<IMailboxProviderDao, MailboxProviderDao>();
            services.AddScoped<IMailboxServerDao, MailboxServerDao>();
            services.AddScoped<IMailboxSignatureDao, MailboxSignatureDao>();
            services.AddScoped<IMailDao, MailDao>();
            services.AddScoped<IMailGarbageDao, MailGarbageDao>();
            services.AddScoped<IMailInfoDao, MailInfoDao>();
            services.AddScoped<IServerAddressDao, ServerAddressDao>();
            services.AddScoped<IServerDao, ServerDao>();
            services.AddScoped<IServerDnsDao, ServerDnsDao>();
            services.AddScoped<IServerDomainDao, ServerDomainDao>();
            services.AddScoped<IServerGroupDao, ServerGroupDao>();
            services.AddScoped<ITagAddressDao, TagAddressDao>();
            services.AddScoped<ITagDao, TagDao>();
            services.AddScoped<ITagMailDao, TagMailDao>();
            services.AddScoped<IUserFolderDao, UserFolderDao>();
            services.AddScoped<IUserFolderTreeDao, UserFolderTreeDao>();
            services.AddScoped<IUserFolderXMailDao, UserFolderXMailDao>();
        }
    }


}
