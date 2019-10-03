using System.Threading;
using System.Threading.Tasks;
using ClientApp;
using Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Unity.Ipc;
using Unity.Ipc.Hosted.Client;
using Logger = Serilog.Core.Logger;

namespace ClientCLI
{
    class Program
    {
        private static Logger logger;
        private static readonly CancellationTokenSource cts = new CancellationTokenSource();

        static async Task Main(string[] args)
        {
            var logLevelSwitch = new LoggingLevelSwitch { MinimumLevel = LogEventLevel.Debug };

            logger = new LoggerConfiguration().MinimumLevel.ControlledBy(logLevelSwitch)
                                              .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                                              .Enrich.FromLogContext()
                                              .WriteTo.Console()
                                              .CreateLogger();


            var host = new IpcHost(Configuration.DefaultPort, IpcVersion.Parse("1.0"));
            host.Configuration.AddLocalTarget<MyClient.MyClient>();
            host.Configuration.AddRemoteTarget<IMyServer>();

            host.UseSerilog(logger)
                .ConfigureAppConfiguration(x => x.AddCommandLine(args))
                .ConfigureServices((context, services) => { services.AddSingleton(logLevelSwitch); })
                .UseConsoleLifetime()
                ;

            var client = await host.Start(cts.Token);

            ThreadPool.QueueUserWorkItem(c => Run((IpcClient)c, cts.Token).Forget(), client);

            await host.Run();
            cts.Cancel();
        }

        private static async Task Run(IpcClient host, CancellationToken token)
        {
            var server = host.GetLocalTarget<MyClient.MyClient>();
            while (!token.IsCancellationRequested)
            {
                var ret = await server.StartJob();
                await server.WaitForJobDone(ret);
                logger.Debug("Done " + ret.ID);
            }
        }
    }
}
