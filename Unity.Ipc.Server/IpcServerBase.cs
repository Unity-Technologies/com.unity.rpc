using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Client;
using JsonRpc.Contracts;
using JsonRpc.Server;
using JsonRpc.Streams;
using Microsoft.Extensions.Logging;
using Unity.Ipc.Client;

namespace Unity.Ipc.Server
{
    public abstract class IpcServerBase : IDisposable
    {
        protected string _fileMutexPath;

        // Set the interval between each call from the server to a client to check if it's still alive
#if DEBUG
        internal static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(1);
#else
        internal static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(5);
#endif
        #region Public properties

        /// <summary>
        /// Return <code>true</code> if the server is started <code>false</code> if it's not.
        /// </summary>
        public bool IsStarted { get; private set; }

        public int BusyPortRetryCount = 128;

        /// <summary>
        /// Access to the task that runs the server hosting routine, waiting for this task completion is equivalent to waiting for the server to shutdown
        /// </summary>
        public Task ServerHostingTask { get; private set; }

        /// <summary>
        /// Access to the TCP port the server is hosted from
        /// </summary>
        public int TcpPort
        {
            get
            {
                if (_listenSocket == null)
                {
                    return -1;
                }

                return ((IPEndPoint)_listenSocket.LocalEndPoint).Port;
            }
        }

        /// <summary>
        /// Access to the dispose state
        /// </summary>
        public bool IsDisposed => _isDisposed == 1;

        /// <summary>
        /// Get the current count of connected clients, mostly for information purpose
        /// </summary>
        public int ClientCount => _clientInfos.Count;

        public int ActiveClientCount => GetClientInfos().Sum(ci => ci.ShouldBeAvailable ? 1 : 0);

        public ClientInfo[] GetClientInfos()
        {
            return _clientInfos.ToArray();
        }

        #endregion

        #region Constructor

        protected IpcServerBase(ILogger logger = null)
        {
            _logger = logger;
            _serverCancellationTokenSource = new CancellationTokenSource();
            _clientInfos = new List<ClientInfo>();
            _disposedClients = new List<ClientInfo>();
        }

        #endregion

        #region Public API

        protected Task StartLocalTcpInternal(string serverUniqueName, int baseTcpPort, IpcVersion version)
        {
            #region Prologue

            // Check if we're already started
            if (IsStarted)
            {
                throw new InvalidOperationException("Can't start the Server: it's already started");
            }

            // Check if it's disposed
            if (IsDisposed)
            {
                throw new InvalidOperationException("Can't start the server: it's disposed");
            }

            // Set the server as started
            IsStarted = true;

            // Set the version of this instance
            _version = version;

            _logger?.LogInformation("Thread: {threadId}, Start Local Server {serverName} with Version {version}", Thread.CurrentThread.ManagedThreadId, serverUniqueName, version.ToString());

            // Create a named mutex to ensure a single instance in the local machine
            _serverUniqueName = serverUniqueName;
            if (Mutex.TryOpenExisting(IpcClient.CreateMutexName(serverUniqueName, version.ProtocolRevision), out var mutex))
            {
                throw new IpcServerAlreadyExistsException($"Couldn't start the server, an instance with the given Name: {serverUniqueName} and Protocol Revision: {version.ProtocolRevision} already is running, please stop it.");
            }

            #endregion

            // Set the default Json serialization settings
            _jsonRpcServiceHostBuilder = new JsonRpcServiceHostBuilder
            {
                ContractResolver = new JsonRpcContractResolver
                {
                    // Use camelcase for RPC method names.
                    NamingStrategy = new CamelCaseJsonRpcNamingStrategy(),
                    // Use camelcase for the property names in parameter value objects
                    ParameterValueConverter = new CamelCaseJsonValueConverter()
                }
            };

            // Socket/JsonRPC startup
            try
            {
                _jsonRpcServiceHostBuilder.Register(_serviceType);
                _listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);

                // Basically we try to open the socket at the given port, but if it's already open by someone else, we increment until we find an open one, maximum try count being BusyPortRetryCount
                var tryCount = BusyPortRetryCount;
                var tcpPort = baseTcpPort + version.ProtocolRevision;
                while (tryCount-- != 0)
                {
                    try
                    {
                        _listenSocket.Bind(new IPEndPoint(IPAddress.Loopback, tcpPort));
                        break;
                    }
                    catch (SocketException e)
                    {
                        if (e.SocketErrorCode == SocketError.AddressAlreadyInUse)
                        {
                            ++tcpPort;
                        }
                    }
                }
                _logger?.LogDebug("Thread: {threadId}, Listening server on port {tcpPort} for protocol revision {protocolRevision}", Thread.CurrentThread.ManagedThreadId, tcpPort, version.ProtocolRevision);
            }
            catch (Exception e)
            {
                throw new IpcServerConnectException("Unknown error occured while attempting to start the server, see inner exception for more detail", e);
            }

