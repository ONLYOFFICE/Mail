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


        public static void AddMailScoppedServices(this DIHelper helper)
        {
            helper.TryAdd<AuthManager>();
            helper.TryAdd<BaseCommonLinkUtility>();
            helper.TryAdd<ASC.Core.SecurityContext>();
            helper.TryAdd<TenantManager>();
            helper.TryAdd<UserManager>();
            helper.TryAdd<IAccountDao, AccountDao>();
            helper.TryAdd<IAlertDao, AlertDao>();
            helper.TryAdd<IAttachmentDao, AttachmentDao>();
            helper.TryAdd<IChainDao, ChainDao>();
            helper.TryAdd<IContactCardDao, ContactCardDao>();
            helper.TryAdd<IContactDao, ContactDao>();
            helper.TryAdd<IContactInfoDao, ContactInfoDao>();
            helper.TryAdd<ICrmContactDao, CrmContactDao>();
            helper.TryAdd<ICrmLinkDao, CrmLinkDao>();
            helper.TryAdd<IDisplayImagesAddressDao, DisplayImagesAddressDao>();
            helper.TryAdd<IFilterDao, FilterDao>();
            helper.TryAdd<IFolderDao, FolderDao>();
            helper.TryAdd<IImapFlagsDao, ImapFlagsDao>();
            helper.TryAdd<IImapSpecialMailboxDao, ImapSpecialMailboxDao>();
            helper.TryAdd<IMailboxAutoreplyDao, MailboxAutoreplyDao>();
            helper.TryAdd<IMailboxAutoreplyHistoryDao, MailboxAutoreplyHistoryDao>();
            helper.TryAdd<IMailboxDao, ASC.Mail.Core.Dao.MailboxDao>();
            helper.TryAdd<IMailDaoFactory, MailDaoFactory>();
            helper.TryAdd<IMailboxDomainDao, MailboxDomainDao>();
            helper.TryAdd<IMailboxProviderDao, MailboxProviderDao>();
            helper.TryAdd<IMailboxServerDao, MailboxServerDao>();
            helper.TryAdd<IMailboxSignatureDao, MailboxSignatureDao>();
            helper.TryAdd<IMailDao, MailDao>();
            helper.TryAdd<IMailGarbageDao, MailGarbageDao>();
            helper.TryAdd<IMailInfoDao, MailInfoDao>();
            helper.TryAdd<IServerAddressDao, ServerAddressDao>();
            helper.TryAdd<IServerDao, ServerDao>();
            helper.TryAdd<IServerDnsDao, ServerDnsDao>();
            helper.TryAdd<IServerDomainDao, ServerDomainDao>();
            helper.TryAdd<IServerGroupDao, ServerGroupDao>();
            helper.TryAdd<ITagAddressDao, TagAddressDao>();
            helper.TryAdd<ITagDao, TagDao>();
            helper.TryAdd<ITagMailDao, TagMailDao>();
            helper.TryAdd<IUserFolderDao, UserFolderDao>();
            helper.TryAdd<IUserFolderTreeDao, UserFolderTreeDao>();
            helper.TryAdd<IUserFolderXMailDao, UserFolderXMailDao>();
        }
    }


}
