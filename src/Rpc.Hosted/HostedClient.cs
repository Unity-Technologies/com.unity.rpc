using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Unity.Rpc.Hosted
{
    public class RpcHostedClient : RpcHost<RpcClient, RpcHostedClient>, IHostedService
    {
        public RpcHostedClient(Configuration configuration)
        {
            Host = this;
            RegisterRpc(new RpcClient(configuration));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            cancellationToken.Register(() => Rpc.Stop());
            await Rpc.Start();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            Rpc.Stop();
            var stopTask = Rpc.Run();
            await Task.WhenAny(stopTask, Task.Delay(-1, cancellationToken));
        }
    }
}
