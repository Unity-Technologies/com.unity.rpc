using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Interfaces;
using Microsoft.Extensions.Logging;
using Unity.Ipc;

namespace MyClient
{
    public class MyClient : IMyClient
    {
        private readonly ILogger logger;
        private Dictionary<string, TaskCompletionSource<bool>> jobs = new Dictionary<string, TaskCompletionSource<bool>>();
        private readonly IMyServer server;

        public MyClient(IRequestContext remoteTargets, ILogger<MyClient> logger)
        {
            this.logger = logger;
            server = remoteTargets.Get<IMyServer>();
            server.ThingDone += (sender, b) =>
            {
                logger.LogDebug("thing done " + b);

            };
        }

        public Task<JobData> StartJob()
        {
            string id = Guid.NewGuid().ToString();
            var job = new TaskCompletionSource<bool>();
            jobs.Add(id, job);
            return server.StartJob(id);
        }

        public Task WaitForJobDone(JobData id)
        {
            return jobs[id.ID].Task;
        }

        public Task ServerJobStarted(string id)
        {
            return Task.CompletedTask;
        }

        public Task ServerJobProgress(string id, string message)
        {
            logger.LogDebug(message);
            return Task.CompletedTask;
        }

        public Task ServerJobDone(string id)
        {
            jobs[id].SetResult(true);
            return Task.CompletedTask;
        }
    }
}
