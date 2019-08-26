using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Unity.Ipc
{
    using Logging;
    using Extensions;

    public class HostedServer : IHostedService, IDisposable
    {
        private readonly ServerConfiguration configuration;
        private readonly IServiceProvider serviceProvider;
        private readonly IApplicationLifetime application;
        private readonly ILog logger = LogProvider.GetCurrentClassLogger();

        private readonly TaskCompletionSource<bool> stopTask = new TaskCompletionSource<bool>();
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private readonly DisposableDictionary<string, Ipc> clients = new DisposableDictionary<string, Ipc>();

        public IpcServer Server { get; }

        public Task Completion => stopTask.Task;

        public HostedServer(ServerConfiguration configuration, IServiceProvider serviceProvider, IApplicationLifetime application)
        {
            this.configuration = configuration;
            this.serviceProvider = serviceProvider;
            this.application = application;
            Server = new IpcServer(configuration, cts.Token);
        }

        /// <summary>
        /// Initializes the server thread to listen to client connections.
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                var startTask = Server.Connect();

                // wait until the server finishes initializing
                await Task.WhenAny(startTask, Task.Delay(100, cancellationToken));

                if (!startTask.IsCompleted && !cancellationToken.IsCancellationRequested)
                    throw new TimeoutException("Server took too long to start up, something is wrong");

                ThreadPool.QueueUserWorkItem(async x => await Listen((CancellationToken)x), cts.Token);
            }
            catch (Exception ex)
            {
                stopTask.SetException(ex);
                logger.FatalException("Failed to start server", ex);
                throw;
            }
        }

        /// <summary>
        /// Signal a stop to the server and wait for it to shutdown
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            cts.Cancel();
            await Task.WhenAny(stopTask.Task, Task.Delay(-1, cancellationToken));
        }

        /// <summary>
        /// Run the ipc server and handle client connections as they come in
        /// </summary>
        private async Task Listen(CancellationToken cancellationToken)
        {
            try
            {
                Server.OnClientConnect += (sender, client) => HandleClientConnection(client);
                Server.OnClientDisconnect += (sender, client) =>
                {
                    clients.TryRemove(client.Id, out _);
                    logger.Trace("Client " + client.Id + " disconnected");
                };
                await Task.WhenAny(Server.Run(), Task.Delay(-1, cancellationToken));

                // we're done with shutting down
                stopTask.TrySetResult(true);
            }
            catch (Exception ex)
            {
                // something fatal happened, exit
                stopTask.TrySetException(ex);
                application.StopApplication();
            }
        }

        /// <summary>
        /// When new clients connect, hook up the configured ipc targets for them
        /// </summary>
        /// <param name="client"></param>
        private void HandleClientConnection(Ipc client)
        {
            // Get all the previously registered instances that can receive calls and add them as rpc targets
            var localTargets = (ILocalTargets)serviceProvider.GetService(typeof(ILocalTargets));
            client.RegisterLocalTargets(localTargets);

            // inside this scope, the objects instantiated via DI are shared with each other
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

            // keep track of the endpoint so we can dispose it
            clients.TryAdd(client.Id, client);
            logger.Trace("Client " + client.Id + " connected");

            // start listening to client requests
            client.StartListening();
        }

        #region IDisposable
        private bool disposed;
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;
            if (disposing)
            {
                clients?.Dispose();
            }
            disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
