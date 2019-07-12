using System;
using System.Threading;
using System.Threading.Tasks;
using JobServer;
using JsonRpc.Contracts;
using JsonRpc.Server;
using Newtonsoft.Json.Linq;
using Unity.Ipc.Client;

namespace JobClient
{
    public class JobClientService : IpcClientService
    {
        private JobClientSession Session => RequestContext.Features.Get<JobClientSession>();

        public JobClientService()
        {
        }

        [JsonRpcMethod]
        public Task<bool> UpdateJobProgress(int jobId, float jobProgress)
        {
            return Session.UpdateJobProgress(jobId, jobProgress);
        }

        [JsonRpcMethod]
        public Task<bool> JobCompleted(int jobId, JobCompletion jobCompletion, DateTime jobCompletedTime)
        {
            return Session.JobCompleted(jobId, jobCompletion, jobCompletedTime);
        }
    }

    public static class JobClientServiceExtensions
    {
        public static async Task<int> CreateJob(this IpcClient<JobClientService> client, string jobDispatcherUniqueName, string uniqueJobName, CancellationToken cancellationToken)
        {
            var jobId = await client.ExecRequest<int>("createJob", JToken.FromObject(new { jobDispatcherUniqueName, uniqueJobName }), cancellationToken);

            if (jobId!=-1)
            {
                var session = client.GetSession<JobClientSession>();
                session.OnJobStatusChanged(jobId, JobStatus.Created);
            }

            return jobId;
        }

        public static async Task<bool> StartJob(this IpcClient<JobClientService> client, int jobId, CancellationToken cancellationToken)
        {
            var jr = await client.ExecRequest<bool>("startJob", JToken.FromObject(new { jobId }), cancellationToken);
            if (jr)
            {
                var session = client.GetSession<JobClientSession>();
                session.OnJobStatusChanged(jobId, JobStatus.Running);
            }
            return jr;
        }

        public static async Task<bool> CancelJob(this IpcClient<JobClientService> client, int jobId, CancellationToken cancellationToken)
        {
            var jr = await client.ExecRequest<bool>("cancelJob", JToken.FromObject(new { jobId }), cancellationToken);
            if (jr)
            {
                var session = client.GetSession<JobClientSession>();
                session.OnJobStatusChanged(jobId, JobStatus.Completed);
                session.OnJobCompleted(jobId, JobCompletion.Cancelled);
            }
            return jr;
        }
    }

}