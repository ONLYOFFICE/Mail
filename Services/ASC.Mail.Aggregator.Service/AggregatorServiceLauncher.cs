namespace ASC.Mail.Aggregator.Service;

[Singletone]
class AggregatorServiceLauncher : IHostedService
{
    private AggregatorService AggregatorService { get; }
    private ConsoleParameters ConsoleParameters { get; }
    private ManualResetEvent ResetEvent;

    private Task AggregatorServiceTask;
    private CancellationTokenSource Cts;

    public AggregatorServiceLauncher(
        AggregatorService aggregatorService,
        ConsoleParser consoleParser)
    {
        AggregatorService = aggregatorService;
        ConsoleParameters = consoleParser.GetParsedParameters();

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (ConsoleParameters.IsConsole)
        {
            AggregatorServiceTask = AggregatorService.StartTimer(Cts.Token, true);
            ResetEvent = new ManualResetEvent(false);
            System.Console.CancelKeyPress += async (sender, e) => await StopAsync(cancellationToken);
            ResetEvent.WaitOne();
        }
        else
        {
            AggregatorServiceTask = AggregatorService.StartTimer(Cts.Token, true);
        }

        return AggregatorServiceTask.IsCompleted ? AggregatorServiceTask : Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            AggregatorService.StopService(Cts);
            await Task.WhenAll(AggregatorServiceTask, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken));
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception ex)
        {
        }
    }
}
