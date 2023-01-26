using ASC.Api.Core.Core;
using ASC.Api.Core.Extensions;
using ASC.Common.Logging;
using ASC.Core.Common.EF.Context;
using ASC.Core.Common.Hosting;
using ASC.MessagingSystem.EF.Context;
using ASC.Webhooks.Core.EF.Context;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace ASC.Mail
{
    public class Startup
    {
        protected IConfiguration Configuration { get; }

        protected IHostEnvironment HostEnvironment { get; }

        protected DIHelper DIHelper { get; }

        public Startup(IConfiguration configuration, IHostEnvironment hostEnvironment)
        {
            Configuration = configuration;
            HostEnvironment = hostEnvironment;
            DIHelper = new DIHelper();
        }

        public virtual void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            services.AddCustomHealthCheck(Configuration);
            services.AddScoped<EFLoggerFactory>();
            services.AddBaseDbContextPool<AccountLinkContext>();
            services.AddBaseDbContextPool<CoreDbContext>();
            services.AddBaseDbContextPool<TenantDbContext>();
            services.AddBaseDbContextPool<UserDbContext>();
            services.AddBaseDbContextPool<TelegramDbContext>();
            services.AddBaseDbContextPool<CustomDbContext>();
            services.AddBaseDbContextPool<WebstudioDbContext>();
            services.AddBaseDbContextPool<InstanceRegistrationContext>();
            services.AddBaseDbContextPool<MessagesContext>();
            services.AddBaseDbContextPool<WebhooksDbContext>();
            services.RegisterFeature();
            services.AddAutoMapper(GetAutoMapperProfileAssemblies());

            services.AddMemoryCache();
            services.AddDistributedCache(Configuration);
            //services.AddEventBus(Configuration);
            services.AddDistributedTaskQueue();
            //services.AddCacheNotify(Configuration);
            services.AddHttpClient();
            DIHelper.Configure(services);
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
