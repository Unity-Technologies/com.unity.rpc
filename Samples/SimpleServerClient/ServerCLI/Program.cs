using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using ServerImplementation;
using TestApp;
using Unity.Ipc;

namespace ServerApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var logLevelSwitch = new LoggingLevelSwitch
            {
                MinimumLevel = LogEventLevel.Verbose
            };

            var host = new IpcHost();
            host.Configuration.AddLocalTarget<ServerMessageImplementation>();
            host.Configuration.AddRemoteTarget<IMyClient>();

            host.UseSerilog((context, config) =>
                       config.MinimumLevel.ControlledBy(logLevelSwitch)
                             .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                             .Enrich.FromLogContext()
                             .WriteTo.Console())
                   .UseConsoleLifetime()
                ;

            try
            {
                await host.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}
