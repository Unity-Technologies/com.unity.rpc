using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Unity.Rpc;

namespace ServerSample
{
    using Logging;
    using Shared;

    public class RpcServerPerClientConnection : IServerPerConnection
    {
        private readonly ILog logger = LogProvider.For<RpcServerPerClientConnection>();

        private readonly RpcServerSingleton globalServer;
        private readonly IRequestContext context;
        private IJobProgress Client => context.GetRemoteTarget<IJobProgress>();

        private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> jobs =
            new ConcurrentDictionary<string, TaskCompletionSource<bool>>();

        private readonly ConcurrentQueue<string> progress = new ConcurrentQueue<string>();
        public event EventHandler<bool> ThingDone;

        public RpcServerPerClientConnection(RpcServerSingleton globalServer, IRequestContext context)
        {
            this.globalServer = globalServer;
            this.context = context;
        }

        public Task<string> GetClientId() => Task.FromResult(context.Id);

        public async Task<JobData> StartJob(string id)
        {
            var progressFlag = new ManualResetEventSlim();
            var finishedFlag = new ManualResetEventSlim();
            var cts = new CancellationTokenSource();

            logger.Debug($"Starting job {id}");

            jobs.TryAdd(id, new TaskCompletionSource<bool>());

            ThreadPool.QueueUserWorkItem(async _ => await ReportProgress(id, cts, progressFlag, finishedFlag));
            ThreadPool.QueueUserWorkItem(async _ => await DoWork(id, cts, progressFlag, finishedFlag));

            // tell the client that we've started the requested job
            await Client.JobStarted(id);

            return new JobData { ID = id };
        }

        private async Task DoWork(string id, CancellationTokenSource cts,
            ManualResetEventSlim progressFlag, ManualResetEventSlim finishedFlag)
        {
            try
            {
                for (var i = 0; i < 100; i++)
                {
                    if (cts.Token.IsCancellationRequested) break;

                    var msg = "(" + Thread.CurrentThread.ManagedThreadId + ") loop " + i;
                    progress.Enqueue(msg);
                    progressFlag.Set();

                    if (i % 5 == 0)
                        finishedFlag.Wait(100, cts.Token);
                }

                progress.Enqueue(null);
                progressFlag.Set();
                finishedFlag.Wait(cts.Token);

                if (jobs.TryGetValue(id, out var t))
                    t.TrySetResult(true);

                await Client.JobDone(id).ConfigureAwait(false);
                ThingDone?.Invoke(this, true);

                jobs.TryRemove(id, out var _);
            }
            catch (Exception ex)
            {
                if (jobs.TryGetValue(id, out var t))
                    t.TrySetException(ex);

                cts.Cancel();
            }
        }

        private async Task ReportProgress(string id, CancellationTokenSource cts,
            ManualResetEventSlim progressFlag, ManualResetEventSlim finishedFlag)
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    progressFlag.Wait(cts.Token);
                    if (cts.IsCancellationRequested) break;

                    progressFlag.Reset();
                    while (progress.TryDequeue(out string msg))
                    {
                        if (cts.IsCancellationRequested) break;
                        if (msg == null)
                        {
                            finishedFlag.Set();
                            return;
                        }
                        await Client.JobProgress(id, msg).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                if (jobs.TryGetValue(id, out var t))
                    t.TrySetException(ex);

                cts.Cancel();
            }
        }
    }
}
