using ASC.Common.Mapping;
using ASC.Common.Threading;
using ASC.Mail.Core.Dao;
using ASC.Mail.Core.Dao.Interfaces;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting.WindowsServices;

var options = new WebApplicationOptions
{
    Args = args,
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : default
};

var builder = WebApplication.CreateBuilder(options);

builder.Host.UseWindowsService();
builder.Host.UseSystemd();
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());

builder.WebHost.ConfigureKestrel((hostingContext, serverOptions) =>
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

var path = builder.Configuration["pathToConf"];
if (!Path.IsPathRooted(path))
{
    path = Path.GetFullPath(CrossPlatform.PathCombine(builder.Environment.ContentRootPath, path));
}

builder.Configuration.SetBasePath(path);
var env = builder.Configuration.GetValue("ENVIRONMENT", "Production");

builder.Configuration
    .AddJsonFile("appsettings.json")
    .AddJsonFile($"appsettings.{env}.json", true)
    .AddJsonFile("storage.json")
    .AddJsonFile($"storage.{env}.json", true)
    .AddJsonFile("mail.json")
    .AddJsonFile($"mail.{env}.json", true)
    .AddJsonFile("elastic.json")
    .AddJsonFile($"elastic.{env}.json", true)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .AddInMemoryCollection(new Dictionary<string, string>
        {
                {"pathToConf", path }
        }
    );

builder.Host.ConfigureServices((hostContext, services) =>
{
    services.AddHttpContextAccessor();
    services.AddMemoryCache();
    services.AddHttpClient();
    var diHelper = new DIHelper(services);
    diHelper.TryAdd<FactoryIndexerMailMail>();
    diHelper.TryAdd<FactoryIndexerMailContact>();
    diHelper.TryAdd(typeof(ICacheNotify<>), typeof(KafkaCacheNotify<>));
    services.AddSingleton(new ConsoleParser(args));
    diHelper.TryAdd<AggregatorServiceLauncher>();
    diHelper.TryAdd<AggregatorServiceScope>();

    services.AddSingleton<ASC.Mail.Core.Dao.Context.MailDbContext>();
    diHelper.TryAdd(typeof(IImapFlagsDao), typeof(ImapFlagsDao));
    
    services.AddTransient<DistributedTaskQueue>();
    services.AddAutoMapper(Assembly.GetAssembly(typeof(DefaultMappingProfile)));
    services.AddHostedService<AggregatorServiceLauncher>();
    services.Configure<HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(15));

    var serviceProvider = services.BuildServiceProvider();
    var logger = serviceProvider.GetService<ILogger<CrmLinkEngine>>();
    services.AddSingleton(typeof(ILogger), logger);
});

//builder.Host.ConfigureNLogLogging();

var startup = new BaseWorkerStartup(builder.Configuration);

startup.ConfigureServices(builder.Services);

//builder.Host.ConfigureContainer<ContainerBuilder>((context, builder) =>
//{
//    builder.Register(context.Configuration, false, false);
//});

var app = builder.Build();

startup.Configure(app);

await app.RunAsync();