using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Client;
using JsonRpc.Contracts;
using JsonRpc.Messages;
using JsonRpc.Server;
using JsonRpc.Streams;
using Newtonsoft.Json.Linq;

[assembly: InternalsVisibleTo("Unity.Ipc.Server")]      // Because I'm a very naughty boy!

namespace Unity.Ipc.Client
{
    /// <summary>
    /// Client Ipc class for one way communication with the Ipc server
    /// </summary>
    /// <remarks>
    /// This class must be used to make one way communication with an Ipc server. The user has to create an instance and call a Connect* method in order to connect to the Ipc Server.
    /// You can then rely on the <see cref="ExecRequest{TRes}"/> method to make calls or simply using the underlying JsonRpc library object exposed by the property <see cref="JsonRpcClient"/>.
    /// Once you're done with the client you should call <see cref="StopClient"/> or <see cref="Dispose"/> to disconnect it from the server.
    /// If you need two ways communication (i.e. need the server to contact the client) then you must use the <see cref="IpcClient{T}"/> class instead.
    /// </remarks>
    public class IpcClient : IDisposable
    {
        #region Public properties

        /// <summary>
        /// Access the JsonRpc client object, see remarks.
        /// </summary>
        /// <remarks>
        /// You should not store this object but accessing it each time you want to send a request, a possible exception will be triggered if the client or server are not in a working state
        /// </remarks>
        /// <exception cref="IpcClientNotAvailableException"></exception>
        public JsonRpcClient JsonRpcClient
        {
            get
            {
                CheckClientAvailability();
                return _client;
            }
        }

        public TSession GetSession<TSession>() where TSession : class, IDisposable
        {
            if (_clientSessionObject is TSession == false)
            {
                throw new InvalidCastException();
            }
            return (TSession) _clientSessionObject;
        }

        /// <summary>
        /// Return true if the client is disposed
        /// </summary>
        public bool IsDisposed => _isDisposed != 0;

        /// <summary>
        /// Return true if the client is started
        /// </summary>
        /// <remarks>
        /// The client may be started but not available. <see cref="ShouldBeAvailable"/> would give a more accurate state.
        /// </remarks>
        public bool IsStarted { get; private set; }

        /// <summary>
        /// Return true if the client should be available (or maybe not), or false if it's definitely not available
        /// </summary>
        /// <remarks>
        /// There's no certain way to know if the server is still working other than making a request.
        /// But if the server is shutting down gracefully, we will know about it.
        /// To summarize: false is definitely not working, true may be working
        /// </remarks>
        public bool ShouldBeAvailable => IsStarted && _client != null && !_serverShuttingDown;

        public int BusyPortRetryCount = 16;

        #endregion

        #region Constructor

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="fileMutexPath"></param>
        /// <param name="logger">You can specify an object that implements the <see cref="IIpcClientLogger"/> interface to log or <value>null</value> if you don't need logging.</param>
        public IpcClient(string fileMutexPath, IIpcClientLogger logger = null)
        {
            _fileMutexPath = fileMutexPath;
            _logger = logger;
            _clientCancellationTokenSource = new CancellationTokenSource();
        }

        #endregion

        #region API

        /// <summary>
        /// Connect the Ipc client to a Ipc Server
        /// </summary>
        /// <param name="serverUniqueName">Unique name that designate the server, will be used to make sure the client connects to the right server</param>
        /// <param name="baseTcpPort">TCP Port of the server</param>
        /// <param name="protocolRevision">Server Protocol revision number</param>
        /// <returns>The actual version of the server this client just connected to</returns>
        /// <exception cref="IpcServerNotFoundException">If there's no server at this port</exception>
        /// <remarks>
        /// This method is used to connect the client to a given local server at the given TCP Port.
        /// If the server allows multiple running instances to support different versions (that were introduced likely because of breaking changes) the <param name="protocolRevision"/> argument is used to specify which protocol revision to connect to.
        /// The server will attempt to open a TCP Socket on "baseTcpPort + protocolRevision", in the unlikely event where the TCP port would be already used, the server will increment the port value until it find a free one. The client will also attempt to find the matching server by starting with the expected port and then incrementing until it find the right one.
        /// </remarks>
        public Task<IpcVersion> ConnectLocalTcp(string serverUniqueName, int baseTcpPort, int protocolRevision)
        {
            return ConnectLocalTcp<IDisposable>(serverUniqueName, baseTcpPort, protocolRevision);
        }

