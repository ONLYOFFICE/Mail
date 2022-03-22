namespace ASC.Mail.Watchdog.Service;

[Singletone]
class WatchdogLauncher : IHostedService
{
    private readonly ILog _log;
    private readonly WatchdogService _watchdogService;
    private readonly ConsoleParameters _consoleParameters;

    private CancellationTokenSource Cts;
    private Task _watchdogTask;
    private ManualResetEvent _mreStop;

    public WatchdogLauncher(
        WatchdogService watchdogService,
        IOptionsMonitor<ILog> options,
        ConsoleParser consoleParser)
    {
        _watchdogService = watchdogService;
        _log = options.Get("ASC.Mail.WatchdogLauncher");
        _consoleParameters = consoleParser.GetParsedParameters();
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        _log.FatalFormat("Unhandled exception: {0}", e.ExceptionObject.ToString());
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _log.Info("Start service\r\n");

        Cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (_consoleParameters.IsConsole)
        {
            _log.Info("Service Start in console-daemon mode");

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

            _log.Error($"Failed to terminate the service correctly. The details:\r\n{e}\r\n");
        }

        _log.Info("Service stopped.\r\n");
    }
}
