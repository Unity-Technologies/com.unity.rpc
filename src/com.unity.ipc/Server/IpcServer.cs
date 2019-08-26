using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MessagePack.Resolvers;

namespace Unity.Ipc
{
    /// <summary>
    /// An ipc server that listens on 
    /// </summary>
    public class IpcServer
    {
        private readonly Configuration configuration;
        private readonly CancellationToken token;
        private Socket socket;
        public event EventHandler<Ipc> OnClientConnect;
        public event EventHandler<Ipc> OnClientDisconnect;

        public IpcServer(Configuration configuration, CancellationToken token = default(CancellationToken))
        {
            this.configuration = configuration;
            this.token = token;

            CompositeResolver.RegisterAndSetAsDefault(
                BuiltinResolver.Instance,
                AttributeFormatterResolver.Instance,

                // replace enum resolver
                DynamicEnumAsStringResolver.Instance,

                DynamicGenericResolver.Instance,
                DynamicUnionResolver.Instance,
                DynamicObjectResolver.Instance,

                PrimitiveObjectResolver.Instance,

                // final fallback(last priority)
                DynamicContractlessObjectResolver.Instance
            );
        }

        /// <summary>
        /// Initialize a socket to listen to clients on the specified port
        /// </summary>
        public Task Connect()
        {
            socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, configuration.Port));
            socket.Listen(128);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Start listening on the socket. When clients connect, <seealso cref="OnClientConnect" /> is
        /// raised. You can attach senders and receivers with
        /// <seealso cref="Ipc.RegisterRemoteTarget"/> and  <seealso cref="Ipc.RegisterLocalTarget"/>
        /// for each client on the <seealso cref="OnClientConnect" /> handler.
        /// Don't forget to call <seealso cref="Ipc.StartListening" /> on the client 
        /// </summary>
        public async Task Run()
        {
            if (socket == null)
            {
                throw new InvalidOperationException("Socket not initialized. Did you forget to call Connect()?");
            }

            // listen to clients until the server shuts down
            while (!token.IsCancellationRequested)
            {
                var socketTask = socket.AcceptAsync();
                // wait for a client connection
                await Task.WhenAny(socketTask, Task.Delay(-1, token));

                // if the wait was signaled by the cancellation token, that means we're shutting down
                if (token.IsCancellationRequested)
                    break;

                try
                {
                    var clientSocket = socketTask.Result;
                    // connect the client
                    var client = new Ipc(token);
                    client.Attach(new NetworkStream(clientSocket));
                    client.Disconnected += (sender, args) =>
                    {
                        OnClientDisconnect?.Invoke(this, client);
                        client.Dispose();
                    };
                    client.RegisterLocalTarget(new ServerInformation { Version = configuration.ProtocolVersion });
                    OnClientConnect?.Invoke(this, client);
                }
                catch (Exception)
                {
                    // log? raise error event?
                }
            }
        }
    }

    class ServerInformation : IServerInformation
    {
        public IpcVersion Version { get; set; }
        public Task<IpcVersion> GetVersion() => Task.FromResult(Version);
    }


}
