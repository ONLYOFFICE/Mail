using ASC.Mail.Aggregator.Service.Loggers;

namespace ASC.Mail.Aggregator.Service;

[Singletone]
class AggregatorServiceLauncher : IHostedService
{
    private readonly AggregatorService _aggregatorService;
    private readonly ConsoleParameters _consoleParameters;
    private ManualResetEvent _resetEvent;

    private Task _aggregatorServiceTask;
    private CancellationTokenSource _cts;

    private readonly ILogger _log;

    public AggregatorServiceLauncher(
        AggregatorService aggregatorService,
        ConsoleParser consoleParser,
        ILoggerProvider loggerProvider)
    {
        _aggregatorService = aggregatorService;
        _consoleParameters = consoleParser.GetParsedParameters();

        _log = loggerProvider.CreateLogger("ASC.Mail.AggregatorLauncher");

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        _log.CritAggregatorServiceLauncher(e.ExceptionObject.ToString());
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _log.InfoAggregatorServiceLauncherStart();

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (_consoleParameters.IsConsole)
        {
            _aggregatorServiceTask = _aggregatorService.StartTimer(_cts.Token, true);
            _resetEvent = new ManualResetEvent(false);
            System.Console.CancelKeyPress += async (sender, e) => await StopAsync(cancellationToken);
            _resetEvent.WaitOne();
        }
        else
        {
            _aggregatorServiceTask = _aggregatorService.StartTimer(_cts.Token, true);
        }

        return _aggregatorServiceTask.IsCompleted ? _aggregatorServiceTask : Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _log.InfoAggregatorServiceLauncherAwaitTask();

        try
        {
            _aggregatorService.StopService(_cts);
            await Task.WhenAll(_aggregatorServiceTask, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken));
        }
        catch (TaskCanceledException)
        {
            _log.ErrorAggregatorServiceLauncherCancel();
        }
        catch (Exception ex)
        {
            _log.ErrorAggregatorServiceLauncherStop(ex.ToString());
        }

        _log.InfoAggregatorServiceLauncherStop();
    }
}
