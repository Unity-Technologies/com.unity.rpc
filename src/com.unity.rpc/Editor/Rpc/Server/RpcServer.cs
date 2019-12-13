using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using StreamRpc;

namespace Unity.Rpc
{
    using System.IO;
    using System.Runtime.ExceptionServices;

    /// <summary>
    /// An ipc server that listens on
    /// </summary>
    public class RpcServer : Rpc<RpcServer>
    {
        private Socket socket;

        public event Action<IRegistration, IRequestContext> OnClientConnect;
        public event Action<IRequestContext> OnClientReady;
        public event Action<IRequestContext, JsonRpcDisconnectedEventArgs> OnClientDisconnect;

        public RpcServer(Configuration configuration, CancellationToken token = default)
            : base(configuration, token)
        {
        }

        /// <summary>
        /// Start listening on the socket. When clients connect, <seealso cref="OnClientConnect" /> is
        /// raised. You can attach senders and receivers with
        /// <seealso cref="Rpc.RegisterRemoteTarget"/> and  <seealso cref="Rpc.RegisterLocalTarget"/>
        /// for each client on the <seealso cref="OnClientConnect" /> handler.
        /// Don't forget to call <seealso cref="Rpc.StartListening" /> on the client
        /// </summary>
        public override async Task Initialize()
        {
            var initTask = base.Initialize();

            // protect against multiple initialization
            if (initTask.IsCompleted)
                return;

            socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, Configuration.Port));
            Configuration.Port = ((IPEndPoint)socket.LocalEndPoint).Port;
            socket.Listen(128);

            ThreadPool.QueueUserWorkItem(_ => InternalStartServer().Forget());

            FinishInitialize(true);
            await initTask;
        }

        public RpcServer ClientConnecting(Action<IRegistration, IRequestContext> onConnect)
        {
            OnClientConnect += onConnect;
            return this;
        }

        public RpcServer ClientDisconnecting(Action<IRequestContext, JsonRpcDisconnectedEventArgs> onDisconnect)
        {
            OnClientDisconnect += onDisconnect;
            return this;
        }

        public RpcServer ClientReady(Action<IRequestContext> onClientReady)
        {
            OnClientReady += onClientReady;
            return this;
        }

        protected void RaiseOnClientConnect(IRegistration registration, IRequestContext context)
        {
            OnClientConnect?.Invoke(registration, context);
        }

        protected void RaiseOnClientDisconnect(IRequestContext client, JsonRpcDisconnectedEventArgs args)
        {
            OnClientDisconnect?.Invoke(client, args);
        }

        protected void RaiseOnClientReady(IRequestContext context)
        {
            OnClientReady?.Invoke(context);
        }

        protected override void RaiseOnStart()
        {
            base.RaiseOnStart();

            if (!LocalTargets.Any(x => x is IServerInformation))
                RegisterLocalTarget(Configuration.GetServerInformation());
        }

        private async Task InternalStartServer()
        {
            if (!Start(new MemoryStream(), false))
                return;

            try
            {
                // listen to clients until the server shuts down
                while (!Token.IsCancellationRequested)
                {
                    var socketTask = socket.AcceptAsync();

                    // wait for a client connection
                    await Task.WhenAny(socketTask, Task.Delay(-1, Token));

                    // if the wait was signaled by the cancellation token, that means we're shutting down
                    if (Token.IsCancellationRequested)
                        break;

                    new Task(sock => HandleClientConnection((Socket)sock), socketTask.Result, Token, TaskCreationOptions.None).Start();
                }
            }
            catch (Exception ex)
            {
                FinishStop(false, ex);
            }
        }

        private void HandleClientConnection(Socket socket)
        {
            // connect the client
            var client = new RpcClient(Configuration, Token)
                         .Starting(RaiseOnClientConnect)
                         .Ready(RaiseOnClientReady);

            foreach (var type in RemoteTypes)
            {
                client.RegisterRemoteTarget(type);
            }

            foreach (var obj in LocalTargets)
            {
                client.RegisterLocalTarget(obj);
            }

            client.OnDisconnected += args => RaiseOnClientDisconnect(client, args);
            client.Start(new NetworkStream(socket));
        }
    }

    static class HelperExtensions
    {
        public static void Forget(this Task task) { }
        public static void Rethrow(this Exception exception)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }
}
