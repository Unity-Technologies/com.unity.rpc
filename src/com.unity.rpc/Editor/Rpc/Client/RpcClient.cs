using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.Rpc
{
    using System.IO;

    /// <summary>
    /// An rpc client that listens on a tcp socket
    /// </summary>
    public class RpcClient : Rpc<RpcClient>
    {
        private Socket socket;

        public RpcClient(Configuration configuration, CancellationToken token = default)
            : base(configuration, token)
        {}

        public override async Task Initialize()
        {
            var initTask = base.Initialize();

            // protect against multiple initialization
            if (initTask.IsCompleted)
                return;

            socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            var socketTask = socket.ConnectAsync(IPAddress.Loopback, Configuration.Port);

            var awaitedTask = await Task.WhenAny(socketTask, Task.Delay(-1, Token));

            Token.ThrowIfCancellationRequested();

            if (awaitedTask.IsFaulted)
                ExceptionDispatchInfo.Capture(awaitedTask.Exception.InnerException).Throw();

            ThreadPool.QueueUserWorkItem(s => Start((Stream)s), new NetworkStream(socket));

            FinishInitialize(true);
            await initTask;
        }

        protected override void RaiseOnStart()
        {
            base.RaiseOnStart();

            // the RpcServer adds this type as a local target when a client connects, and clients can also
            // add their own type, so only register this if it's not on one of these lists
            if (!RemoteTypes.Any(x => x is IServerInformation) && !LocalTargets.Any(x => x is IServerInformation))
                RegisterRemoteTarget<IServerInformation>();
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
