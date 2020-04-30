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
using Unity.Rpc;
using Unity.Rpc.Hosted;
using Unity.Rpc.Hosted.Extensions;

namespace HostedClientServer
{
    using System.IO;
    using ClientSample;
    using SpoiledCat.Extensions.Configuration;

    class HostedClientApp
    {
        static async Task Main(string[] args)
        {
            // monitoring when the rpc host shuts down
            var exiting = new CancellationTokenSource();

            // Read command line args and other settings to populate a configuration object
            var configuration = GetConfiguration(args);

            // our rpc host
            var host = new RpcHostedClient(configuration);

            // these are the RPC interfaces available on the server that the client can call
            host.AddRemoteProxy<IServerSingleton>()
                .AddRemoteProxy<IServerPerConnection>()
                .AddRemoteProxy<IClientInfo>();

            // this is the client implementation
            host.AddLocalTarget<RpcClientSample>();

            // callbacks for the client lifetime. These can be registered as events or callbacks
            // you can register multiple handlers for the same callback, they'll all be called

            // do something on start, like registering additional things
            host.OnStart += provider => provider.GetLogger<HostedClientApp>().LogDebug("Client starting");

            // do something on stop, like telling everyone else the client is stopping
            host.Stopping(provider => {
                var clientImplementation = provider.GetService<RpcClientSample>();
                provider.GetLogger<HostedClientApp>().LogDebug("Client stopping");
                exiting.Cancel();
                clientImplementation.Completion.Wait();
            });

            // Main body of the client. The client is connected to the server, do things
            host.Ready(async provider => {
                provider.GetLogger<HostedClientApp>().LogDebug("Client connected, do things");
                var clientImplementation = provider.GetService<RpcClientSample>();
                await clientImplementation.RunUntilQuit(exiting.Token);
            });

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

        private const string YamlSettingsFile = "appsettings.yaml";
        private const string JsonSettingsFile = "appsettings.json";
        // configure the host environment. this will be inherited into the app environment
        private static Configuration GetConfiguration(string[] args)
        {
            // configure app settings, merging settings from a json file, environment variables
            // and command line arguments into the configuration object

            var builder = new ConfigurationBuilder()
                          .SetBasePath(Directory.GetCurrentDirectory())
                          .AddJsonFile(JsonSettingsFile, optional: true, reloadOnChange: false)
                          .AddYamlFile(YamlSettingsFile, optional: true, reloadOnChange: false)
                          .AddEnvironmentVariables("HOSTEDSERVER_")
                          .AddExtendedCommandLine(args);
            var conf = builder.Build();
            var configuration = conf.Get<Configuration>() ?? new Configuration();
            return configuration;
        }

        private static void ConfigureLogging(RpcHostedClient host)
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
