using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Unity.Ipc.Server
{
    public static class JsonRpcClientExtensions
    {
        public static async Task<(bool, TRes)> ExecClientRequest<TRes>(this JsonRpcClient client, string requestName, object arg, CancellationToken cancellationToken)
        {
            var response = await client.SendRequestAsync(requestName, JToken.FromObject(arg), cancellationToken).ConfigureAwait(false);
            if (response.Error == null)
            {
                return (true, response.Result.ToObject<TRes>());
            }
            return (false, default(TRes));
        }

        public static async Task<bool> ExecClientRequest(this JsonRpcClient client, string requestName, object arg, CancellationToken cancellationToken)
        {
            var response = await client.SendRequestAsync(requestName, JToken.FromObject(arg), cancellationToken).ConfigureAwait(false);
            if (response.Error == null)
            {
                return true;
            }
            return false;
        }

    }
}