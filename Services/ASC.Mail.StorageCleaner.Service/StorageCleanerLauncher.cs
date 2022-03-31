namespace ASC.Mail.StorageCleaner.Service;

[Singletone]
class StorageCleanerLauncher : IHostedService
{
    private Task _cleanerTask;
    private CancellationTokenSource Cts;
    private ManualResetEvent MreStop;

    private readonly ILog _log;
    private readonly StorageCleanerService _storageCleanerService;
    private readonly ConsoleParameters _consoleParameters;

    public StorageCleanerLauncher(
        IOptionsMonitor<ILog> options,
        StorageCleanerService cleanerService,
        ConsoleParser consoleParser)
    {
        _log = options.Get("ASC.Mail.Cleaner");
        _storageCleanerService = cleanerService;
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

        try
        {
            Cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            if (_consoleParameters.IsConsole)
            {
                _log.Info("Service Start in console-daemon mode");

                _cleanerTask = _storageCleanerService.StartTimer(Cts.Token, true);

                MreStop = new ManualResetEvent(false);
                Console.CancelKeyPress += async (sender, e) => await StopAsync(cancellationToken);
                MreStop.WaitOne();
            }
            else
            {
                _cleanerTask = _storageCleanerService.StartTimer(Cts.Token, true);
            }

            return _cleanerTask.IsCompleted ? _cleanerTask : Task.CompletedTask;
        }
        catch (Exception)
        {
            return StopAsync(cancellationToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _log.Info("Stop service\r\n");
            _storageCleanerService.StopService(Cts, MreStop);
            await Task.WhenAny(_cleanerTask, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken));
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to terminate the service correctly. The details:\r\n{ex}\r\n");
        }
    }
}
