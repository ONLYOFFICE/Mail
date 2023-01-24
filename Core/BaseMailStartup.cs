using ASC.Api.Core.Core;
using ASC.Api.Core.Extensions;
using ASC.Common.Logging;
using ASC.Common.Mapping;
using ASC.Core.Common.EF.Context;
using ASC.Core.Common.Hosting;
using ASC.MessagingSystem.EF.Context;
using ASC.Webhooks.Core.EF.Context;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using IMailboxDao = ASC.Mail.Core.Dao.Interfaces.IMailboxDao;

namespace ASC.Mail.Core
{
    public class BaseMailStartup
    {
        protected IConfiguration Configuration { get; }

        protected IHostEnvironment HostEnvironment { get; }

        protected DIHelper DIHelper { get; }

        public BaseMailStartup(IConfiguration configuration, IHostEnvironment hostEnvironment)
        {
            Configuration = configuration;
            HostEnvironment = hostEnvironment;
            DIHelper = new DIHelper();
        }

        public virtual void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            //services.AddCustomHealthCheck(Configuration);
            services.AddScoped<EFLoggerFactory>();
            services.AddBaseDbContextPool<CoreDbContext>();
            services.AddBaseDbContextPool<TenantDbContext>();
            services.AddBaseDbContextPool<UserDbContext>();
            services.AddBaseDbContextPool<CustomDbContext>();

            services.AddBaseDbContextPool<MailServerDbContext>();
            services.AddBaseDbContextPool<MailDbContext>();

            services.RegisterFeature();
            services.AddAutoMapper(GetAutoMapperProfileAssemblies());

            services.AddMemoryCache();
            //services.AddDistributedCache(Configuration);
            services.AddDistributedTaskQueue();
            //services.AddCacheNotify(Configuration);
            services.AddHttpClient();
            DIHelper.Configure(services);

            services.AddHttpContextAccessor();
            services.AddMemoryCache();
            services.AddHttpClient();

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



            var diHelper = new DIHelper(services);
            diHelper.TryAdd<FactoryIndexerMailMail>();
            diHelper.TryAdd<FactoryIndexerMailContact>();
            diHelper.TryAdd(typeof(ICacheNotify<>), typeof(KafkaCacheNotify<>));


            //var serviceProvider = services.BuildServiceProvider();
            //var logger = serviceProvider.GetService<ILogger<CrmLinkEngine>>();
            //services.AddSingleton(typeof(ILogger), logger);
        }

        private IEnumerable<Assembly> GetAutoMapperProfileAssemblies()
        {
            return from x in AppDomain.CurrentDomain.GetAssemblies()
                   where x.GetName().Name!.StartsWith("ASC.")
                   select x;
        }

        public virtual void Configure(IApplicationBuilder app)
        {

        }
    }
}