        /// <summary>
        /// Connect the Ipc client to a Ipc Server
        /// </summary>
        /// <param name="serverUniqueName">Unique name that designate the server, will be used to make sure the client connects to the right server</param>
        /// <param name="baseTcpPort">TCP Port of the server</param>
        /// <param name="protocolRevision">Server Protocol revision number</param>
        /// <param name="sessionFactory">A factory that will be called to retrieve an object that will be used as the client session object.
        /// From the client side service you can call <code>RequestContext.Features.Get()</code> to retrieve your session object.</param>
        /// <returns>The actual version of the server this client just connected to</returns>
        /// <exception cref="IpcServerNotFoundException">If there's no server at this port</exception>
        /// <remarks>
        /// This method is used to connect the client to a given local server at the given TCP Port.
        /// If the server allows multiple running instances to support different versions (that were introduced likely because of breaking changes) the <param name="protocolRevision"/> argument is used to specify which protocol revision to connect to.
        /// The server will attempt to open a TCP Socket on "baseTcpPort + protocolRevision", in the unlikely event where the TCP port would be already used, the server will increment the port value until it find a free one. The client will also attempt to find the matching server by starting with the expected port and then incrementing until it find the right one.
        /// </remarks>
        public async Task<IpcVersion> ConnectLocalTcp<TSession>(string serverUniqueName, int baseTcpPort, int protocolRevision, Func<TSession> sessionFactory = null) where TSession : class, IDisposable
        {
            // Check if it's already started
            if (IsStarted)
            {
                throw new IpcClientAlreadyStartedException();
            }

            // Try to open the mutex that allow us to know the server is up and running...somewhere...locally

            if (!string.IsNullOrEmpty(_fileMutexPath) && FileMutex.IsTaken(_fileMutexPath, CreateMutexName(serverUniqueName, protocolRevision)) == false)
            {
                throw new IpcServerNotFoundException();
            }

            void CleanUp()
            {
                _socket?.Dispose();
                _socket = null;
                _client = null;
                _clientHandlerDisposable?.Dispose();
                _clientHandlerDisposable = null;
                _writer?.Dispose();
                _writer = null;
                _reader?.Dispose();
                _reader = null;
                _netStream?.Dispose();
                _netStream = null;
            }

            // Try to connect to the server at the expected port, if we fail, we'll try ports that are following BusyPortRetryCount times
            var tryCount = BusyPortRetryCount;
            var tcpPort = baseTcpPort + protocolRevision;
            while (tryCount-- != 0)
            {
                try
                {
                    // Create the socket
                    _socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                    var curTcpPort = tcpPort;

                    // Connect to it
                    _logger?.LogDebug("Thread: {threadId}, Attempt to connect to socket at port {tcpPort} for protocol revision {protocolRevision}", Thread.CurrentThread.ManagedThreadId, curTcpPort, protocolRevision);
                    _socket.Connect(new IPEndPoint(IPAddress.Loopback, tcpPort++));
                    _logger?.LogDebug("Thread: {threadId}, Socket connected to {endPoint}", Thread.CurrentThread.ManagedThreadId, _socket.RemoteEndPoint.ToString());

                    // Create the stream, reader & writer
                    _netStream = new NetworkStream(_socket);
                    _reader = new ByLineTextMessageReader(_netStream);
                    _writer = new ByLineTextMessageWriter(_netStream);

                    // Create the JsonRpc client
                    var clientHandler = new StreamRpcClientHandler();
                    _clientHandlerDisposable = clientHandler.Attach(_reader, _writer);
                    _client = new JsonRpcClient(clientHandler);

                    // The server might not be ready to receive the handshake message, so we use a loop to retry
                    ResponseMessage res = null;
                    for (int i = 0; i < 50; i++)
                    {
                        try
                        {
                            // Send the request, wait for 500ms for a reply
                            _logger?.LogDebug("Thread: {threadId}, Sending handshake message on port {tcpPort}, for protocol revision {protocolRevision} attempt #{attempt}...", Thread.CurrentThread.ManagedThreadId, curTcpPort, protocolRevision, i);
                            var task = _client.SendRequestAsync("handshake", null, CancellationToken.None);
                            if (await Task.WhenAny(task, Task.Delay(500, CancellationToken)).ConfigureAwait(false) == task)
                            {
                                res = task.Result;
                                break;
                            }
                            _logger?.LogDebug("Thread: {threadId}, Handshake attempt #{attempt} on port {tcpPort} timed out", Thread.CurrentThread.ManagedThreadId, i, curTcpPort);
                        }
                        catch (Exception e)
                        {
                            _logger?.LogDebug(e, "Thread: {threadId}, Error during handshake {exception}", Thread.CurrentThread.ManagedThreadId, e.ToString());
                        }
                    }

                    // We couldn't successfully send a handshake, it means the server is not hosted at this port, try the next one.
                    if (res == null)
                    {
                        CleanUp();
                        continue;
                    }

                    // Check if the message was successful
                    if (res.Error != null)
                    {
                        _logger?.LogDebug("Thread: {threadId}, Handshake replied, but with error {error}", Thread.CurrentThread.ManagedThreadId, res.Error.ToString());
                        continue;
                    }
                    _logger?.LogDebug("Thread: {threadId}, Handshake on port {tcpPort} successful!", Thread.CurrentThread.ManagedThreadId, curTcpPort);

                    // Get the server version object and compare the protocol revision with the client's one
                    var serverVersion = res.Result.ToObject<IpcVersion>();
                    if (protocolRevision != serverVersion.ProtocolRevision)
                    {
                        _logger?.LogDebug("Thread: {threadId}, Handshake made on the wrong server, try the next TCP port...", Thread.CurrentThread.ManagedThreadId);
                        CleanUp();
                        continue;
                    }

                    // At this point the client is connected and ready to send requests
                    IsStarted = true;
                    _serverVersion = serverVersion;
                    break;
                }
                catch (Exception e)
                {
                    _logger?.LogError(e, "Thread: {threadId}, Unexpected exception during client connection {exception}", Thread.CurrentThread.ManagedThreadId, e.ToString());
                    CleanUp();
                }
            }

            // Check if we could connect to the server
            if (tryCount == 0)
            {
                throw new IpcServerNotFoundException();
            }

            // Start a JsonRpc Server on the socket we used for the client, the server will send some "IsAlive" message to check for the client
            try
            {
                _logger?.LogDebug("Thread: {threadId}, Start creation of client side server", Thread.CurrentThread.ManagedThreadId);
                var hostBuilder = new JsonRpcServiceHostBuilder
                {
                    ContractResolver = new JsonRpcContractResolver
                    {
                        // Use camelcase for RPC method names.
                        NamingStrategy = new CamelCaseJsonRpcNamingStrategy(),
                        // Use camelcase for the property names in parameter value objects
                        ParameterValueConverter = new CamelCaseJsonValueConverter()
                    }
                };

                hostBuilder.Register(IpcClientServiceType ?? typeof(IpcClientService));
                var serviceHost = hostBuilder.Build();
                var serverHandler = new StreamRpcServerHandler(serviceHost);
                serverHandler.DefaultFeatures.Set(typeof(IpcClient), this);
                if (sessionFactory != null)
                {
                    _clientSessionObject = sessionFactory();
                    serverHandler.DefaultFeatures.Set(typeof(TSession), _clientSessionObject);
                }
                _clientServerIDisposable = serverHandler.Attach(_reader, _writer);
                _logger?.LogDebug("Thread: {threadId}, Client side server ready for incoming requests", Thread.CurrentThread.ManagedThreadId);
            }
            catch (Exception e)
            {
                _logger?.LogError(e, "Thread: {threadId}, Exception during client side server creation {exception}", Thread.CurrentThread.ManagedThreadId, e.ToString());
            }

            return _serverVersion;
        }

