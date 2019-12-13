using System.Threading.Tasks;

namespace Unity.Rpc
{
    class ServerInformation : IServerInformation
    {
        public RpcVersion Version { get; set; }
        public Task<RpcVersion> GetVersion() => Task.FromResult(Version);
    }
}
