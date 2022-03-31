namespace ASC.Mail.Aggregator.Service;

[Singletone]
class AggregatorServiceLauncher : IHostedService
{
    private readonly AggregatorService _aggregatorService;
    private readonly ConsoleParameters _consoleParameters;
    private ManualResetEvent _resetEvent;

    private Task _aggregatorServiceTask;
    private CancellationTokenSource _cts;

    public AggregatorServiceLauncher(
        AggregatorService aggregatorService,
        ConsoleParser consoleParser)
    {
        _aggregatorService = aggregatorService;
        _consoleParameters = consoleParser.GetParsedParameters();

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
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
        try
        {
            _aggregatorService.StopService(_cts);
            await Task.WhenAll(_aggregatorServiceTask, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken));
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception ex)
        {
        }
    }
}
