using System;
using System.Dynamic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Rpc;
using Mono.Options;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Shared;

namespace SimpleClientServer
{
    using ClientSample;

    class ClientConfiguration : Configuration
    {
        public ClientConfiguration(int port)
        {
            Port = port;
        }
    }

    class ClientProgram
    {
        static async Task<int> Main(string[] args)
        {
            if (!ParseCommandLine(args, out int retCode, out int verbosity, out int port))
            {
                return retCode;
            }

            // set up some logging
            var logLevelSwitch = new LoggingLevelSwitch { MinimumLevel = (LogEventLevel)(verbosity < 0 ? 0 : verbosity) };
            var loggerConfiguration = new LoggerConfiguration().WriteTo.Console().MinimumLevel.ControlledBy(logLevelSwitch);
            ILogger logger = loggerConfiguration.CreateLogger();
            var loggerFactory = new Serilog.Extensions.Logging.SerilogLoggerFactory(logger);

            // controlling when the rpc host shuts down
            var cts = new CancellationTokenSource();

            // our rpc host
            var client = new RpcClient(new ClientConfiguration(port), cts.Token);

            // these are the RPC interfaces available on the server that the client can call
            client.RegisterRemoteTarget<IServerSingleton>()
                  .RegisterRemoteTarget<IServerPerConnection>()
                  .RegisterRemoteTarget<IClientInfo>();


            // We could register our client singleton implementation here instead of doing it on the Starting callback if we wanted to.
            //client.RegisterLocalTarget(new RpcClientSample(client, loggerFactory.CreateLogger(nameof(RpcClientSample))))

            // this is called when the client is starting, for registering additional rpc targets or doing other setup things
            client
                .Starting((registration, context) => {
                    logger.Debug("Starting client");

                    // registering a singleton instance in the Starting callback
                    registration.RegisterLocalTarget(new RpcClientSample(context, loggerFactory.CreateLogger(nameof(RpcClientSample))));
                })

                // this is called when the client is ready to do things
                .Ready(async theClient => {
                    var clientImplementation = theClient.GetLocalTarget<RpcClientSample>();
                    await clientImplementation.RunUntilQuit(cts.Token);
                });

            // this is called when the client is stopping
            client.OnStop += _ => logger.Debug("Stopping client");


            Console.CancelKeyPress += (sender, eventArgs) => {
                eventArgs.Cancel = true;
                cts.Cancel();
            };

            await client.Run();

            return retCode;
        }

        private static bool ParseCommandLine(string[] args, out int retCode, out int verbosity, out int port)
        {
            retCode = 0;

            int verbose = 0;
            var showUsage = false;
            var port_ = Configuration.DefaultPort;
            verbosity = verbose;
            port = port_;

            // these are the available options, not that they set the variables
            var options = new OptionSet {
                { "v", "increase debug message verbosity", v => verbose += v != null ? -1 : 0 },
                { "h|help", "show this message and exit", h => showUsage = h != null },
                { "p=|port=", "Port", v => port_ = int.Parse(v) },
            };

            try
            {
                // parse the command line
                options.Parse(args);
            }
            catch (OptionException e)
            {
                Console.WriteLine(e.Message);
                showUsage = true;
                retCode = -1;
            }

            if (showUsage)
            {
                Console.WriteLine(options.ToString());
                return false;
            }

            verbosity = verbose;
            port = port_;
            return true;
        }
    }
}
