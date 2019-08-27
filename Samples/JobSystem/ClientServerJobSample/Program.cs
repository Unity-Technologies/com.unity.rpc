using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JobClient;
using JobServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Targets.Seq;
using NLog.Targets.Wrappers;
using Unity.Ipc;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using LogLevel = NLog.LogLevel;
using IpcServerHost = Unity.Ipc.Hosted.Server.IpcHost;
using IpcClientHost = Unity.Ipc.Hosted.Client.IpcHost;

namespace ClientServerJobSample
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var cts = new CancellationTokenSource();
            LogManager.Configuration = NLogConfigurator.ConfigureSeq(LogLevel.Debug);

            var serverHost = ConfigureServer(cts);

            await serverHost.Start(cts.Token);
            ThreadPool.QueueUserWorkItem(s => ((IpcServerHost)s).Run(), serverHost);

            var clientHost = ConfigureClient();

            var ipcClient = await clientHost.Start(cts.Token);
            var logger = clientHost.ServiceProvider.GetService<ILogger<Program>>();

            var client = ipcClient.GetLocalTarget<JobClientService>();
            client.JobStatusChangedEventHandler += (sender, data) => {
                logger?.LogInformation("[{threadId}] Job [{jobId}] - status changed to {status}",
                    Thread.CurrentThread.ManagedThreadId, data.JobId, data.Data);
            };

            client.JobProgressUpdatedEventHandler += (sender, data) => {
                logger?.LogInformation("[{threadId}] Job [{jobId}] - in progress: {jobProgress}%",
                    Thread.CurrentThread.ManagedThreadId, data.JobId, data.Data * 100);
            };

            client.JobCompletedEventHandler += (sender, data) => {
                logger?.LogInformation("[{threadId}] Job [{jobId}] - job completed with status",
                    Thread.CurrentThread.ManagedThreadId, data.JobId, data.Data.ToString());
            };


            var jobid = await client.CreateJob("Unity.Basic.Job", $"job-{Guid.NewGuid()}", cts.Token);
            var res = await client.StartJob(jobid, cts.Token);

            jobid = await client.CreateJob("Unity.Basic.Job", $"job-{Guid.NewGuid()}", cts.Token);
            res = await client.StartJob(jobid, cts.Token);

            await clientHost.Run();

        }

        private static IpcClientHost ConfigureClient()
        {
            var clientHost = new IpcClientHost(29000, IpcVersion.Parse("1.1"));
            clientHost.Configuration.AddLocalTarget<JobClientService>();
            clientHost.Configuration.AddRemoteTarget<IJobServerService>();

            clientHost
                .UseConsoleLifetime()
                .ConfigureServices((context, services) => ConfigureServices(services))
                ;
            return clientHost;
        }

        private static IpcServerHost ConfigureServer(CancellationTokenSource cts)
        {
            var serverHost = new IpcServerHost(29000, IpcVersion.Parse("1.1"));
            serverHost.Configuration.AddLocalTarget<JobServerSession>();
            serverHost.Configuration.AddRemoteTarget<IJobClientService>();

            serverHost
                .UseConsoleLifetime()
                .ConfigureServices((context, services) => {
                    ConfigureServices(services);

                    var serverService = new JobServerService(cts.Token);
                    serverService.RegisterDispatcher<BasicJob.BasicJob>("Unity.Basic.Job");
                    services.AddSingleton(serverService);
                })
                ;

            return serverHost;
        }

        public static string GetTemporaryDirectory()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }

        private static void ConfigureServices(IServiceCollection services)
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

    public class NLogClientIpcLogger
    {
        private readonly ILogger _logger;

        public NLogClientIpcLogger(ILogger logger)
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
