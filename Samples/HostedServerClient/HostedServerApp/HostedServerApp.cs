using System;
using System.IO;
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
    class HostedServerApp
    {
        private const string AppSettingsFile = "appsettings.json";

        static async Task Main(string[] args)
        {
            // monitoring when the ipc host shuts down
            var exiting = new CancellationTokenSource();

            // default configuration object. the values in it will be set by the ConfigureAppConfiguration call below
            var configuration = GetConfiguration(args);

            // our ipc host
            var host = new IpcHostedServer(configuration);

            // these are the IPC interfaces available on the client that the server can call
            host.AddRemoteProxy<IIpcJobProgress>();

            // This is a singleton instance that all clients can call.
            host.AddLocalTarget<IpcServerSingleton>();

            // This is an instance that will be available per client
            host.AddLocalScoped<IpcServerPerClientConnection>();

            // callbacks for the client lifetime. These can also be registered as events
            // you can register multiple handlers for the same callback, they'll all be called

            // do something on start, like registering additional things
            // if there's something that needs to be started up, this is the place
            // this can also be a callback instead of an event
            host.OnStart += provider => provider.GetLogger<HostedServerApp>().LogDebug($"Server starting on port {configuration.Port}");

            // do something on stop, like telling everyone else the client is stopping
            host.Stopping(provider => provider.GetLogger<HostedServerApp>().LogDebug("Server stopping"));

            // A client connected to the server. Do any additional registration/setup per client
            host.ClientConnecting(provider => provider.GetLogger<HostedServerApp>().LogDebug($"client {provider.GetRequestContext().Id} connecting"));

            // A client is ready to go
            host.ClientReady(provider => provider.GetLogger<HostedServerApp>().LogDebug($"client {provider.GetRequestContext().Id} ready"));

            // A client is disconnecting
            host.ClientDisconnecting((provider, reason) => provider.GetLogger<HostedServerApp>().LogDebug($"client {provider.GetRequestContext().Id} disconnected because {reason.Description}"));

            ConfigureLogging(host);

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
        private static Configuration GetConfiguration(string[] args)
        {
            // configure app settings, merging settings from a json file, environment variables
            // and command line arguments into the configuration object

            var builder = new ConfigurationBuilder()
                          .SetBasePath(Directory.GetCurrentDirectory())
                          .AddJsonFile(AppSettingsFile, optional: true, reloadOnChange: true)
                          .AddEnvironmentVariables("HOSTEDSERVER_")
                          .AddCommandLine(args);
            var conf = builder.Build();
            var configuration = conf.Get<Configuration>();
            return configuration;
        }

        private static void ConfigureLogging(IpcHostedServer host)
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
