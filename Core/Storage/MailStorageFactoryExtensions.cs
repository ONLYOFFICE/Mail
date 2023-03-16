using ASC.Data.Storage.DiscStorage;
using ASC.Data.Storage.GoogleCloud;
using ASC.Data.Storage.RackspaceCloud;
using ASC.Data.Storage.S3;

namespace ASC.Mail.Core.Storage
{
    public static class MailStorageFactoryExtension
    {
        public static void Register(DIHelper services)
        {
            services.TryAdd<DiscDataStore>();
            services.TryAdd<GoogleCloudStorage>();
            services.TryAdd<RackspaceCloudStorage>();
            services.TryAdd<S3Storage>();
            services.TryAdd<MailTenantQuotaController>();
            services.TryAdd<DbMailQuotaService>();
        }
    }
}
