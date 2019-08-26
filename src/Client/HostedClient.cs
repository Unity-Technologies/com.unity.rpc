using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Unity.Ipc.Extensions;

namespace Unity.Ipc
{

    public class HostedClient : IHostedService, IDisposable
    {
        private readonly ClientConfiguration configuration;
        private readonly IServiceProvider serviceProvider;
        private readonly TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

        public IpcClient Client { get; }

        public HostedClient(ClientConfiguration configuration, IServiceProvider serviceProvider)
        {
            this.configuration = configuration;
            this.serviceProvider = serviceProvider;
            Client = new IpcClient(configuration, cts.Token);
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
                await Client.Connect();

                // Get all the previously registered instances that can receive calls and add them as rpc targets
                Client.RegisterLocalTargets((ILocalTargets)serviceProvider.GetService(typeof(ILocalTargets)));

                using (var scope = serviceProvider.CreateScope())
                {
                    Client.RegisterRemoteTargets(configuration.RemoteTypes);
                    var remoteTargets = (IRequestContext)scope.ServiceProvider.GetService(typeof(IRequestContext));
                    remoteTargets.AddTargets(Client.RemoteTargets);

                    // Get or instantiate all the IPC targets that were registered, and add them
                    // as local targets so they can receive rpc calls
                    foreach (var t in configuration.LocalTypes)
                    {
                        var obj = scope.ServiceProvider.GetService(t);
                        Client.RegisterLocalTarget(obj);
                    }
                }

                var serverVersion = await Client.Start();
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
                Client?.Dispose();
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
