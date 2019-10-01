namespace Unity.Ipc.Hosted.Server
{
    public class IpcHost : Hosted.IpcHost<HostedServer>
    {
        public IpcHost(int port = 0, IpcVersion protocolVersion = default) : base(port, protocolVersion) { }
    }
}
