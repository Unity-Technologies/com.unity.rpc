using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JobClient;
using JobServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Targets.Seq;
using NLog.Targets.Wrappers;
using Unity.Ipc.Client;
using Unity.Ipc.Server;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using LogLevel = NLog.LogLevel;

namespace ClientServerJobSample
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var logger = serviceProvider.GetService<ILogger<Program>>();

            var fileMutexPath = GetTemporaryDirectory();
            LogManager.Configuration = NLogConfigurator.ConfigureSeq(LogLevel.Debug);

            using (var server = new IpcServer<JobServerService, JobServerSession>(fileMutexPath, logger))
            {
                var serverTask = server.StartLocalTcp("Test.Server", 29000, new IpcVersion(1, 0, 0, 0), () =>
                {
                    var jobServerSession = new JobServerSession();
                    jobServerSession.RegisterDispatcher<BasicJob.BasicJob>("Unity.Basic.Job");
                    return jobServerSession;
                });

                var client = new IpcClient<JobClientService>(fileMutexPath, new NLogClientIpcLogger(logger));
                _ = await client.ConnectLocalTcp("Test.Server", 29000, 0, () =>
                {
                    var session = new JobClientSession();

                    session.JobStatusChangedEventHandler += (sender, data) =>
                    {
                        logger?.LogInformation("[{threadId}] Job [{jobId}] - status changed to {status}", Thread.CurrentThread.ManagedThreadId, data.JobId, data.Data);
                    };

                    session.JobProgressUpdatedEventHandler += (sender, data) =>
                    {
                        logger?.LogInformation("[{threadId}] Job [{jobId}] - in progress: {jobProgress}%", Thread.CurrentThread.ManagedThreadId, data.JobId, data.Data * 100);
                    };

                    session.JobCompletedEventHandler += (sender, data) =>
                    {
                        logger?.LogInformation("[{threadId}] Job [{jobId}] - job completed with status", Thread.CurrentThread.ManagedThreadId, data.JobId, data.Data.ToString());
                    };

                    return session;
                });

                var jobId = await client.CreateJob("Unity.Basic.Job", $"Job-{Guid.NewGuid()}", CancellationToken.None);
                var res = await client.StartJob(jobId, CancellationToken.None);

                jobId = await client.CreateJob("Unity.Basic.Job", $"Job-{Guid.NewGuid()}", CancellationToken.None);
                res = await client.StartJob(jobId, CancellationToken.None);

                Console.ReadLine();
            }
        }

        public static string GetTemporaryDirectory()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }

        private static void ConfigureServices(ServiceCollection services)
        {
            services.AddLogging(configure => configure.AddConsole().AddNLog()).Configure<LoggerFilterOptions>(options => options.MinLevel = Microsoft.Extensions.Logging.LogLevel.Debug);
        }
    }
    public static class NLogConfigurator
    {
        public static LoggingConfiguration ConfigureSeq(NLog.LogLevel level, string url = "http://localhost:5341", string apiKey = "", int bufferSize = 1000, int flushTimeout = 2000)
        {
            // Step 1. Create configuration object
            var config = new LoggingConfiguration();
            var seqTarget = new SeqTarget
            {
                ServerUrl = url,
                ApiKey = apiKey
            };

            var bufferWrapper = new BufferingTargetWrapper
            {
                Name = "seq",
                BufferSize = bufferSize,
                FlushTimeout = flushTimeout,
                WrappedTarget = seqTarget
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
    public class NLogClientIpcLogger : IIpcClientLogger
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
}
