using System;
using System.Threading;
using System.Threading.Tasks;

namespace JobServer
{

    public abstract class JobDispatcherBase
    {
        protected CancellationToken CancellationToken { get; }

        protected JobDispatcherBase(int jobId, IJobServerService server, IJobClientService client, CancellationToken cancellationToken)
        {
            JobId = jobId;
            Client = client;
            CancellationToken = cancellationToken;
            Server = server;
            JobCreationTime = DateTime.UtcNow;
            JobStatus = JobStatus.Created;
            JobCompletion = JobCompletion.Undefined;
        }

        public int JobId { get; }
        protected IJobServerService Server { get; }
        protected IJobClientService Client { get; }
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

            JobStartTime = DateTime.UtcNow;
            JobLastUpdateTime = JobStartTime;
            JobStatus = JobStatus.Running;
            JobProgress = 0;

            return Task.FromResult(true);
        }

        public virtual Task<bool> CancelJob()
        {
            if (JobStatus != JobStatus.Running)
            {
                return Task.FromResult(false);
            }

            JobCompletedTime = DateTime.UtcNow;
            JobStatus = JobStatus.Completed;
            JobCompletion = JobCompletion.Cancelled;

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

            CancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await Client.UpdateJobProgress(JobId, jobProgress);
            }
            catch (OperationCanceledException)
            {
                throw;
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
                return await Client.JobCompleted(JobId, JobCompletion.Successful, JobCompletedTime);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return false;
        }
    }
}
