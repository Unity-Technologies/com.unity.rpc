using System;
using System.Threading;
using System.Threading.Tasks;
using JobServer;
using Unity.Rpc;
using Unity.Rpc.Extensions;

namespace JobClient
{
    public class JobEventData<T>
    {
        public JobEventData(int jobId, T data)
        {
            JobId = jobId;
            Data = data;
        }

        public int JobId { get; }
        public T Data { get; }
    }

    public class JobClientService : IJobClientService, IDisposable
    {
        private readonly IRequestContext context;
        private IJobServerService Server => context.GetRemoteTarget<IJobServerService>();
        public event EventHandler<JobEventData<JobStatus>> JobStatusChangedEventHandler;
        public event EventHandler<JobEventData<float>> JobProgressUpdatedEventHandler;
        public event EventHandler<JobEventData<JobCompletion>> JobCompletedEventHandler;

        public JobClientService(IRequestContext context)
        {
            this.context = context;
        }

        public async Task<int> CreateJob(string jobDispatcherUniqueName, string uniqueJobName, CancellationToken cancellationToken)
        {
            (int jobId, bool success) = await Server.CreateJob(jobDispatcherUniqueName, uniqueJobName).Await(cancellationToken);
            if (success && jobId != -1)
            {
                OnJobStatusChanged(jobId, JobStatus.Created);
            }
            return jobId;
        }

        public async Task<bool> StartJob(int jobId, CancellationToken cancellationToken)
        {
            (bool jr, bool success) = await Server.StartJob(jobId).Await(cancellationToken);
            if (success && jr)
            {
                OnJobStatusChanged(jobId, JobStatus.Running);
            }
            return jr;
        }

        public async Task<bool> CancelJob(int jobId, CancellationToken cancellationToken)
        {
            (bool jr, bool success) = await Server.CancelJob(jobId).Await(cancellationToken);
            if (success && jr)
            {
                OnJobStatusChanged(jobId, JobStatus.Completed);
                OnJobCompleted(jobId, JobCompletion.Cancelled);
            }
            return jr;
        }

        public virtual Task<bool> JobCompleted(int jobId, JobCompletion jobCompletion, DateTime jobCompletedTime)
        {
            OnJobStatusChanged(jobId, JobStatus.Completed);
            OnJobCompleted(jobId, jobCompletion);
            return Task.FromResult(true);
        }

        public virtual Task<bool> UpdateJobProgress(int jobId, float jobProgress)
        {
            OnJobProgressUpdated(jobId, jobProgress);
            return Task.FromResult(true);
        }

        internal void OnJobStatusChanged(int jobId, JobStatus jobStatus)
        {
            JobStatusChangedEventHandler?.Invoke(this, new JobEventData<JobStatus>(jobId, jobStatus));
        }

        internal void OnJobProgressUpdated(int jobId, float jobProgress)
        {
            JobProgressUpdatedEventHandler?.Invoke(this, new JobEventData<float>(jobId, jobProgress));
        }

        internal void OnJobCompleted(int jobId, JobCompletion jobCompletion)
        {
            JobCompletedEventHandler?.Invoke(this, new JobEventData<JobCompletion>(jobId, jobCompletion));
        }

        public void Dispose()
        {
        }
    }
}
