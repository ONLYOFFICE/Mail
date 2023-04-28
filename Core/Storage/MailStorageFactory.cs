using ASC.Core.Common.EF.Context;
using ASC.Data.Storage.Configuration;
using Module = ASC.Data.Storage.Configuration.Module;
using Properties = ASC.Data.Storage.Configuration.Properties;

namespace ASC.Mail.Core.Storage
{
    [Scope(Additional = typeof(StorageFactoryExtension))]
    public class MailStorageFactory
    {
        private readonly StorageFactoryConfig _storageFactoryConfig;

        private readonly StorageSettingsHelper _storageSettingsHelper;

        private readonly CoreBaseSettings _coreBaseSettings;

        private readonly IServiceProvider _serviceProvider;

        public MailStorageFactory(IServiceProvider serviceProvider, StorageFactoryConfig storageFactoryConfig, StorageSettingsHelper storageSettingsHelper, CoreBaseSettings coreBaseSettings)
        {
            _serviceProvider = serviceProvider;
            _storageFactoryConfig = storageFactoryConfig;
            _storageSettingsHelper = storageSettingsHelper;
            _coreBaseSettings = coreBaseSettings;
        }

        public MailTenantQuotaController GetMailQuotaContriller(int tenant)
        {
            MailTenantQuotaController service = _serviceProvider.GetService<MailTenantQuotaController>();
            service.Init(tenant);

            return service;
        }


        public IDataStore GetStorage(int tenant, string module)
        {
            MailTenantQuotaController service = GetMailQuotaContriller(tenant);

            return GetStorage(tenant, module, service);
        }

        public IDataStore GetStorage(int? tenant, string module, IQuotaController controller)
        {
            string tenantPath = (tenant.HasValue ? TenantPath.CreatePath(tenant.Value) : TenantPath.CreatePath("default"));

            DataStoreConsumer dataStoreConsumer = new();

            //Change serializer to newtonsoft.json
            try
            {
                var _dbContextFactory = _serviceProvider.GetRequiredService<IDbContextFactory<WebstudioDbContext>>();

                using WebstudioDbContext webstudioDbContext = _dbContextFactory.CreateDbContext();
                string text = (from r in webstudioDbContext.WebstudioSettings
                               where r.Id == new Guid("f13eaf2d-fa53-44f1-a6d6-a5aeda46fa2b")
                               where r.TenantId == tenant
                               where r.UserId == new Guid("00000000-0000-0000-0000-000000000000")
                               select r.Data).FirstOrDefault();

                text = text.Replace("\"Key\":", "").Replace(",\"Value\"", "").Replace("[", "").Replace("]", "").Replace("},{", ",");

                if (!string.IsNullOrEmpty(text))
                {
                    var baseStorageSettings = System.Text.Json.JsonSerializer.Deserialize<StorageSettings>(text); //settingsManager.Load<StorageSettings>();

                    if (baseStorageSettings != null && baseStorageSettings.Module.ToLower() == "s3")
                    {
                        dataStoreConsumer = _storageSettingsHelper.DataStoreConsumer(baseStorageSettings);
                    }
                }
            }
            catch (Exception ex)
            {

            }

            return GetDataStore(tenantPath, module, dataStoreConsumer, controller);
        }

        public IDataStore GetStorageFromConsumer(int tenant, string module, DataStoreConsumer consumer, string region = "current")
        {
            string tenantPath = TenantPath.CreatePath(tenant); //: TenantPath.CreatePath("default"));
            ASC.Data.Storage.Configuration.Storage storage = _storageFactoryConfig.GetStorage(region);
            if (storage == null)
            {
                throw new InvalidOperationException("config section not found");
            }

            MailTenantQuotaController service = _serviceProvider.GetService<MailTenantQuotaController>();
            service.Init(tenant);

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
            mailTenantQuotaController.Init(tenant);

            return GetStorage(tenant, DefineConstants.MODULE_NAME, mailTenantQuotaController);
        }
    }
}
