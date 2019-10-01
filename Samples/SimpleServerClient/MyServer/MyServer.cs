using System;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Interfaces;
using Unity.Ipc;

namespace MyServer
{
    using Logging;

    public class MyServer : IMyServer
    {
        private readonly IRequestContext context;
        private IMyClient Client => context.Get<IMyClient>();
        private readonly ILog logger = LogProvider.GetCurrentClassLogger();

        private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> jobs =
            new ConcurrentDictionary<string, TaskCompletionSource<bool>>();

        private readonly ConcurrentQueue<string> progress = new ConcurrentQueue<string>();
        public event EventHandler<bool> ThingDone;


        public MyServer(IRequestContext context)
        {
            this.context = context;
        }

        public async Task<string> StartJob(string id)
        {
            logger.Debug($"Starting job {id}");

            jobs.TryAdd(id, new TaskCompletionSource<bool>());
            var progressFlag = new ManualResetEventSlim();
            var finishedFlag = new ManualResetEventSlim();
            var cts = new CancellationTokenSource();

            ThreadPool.QueueUserWorkItem(async _ => await ReportProgress(id, cts, progressFlag, finishedFlag));
            ThreadPool.QueueUserWorkItem(async _ => await DoWork(id, cts, progressFlag, finishedFlag));

            await Client.ServerJobStarted(id);
            return id;
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

                await Client.ServerJobDone(id).ConfigureAwait(false);
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
                        await Client.ServerJobProgress(id, msg).ConfigureAwait(false);
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

    static class HelperExtensions
    {
        public static void Forget(this Task task) { }
        public static void Rethrow(this Exception exception)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }
}
