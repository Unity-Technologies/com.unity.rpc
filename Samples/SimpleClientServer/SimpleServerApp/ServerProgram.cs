using Serilog.Core;
using Serilog.Events;
using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Rpc;
using Mono.Options;
using Serilog;
using Shared;

namespace SimpleClientServer
{
    using ServerSample;

    class ServerProgram
    {
        static async Task<int> Main(string[] args)
        {
            if (!ParseCommandLine(args, out int retCode, out int verbosity, out int port))
            {
                return retCode;
            }

            var logLevelSwitch = new LoggingLevelSwitch
            {
                MinimumLevel = (LogEventLevel)(verbosity < 0 ? 0 : verbosity)
            };

            var loggerConfiguration = new LoggerConfiguration()
                                      .WriteTo.Console(outputTemplate: "{Timestamp:HH:mm} [{Level}] ({Name:l}) {Message}{NewLine}{Exception}")
                                      .MinimumLevel.ControlledBy(logLevelSwitch);
            var logger = loggerConfiguration.CreateLogger();
            Log.Logger = logger;

            CancellationTokenSource cts = new CancellationTokenSource();

            var server = new RpcServer(new Configuration { Port = port }, cts.Token)

                         // register rpc proxy types to be dynamically generated when the server starts up
                         .RegisterRemoteTarget<IJobProgress>()

                         // this is called when the server is initialized
                         .Starting((registration, context) => {
                             // register a singleton object that will be available for all clients
                             registration.RegisterLocalTarget(new RpcServerSingleton());

                             logger.Debug("Starting server");
                         })

                         // this is called when the server stops
                         .Stopping(theServer => {
                             logger.Debug("Stopping server");
                         })

                         // This is called for every client connection
                         .ClientConnecting((registration, client) => {
                             logger.Debug($"Client {client.Id} connected");

                             var singleton = client.GetLocalTarget<RpcServerSingleton>();
                             singleton.AddClient(client.Id);
                             // register a per-client object that has a reference to the server singleton registered above, and a reference to the client
                             registration.RegisterLocalTarget(new RpcServerPerClientConnection(singleton, client));
                         })

                         // do some per client initialization
                         .ClientReady(client => {
                             logger.Debug($"Client {client.Id} ready");

                             var singleton = client.GetLocalTarget<RpcServerSingleton>();
                             singleton.RaiseOnClientConnected(client.Id);
                         })

                         // This is called for every client connection
                         .ClientDisconnecting((client, reason) => {
                             logger.Debug($"Client {client.Id} disconnected");
                         })

                ;


            Console.CancelKeyPress += (sender, eventArgs) => {
                eventArgs.Cancel = true;
                cts.Cancel();
            };

            await server.Run();

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
