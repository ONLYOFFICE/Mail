using ASC.Api.Core;
using ASC.Api.Core.Auth;
using ASC.Api.Core.Middleware;
using ASC.Common;
using ASC.Core;
using ASC.CRM.Core;
using ASC.Mail.Configuration;
using ASC.Mail.Core.Engine;
using ASC.Mail.Utils;
using ASC.Web.Files.Api;
using ASC.Web.Files.Utils;

namespace ASC.Mail.Tests
{
    [Scope]
    public class MailTestsScope
    {
        private ApiDateTimeHelper ApiDateTimeHelper { get; }
        private ApiHelper ApiHelper { get; }
        private ApiContext ApiContext { get; }
        private CookieAuthHandler CookieAuthHandler { get; }
        private CultureMiddleware CultureMiddleware { get; }
        private CoreSettings CoreSettings { get; }
        private FilesIntegration FilesIntegration { get; }
        private FileSecurity FileSecurity { get; }
        private FileConverter FileConverter { get; }
        private IpSecurityFilter IpSecurityFilter { get; }
        private MailEnginesFactory MailEnginesFactory { get; }
        private ProductSecurityFilter ProductSecurityFilter { get; }
        private PaymentFilter PaymentFilter { get; }
        private SecurityContext SecurityContext { get; }
        private TenantManager TenantManager { get; }
        private TenantStatusFilter TenantStatusFilter { get; }
        private UserManager UserManager { get; }
        private MailSettings MailSettings { get; }
        private MailGarbageEngine MailGarbageEngine { get; }
        private MessageEngine MessageEngine { get; }
        private FilterEngine FilterEngine { get; }

        public MailTestsScope(
            ApiDateTimeHelper apiDateTimeHelper,
            ApiHelper apiHelper,
            ApiContext apiContext,
            CookieAuthHandler cookieAuthHandler,
            CultureMiddleware cultureMiddleware,
            CoreSettings coreSettings,
            FilesIntegration filesIntegration,
            FileSecurity fileSecurity,
            FileConverter fileConverter,
            IpSecurityFilter ipSecurityFilter,
            MailEnginesFactory mailEnginesFactory,
            ProductSecurityFilter productSecurityFilter,
            PaymentFilter paymentFilter,
            SecurityContext securityContext,
            TenantManager tenantManager,
            TenantStatusFilter tenantStatusFilter,
            UserManager userManager,
            MailSettings mailSettings,
            MailGarbageEngine mailGarbageEngine,
            MessageEngine messageEngine,
            FilterEngine filterEngine
            )
        {
            ApiDateTimeHelper = apiDateTimeHelper;
            ApiHelper = apiHelper;
            ApiContext = apiContext;
            CookieAuthHandler = cookieAuthHandler;
            CultureMiddleware = cultureMiddleware;
            CoreSettings = coreSettings;
            FileSecurity = fileSecurity;
            FileConverter = fileConverter;
            IpSecurityFilter = ipSecurityFilter;
            MailEnginesFactory = mailEnginesFactory;
            ProductSecurityFilter = productSecurityFilter;
            PaymentFilter = paymentFilter;
            SecurityContext = securityContext;
            TenantManager = tenantManager;
            TenantStatusFilter = tenantStatusFilter;
            UserManager = userManager;
            MailSettings = mailSettings;
            MailGarbageEngine = mailGarbageEngine;
            MessageEngine = messageEngine;
            FilterEngine = filterEngine;
        }
    }
}
