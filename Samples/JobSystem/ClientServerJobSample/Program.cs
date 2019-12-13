using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JobClient;
using JobServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mono.Options;
using NLog;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Targets.Seq;
using NLog.Targets.Wrappers;
using Unity.Rpc;
using Unity.Rpc.Hosted;
using Unity.Rpc.Hosted.Extensions;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using LogLevel = NLog.LogLevel;

namespace ClientServerJobSample
{
    class Program
    {
        private const string AppSettingsFile = "appsettings.json";

        static async Task Main(string[] args)
        {
            var cts = new CancellationTokenSource();
            LogManager.Configuration = NLogConfigurator.ConfigureSeq(LogLevel.Debug);

            var conf = GetConfiguration(args);

            var server = ConfigureServer(conf, cts.Token)
                .Starting(provider => provider.GetLogger<Program>().LogDebug("Server starting"))
                .Stopping(provider => provider.GetLogger<Program>().LogDebug("Server stopping"))
                ;

            var client = ConfigureClient(conf, cts.Token)
                .Starting(provider => provider.GetLogger<Program>().LogDebug("Client starting"))
                .Stopping(provider => provider.GetLogger<Program>().LogDebug("Client stopping"))
                ;

            client.Ready(async provider => {
                var logger = provider.GetService<ILogger<Program>>();

                var clientService = provider.GetService<JobClientService>();
                clientService.JobStatusChangedEventHandler += (sender, data) => {
                    logger?.LogInformation("[{threadId}] Job [{jobId}] - status changed to {status}",
                        Thread.CurrentThread.ManagedThreadId, data.JobId, data.Data);
                };

                clientService.JobProgressUpdatedEventHandler += (sender, data) => {
                    logger?.LogInformation("[{threadId}] Job [{jobId}] - in progress: {jobProgress}%",
                        Thread.CurrentThread.ManagedThreadId, data.JobId, data.Data * 100);
                };

                clientService.JobCompletedEventHandler += (sender, data) => {
                    logger?.LogInformation("[{threadId}] Job [{jobId}] - job completed with status",
                        Thread.CurrentThread.ManagedThreadId, data.JobId, data.Data.ToString());
                };


                var jobid = await clientService.CreateJob("Unity.Basic.Job", $"job-{Guid.NewGuid()}", cts.Token);
                var res = await clientService.StartJob(jobid, cts.Token);

                jobid = await clientService.CreateJob("Unity.Basic.Job", $"job-{Guid.NewGuid()}", cts.Token);
                res = await clientService.StartJob(jobid, cts.Token);
            });

            await server.Start(cts.Token);
            await client.Run(cts.Token);
        }

        // configure the host environment. this will be inherited into the app environment
        private static Configuration GetConfiguration(string[] args)
        {
            // configure app settings, merging settings from a json file, environment variables
            // and command line arguments into the configuration object

            var builder = new ConfigurationBuilder()
                          .SetBasePath(Directory.GetCurrentDirectory())
                          .AddJsonFile(AppSettingsFile, optional: true, reloadOnChange: true)
                          .AddEnvironmentVariables("JOBSERVER_")
                          .AddCommandLine(args);
            var conf = builder.Build();
            var configuration = conf.Get<Configuration>();
            return configuration;
        }



        private static RpcHostedServer ConfigureServer(Configuration configuration, CancellationToken token)
        {
            return (RpcHostedServer) new RpcHostedServer(configuration)
                .AddRemoteProxy<IJobClientService>()
                .AddLocalTarget(provider => {
                    var service = new JobServerService(token);
                    service.RegisterDispatcher<BasicJob.BasicJob>("Unity.Basic.Job");
                    return service;
                })
                .AddLocalScoped<JobServerSession>()
                .ConfigureServices(ConfigureLogging)
                .UseConsoleLifetime();
        }


        private static RpcHostedClient ConfigureClient(Configuration configuration, CancellationToken token)
        {
            return (RpcHostedClient) new RpcHostedClient(configuration)
                         .AddRemoteProxy<IJobServerService>()
                         .AddLocalTarget<JobClientService>()
                         .ConfigureServices(ConfigureLogging)
                         .UseConsoleLifetime();

        }

        public static string GetTemporaryDirectory()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }

        private static void ConfigureLogging(HostBuilderContext ctx, IServiceCollection services)
        {
            services.AddLogging(configure => configure.AddConsole().AddNLog()).Configure<LoggerFilterOptions>(options =>
                options.MinLevel = Microsoft.Extensions.Logging.LogLevel.Debug);
        }
    }



    public static class NLogConfigurator
    {
        public static LoggingConfiguration ConfigureSeq(NLog.LogLevel level,
            string url = "http://localhost:5341",
            string apiKey = "",
            int bufferSize = 1000,
            int flushTimeout = 2000)
        {
            // Step 1. Create configuration object
            var config = new LoggingConfiguration();
            var seqTarget = new SeqTarget { ServerUrl = url, ApiKey = apiKey };

            var bufferWrapper = new BufferingTargetWrapper {
                Name = "seq", BufferSize = bufferSize, FlushTimeout = flushTimeout, WrappedTarget = seqTarget
            };

            config.AddTarget(bufferWrapper);

            // Step 4. Define rules
            var rule1 = new LoggingRule("*", level, bufferWrapper);
            config.LoggingRules.Add(rule1);

            // Step 5. Activate the configuration
            LogManager.Configuration = config;

            return config;
        }
    }

    public class NLogClientRpcLogger
    {
        private readonly ILogger _logger;

        public NLogClientRpcLogger(ILogger logger)
        {
            _logger = logger;
        }

        public void LogError(Exception exception, string message, params object[] args)
        {
            _logger.LogError(0, exception, message, args);
        }

        public void LogDebug(Exception exception, string message, params object[] args)
        {
            _logger.LogDebug(0, exception, message, args);
        }

        public void LogInformation(Exception exception, string message, params object[] args)
        {
            _logger.LogInformation(0, exception, message, args);
        }

        public void LogError(string message, params object[] args)
        {
            _logger.LogError(message, args);
        }

        public void LogDebug(string message, params object[] args)
        {
            _logger.LogDebug(message, args);
        }

        public void LogInformation(string message, params object[] args)
        {
            _logger.LogInformation(message, args);
        }
    }

    static class HelperExtensions
    {
        public static void Forget(this Task task)
        { }
    }
}