            // We use an event to make this thread wait for the Server Routine to be ready for incoming requests.
            // We don't want to return from this method if we are not able to accept requests.
            _serverRunningWaitHandle = new AutoResetEvent(false);

            // Fire the task that will run the Server routine
            ServerHostingTask = Task.Run(() => ServerRoutine(serverUniqueName), CancellationToken);
            _logger?.LogDebug("Thread: {threadId}, Starting Server Routine on Task: {taskId}", Thread.CurrentThread.ManagedThreadId, ServerHostingTask.Id);

            // Now we wait the Server routine is signaling it's ready
            _serverRunningWaitHandle.WaitOne();
            _logger?.LogDebug("Thread: {threadId}, Server is up and running, waiting for requests", Thread.CurrentThread.ManagedThreadId);

            return ServerHostingTask;
        }

        /// <summary>
        /// Stops the server.
        /// </summary>
        /// <remarks>
        /// This will broadcast a shutdown message to all connected clients and then will trigger the cancellation token that will end the hosting task returned by the <see cref="StartLocalTcp"/> method.
        /// </remarks>
        public void StopServer()
        {
            if (IsStarted == false)
            {
                return;
            }

            var clientInfos = _clientInfos.ToArray();
            foreach (var clientInfo in clientInfos)
            {
                clientInfo.SendServerShutdown();
                ShutDownClient(clientInfo);
                _clientInfos.Remove(clientInfo);
            }

            _logger?.LogInformation("Thread: {threadId}, Stopping server {serverName}", Thread.CurrentThread.ManagedThreadId, _serverUniqueName);
            _serverCancellationTokenSource.Cancel();
            _logger?.LogInformation("Thread: {threadId}, Stopping server {serverName} Cancellation Token set!", Thread.CurrentThread.ManagedThreadId, _serverUniqueName);
            IsStarted = false;
        }

