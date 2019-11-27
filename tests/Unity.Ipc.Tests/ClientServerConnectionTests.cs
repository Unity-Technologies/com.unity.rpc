using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using Unity.Ipc.Hosted;
using Xunit;
using Xunit.Abstractions;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using LogFactory = Divergic.Logging.Xunit.LogFactory;
using LogLevel = NLog.LogLevel;
using Microsoft.Extensions.DependencyInjection;

namespace Unity.Ipc.Tests
{
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


    public interface IMathSession
    {
        Task<int> Add(int opA, int opB);
    }

    public class BasicMathService : IMathSession
    {
        public Task<int> Add(int opA, int opB)
        {
            return Task.FromResult(opA + opB);
        }
    }

    public class GetUniqueValues
    {
        private int _currentPort = 54999;

        public int NextTcpPort()
        {
            return Interlocked.Increment(ref _currentPort);
        }

        public int NextTcpPortWithRange(int range)
        {
            return Interlocked.Add(ref _currentPort, range) - (range - 1);
        }

        private int _serverNumber = 1;

        public string NextServerName()
        {
            return $"Server_{Interlocked.Increment(ref _serverNumber)}";
        }
    }

    public class MathSession
    {
        public int LastResult;
    }

    public class Helpers
    {
        public static Configuration GetConfiguration(int port, int protocol) =>
            new Configuration { Port = port, ProtocolVersion = IpcVersion.Parse(protocol.ToString()) };

        public static async Task<IpcHostedServer> NewServer<T>(Configuration configuration, CancellationTokenSource cts)
            where T : class
        {
            return await new IpcHostedServer(configuration)
                               .AddLocalTarget<T>()
                               .Start(cts.Token);
        }

        public static async Task<IRequestContext> NewClient<T>(Configuration configuration, CancellationTokenSource cts)
            where T : class
        {
            var client = await new IpcHostedClient(configuration)
                               .AddRemoteProxy<T>()
                               .Start(cts.Token);
            return client.ServiceProvider.GetService<IRequestContext>();
        }
    }

    public class ClientServerConnectionTests : IClassFixture<GetUniqueValues>
    {
        private GetUniqueValues _uniqueValues;
        private readonly ITestOutputHelper _output;
        private readonly ILogger _serverLogger;
        private readonly NLogClientIpcLogger _clientLogger;

        public ClientServerConnectionTests(GetUniqueValues uniqueValues, ITestOutputHelper output)
        {
            _uniqueValues = uniqueValues;
            _output = output;

            NLogConfigurator.ConfigureSeq(LogLevel.Debug);

            var loggerFactory = LogFactory.Create(_output).AddNLog();

            _serverLogger = loggerFactory.CreateLogger("Unity.Ipc.Server");
            _clientLogger = new NLogClientIpcLogger(loggerFactory.CreateLogger("Unity.Ipc.Client"));
        }

        public string GetTemporaryDirectory()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }

        [Fact]
        public async void SingleClientServerHandshake()
        {
            var cts = new CancellationTokenSource();

            var protocolRevision = 2;
            var tcpPort = _uniqueValues.NextTcpPortWithRange(protocolRevision + 1);

            var configuration = Helpers.GetConfiguration(tcpPort, protocolRevision);
            var server = await Helpers.NewServer<BasicMathService>(configuration, cts);

            var clientA = await Helpers.NewClient<IMathSession>(configuration, cts);
            var mathSessionA = clientA.GetRemoteTarget<IMathSession>();

            var clientB = await Helpers.NewClient<IMathSession>(configuration, cts);
            var mathSessionB = clientB.GetRemoteTarget<IMathSession>();

            int retAdd = await mathSessionA.Add(9, 11);
            Assert.Equal(9 + 11, retAdd);

            retAdd = await mathSessionB.Add(3, 12);
            Assert.Equal(3 + 12, retAdd);


            var stopTask = server.Stop();
            bool timedout = (await Task.WhenAny(stopTask, Task.Delay(100))) != stopTask;
            Assert.False(timedout, "Server didn't shutdown in an acceptable timely fashion");
        }

        [Fact]
        public async void SingleClientNoServer()
        {
            var tcpPort = _uniqueValues.NextTcpPort();
            var client = new IpcClient(Helpers.GetConfiguration(tcpPort, 1));
            await Assert.ThrowsAnyAsync<SocketException>(() => client.Run());
        }
    }
}
