using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Shared;
using Unity.Ipc;
using Unity.Ipc.Hosted;
using Unity.Ipc.Hosted.Extensions;

namespace HostedClientServer
{
    class HostedClientApp
    {
        private const string AppSettingsFile = "appsettings.json";

        static async Task Main(string[] args)
        {
            // monitoring when the ipc host shuts down
            var exiting = new CancellationTokenSource();

            // default configuration object. the values in it will be set by the ConfigureAppConfiguration call below
            var configuration = new Configuration();

            // our ipc host
            var host = new IpcHostedClient(configuration);

            // these are the IPC interfaces available on the server that the client can call
            host.AddRemoteProxy<IIpcServerSingleton>()
                .AddRemoteProxy<IIpcServerPerConnection>()
                .AddRemoteProxy<IIpcClientInfo>();

            // this is the client implementation
            host.AddLocalTarget<IpcClientSample>();

            // callbacks for the client lifetime. These can be registered as events or callbacks
            // you can register multiple handlers for the same callback, they'll all be called

            // do something on start, like registering additional things
            host.OnStart += provider => provider.GetLogger<HostedClientApp>().LogDebug("Client starting");

            // do something on stop, like telling everyone else the client is stopping
            host.Stopping(provider => {
                var clientImplementation = provider.GetService<IpcClientSample>();
                provider.GetLogger<HostedClientApp>().LogDebug("Client stopping");
                exiting.Cancel();
                clientImplementation.Completion.Wait();
            });

            // Main body of the client. The client is connected to the server, do things
            host.Ready(async provider => {
                provider.GetLogger<HostedClientApp>().LogDebug("Client connected, do things");
                var clientImplementation = provider.GetService<IpcClientSample>();
                await clientImplementation.RunUntilQuit(exiting.Token);
            });

            ConfigureLogging(host);
            SetupPortAndOtherSettings(host, configuration, args);

            // set up a console lifetime so it handles ctrl+c and app shutdown
            host.UseConsoleLifetime();

            try
            {
                // run the thing. this won't return until the client shuts down
                await host.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        // configure the host environment. this will be inherited into the app environment
        private static void SetupPortAndOtherSettings(IpcHostedClient host, Configuration configuration, string[] args)
        {
            // configure app settings, merging settings from a json file, environment variables
            // and command line arguments into the configuration object

            host.ConfigureHostConfiguration(c => c.AddEnvironmentVariables("HOSTEDSERVER_"));
            host.ConfigureAppConfiguration((context, app) => {
                var env = context.HostingEnvironment;

                app.AddJsonFile(AppSettingsFile, optional: true, reloadOnChange: true);
                app.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);
                app.AddEnvironmentVariables();
                app.AddCommandLine(args);

                var confRoot = app.Build();
                confRoot.Bind(configuration);
            });
        }

        private static void ConfigureLogging(IpcHostedClient host)
        {
            var logLevelSwitch = new LoggingLevelSwitch { MinimumLevel = LogEventLevel.Debug };

            // set up a logger
            host.UseSerilog((context, config) =>
                config.MinimumLevel.ControlledBy(logLevelSwitch)
                      .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                      .Enrich.FromLogContext()
                      .WriteTo.Console());

            // register the log switch so it can be retrieved and changed by any code
            host.ConfigureServices((context, services) => services.AddSingleton(logLevelSwitch));
        }
    }
}
