using NLog;

string Namespace = typeof(ImapSyncService).Namespace;
string AppName = Namespace.Substring(Namespace.LastIndexOf('.') + 1);

var options = new WebApplicationOptions
{
    Args = args,
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : default
};

var builder = WebApplication.CreateBuilder(options);
var diHelper = new DIHelper(builder.Services);

var path = builder.Configuration["pathToConf"];

if (!Path.IsPathRooted(path))
{
    path = Path.GetFullPath(CrossPlatform.PathCombine(builder.Environment.ContentRootPath, path));
}

builder.Configuration.SetBasePath(path);
var env = builder.Configuration.GetValue("ENVIRONMENT", "Production");

builder.Configuration
    .AddMailJsonFiles(env)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .AddInMemoryCollection(new Dictionary<string, string>
        {
                {"pathToConf", path }
        }
    ).Build();

var logger = LogManager.Setup()
                            .SetupExtensions(s =>
                            {
                                s.RegisterLayoutRenderer("application-context", (logevent) => AppName);
                            })
                            .LoadConfiguration(builder.Configuration, builder.Environment)
                            .GetLogger(typeof(ImapSyncService).Namespace);

logger.Debug("path: " + path);
logger.Debug("EnvironmentName: " + builder.Environment.EnvironmentName);

builder.WebHost.MailConfigureKestrel();
builder.Host.ConfigureDefault();

builder.Services.AddMailServices();
builder.Services.AddBaseDbContext<MailServerDbContext>();
builder.Services.AddBaseDbContext<MailDbContext>();
builder.Services.AddDistributedTaskQueue();
builder.Services.AddDistributedCache(builder.Configuration);
builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();

diHelper.TryAdd<FactoryIndexerMailMail>();
diHelper.TryAdd<FactoryIndexerMailContact>();
diHelper.TryAdd(typeof(ICacheNotify<>), typeof(RedisCacheNotify<>));
diHelper.TryAdd<ImapSyncService>();
diHelper.TryAdd<MailEnginesFactory>();
diHelper.AddMailScoppedServices();

var redisConfiguration = builder.Configuration.GetSection("mail:ImapSync:Redis").Get<RedisConfiguration>();
builder.Services.AddStackExchangeRedisExtensions<NewtonsoftSerializer>(redisConfiguration);

builder.Services.AddHostedService<ImapSyncService>();
builder.Services.Configure<HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(15));

var app = builder.Build();

await app.RunAsync();