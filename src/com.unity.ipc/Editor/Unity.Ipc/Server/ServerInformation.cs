using System.Threading.Tasks;

namespace Unity.Ipc
{
    class ServerInformation : IServerInformation
    {
        public IpcVersion Version { get; set; }
        public Task<IpcVersion> GetVersion() => Task.FromResult(Version);
    }
}
