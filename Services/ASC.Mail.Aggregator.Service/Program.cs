using ASC.Api.Core.Core;
using ASC.Api.Core.Extensions;
using ASC.Common.Logging;
using ASC.Core.Common.EF;
using ASC.Core.Common.EF.Context;
using ASC.Mail.Core.Dao;
using ASC.Mail.Core.Dao.Context;
using ASC.Mail.Core.Dao.Interfaces;
using ASC.Mail.Server.Core.Dao;
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

builder.Host.ConfigureAppConfiguration((hostContext, config) =>
{
    var builded = config.Build();
    var path = builded["pathToConf"];
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
});

//builder.Host.ConfigureServices((hostContext, services) =>
//{


//});

//builder.Host.ConfigureNLogLogging();


builder.Services.AddHttpContextAccessor();
//services.AddCustomHealthCheck(Configuration);
builder.Services.AddScoped<EFLoggerFactory>();
builder.Services.AddBaseDbContextPool<CoreDbContext>();
builder.Services.AddBaseDbContextPool<TenantDbContext>();
builder.Services.AddBaseDbContextPool<UserDbContext>();
builder.Services.AddBaseDbContextPool<CustomDbContext>();

builder.Services.AddBaseDbContextPool<MailServerDbContext>();
builder.Services.AddBaseDbContextPool<MailDbContext>();

builder.Services.RegisterFeature();
builder.Services.AddAutoMapper(GetAutoMapperProfileAssemblies());

builder.Services.AddMemoryCache();
//services.AddDistributedCache(Configuration);
builder.Services.AddDistributedTaskQueue();
//services.AddCacheNotify(Configuration);
builder.Services.AddHttpClient();

builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();

builder.Services.AddScoped<IAccountDao, AccountDao>();
builder.Services.AddScoped<IAlertDao, AlertDao>();
builder.Services.AddScoped<IAttachmentDao, AttachmentDao>();
builder.Services.AddScoped<IChainDao, ChainDao>();
builder.Services.AddScoped<IContactCardDao, ContactCardDao>();
builder.Services.AddScoped<IContactDao, ContactDao>();
builder.Services.AddScoped<IContactInfoDao, ContactInfoDao>();
builder.Services.AddScoped<ICrmContactDao, CrmContactDao>();
builder.Services.AddScoped<ICrmLinkDao, CrmLinkDao>();
builder.Services.AddScoped<IDisplayImagesAddressDao, DisplayImagesAddressDao>();
builder.Services.AddScoped<IFilterDao, FilterDao>();
builder.Services.AddScoped<IFolderDao, FolderDao>();
builder.Services.AddScoped<IImapFlagsDao, ImapFlagsDao>();
builder.Services.AddScoped<IImapSpecialMailboxDao, ImapSpecialMailboxDao>();
builder.Services.AddScoped<IMailboxAutoreplyDao, MailboxAutoreplyDao>();
builder.Services.AddScoped<IMailboxAutoreplyHistoryDao, MailboxAutoreplyHistoryDao>();
builder.Services.AddScoped<IMailboxDao, ASC.Mail.Core.Dao.MailboxDao>();
builder.Services.AddScoped<IMailDaoFactory, MailDaoFactory>();
builder.Services.AddScoped<IMailboxDomainDao, MailboxDomainDao>();
builder.Services.AddScoped<IMailboxProviderDao, MailboxProviderDao>();
builder.Services.AddScoped<IMailboxServerDao, MailboxServerDao>();
builder.Services.AddScoped<IMailboxSignatureDao, MailboxSignatureDao>();
builder.Services.AddScoped<IMailDao, MailDao>();
builder.Services.AddScoped<IMailGarbageDao, MailGarbageDao>();
builder.Services.AddScoped<IMailInfoDao, MailInfoDao>();
builder.Services.AddScoped<IServerAddressDao, ServerAddressDao>();
builder.Services.AddScoped<IServerDao, ServerDao>();
builder.Services.AddScoped<IServerDnsDao, ServerDnsDao>();
builder.Services.AddScoped<IServerDomainDao, ServerDomainDao>();
builder.Services.AddScoped<IServerGroupDao, ServerGroupDao>();
builder.Services.AddScoped<ITagAddressDao, TagAddressDao>();
builder.Services.AddScoped<ITagDao, TagDao>();
builder.Services.AddScoped<ITagMailDao, TagMailDao>();
builder.Services.AddScoped<IUserFolderDao, UserFolderDao>();
builder.Services.AddScoped<IUserFolderTreeDao, UserFolderTreeDao>();
builder.Services.AddScoped<IUserFolderXMailDao, UserFolderXMailDao>();



var diHelper = new DIHelper(services);
builder.Services.AddScoped<FactoryIndexerMailMail>();
builder.Services.AddScoped<FactoryIndexerMailContact>();
builder.Services.AddScoped(typeof(ICacheNotify<>), typeof(KafkaCacheNotify<>));

builder.Services.AddSingleton(new ConsoleParser(args));
builder.Services.AddScoped<AggregatorServiceLauncher>();



builder.Services.AddSingleton<MailQueueItemSettings>();
builder.Services.AddSingleton<SocketIoNotifier>();
builder.Services.AddSingleton<MailSettings>();
builder.Services.AddSingleton<QueueManager>();
builder.Services.AddSingleton<AggregatorService>();
builder.Services.AddScoped<AggregatorServiceScope>();
builder.Services.AddDistributedTaskQueue();
builder.Services.AddAutoMapper(Assembly.GetAssembly(typeof(DefaultMappingProfile)));
builder.Services.AddHostedService<AggregatorServiceLauncher>();
builder.Services.Configure<HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(15));
//var serviceProvider = services.BuildServiceProvider();
//var logger = serviceProvider.GetService<ILogger<CrmLinkEngine>>();
//services.AddSingleton(typeof(ILogger), logger);

//var startup = new BaseMailStartup(builder.Configuration, builder.Environment);

//startup.ConfigureServices(builder.Services);

builder.Host.ConfigureContainer<ContainerBuilder>((context, builder) =>
{
    builder.Register(context.Configuration, false, false, "search.json");
});

var app = builder.Build();

//startup.Configure(app);

await app.RunAsync();

IEnumerable<Assembly> GetAutoMapperProfileAssemblies()
{
    return from x in AppDomain.CurrentDomain.GetAssemblies()
           where x.GetName().Name!.StartsWith("ASC.")
           select x;
}