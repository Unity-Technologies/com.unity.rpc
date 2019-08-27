namespace Unity.Ipc.Hosted.Client
{
    public class IpcHost : Hosted.IpcHost<HostedClient>
    {
        public IpcHost(int port = 0, IpcVersion protocolVersion = default) : base(port, protocolVersion) { }
    }
}
