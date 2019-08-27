using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Unity.Ipc.Hosted.Client
{
    public class HostedClient : IHostedService, IIpcHost
    {
        private readonly HostedConfiguration configuration;
        private readonly IServiceProvider serviceProvider;
        private readonly TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private readonly IpcClient client;

        public Ipc Ipc => client;

        public HostedClient(HostedConfiguration configuration, IServiceProvider serviceProvider)
        {
            this.configuration = configuration;
            this.serviceProvider = serviceProvider;
            client = new IpcClient(configuration, cts.Token);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            ThreadPool.QueueUserWorkItem(_ => Connect().Forget());
            await tcs.Task;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            cts.Cancel();
            return Task.WhenAny(tcs.Task, Task.Delay(-1, cancellationToken));
        }

        private async Task Connect()
        {
            try
            {
                await client.Connect();

                // Get all the previously registered instances that can receive calls and add them as rpc targets
                client.RegisterLocalTargets((ILocalTargets)serviceProvider.GetService(typeof(ILocalTargets)));

                using (var scope = serviceProvider.CreateScope())
                {
                    client.RegisterRemoteTargets(configuration.RemoteTypes);
                    var remoteTargets = (IRequestContext)scope.ServiceProvider.GetService(typeof(IRequestContext));
                    remoteTargets.AddTargets(client.RemoteTargets);

                    // Get or instantiate all the IPC targets that were registered, and add them
                    // as local targets so they can receive rpc calls
                    foreach (var t in configuration.LocalTypes)
                    {
                        var obj = scope.ServiceProvider.GetService(t);
                        client.RegisterLocalTarget(obj);
                    }
                }

                var serverVersion = await client.Start();
                if (serverVersion != configuration.ProtocolVersion)
                    throw new ProtocolVersionMismatchException(configuration.ProtocolVersion, serverVersion);
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }

        private bool disposed;

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;
            if (disposing)
            {
                client?.Dispose();
            }
            disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
