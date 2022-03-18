namespace ASC.Mail.ImapSync;

class Program
{
    async static Task Main(string[] args)
    {
        await CreateHostBuilder(args).Build().RunAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseSystemd()
            .UseWindowsService()
            .UseServiceProviderFactory(new AutofacServiceProviderFactory())
            .ConfigureWebHostDefaults(webBuilder =>
            {
                var builder = webBuilder.UseStartup<BaseWorkerStartup>();

                builder.ConfigureKestrel((hostingContext, serverOptions) =>
                {
                    var kestrelConfig = hostingContext.Configuration.GetSection("Kestrel");

                    if (!kestrelConfig.Exists()) return;

                    var unixSocket = kestrelConfig.GetValue<string>("ListenUnixSocket");

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        if (!String.IsNullOrWhiteSpace(unixSocket))
                        {
                            unixSocket = String.Format(unixSocket, hostingContext.HostingEnvironment.ApplicationName.Replace("ASC.", "").Replace(".", ""));

                            serverOptions.ListenUnixSocket(unixSocket);
                        }
                    }
                });
            })
            .ConfigureAppConfiguration((hostContext, config) =>
            {
                var buided = config.Build();
                var path = buided["pathToConf"];
                if (!Path.IsPathRooted(path))
                {
                    path = Path.GetFullPath(CrossPlatform.PathCombine(hostContext.HostingEnvironment.ContentRootPath, path));
                }

                config.SetBasePath(path);
                var env = hostContext.Configuration.GetValue("ENVIRONMENT", "Production");
                config
                    .AddJsonFile("appsettings.json")
                    .AddJsonFile($"appsettings.{env}.json", true)
                    .AddJsonFile("storage.json")
                    .AddJsonFile($"storage.{env}.json")
                    .AddJsonFile("mail.json")
                    .AddJsonFile($"mail.{env}.json", true)
                    .AddJsonFile("elastic.json", true)
                    .AddJsonFile($"elastic.{env}.json", true)
                    .AddEnvironmentVariables()
                    .AddCommandLine(args)
                    .AddInMemoryCollection(new Dictionary<string, string>
                    {
                        {"pathToConf", path }
                    }
                    );
            })
        .ConfigureServices((hostContext, services) =>
        {
            services.AddHttpContextAccessor();
            services.AddMemoryCache();
            services.AddHttpClient();
            var diHelper = new DIHelper(services);
            diHelper.TryAdd<FactoryIndexerMailMail>();
            diHelper.TryAdd<FactoryIndexerMailContact>();
            diHelper.TryAdd(typeof(ICacheNotify<>), typeof(KafkaCache<>));
            diHelper.TryAdd<MailClientScope>();
            diHelper.TryAdd<ImapSyncService>();
            services.AddAutoMapper(Assembly.GetAssembly(typeof(MappingProfile)));
            services.AddHostedService<ImapSyncService>();

            var redisConfiguration = hostContext.Configuration.GetSection("mail:ImapSync:Redis").Get<RedisConfiguration>();
            services.AddSingleton(redisConfiguration);

            services.Configure<HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(15));
        })
        .ConfigureContainer<ContainerBuilder>((context, builder) =>
        {
            builder.Register(context.Configuration, false, false, "search.json");
        });
}
