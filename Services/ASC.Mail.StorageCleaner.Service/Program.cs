namespace ASC.Mail.StorageCleaner.Service;

class Program
{
    public static async Task Main(string[] args) => await CreateHostBuilder(args).Build().RunAsync();

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
                        if (!string.IsNullOrWhiteSpace(unixSocket))
                        {
                            unixSocket = string.Format(unixSocket, hostingContext.HostingEnvironment.ApplicationName.Replace("ASC.", "").Replace(".", ""));

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
                    .AddJsonFile("mail.json")
                    .AddJsonFile($"mail.{env}.json")
                    .AddJsonFile("storage.json")
                    .AddJsonFile($"storage.{env}.json")

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

                diHelper.TryAdd<StorageCleanerLauncher>();
                services.AddHostedService<StorageCleanerLauncher>();
                diHelper.TryAdd(typeof(ICacheNotify<>), typeof(KafkaCache<>));
                diHelper.TryAdd<StorageCleanerScope>();
                services.Configure<HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(15));
            })
            .ConfigureContainer<ContainerBuilder>((context, builder) =>
            {
                builder.Register(context.Configuration, false, false);
            });
}
