using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Unity.Ipc.Server;

namespace JobServer
{
    public class JobServerSession : ServerSession
    {
        public JobServerSession()
        {
            _dispatchersByName = new ConcurrentDictionary<string, Type>();
            _jobsByName        = new ConcurrentDictionary<string, JobDispatcherBase>();
            _jobsById          = new ConcurrentDictionary<int, JobDispatcherBase>();
        }

        public bool RegisterDispatcher<TDispatcher>(string dispatcherUniqueName)
        {
            return _dispatchersByName.TryAdd(dispatcherUniqueName, typeof(TDispatcher));
        }

        private ConcurrentDictionary<string, Type>              _dispatchersByName;
        private ConcurrentDictionary<string, JobDispatcherBase> _jobsByName;
        private ConcurrentDictionary<int, JobDispatcherBase>    _jobsById;
        private int                                             _nextJobId;

        public Task<int> CreateJob(string jobDispatcherUniqueName, string uniqueJobName, JobServerService jobServerService)
        {
            if (!_dispatchersByName.TryGetValue(jobDispatcherUniqueName, out var dispatcherType))
            {
                return Task.FromResult(-1);
            }

            var jobId = Interlocked.Increment(ref _nextJobId);
            jobServerService.TryGetJsonRpcClient(out var serverSideClient);
            var jobData = Activator.CreateInstance(dispatcherType, jobId, this, serverSideClient) as JobDispatcherBase;
            if (_jobsByName.TryAdd(uniqueJobName, jobData))
            {
                _jobsById.TryAdd(jobId, jobData);
                return Task.FromResult(jobId);
            }
            else
            {
                return Task.FromResult(-1);
            }
        }

        public Task<bool> StartJob(int jobId)
        {
            if (!_jobsById.TryGetValue(jobId, out var job))
            {
                return Task.FromResult(false);
            }

            return job.StartJob();
        }

        public Task<bool> CancelJob(int jobId)
        {
            if (!_jobsById.TryGetValue(jobId, out var job))
            {
                return Task.FromResult(false);
            }

            return job.CancelJob();
        }

        public Task<bool> CompleteJobSuccessfully(int jobId)
        {
            if (!_jobsById.TryGetValue(jobId, out var job))
            {
                return Task.FromResult(false);
            }

            return job.CompleteSuccessfully();
        }

        public Task<bool> UpdateJobProgress(int jobId, float completion)
        {
            if (!_jobsById.TryGetValue(jobId, out var job))
            {
                return Task.FromResult(false);
            }

            return job.UpdateJobProgress(completion);
        }
    }
}
