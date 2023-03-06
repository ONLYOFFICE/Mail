using ASC.Mail.Watchdog.Loggers;

namespace ASC.Mail.Watchdog.Service;

[Singletone]
class WatchdogLauncher : IHostedService
{
    private readonly ILogger _log;
    private readonly WatchdogService _watchdogService;
    private readonly ConsoleParameters _consoleParameters;

    private CancellationTokenSource Cts;
    private Task _watchdogTask;
    private ManualResetEvent _mreStop;

    public WatchdogLauncher(
        WatchdogService watchdogService,
        ILoggerProvider logProvider,
        ConsoleParser consoleParser)
    {
        _watchdogService = watchdogService;
        _log = logProvider.CreateLogger("ASC.Mail.WatchdogLauncher");
        _consoleParameters = consoleParser.GetParsedParameters();
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        _log.CritWatchdogLauncher(e.ExceptionObject.ToString());
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _log.InfoWatchdogLauncherStart();

        Cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (_consoleParameters.IsConsole)
        {
            _log.InfoWatchdogMessage("Console mode.");
            _watchdogTask = _watchdogService.StarService(Cts.Token);

            _mreStop = new ManualResetEvent(false);
            Console.CancelKeyPress += async (sender, e) => await StopAsync(cancellationToken);
            _mreStop.WaitOne();
        }
        else
        {
            _watchdogTask = _watchdogService.StarService(Cts.Token);
        }

        return _watchdogTask.IsCompleted ? _watchdogTask : Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _watchdogService.StopService(Cts);
            await Task.WhenAny(_watchdogTask, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken));
        }
        catch (Exception e)
        {

            _log.ErrorWatchdogLauncherStop(e.ToString());
        }

        _log.InfoWatchdogLauncherStop();
    }
}
