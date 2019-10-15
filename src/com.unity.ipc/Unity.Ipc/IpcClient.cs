using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using MessagePack.Resolvers;

namespace Unity.Ipc
{
    /// <summary>
    /// An ipc client that listens on a tcp socket
    /// </summary>
    public class IpcClient : Ipc
    {
        private readonly Configuration configuration;
        private Socket socket;

        public IpcClient(Configuration configuration, CancellationToken token = default(CancellationToken))
            : base(token)
        {
            this.configuration = configuration;
        }

        /// <summary>
        /// Connect to a server on the port specified in the configuration.
        /// After calling this method,  you can attach senders and receivers with
        /// <seealso cref="Ipc.RegisterRemoteTarget"/> and  <seealso cref="Ipc.RegisterLocalTarget"/>
        /// You must call <seealso cref="Start" /> after setting targets.
        /// </summary>
        /// <returns></returns>
        public async Task Connect()
        {
            socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            var socketTask = socket.ConnectAsync(IPAddress.Loopback, configuration.Port);

            var awaitedTask = await Task.WhenAny(socketTask, Task.Delay(-1, Token));
            Token.ThrowIfCancellationRequested();
            if (awaitedTask.IsFaulted)
                ExceptionDispatchInfo.Capture(awaitedTask.Exception.InnerException).Throw();

            Attach(new NetworkStream(socket));

            RegisterRemoteTarget<IServerInformation>();
        }

        public async Task<IpcVersion> Start()
        {
            base.StartListening();
            return await GetRemoteTarget<IServerInformation>().GetVersion();
        }

        #region IDisposable

        private bool disposed;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposed)
                return;
            if (disposing)
            {
                socket?.Dispose();
            }
            disposed = true;
        }

        #endregion
    }
}