        /// <summary>
        /// Try to access the client
        /// </summary>
        /// <param name="client">If the client may be available, this argument will contain the object, if the client or server are not available, null will be stored</param>
        /// <returns>true if the client and server may be working, false if one of them is definitely down.</returns>
        public bool TryGetJsonRpcClient(out JsonRpcClient client)
        {
            if (_serverShuttingDown)
            {
                StopClient();
            }

            if (IsDisposed || IsStarted == false || _client == null)
            {
                client = null;
                return false;
            }

            client = _client;
            return true;
        }

        /// <summary>
        /// Make a request to the client, in a more encapsulated way
        /// </summary>
        /// <typeparam name="TRes">Type of the returned value</typeparam>
        /// <param name="requestName">Json method name</param>
        /// <param name="arg">Argument of the Json method, you should construct an anonymous object with named properties that matches the ones needed by the server call</param>
        /// <param name="cancellationToken">A cancellation token, use CancellationToken.None if none should be passed.</param>
        /// <returns>The request result value</returns>
        /// <exception cref="BadExecRequestException">If the request fail, for any reason.</exception>
        public async Task<TRes> ExecRequest<TRes>(string requestName, object arg, CancellationToken cancellationToken)
        {
            if (!TryGetJsonRpcClient(out var client))
            {
                throw new BadExecRequestException($"Error during sending the request {requestName}, couldn't access the JsonRpcClient object");
            }

            try
            {
                var response = await client.SendRequestAsync(requestName, arg != null ? JToken.FromObject(arg) : null, cancellationToken).ConfigureAwait(false);
                if (response.Error == null)
                {
                    return response.Result.ToObject<TRes>();
                }

                _logger?.LogError("Error during sending the request {requestName}, error message: {errorMessage}", requestName, response.Error.Message.ToString());
                throw new BadExecRequestException($"Error during sending the request {requestName}, error message: {response.Error.Message}");
            }
            catch (Exception e)
            {
                _logger?.LogError(e, "Thread: {threadId}, Error while executing request {r}", Thread.CurrentThread.ManagedThreadId, requestName);
                throw new BadExecRequestException($"Error during sending the request {requestName}, see inner exception for more information", e);
            }
        }

