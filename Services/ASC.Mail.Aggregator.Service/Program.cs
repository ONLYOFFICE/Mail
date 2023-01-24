using ASC.Api.Core.Extensions;
using ASC.Core.Common.EF;
using ASC.Mail.Core.Dao.Interfaces;
using ASC.Mail.Core.Dao;
using ASC.Mail.Server.Core.Dao;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting.WindowsServices;
using ASC.Mail.Core.Dao.Context;
using ASC.Common;

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

builder.Host.ConfigureServices((hostContext, services) =>
{
    services.AddSingleton(new ConsoleParser(args));
    services.AddScoped<AggregatorServiceLauncher>();
    services.AddScoped<AggregatorServiceScope>();
    services.AddDistributedTaskQueue();
    services.AddAutoMapper(Assembly.GetAssembly(typeof(DefaultMappingProfile)));
    services.AddHostedService<AggregatorServiceLauncher>();
    services.Configure<HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(15));

});

//builder.Host.ConfigureNLogLogging();

var startup = new BaseMailStartup(builder.Configuration, builder.Environment);

startup.ConfigureServices(builder.Services);

builder.Host.ConfigureContainer<ContainerBuilder>((context, builder) =>
{
    builder.Register(context.Configuration, false, false, "search.json");
});

var app = builder.Build();

startup.Configure(app);

await app.RunAsync();