using System;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Client;
using Unity.Ipc.Server;

namespace JobServer
{

    [Serializable]
    public enum JobStatus
    {
        Undefined = 0,
        Created = 1,
        Running = 2,
        Completed = 3
    }

    [Serializable]
    public enum JobCompletion
    {
        Undefined = 0,
        Successful,
        Cancelled,
        Faulted
    }

    public abstract class JobDispatcherBase
    {
        protected JobDispatcherBase(int jobId, JobServerSession jobServerSession, JsonRpcClient serverSideClient)
        {
            JobId            = jobId;
            ServerSideClient = serverSideClient;
            JobServerSession = jobServerSession;
            JobCreationTime  = DateTime.UtcNow;
            JobStatus        = JobStatus.Created;
            JobCompletion    = JobCompletion.Undefined;
        }

        public int JobId { get; }
        protected JobServerSession JobServerSession { get; }
        protected JsonRpcClient ServerSideClient { get; }
        public DateTime JobCreationTime { get; }
        public DateTime JobStartTime { get; private set; }
        public DateTime JobCompletedTime { get; private set; }
        public DateTime JobLastUpdateTime { get; private set; }
        public JobStatus JobStatus { get; private set; }
        public JobCompletion JobCompletion { get; private set; }
        public float JobProgress { get; protected set; }

        public virtual Task<bool> StartJob()
        {
            if (JobStatus != JobStatus.Created)
            {
                return Task.FromResult(false);
            }

            JobStartTime      = DateTime.UtcNow;
            JobLastUpdateTime = JobStartTime;
            JobStatus         = JobStatus.Running;
            JobProgress       = 0;

            return Task.FromResult(true);
        }

        public virtual Task<bool> CancelJob()
        {
            if (JobStatus != JobStatus.Running)
            {
                return Task.FromResult(false);
            }

            JobCompletedTime = DateTime.UtcNow;
            JobStatus        = JobStatus.Completed;
            JobCompletion    = JobCompletion.Cancelled;

            return Task.FromResult(true);
        }

        public virtual async Task<bool> UpdateJobProgress(float jobProgress)
        {
            if (JobStatus != JobStatus.Running)
            {
                return false;
            }

            JobLastUpdateTime = DateTime.UtcNow;
            JobProgress = jobProgress;

            try
            {
                return await ServerSideClient.ExecClientRequest("updateJobProgress", new { jobId = JobId, jobProgress }, CancellationToken.None);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return false;
        }

        public virtual async Task<bool> CompleteSuccessfully()
        {
            if (JobStatus != JobStatus.Running)
            {
                return false;
            }

            JobCompletedTime = DateTime.UtcNow;
            JobStatus = JobStatus.Completed;
            JobCompletion = JobCompletion.Successful;
            JobProgress = 1.0f;

            try
            {
                return await ServerSideClient.ExecClientRequest("jobCompleted", new { jobId = JobId, jobCompletion = JobCompletion.Successful, jobCompletedTime = JobCompletedTime }, CancellationToken.None);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return false;
        }
    }
}