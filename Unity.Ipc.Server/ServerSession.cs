using System;

namespace Unity.Ipc.Server
{
    public class ServerSession : IDisposable
    {
        public event EventHandler<ClientInfo> NewClientConnected;
        public event EventHandler<ClientInfo> ClientDisconnected;

        internal void DoNewClientConnected(ClientInfo ci)
        {
            NewClientConnected?.Invoke(this, ci);
        }

        internal void DoClientDisconnected(ClientInfo ci)
        {
            ClientDisconnected?.Invoke(this, ci);
        }
        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            OnDispose();

            IsDisposed = true;
        }

        protected virtual void OnDispose()
        {
        }

        public bool IsDisposed { get; private set; }
    }
}
