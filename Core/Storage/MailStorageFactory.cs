using ASC.Data.Storage.Configuration;
using Module = ASC.Data.Storage.Configuration.Module;
using Properties = ASC.Data.Storage.Configuration.Properties;

namespace ASC.Mail.Core.Storage
{
    [Scope(Additional = typeof(StorageFactoryExtension))]
    public class MailStorageFactory
    {
        private const string DefaultTenantName = "default";

        private readonly StorageFactoryConfig _storageFactoryConfig;

        private readonly SettingsManager _settingsManager;

        private readonly StorageSettingsHelper _storageSettingsHelper;

        private readonly CoreBaseSettings _coreBaseSettings;

        private readonly IServiceProvider _serviceProvider;

        public MailStorageFactory(IServiceProvider serviceProvider, StorageFactoryConfig storageFactoryConfig, SettingsManager settingsManager, StorageSettingsHelper storageSettingsHelper, CoreBaseSettings coreBaseSettings)
        {
            _serviceProvider = serviceProvider;
            _storageFactoryConfig = storageFactoryConfig;
            _settingsManager = settingsManager;
            _storageSettingsHelper = storageSettingsHelper;
            _coreBaseSettings = coreBaseSettings;
        }

        public IDataStore GetStorage(int? tenant, string module, string region = "current")
        {
            MailTenantQuotaController service = _serviceProvider.GetService<MailTenantQuotaController>();
            return GetStorage(tenant, module, service, region);
        }

        public IDataStore GetStorage(int? tenant, string module, IQuotaController controller, string region = "current")
        {
            string tenantPath = (tenant.HasValue ? TenantPath.CreatePath(tenant.Value) : TenantPath.CreatePath("default"));
            tenant = tenant ?? (-2);
            ASC.Data.Storage.Configuration.Storage storage = _storageFactoryConfig.GetStorage(region);
            if (storage == null)
            {
                throw new InvalidOperationException("config section not found");
            }

            StorageSettings baseStorageSettings = _settingsManager.Load<StorageSettings>();

            return GetDataStore(tenantPath, module, _storageSettingsHelper.DataStoreConsumer(baseStorageSettings), controller, region);
        }

        public IDataStore GetStorageFromConsumer(int? tenant, string module, DataStoreConsumer consumer, string region = "current")
        {
            string tenantPath = (tenant.HasValue ? TenantPath.CreatePath(tenant.Value) : TenantPath.CreatePath("default"));
            ASC.Data.Storage.Configuration.Storage storage = _storageFactoryConfig.GetStorage(region);
            if (storage == null)
            {
                throw new InvalidOperationException("config section not found");
            }

            MailTenantQuotaController service = _serviceProvider.GetService<MailTenantQuotaController>();
            return GetDataStore(tenantPath, module, consumer, service);
        }

        private IDataStore GetDataStore(string tenantPath, string module, DataStoreConsumer consumer, IQuotaController controller, string region = "current")
        {
            ASC.Data.Storage.Configuration.Storage storage = _storageFactoryConfig.GetStorage(region);
            Module moduleElement = storage.GetModuleElement(module);
            if (moduleElement == null)
            {
                throw new ArgumentException("no such module", module);
            }

            Handler handler = storage.GetHandler(moduleElement.Type);
            Type instanceType;
            IDictionary<string, string> props;
            if (_coreBaseSettings.Standalone && !moduleElement.DisableMigrate && consumer.IsSet)
            {
                instanceType = consumer.HandlerType;
                props = consumer;
            }
            else
            {
                instanceType = Type.GetType(handler.Type, throwOnError: true);
                props = handler.Property.ToDictionary((Properties r) => r.Name, (Properties r) => r.Value);
            }

            return ((IDataStore)ActivatorUtilities.CreateInstance(_serviceProvider, instanceType)).Configure(tenantPath, handler, moduleElement, props).SetQuotaController(moduleElement.Count ? controller : null);
        }

        public IDataStore GetMailStorage(int tenant)
        {
            var mailTenantQuotaController = _serviceProvider.GetRequiredService<MailTenantQuotaController>();

            return GetStorage(tenant, DefineConstants.MODULE_NAME, mailTenantQuotaController);
        }
    }
}
