using System;
using JsonRpc.Client;

namespace Unity.Ipc.Server
{
    public abstract class ClientSessionBase : IDisposable
    {
        protected ClientSessionBase()
        {
            IsDisposed = false;
        }

        internal void Initialize(ServerSession serverSession, ClientInfo clientInfo)
        {
            ServerSessionBase = serverSession;
            ClientInfo = clientInfo;
        }

        public ClientInfo ClientInfo { get; private set; }
        public JsonRpcClient JsonRpcClient => ClientInfo?._serverSideClient;

        protected ServerSession ServerSessionBase;

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
            ClientInfo = null;
            ServerSessionBase = null;
        }

        public bool IsDisposed { get; private set; }
    }

    public class ClientSession<TServerSession> : ClientSessionBase where TServerSession : ServerSession
    {
        public TServerSession ServerSession => (TServerSession) ServerSessionBase;
    }
}
