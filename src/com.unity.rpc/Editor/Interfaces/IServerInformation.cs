using System.Threading.Tasks;

namespace Unity.Rpc
{
    public interface IServerInformation
    {
        Task<RpcVersion> GetVersion();
    }
}
