using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ASC.Mail.Core.Core.Dao.Context
{
    public class BaseDbContext : DbContext
    {
        public static readonly ServerVersion ServerVersion = ServerVersion.Parse("8.0.25");

        protected Provider _provider;

        public ConnectionStringSettings ConnectionStringSettings { get; set; }

        internal string MigrateAssembly { get; set; }

        internal ILoggerFactory LoggerFactory { get; set; }

        protected virtual Dictionary<Provider, Func<BaseDbContext>> ProviderContext => null;

        public BaseDbContext()
        {
        }

        public BaseDbContext(DbContextOptions options)
            : base(options)
        {
        }

        public void Migrate()
        {
            if (ProviderContext != null)
            {
                Provider providerByConnectionString = GetProviderByConnectionString();
                using BaseDbContext baseDbContext = ProviderContext[providerByConnectionString]();
                baseDbContext.ConnectionStringSettings = ConnectionStringSettings;
                baseDbContext.LoggerFactory = LoggerFactory;
                baseDbContext.MigrateAssembly = MigrateAssembly;
                baseDbContext.Database.Migrate();
            }
            else
            {
                Database.Migrate();
            }
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseLoggerFactory(LoggerFactory);
            optionsBuilder.EnableSensitiveDataLogging();
            _provider = GetProviderByConnectionString();
            switch (_provider)
            {
                case Provider.MySql:
                    optionsBuilder.UseMySql(ConnectionStringSettings.ConnectionString, ServerVersion, delegate (MySqlDbContextOptionsBuilder providerOptions)
                    {
                        if (!string.IsNullOrEmpty(MigrateAssembly))
                        {
                            providerOptions.MigrationsAssembly(MigrateAssembly);
                        }

                        providerOptions.EnableRetryOnFailure(15, TimeSpan.FromSeconds(30.0), null);
                    });
                    break;
                case Provider.PostgreSql:
                    optionsBuilder.UseNpgsql(ConnectionStringSettings.ConnectionString);
                    break;
            }
        }

        public Provider GetProviderByConnectionString()
        {
            string providerName = ConnectionStringSettings.ProviderName;
            string text = providerName;
            if (!(text == "MySql.Data.MySqlClient"))
            {
                if (text == "Npgsql")
                {
                    return Provider.PostgreSql;
                }

                return Provider.MySql;
            }

            return Provider.MySql;
        }
    }
}
