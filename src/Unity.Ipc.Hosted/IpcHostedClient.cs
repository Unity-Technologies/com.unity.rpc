using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Unity.Ipc.Hosted
{
    public class IpcHostedClient : IpcHost<IpcClient, IpcHostedClient>, IHostedService
    {
        public IpcHostedClient(Configuration configuration)
        {
            Host = this;
            RegisterIpc(new IpcClient(configuration));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            cancellationToken.Register(() => Ipc.Stop());
            await Ipc.Initialize();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            Ipc.Stop();
            var stopTask = Ipc.Run();
            await Task.WhenAny(stopTask, Task.Delay(-1, cancellationToken));
        }
    }
}
