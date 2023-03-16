using ASC.Core.Billing;
using ASC.Mail.Core.Extensions;
using NLog;
using StackExchange.Redis.Extensions.Core.Configuration;
using StackExchange.Redis.Extensions.Newtonsoft;

string Namespace = typeof(AggregatorService).Namespace;
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
                            .GetLogger(typeof(AggregatorService).Namespace);

logger.Debug("path: " + path);
logger.Debug("EnvironmentName: " + builder.Environment.EnvironmentName);


diHelper.TryAdd<FactoryIndexerMailMail>();

diHelper.TryAdd<FactoryIndexerMailContact>();
diHelper.TryAdd(typeof(ICacheNotify<>), typeof(RedisCacheNotify<>));
diHelper.TryAdd<AggregatorServiceLauncher>();
diHelper.TryAdd<AggregatorServiceScope>();
diHelper.AddMailScoppedServices();

builder.WebHost.MailConfigureKestrel();
builder.Host.ConfigureDefault();

builder.Services.AddBaseDbContext<MailServerDbContext>();
builder.Services.AddBaseDbContext<MailDbContext>();
builder.Services.AddDistributedTaskQueue();
builder.Services.AddDistributedCache(builder.Configuration);
builder.Services.AddSingleton(new ConsoleParser(args));

builder.Services.AddHostedService<AggregatorServiceLauncher>();
builder.Services.Configure<HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(15));

var redisConfiguration = builder.Configuration.GetSection("mail:ImapSync:Redis").Get<RedisConfiguration>();
builder.Services.AddStackExchangeRedisExtensions<NewtonsoftSerializer>(redisConfiguration);
builder.Services.AddMailServices();

var app = builder.Build();

await app.RunAsync();