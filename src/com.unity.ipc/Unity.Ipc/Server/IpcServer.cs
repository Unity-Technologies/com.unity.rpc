using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MessagePack.Resolvers;
using StreamRpc;

namespace Unity.Ipc
{
    /// <summary>
    /// An ipc server that listens on 
    /// </summary>
    public class IpcServer : Ipc<IpcServer>
    {
        private Socket socket;
        private readonly TaskCompletionSource<bool> stopTask = new TaskCompletionSource<bool>();

        public event Action<IIpcRegistration, IRequestContext> OnClientConnect;
        public event Action<IRequestContext> OnClientReady;
        public event Action<IRequestContext, JsonRpcDisconnectedEventArgs> OnClientDisconnect;

        public IpcServer(Configuration configuration, CancellationToken token = default)
            : base(configuration, token)
        {
        }

        /// <summary>
        /// Start listening on the socket. When clients connect, <seealso cref="OnClientConnect" /> is
        /// raised. You can attach senders and receivers with
        /// <seealso cref="Ipc.RegisterRemoteTarget"/> and  <seealso cref="Ipc.RegisterLocalTarget"/>
        /// for each client on the <seealso cref="OnClientConnect" /> handler.
        /// Don't forget to call <seealso cref="Ipc.StartListening" /> on the client 
        /// </summary>
        public override async Task Initialize()
        {
            await base.Initialize();

            socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, Configuration.Port));
            socket.Listen(128);

            ThreadPool.QueueUserWorkItem(async _ => await InternalListen().ConfigureAwait(false));
        }

        public override async Task Run()
        {
            if (socket == null)
                await Initialize();

            await stopTask.Task;
        }

        public IpcServer ClientConnecting(Action<IIpcRegistration, IRequestContext> onConnect)
        {
            OnClientConnect += onConnect;
            return this;
        }

        public IpcServer ClientDisconnecting(Action<IRequestContext, JsonRpcDisconnectedEventArgs> onDisconnect)
        {
            OnClientDisconnect += onDisconnect;
            return this;
        }

        public IpcServer ClientReady(Action<IRequestContext> onClientReady)
        {
            OnClientReady += onClientReady;
            return this;
        }

        protected void RaiseOnClientConnect(IIpcRegistration registration, IRequestContext context)
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

        private async Task InternalListen()
        {
            try
            {
                // add any instances that were added prior to start getting called
                AddTargets();

                RaiseOnStart();

                if (!LocalTargets.Any(x => x is IServerInformation))
                    RegisterLocalTarget(Configuration.GetServerInformation());

                // add anything that was registered on the start callback
                AddTargets();

                RaiseOnReady();

                // listen to clients until the server shuts down
                while (!Token.IsCancellationRequested)
                {
                    var socketTask = socket.AcceptAsync();

                    // wait for a client connection
                    await Task.WhenAny(socketTask, Task.Delay(-1, Token));

                    // if the wait was signaled by the cancellation token, that means we're shutting down
                    if (Token.IsCancellationRequested)
                        break;

                    new Task(() => HandleClientConnection(socketTask.Result), Token, TaskCreationOptions.None).Start();
                }

                RaiseOnStop();

                stopTask.TrySetResult(true);
            }
            catch (Exception ex)
            {
                stopTask.SetException(ex);
                throw;
            }
        }

        private void HandleClientConnection(Socket socket)
        {
            // connect the client
            var client = new IpcClient(Configuration, Token)
                         .Starting(RaiseOnClientConnect)
                         .Ready(RaiseOnClientReady);

            foreach (var type in RemoteTypes)
            {
                client.RegisterRemoteTarget(type);
            }

            foreach (var obj in RemoteTargets)
            {
                client.RegisterRemoteTarget(obj);
            }

            foreach (var obj in LocalTargets)
            {
                client.RegisterLocalTarget(obj);
            }

            client.Disconnected += args => RaiseOnClientDisconnect(client, args);

            try
            {
                client.InternalStart(socket);
            }
            catch (Exception)
            {
                client.Dispose();
            }
        }
    }
}
