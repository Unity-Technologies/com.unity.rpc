using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.Ipc
{
    /// <summary>
    /// An ipc client that listens on a tcp socket
    /// </summary>
    public class IpcClient : Ipc<IpcClient>
    {
        private Socket socket;
        private readonly TaskCompletionSource<bool> stopTask = new TaskCompletionSource<bool>();

        public IpcClient(Configuration configuration, CancellationToken token = default)
            : base(configuration, token)
        {}

        public override async Task Initialize()
        {
            await base.Initialize();

            socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            var socketTask = socket.ConnectAsync(IPAddress.Loopback, Configuration.Port);

            var awaitedTask = await Task.WhenAny(socketTask, Task.Delay(-1, Token));

            Token.ThrowIfCancellationRequested();

            if (awaitedTask.IsFaulted)
                ExceptionDispatchInfo.Capture(awaitedTask.Exception.InnerException).Throw();

            ThreadPool.QueueUserWorkItem(s => InternalStart((Socket)s), socket);
        }

        internal void InternalStart(Socket sock)
        {
            Attach(new NetworkStream(sock));

            // add any instances that were added prior to start getting called
            AddTargets();

            RaiseOnStart();

            // the IpcServer adds this type as a local target when a client connects, and clients can also
            // add their own type, so only register this if it's not on one of these lists
            if (!RemoteTypes.Any(x => x is IServerInformation) && !LocalTargets.Any(x => x is IServerInformation))
                RegisterRemoteTarget<IServerInformation>();

            // add anything that was registered on the start callback
            AddTargets();

            StartListening();

            RaiseOnReady();
        }

        public override async Task Run()
        {
            if (socket == null)
                await Initialize();

            await stopTask.Task;
        }

        public override void Stop()
        {
            base.Stop();

            RaiseOnStop();

            stopTask.TrySetResult(true);
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