        /// <summary>
        /// Dispose the server object. If the server is started then it will be stopped.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _isDisposed, 1, 0) == 0)
            {
                StopServer();
            }
        }

        #endregion

        #region Internals

        #endregion

        #region Privates

        private void ServerRoutine(string serverUniqueName)
        {
            try
            {
                // Name the thread, mainly for debugging purpose
                var currentThread = Thread.CurrentThread;
                currentThread.Name = "ServerRoutine";
                _logger?.LogInformation("Thread: {threadId}, Server routine is started.", currentThread.ManagedThreadId);

                // Create the mutex in this method, we must release in the same thread that creates it, so this place is the best place, once this method exists, there's no longer any server anyway
                var mutexName = IpcClient.CreateMutexName(serverUniqueName, _version.ProtocolRevision);

                if (FileMutex.IsTaken(_fileMutexPath, mutexName))
                {
                    throw new IpcServerAlreadyExistsException($"Couldn't start the server, an instance with the given Name: {serverUniqueName} and Protocol Revision: {_version.ProtocolRevision} already is running, please stop it.");
                }

                FileMutex mutex = new FileMutex(_fileMutexPath, mutexName);
                mutex.Acquire();
                _logger?.LogDebug("Thread: {threadId}, Mutex {mutexName} is created for this server", Thread.CurrentThread.ManagedThreadId, mutexName);

                // Once we put the socket in listen mode, all incoming connection request will be queued, so it's safe to signal the Start method it can finish its execution
                _listenSocket.Listen(128);
                _serverRunningWaitHandle.Set();
                _logger?.LogDebug("Thread: {threadId}, Server is now listening for incoming client connection requests", Thread.CurrentThread.ManagedThreadId);

                // Loop as long as cancellation is not requested
                var cancellationToken = CancellationToken;
                while (cancellationToken.IsCancellationRequested == false)
                {
                    // Listen for incoming connection to server, wait for 100ms
                    var socketTask = _listenSocket.AcceptAsync();
                    socketTask.Wait(100);

                    // Whether or not we have incoming client, it's now the time to check if each existing client is still alive
                    CheckExistingClientStatus();

                    // If nothing was connected, loop. Otherwise...
                    if (socketTask.IsCompletedSuccessfully == false)
                    {
                        continue;
                    }

                    // ... Get the socket resulted from the incoming connection
                    var socket = socketTask.Result;
                    _logger?.LogDebug("Thread: {threadId}, A new client socket is connected from endpoint {endPoint} ", Thread.CurrentThread.ManagedThreadId, socket.RemoteEndPoint.ToString());

                    // Now we have the socket, initialize the JsonRpc client
                    ConnectNewClient(socket);
                }
                _logger?.LogDebug("Thread: {threadId}, Server process loop is stopped on thread, now waiting for Client Process routines to end", Thread.CurrentThread.ManagedThreadId);

                // Release all clients data
                _logger?.LogDebug("Thread: {threadId}, Releasing connected clients' resources", Thread.CurrentThread.ManagedThreadId);
                _clientInfos.ForEach(ShutDownClient);

                // Dispose the session object if any
                _serverSessionObject?.Dispose();

                // Now we can release our mutex
                mutex.Dispose();
                _logger?.LogDebug("Thread: {threadId}, Mutex {mutexName} is released for this server", Thread.CurrentThread.ManagedThreadId, mutexName);
            }
            catch (Exception e)
            {
                _logger?.LogDebug(0, e, "Thread: {threadId}, Unknown exception during server routine {exception}", Thread.CurrentThread.ManagedThreadId, e);
            }
        }

        private void CheckExistingClientStatus()
        {
            _disposedClients.Clear();
            _clientInfos.ForEach(ci =>
            {
                // Check if we should shutdown the client, as requested by the client counterpart
                if (ci.ToShutDown)
                {
                    ShutDownClient(ci);
                }

                // Check if we should remove a disposed client from the client list
                if (ci.IsDisposed)
                {
                    _disposedClients.Add(ci);
                }
            });

            // Remove disposed clients
            _disposedClients.ForEach(ci => _clientInfos.Remove(ci));
            _disposedClients.Clear();

            // Foreach is safe here as _clientInfos is only manipulated in the server routine's thread
            _clientInfos.ForEach(ci => _ = ci.CheckIfClientIsAlive());
        }

        private void ShutDownClient(ClientInfo ci)
        {
            // Let the server session object know the client is being disconnected
            _serverSessionObject?.DoClientDisconnected(ci);

            // Dispose the client
            ci.Dispose();
        }

        private void ConnectNewClient(Socket socket)
        {
            // Build a JsonRPC Host Service
            var serviceHost = _jsonRpcServiceHostBuilder.Build();

            // Set the server wide session object, if any
            var serverHandler = new StreamRpcServerHandler(serviceHost);
            if (_serverSessionObject != null)
            {
                serverHandler.DefaultFeatures.Set(_serverSessionObjectType, _serverSessionObject);
            }

            // Set the client wide session object, if any
            var clientWideSession = CreateClientWideSession();
            if (clientWideSession != null)
            {
                serverHandler.DefaultFeatures.Set(clientWideSession.GetType(), clientWideSession);
            }

            // Set the version object as a feature for our Handshake operation to retrieve and return it
            serverHandler.DefaultFeatures.Set(_version);

            // Set the server also as a feature to redirect client initiated operation
            serverHandler.DefaultFeatures.Set(typeof(IpcServerBase), this);

            // Set the logger to access it from the JsonRcpServer
            serverHandler.DefaultFeatures.Set(typeof(ILogger), _logger);

            try
            {
                // Create the JsonRpc server
                var netStream = new NetworkStream(socket);
                var reader = new ByLineTextMessageReader(netStream);
                var writer = new ByLineTextMessageWriter(netStream);
                var serverIDisposable = serverHandler.Attach(reader, writer);

                var clientHandler = new StreamRpcClientHandler();
                var serverSideClientIDisposable = clientHandler.Attach(reader, writer);
                var serverSideClient = new JsonRpcClient(clientHandler);

                var ci = new ClientInfo(_logger, socket, netStream, reader, writer, serverIDisposable, serverSideClient, clientWideSession, serverSideClientIDisposable);
                _clientInfos.Add(ci);

                // Initialize the client session, if any
                clientWideSession?.Initialize(_serverSessionObject, ci);

                // Execute the event to let know there's a new client
                _serverSessionObject?.DoNewClientConnected(ci);

                // Add the client info as a feature to allow access to the server side client object
                serverHandler.DefaultFeatures.Set(typeof(ClientInfo), ci);

                _logger?.LogInformation("Thread: {threadId}, JsonRpc is ready to serve client from endpoint {endPoint}", Thread.CurrentThread.ManagedThreadId, socket.RemoteEndPoint.ToString());
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e.ToString()}\r\nBye bye from {socket.RemoteEndPoint}...");
                return;
            }
        }

        protected virtual ClientSessionBase CreateClientWideSession() => null;

        #endregion

        #region Private data

        private ILogger _logger;
        protected Type _serviceType;
        private readonly CancellationTokenSource _serverCancellationTokenSource;
        private EventWaitHandle _serverRunningWaitHandle;
        private Socket _listenSocket;
        protected ServerSession _serverSessionObject;
        protected Type _serverSessionObjectType;
        private JsonRpcServiceHostBuilder _jsonRpcServiceHostBuilder;
        private IpcVersion _version;
        private int _isDisposed;
        private string _serverUniqueName;
        private List<ClientInfo> _clientInfos;
        private List<ClientInfo> _disposedClients;
        private CancellationToken CancellationToken => _serverCancellationTokenSource.Token;

        #endregion
    }
}
