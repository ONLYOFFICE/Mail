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

        }



        public virtual void Configure(IApplicationBuilder app)
        {

        }
    }
}

