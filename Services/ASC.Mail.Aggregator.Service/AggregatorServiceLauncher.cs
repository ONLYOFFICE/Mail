using ASC.Common;
using ASC.Mail.Aggregator.Service.Console;
using ASC.Mail.Aggregator.Service.Service;

using Microsoft.Extensions.Hosting;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace ASC.Mail.Aggregator.Service
{
    [Singletone]
    class AggregatorServiceLauncher : IHostedService
    {
        //private ILog Log { get; }
        private AggregatorService AggregatorService { get; }
        private ConsoleParameters ConsoleParameters { get; }
        private ManualResetEvent ResetEvent;

        private Task AggregatorServiceTask;
        private CancellationTokenSource Cts;

        public AggregatorServiceLauncher(
            //IOptionsMonitor<ILog> options,
            AggregatorService aggregatorService,
            ConsoleParser consoleParser)
        {
            //Log = options.Get("ASC.Mail.MainThread");
            AggregatorService = aggregatorService;
            ConsoleParameters = consoleParser.GetParsedParameters();

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            //Log.FatalFormat("Unhandled exception: {0}", e.ExceptionObject.ToString());
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            //Log.Info("Start service\r\n");

            Cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            if (ConsoleParameters.IsConsole)
            {
                //Log.Info("Service Start in console-daemon mode");

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
                //Log.Info("Trying to stop the service. Await task...");
                AggregatorService.StopService(Cts);
                await Task.WhenAll(AggregatorServiceTask, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken));
            }
            catch (TaskCanceledException)
            {
                //Log.ErrorFormat($"AggregatorServiceTask was canceled.");
            }
            catch (Exception ex)
            {
                //Log.ErrorFormat($"Failed to terminate the service correctly. The details:\r\n{ex}\r\n");
            }

            //Log.Info("Stop service\r\n");
        }
    }
}
