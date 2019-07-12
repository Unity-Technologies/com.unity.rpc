using System.Threading.Tasks;
using JsonRpc.Contracts;
using JsonRpc.Server;

namespace Unity.Ipc.Client
{
    public class IpcClientService : JsonRpcService
    {
        [JsonRpcMethod]
        public Task<bool> IsAlive()
        {
            return Task.FromResult(true);
        }

        [JsonRpcMethod]
        public Task<bool> ServerIsShuttingDown()
        {
            var client = RequestContext.Features.Get<IpcClient>();
            client.ServerIsShuttingDown();

            return Task.FromResult(true);
        }
    }
}