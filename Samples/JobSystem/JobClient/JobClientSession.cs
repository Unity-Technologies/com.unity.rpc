using System;
using System.Threading.Tasks;
using JobServer;

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

    public class JobClientSession : IDisposable
    {
        public void Dispose()
        {
        }

        public event EventHandler<JobEventData<JobStatus>>      JobStatusChangedEventHandler;
        public event EventHandler<JobEventData<float>>          JobProgressUpdatedEventHandler;
        public event EventHandler<JobEventData<JobCompletion>>  JobCompletedEventHandler;

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
    }
}