        /// <summary>
        /// Make a request to the client, in a more encapsulated way
        /// </summary>
        /// <param name="requestName">Json method name</param>
        /// <param name="arg">Argument of the Json method, you should construct an anonymous object with named properties that matches the ones needed by the server call</param>
        /// <param name="cancellationToken">A cancellation token, use CancellationToken.None if none should be passed.</param>
        /// <returns>The task that will complete the request execution</returns>
        /// <exception cref="BadExecRequestException">If the request fail, for any reason.</exception>
        public async Task ExecRequest(string requestName, object arg, CancellationToken cancellationToken)
        {
            if (!TryGetJsonRpcClient(out var client))
            {
                throw new BadExecRequestException($"Error during sending the request {requestName}, couldn't access the JsonRpcClient object");
            }

            try
            {
                var response = await client.SendRequestAsync(requestName, arg != null ? JToken.FromObject(arg) : null, cancellationToken).ConfigureAwait(false);
                if (response.Error == null)
                {
                    return;
                }

                _logger?.LogError("Error during sending the request {requestName}, error message: {errorMessage}", requestName, response.Error.Message.ToString());
                throw new BadExecRequestException($"Error during sending the request {requestName}, error message: {response.Error.Message}");
            }
            catch (Exception e)
            {
                _logger?.LogError(e, "Thread: {threadId}, Error while executing request {r}", Thread.CurrentThread.ManagedThreadId, requestName);
                throw new BadExecRequestException($"Error during sending the request {requestName}, see inner exception for more information", e);
            }
        }

        /// <summary>
        /// Call this method to stop the client and disconnect it from the server.
        /// </summary>
        public void StopClient()
        {
            if (IsStarted == false)
            {
                return;
            }

            if (TryGetJsonRpcClient(out var client))
            {
                try
                {
                    client.SendRequestAsync("clientIsShuttingDown", null, CancellationToken.None);
                }
                catch (Exception)
                {
                }
            }

            _clientSessionObject?.Dispose();

            _clientCancellationTokenSource.Cancel();

            _clientServerIDisposable?.Dispose();
            _clientHandlerDisposable?.Dispose();
            _writer?.Dispose();
            _reader?.Dispose();
            _netStream?.Dispose();
            _socket?.Dispose();

            _clientServerIDisposable = null;
            _client = null;
            _clientHandlerDisposable = null;
            _writer = null;
            _reader = null;
            _netStream = null;

            IsStarted = false;
        }

        /// <summary>
        /// Dispose the client, stopping the connection with the server
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _isDisposed, 1, 0) == 0)
            {
                StopClient();
            }
        }

        #endregion

        #region Private methods

        private void CheckClientAvailability()
        {
            if (_serverShuttingDown)
            {
                StopClient();
            }

            if (IsDisposed || IsStarted == false || _client == null)
            {
                throw new IpcClientNotAvailableException();
            }
        }

        internal void ServerIsShuttingDown()
        {
            _serverShuttingDown = true;
        }

        #endregion

        #region Private data

        private IpcVersion _serverVersion;
        private Socket _socket;
        private NetworkStream _netStream;
        private ByLineTextMessageReader _reader;
        private ByLineTextMessageWriter _writer;
        private IDisposable _clientHandlerDisposable;
        private JsonRpcClient _client;
        private int _isDisposed;
        readonly string _fileMutexPath;
        private IIpcClientLogger _logger;
        private readonly CancellationTokenSource _clientCancellationTokenSource;
        private IDisposable _clientServerIDisposable;
        private CancellationToken CancellationToken => _clientCancellationTokenSource.Token;
        private bool _serverShuttingDown;
        protected Type IpcClientServiceType;
        private IDisposable _clientSessionObject;

        #endregion

        #region Helpers

        public static string CreateMutexName(string serverUniqueName, int versionProtocolRevision)
        {
            return $"Unity.Ipc.{serverUniqueName}.{versionProtocolRevision}";
        }

        #endregion
    }

    public class IpcClient<T> : IpcClient where T : IpcClientService
    {
        public IpcClient(string fileMutexPath, IIpcClientLogger logger=null) : base(fileMutexPath, logger)
        {
            IpcClientServiceType = typeof(T);
        }
    }
}
