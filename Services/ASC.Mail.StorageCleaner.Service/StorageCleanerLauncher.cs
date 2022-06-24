using ASC.Mail.StorageCleaner.Loggers;

using Microsoft.Extensions.Logging;

namespace ASC.Mail.StorageCleaner.Service;

[Singletone]
class StorageCleanerLauncher : IHostedService
{
    private Task _cleanerTask;
    private CancellationTokenSource Cts;
    private ManualResetEvent MreStop;

    private readonly ILogger _log;
    private readonly StorageCleanerService _storageCleanerService;
    private readonly ConsoleParameters _consoleParameters;

    public StorageCleanerLauncher(
        ILoggerProvider logProvider,
        StorageCleanerService cleanerService,
        ConsoleParser consoleParser)
    {
        _log = logProvider.CreateLogger("ASC.Mail.Cleaner");
        _storageCleanerService = cleanerService;
        _consoleParameters = consoleParser.GetParsedParameters();

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        _log.CritStorageCleanerLauncher(e.ExceptionObject.ToString());
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _log.InfoStorageCleanerLauncherStart();

        try
        {
            Cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            if (_consoleParameters.IsConsole)
            {
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
        _log.InfoStorageCleanerLauncherTryingToStop();

        try
        {
            _storageCleanerService.StopService(Cts, MreStop);
            await Task.WhenAny(_cleanerTask, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken));
        }
        catch (Exception ex)
        {
            _log.ErrorStorageCleanerLauncherStop(ex.ToString());
        }
    }
}
