using NLog;

string Namespace = typeof(StorageCleanerService).Namespace;
string AppName = Namespace.Substring(Namespace.LastIndexOf('.') + 1);

var options = new WebApplicationOptions
{
    Args = args,
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : default
};

var builder = WebApplication.CreateBuilder(options);

builder.WebHost.MailConfigureKestrel();

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

builder.Services.AddMailServices();
builder.Services.AddDistributedTaskQueue();

var diHelper = new DIHelper(builder.Services);

builder.Services.AddMailServices();
builder.Services.AddDistributedTaskQueue();
builder.Services.AddDistributedCache(builder.Configuration);
diHelper.AddMailScoppedServices();
diHelper.TryAdd<StorageCleanerLauncher>();
builder.Services.AddHostedService<StorageCleanerLauncher>();
diHelper.TryAdd(typeof(ICacheNotify<>), typeof(KafkaCacheNotify<>));
diHelper.TryAdd<StorageCleanerScope>();
builder.Services.AddAutoMapper(Assembly.GetAssembly(typeof(DefaultMappingProfile)));
builder.Services.Configure<HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(15));


builder.Host.ConfigureContainer<ContainerBuilder>((context, builder) =>
{
    builder.Register(context.Configuration, false, false);
});

var app = builder.Build();

await app.RunAsync();