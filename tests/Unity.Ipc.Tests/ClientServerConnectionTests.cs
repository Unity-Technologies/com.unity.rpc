using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NLog.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using LogFactory = Divergic.Logging.Xunit.LogFactory;
using LogLevel = NLog.LogLevel;

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

            var serverHost = new IpcHost(tcpPort);
            serverHost.Configuration.AddLocalTarget<BasicMathService>();
            ThreadPool.QueueUserWorkItem(async _ => await serverHost.Run(cts.Token));

            var clientA = await NewClient(tcpPort, cts);
            var mathSessionA = clientA.GetRemoteTarget<IMathSession>();

            var clientB = await NewClient(tcpPort, cts);
            var mathSessionB = clientB.GetRemoteTarget<IMathSession>();

            int retAdd = await mathSessionA.Add(9, 11);
            Assert.Equal(9 + 11, retAdd);

            retAdd = await mathSessionB.Add(3, 12);
            Assert.Equal(3 + 12, retAdd);


            var stopTask = serverHost.Stop();
            bool timedout = (await Task.WhenAny(stopTask, Task.Delay(100))) != stopTask;
            Assert.False(timedout, "Server didn't shutdown in an acceptable timely fashion");
        }

        private static async Task<IpcClient> NewClient(int tcpPort, CancellationTokenSource cts)
        {
            var clientHost = new IpcHostClient(tcpPort);
            clientHost.Configuration.AddRemoteTarget<IMathSession>();
            return await clientHost.Start(cts.Token);
        }

        //[Fact]
        //public void ServerAlreadyExist()
        //{
        //    var tcpPortA = _uniqueValues.NextTcpPortWithRange(2);
        //    var tcpPortB = tcpPortA + 1;

        //    var temporaryDirectory = GetTemporaryDirectory();

        //    using (var serverA = new IpcServer<BasicMathService>(temporaryDirectory, _serverLogger))
        //    using (var serverB = new IpcServer<BasicMathService>(temporaryDirectory, _serverLogger))
        //    {
        //        var name = _uniqueValues.NextServerName();
        //        var version = new IpcVersion(1, 0, 0, 0);

        //        serverA.StartLocalTcp(name, tcpPortA, version);

        //        // Give time to Server A to start...Async stuff...
        //        Thread.Sleep(100);

        //        void TestCode() => serverB.StartLocalTcp(name, tcpPortB, version);

        //        Assert.Throws<IpcServerAlreadyExistsException>(() => TestCode());

        //    }
        //}

        [Fact]
        public async void SingleClientNoServer()
        {
            var tcpPort = _uniqueValues.NextTcpPort();
            var client = new IpcHostClient(tcpPort);
            await Assert.ThrowsAnyAsync<SocketException>(() => client.Start());
        }

        //[Fact]
        //public void SingleClientOnServerWithOffsetPort()
        //{
        //    // Get a TCP Port to use for the test
        //    var tcpPort = _uniqueValues.NextTcpPortWithRange(2);
        //    var temporaryDirectory = GetTemporaryDirectory();

        //    // Open a fake socket to occupy this port
        //    using (var fakeSocket = new Socket(SocketType.Stream, ProtocolType.Tcp))
        //    {
        //        // Bind the socket to the port to open it
        //        fakeSocket.Bind(new IPEndPoint(IPAddress.Loopback, tcpPort));

        //        // Now start our server, it should work but the port should not be 'tcpPort' but +1
        //        using (var server = new IpcServer<BasicMathService>(temporaryDirectory, _serverLogger))
        //        {
        //            var serverTask = server.StartLocalTcp(_uniqueValues.NextServerName(), tcpPort, new IpcVersion(1, 0, 0, 0));

        //            // Compare the TCP Port of our started server
        //            Assert.Equal(tcpPort + 1, server.TcpPort);
        //        }
        //    }
        //}

        //[Fact]
        //public async void ServerEndedByClient()
        //{
        //    var tcpPort = _uniqueValues.NextTcpPort();

        //    var fileMutexPath = GetTemporaryDirectory();
        //    using (var server = new IpcServer<BasicMathService, MathSession>(fileMutexPath,_serverLogger))
        //    {
        //        var serverName = _uniqueValues.NextServerName();
        //        var serverTask = server.StartLocalTcp(serverName, tcpPort, new IpcVersion(1, 0, 5, 0), () => new MathSession());
        //        var client = new IpcClient(fileMutexPath, _clientLogger);
        //        var version = await client.ConnectLocalTcp(serverName, tcpPort, 0);

        //        var rm = await client.JsonRpcClient.SendRequestAsync("add", JToken.FromObject(new { opA = 9, opB = 11 }), CancellationToken.None);
        //        Assert.Null(rm.Error);
        //        Assert.Equal(20, rm.Result.ToObject<int>());

        //        client.StopClient();

        //        Thread.Sleep(2_000);

        //        Assert.Equal(0, server.ClientCount);

        //        server.StopServer();
        //        Assert.True(serverTask.Wait(10_000), "Server didn't shutdown in an acceptable timely fashion");
        //    }
        //}

        //[Fact]
        //public async void ClientEndedByServer()
        //{
        //    var tcpPort = _uniqueValues.NextTcpPort();

        //    var fileMutexPath = GetTemporaryDirectory();
        //    using (var server = new IpcServer<BasicMathService, MathSession>(fileMutexPath, _serverLogger))
        //    {
        //        var serverName = _uniqueValues.NextServerName();
        //        var serverTask = server.StartLocalTcp(serverName, tcpPort, new IpcVersion(1, 0, 5, 0), () => new MathSession());
        //        var client = new IpcClient(fileMutexPath, _clientLogger);
        //        var version = await client.ConnectLocalTcp(serverName, tcpPort, 0);

        //        var rm = await client.JsonRpcClient.SendRequestAsync("add", JToken.FromObject(new { opA = 9, opB = 11 }), CancellationToken.None);
        //        Assert.Null(rm.Error);
        //        Assert.Equal(20, rm.Result.ToObject<int>());

        //        server.StopServer();

        //        Thread.Sleep(2_000);

        //        Assert.False(client.ShouldBeAvailable);
        //    }
        //}

        //[Fact]
        //public async void BigCheesyClientServerHandshake()
        //{
        //    var sockets = new List<Socket>();
        //    var temporaryDirectory = GetTemporaryDirectory();
        //    var servers = new List<IpcServer<BasicMathService>>();
        //    try
        //    {
        //        // Get the starting TCP Port
        //        var tcpPort = _uniqueValues.NextTcpPortWithRange(100) + 50;

        //        // Let's create many socket to make these port busy
        //        // -X--XXX--   (- = open, X = busy)

        //        var busyOffsets = new int[] {1, 4, 5, 6};
        //        foreach (var busyOffset in busyOffsets)
        //        {
        //            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        //            socket.Bind(new IPEndPoint(IPAddress.Loopback, tcpPort + busyOffset));
        //            sockets.Add(socket);
        //        }

        //        // Generate a server name, it's the same for all the ones we'll create, only the protocol changes
        //        var serverName = _uniqueValues.NextServerName();



        //        // Let's create some server in a non sequential ProtocolRevision order
        //        var protocolRevisions = new int[] {3, 2, 4, 0, 1};
        //        foreach (var protocolRevision in protocolRevisions)
        //        {
        //            var server = new IpcServer<BasicMathService>(temporaryDirectory, _serverLogger);
        //            _ = server.StartLocalTcp(serverName, tcpPort, new IpcVersion(1, 0, 0, protocolRevision));
        //            servers.Add(server);
        //        }

        //        // Now checks our servers are allocated at the expected
        //        // -X--XXX--   (- = open, X = busy)
        //        // 0 23   41   (expected protocol revision at each position)

        //        Assert.Equal(0, servers[3].TcpPort - tcpPort);
        //        Assert.Equal(8, servers[4].TcpPort - tcpPort);
        //        Assert.Equal(2, servers[1].TcpPort - tcpPort);
        //        Assert.Equal(3, servers[0].TcpPort - tcpPort);
        //        Assert.Equal(7, servers[2].TcpPort - tcpPort);

        //        // Now let's connect clients and check they have the expected Protocol version
        //        for (int i = 0; i < 5; i++)
        //        {
        //            var client = new IpcClient(temporaryDirectory, _clientLogger);
        //            var version = await client.ConnectLocalTcp(serverName, tcpPort, i);
        //            Assert.Equal(i, version.ProtocolRevision);
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        _ = e.ToString();
        //    }
        //    finally
        //    {
        //        sockets.ForEach(s => s.Dispose());
        //        sockets.Clear();

        //        servers.ForEach(async s =>
        //        {
        //            s.StopServer();
        //            await s.ServerHostingTask;
        //            s.Dispose();
        //        });
        //        servers.Clear();
        //    }
        //}

        //#region AccumulatedAdd test: two ways communication

        //public class AccumulatedAddClientSession : IDisposable
        //{
        //    public AccumulatedAddClientSession(IIpcClientLogger logger)
        //    {
        //        Logger = logger;
        //    }
        //    public IIpcClientLogger Logger;
        //    public int Result = 0;

        //    public bool IsDisposed { get; private set; }

        //    public void Dispose()
        //    {
        //        IsDisposed = true;
        //    }
        //}

        //public class AccumulatedAddServerSession : ServerSession
        //{
        //    public ILogger Logger { get; }

        //    public AccumulatedAddServerSession(ILogger logger)
        //    {
        //        Logger = logger;
        //    }
        //}

        //public class AccumulatedAddClientService : IpcClientService
        //{
        //    public static AutoResetEvent Event = new AutoResetEvent(false);

        //    [JsonRpcMethod]
        //    public Task Result(int result)
        //    {
        //        // Get the client side session object
        //        var session = RequestContext.Features.Get<AccumulatedAddClientSession>();

        //        session.Logger.LogDebug("Get Result to acc and store {r}", result);

        //        // The unit test will make two calls to this method, we want to raise the event on the second call, so detect if we're calling this method the first time (Result is 0) or the second (result not zero)
        //        var first = session.Result == 0;

        //        // Accumulate result in the session object
        //        session.Result += result;

        //        // If it's not the first call to this method raise the event to resume the unit test execution.
        //        if (!first)
        //        {
        //            Event.Set();
        //        }
        //        return Task.CompletedTask;
        //    }
        //}

        //public class AccumulatedAddServerService : IpcServerServiceBase<AccumulatedAddServerSession, ClientSession<AccumulatedAddServerSession>>
        //{
        //    // This method does not return a value, but calls a client side method that will store the result in the client side session object
        //    [JsonRpcMethod]
        //    public async Task Add(int opA, int opB)
        //    {
        //        ServerSession.Logger?.LogDebug("Getting add operation with {a} and {b}", opA, opB);
        //        var res = opA + opB;

        //        await Task.Run(async () =>
        //        {
        //            await Task.Delay(10);
        //            await ClientSession.JsonRpcClient.ExecClientRequest("result", JToken.FromObject(new { result = res }), CancellationToken.None);
        //        });
        //    }
        //}

        //public async void TwoWaysCommunication()
        //{
        //    var protocolRevision = 0;
        //    var tcpPort = _uniqueValues.NextTcpPort();

        //    var fileMutexPath = GetTemporaryDirectory();

        //    // The client side session object that we'll use to accumulate and store the result of our add operations
        //    var clientSideSession = new AccumulatedAddClientSession(_clientLogger);

        //    using (var server = new IpcServer<AccumulatedAddServerService, AccumulatedAddServerSession, ClientSession<AccumulatedAddServerSession>>(fileMutexPath, _serverLogger))
        //    {
        //        var serverName = _uniqueValues.NextServerName();
        //        var serverTask = server.StartLocalTcp(serverName, tcpPort, new IpcVersion(1, 0, 5, protocolRevision), () =>
        //        {
        //            var serverSession = new AccumulatedAddServerSession(_serverLogger);
        //            serverSession.NewClientConnected += (sender, info) =>
        //            {
        //                _serverLogger?.LogInformation(" ServerSession Notification: New Client Connected {client}", info.RemoteEndPoint);
        //            };
        //            serverSession.ClientDisconnected += (sender, info) =>
        //            {
        //                _serverLogger?.LogInformation("ServerSession Notification: Client Disconnected {Client}", info.RemoteEndPoint);
        //            };
        //            return serverSession;
        //        }, () => new ClientSession<AccumulatedAddServerSession>());

        //        using (var client = new IpcClient<AccumulatedAddClientService>(fileMutexPath, _clientLogger))
        //        {
        //            _ = await client.ConnectLocalTcp(serverName, tcpPort, protocolRevision, () => clientSideSession);

        //            _clientLogger.LogDebug("Start sending add requests");

        //            _ = await client.JsonRpcClient.SendRequestAsync("add", JToken.FromObject(new { opA = 12, opB = 23 }), CancellationToken.None);
        //            _ = await client.JsonRpcClient.SendRequestAsync("add", JToken.FromObject(new { opA = 2, opB = 9}), CancellationToken.None);
        //            _clientLogger.LogDebug("Add requests sent, waiting for client service to release");

        //            if (AccumulatedAddClientService.Event.WaitOne(TimeSpan.FromSeconds(5)))
        //            {
        //                _clientLogger.LogDebug("Client signaled release");
        //            }
        //            else
        //            {
        //                _clientLogger.LogError("Client failed to signal release");
        //            }

        //            Assert.Equal(46, clientSideSession.Result);
        //        }
        //    }
        //}
        //#endregion

        //#region Session tests

        //public class ASTSServer : ServerSession
        //{
        //    public int ServerWideValue;
        //}

        //public class ASTSClient : ClientSession<ASTSServer>
        //{
        //    public int ClientWideValue;
        //    protected override void OnDispose()
        //    {

        //    }
        //}

        //public class AllSessionTypesService : IpcServerServiceBase<ASTSServer, ASTSClient>
        //{
        //    [JsonRpcMethod]
        //    public Task<int> SetServerWideValue(int value)
        //    {
        //        var curValue = ServerSession.ServerWideValue;
        //        ServerSession.ServerWideValue = value;
        //        return Task.FromResult(curValue);
        //    }

        //    [JsonRpcMethod]
        //    public Task<int> IncrementServerWideValue()
        //    {
        //        return Task.FromResult(Interlocked.Increment(ref ServerSession.ServerWideValue));
        //    }

        //    [JsonRpcMethod]
        //    public Task<int> SetClientWideValue(int value)
        //    {
        //        var curValue = ClientSession.ClientWideValue;
        //        ClientSession.ClientWideValue = value;
        //        return Task.FromResult(curValue);
        //    }

        //    [JsonRpcMethod]
        //    public Task<int> IncrementClientWideValue()
        //    {
        //        return Task.FromResult(Interlocked.Increment(ref ClientSession.ClientWideValue));
        //    }

        //}

        //[Fact]
        //public async void ServerWideSessionTest()
        //{
        //    var protocolRevision = 0;
        //    var tcpPort = _uniqueValues.NextTcpPort();
        //    var temporaryDirectory = GetTemporaryDirectory();

        //    using (var server = new IpcServer<AllSessionTypesService, ASTSServer, ASTSClient>(temporaryDirectory, _serverLogger))
        //    {
        //        var serverName = _uniqueValues.NextServerName();
        //        var serverTask = server.StartLocalTcp(serverName, tcpPort, new IpcVersion(1, 0, 5, protocolRevision),
        //            () => new ASTSServer(), () => new ASTSClient());

        //        var currentExpectedValue = 10;

        //        using (var client = new IpcClient(temporaryDirectory, _clientLogger))
        //        {
        //            _ = await client.ConnectLocalTcp(serverName, tcpPort, protocolRevision);
        //            var val = await client.ExecRequest<int>("setServerWideValue", new {value = 10}, CancellationToken.None);
        //            Assert.Equal(0, val);

        //            for (int i = 0; i < 10; i++)
        //            {
        //                val = await client.ExecRequest<int>("incrementServerWideValue", null, CancellationToken.None);
        //                Assert.Equal(++currentExpectedValue, val);
        //            }
        //        }

        //        using (var client = new IpcClient(temporaryDirectory, _clientLogger))
        //        {
        //            _ = await client.ConnectLocalTcp(serverName, tcpPort, protocolRevision);
        //            for (int i = 0; i < 10; i++)
        //            {
        //                var val = await client.ExecRequest<int>("incrementServerWideValue", null, CancellationToken.None);
        //                Assert.Equal(++currentExpectedValue, val);

        //            }
        //        }
        //    }
        //}

        //[Fact]
        //public async void ClientWideSessionTest()
        //{
        //    var protocolRevision = 0;
        //    var tcpPort = _uniqueValues.NextTcpPort();
        //    var temporaryDirectory = GetTemporaryDirectory();

        //    using (var server = new IpcServer<AllSessionTypesService, ASTSServer, ASTSClient>(temporaryDirectory, _serverLogger))
        //    {
        //        var serverName = _uniqueValues.NextServerName();
        //        var serverTask = server.StartLocalTcp(serverName, tcpPort, new IpcVersion(1, 0, 5, protocolRevision),
        //            () => new ASTSServer(), () => new ASTSClient());

        //        var currentExpectedValue = 10;

        //        using (var client = new IpcClient(temporaryDirectory, _clientLogger))
        //        {
        //            _ = await client.ConnectLocalTcp(serverName, tcpPort, protocolRevision);
        //            var val = await client.ExecRequest<int>("setClientWideValue", new { value = 10 }, CancellationToken.None);
        //            Assert.Equal(0, val);

        //            for (int i = 0; i < 10; i++)
        //            {
        //                val = await client.ExecRequest<int>("incrementClientWideValue", null, CancellationToken.None);
        //                Assert.Equal(++currentExpectedValue, val);
        //            }

        //            // I/O need to complete in order to kill the client
        //            Thread.Sleep(5);
        //        }

        //        currentExpectedValue = 0;

        //        using (var client = new IpcClient(temporaryDirectory, _clientLogger))
        //        {
        //            _ = await client.ConnectLocalTcp(serverName, tcpPort, protocolRevision);
        //            for (int i = 0; i < 10; i++)
        //            {
        //                var val = await client.ExecRequest<int>("incrementClientWideValue", null, CancellationToken.None);
        //                Assert.Equal(++currentExpectedValue, val);

        //            }

        //            // I/O need to complete in order to kill the client
        //            Thread.Sleep(5);
        //        }
        //    }
        //}

        //public class BasicMathServiceWithSession : IpcServerServiceBase<SessionTest>
        //{
        //    [JsonRpcMethod]
        //    public Task<int> Add(int opA, int opB)
        //    {
        //        return Task.FromResult(opA + opB);
        //    }
        //}

        //public class SessionTest : ServerSession
        //{
        //}

        //[Fact]
        //public async void ServerSideSessionDisposed()
        //{
        //    var protocolRevision = 0;
        //    var tcpPort = _uniqueValues.NextTcpPort();

        //    var fileMutexPath = GetTemporaryDirectory();
        //    var server = new IpcServer<BasicMathServiceWithSession, SessionTest>(fileMutexPath, _serverLogger);
        //    var serverName = _uniqueValues.NextServerName();
        //    var serverSession = new SessionTest();
        //    var serverTask = server.StartLocalTcp(serverName, tcpPort, new IpcVersion(1, 0, 5, protocolRevision), () => serverSession);

        //    Assert.False(serverSession.IsDisposed);

        //    using (var client = new IpcClient(fileMutexPath, _clientLogger))
        //    {
        //        _ = await client.ConnectLocalTcp(serverName, tcpPort, protocolRevision);
        //        var val = await client.ExecRequest<int>("add", new { opA = 3, opB = 4 }, CancellationToken.None);
        //    }

        //    Assert.False(serverSession.IsDisposed);

        //    server.StopServer();
        //    await serverTask;

        //    Assert.True(serverSession.IsDisposed);
        //}

        //[Fact]
        //public async void ClientSideSessionDisposed()
        //{
        //    var protocolRevision = 0;
        //    var tcpPort = _uniqueValues.NextTcpPort();

        //    // The client side session object that we'll use to accumulate and store the result of our add operations
        //    var clientSideSession = new AccumulatedAddClientSession(_clientLogger);

        //    var fileMutexPath = GetTemporaryDirectory();
        //    using (var server = new IpcServer<AccumulatedAddServerService, AccumulatedAddServerSession>(fileMutexPath, _serverLogger))
        //    {
        //        var serverName = _uniqueValues.NextServerName();
        //        var serverTask = server.StartLocalTcp(serverName, tcpPort, new IpcVersion(1, 0, 5, protocolRevision), () => new AccumulatedAddServerSession(_serverLogger));

        //        using (var client = new IpcClient<AccumulatedAddClientService>(fileMutexPath, _clientLogger))
        //        {
        //            _ = await client.ConnectLocalTcp(serverName, tcpPort, protocolRevision, () => clientSideSession);
        //            _ = await client.JsonRpcClient.SendRequestAsync("add", JToken.FromObject(new { opA = 12, opB = 23 }), CancellationToken.None);
        //            _ = await client.JsonRpcClient.SendRequestAsync("add", JToken.FromObject(new { opA = 2, opB = 9 }), CancellationToken.None);

        //            AccumulatedAddClientService.Event.WaitOne(TimeSpan.FromSeconds(5));

        //            Assert.False(clientSideSession.IsDisposed);
        //        }
        //        Assert.True(clientSideSession.IsDisposed);
        //    }
        //}
        //#endregion

        //[Fact]
        //public async void ClientCount()
        //{
        //    var protocolRevision = 0;
        //    var tcpPort = _uniqueValues.NextTcpPortWithRange(protocolRevision + 1);

        //    var fileMutexPath = GetTemporaryDirectory();
        //    using (var server = new IpcServer<BasicMathService, MathSession>(fileMutexPath, _serverLogger))
        //    {
        //        var serverName = _uniqueValues.NextServerName();
        //        var serverTask = server.StartLocalTcp(serverName, tcpPort, new IpcVersion(1, 0, 5, protocolRevision), () => new MathSession());

        //        Assert.Equal(0, server.ClientCount);
        //        Assert.Equal(0, server.ActiveClientCount);

        //        var clientA = new IpcClient(fileMutexPath, _clientLogger);
        //        _ = await clientA.ConnectLocalTcp(serverName, tcpPort, protocolRevision);

        //        Assert.Equal(1, server.ClientCount);
        //        Assert.Equal(1, server.ActiveClientCount);

        //        var clientB = new IpcClient(fileMutexPath, _clientLogger);
        //        _ = await clientB.ConnectLocalTcp(serverName, tcpPort, protocolRevision);

        //        Assert.Equal(2, server.ClientCount);
        //        Assert.Equal(2, server.ActiveClientCount);

        //        clientB.StopClient();

        //        Thread.Sleep(500);
        //        Assert.Equal(1, server.ClientCount);
        //        Assert.Equal(1, server.ActiveClientCount);

        //        server.StopServer();
        //        Assert.True(serverTask.Wait(10_000), "Server didn't shutdown in an acceptable timely fashion");
        //    }
        //}

    }
}
