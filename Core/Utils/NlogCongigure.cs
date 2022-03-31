namespace ASC.Mail.Core.Utils;

[Scope]
public class NlogCongigure
{
    private IConfiguration _configuration;
    private ConfigurationExtension _configurationExtension;

    public NlogCongigure(
        IConfiguration configuration,
        ConfigurationExtension configurationExtension)
    {
        _configuration = configuration;
        _configurationExtension = configurationExtension;
    }

    public void Configure()
    {
        var fileName = CrossPlatform.PathCombine(_configuration["pathToConf"], "nlog.config");

        LogManager.Configuration = new NLog.Config.XmlLoggingConfiguration(fileName);
        LogManager.ThrowConfigExceptions = false;

        var settings = _configurationExtension.GetSetting<NLogSettings>("log");
        if (!string.IsNullOrEmpty(settings.Name))
        {
            LogManager.Configuration.Variables["name"] = settings.Name;
        }

        if (!string.IsNullOrEmpty(settings.Dir))
        {
            LogManager.Configuration.Variables["dir"] = settings.Dir.TrimEnd('/').TrimEnd('\\') + Path.DirectorySeparatorChar;
        }

        NLog.Targets.Target.Register<SelfCleaningTarget>("SelfCleaning");
    }
}
