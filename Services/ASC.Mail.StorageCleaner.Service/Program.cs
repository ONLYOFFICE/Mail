using NLog;
using StackExchange.Redis.Extensions.Core.Configuration;
using StackExchange.Redis.Extensions.Newtonsoft;

string Namespace = typeof(StorageCleanerService).Namespace;
string AppName = Namespace.Substring("ASC.Mail".Length + 1);

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
                            .GetLogger(typeof(StorageCleanerService).Namespace);

logger.Debug("path: " + path);
logger.Debug("EnvironmentName: " + builder.Environment.EnvironmentName);

builder.Host.ConfigureDefault();
builder.WebHost.MailConfigureKestrel();

diHelper.AddMailScoppedServices();
builder.Services.AddDistributedCache(builder.Configuration);

diHelper.TryAdd<StorageCleanerLauncher>();
builder.Services.AddHostedService<StorageCleanerLauncher>();
diHelper.TryAdd(typeof(ICacheNotify<>), typeof(RedisCacheNotify<>));
var redisConfiguration = builder.Configuration.GetSection("mail:ImapSync:Redis").Get<RedisConfiguration>();
builder.Services.AddStackExchangeRedisExtensions<NewtonsoftSerializer>(redisConfiguration);
diHelper.TryAdd<StorageCleanerScope>();
builder.Services.AddSingleton(new ConsoleParser(args));
builder.Services.Configure<HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(15));
builder.Services.AddMailServices();

var app = builder.Build();

await app.RunAsync();