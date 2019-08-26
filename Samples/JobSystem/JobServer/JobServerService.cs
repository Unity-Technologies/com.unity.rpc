using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Unity.Ipc;

namespace JobServer
{

    public class JobServerService
    {
        public ConcurrentDictionary<string, Type> DispatchersByName { get; }
        public CancellationToken CancellationToken { get; }

        public JobServerService(CancellationToken cancellationToken)
        {
            DispatchersByName = new ConcurrentDictionary<string, Type>();
            CancellationToken = cancellationToken;
        }

        public bool RegisterDispatcher<TDispatcher>(string dispatcherUniqueName)
        {
            return DispatchersByName.TryAdd(dispatcherUniqueName, typeof(TDispatcher));
        }
    }

    public class JobServerSession : IJobServerService
    {
        private readonly JobServerService serverService;
        private readonly IRequestContext context;
        private IJobClientService Client => context.Get<IJobClientService>();

        private ConcurrentDictionary<string, JobDispatcherBase> _jobsByName;
        private ConcurrentDictionary<int, JobDispatcherBase> _jobsById;
        private int _nextJobId;


        public JobServerSession(JobServerService serverService, IRequestContext context)
        {
            this.serverService = serverService;
            this.context = context;
            _jobsByName = new ConcurrentDictionary<string, JobDispatcherBase>();
            _jobsById = new ConcurrentDictionary<int, JobDispatcherBase>();
        }

        public Task<int> CreateJob(string jobDispatcherUniqueName, string uniqueJobName)
        {
            if (!serverService.DispatchersByName.TryGetValue(jobDispatcherUniqueName, out var dispatcherType))
            {
                return Task.FromResult(-1);
            }

            var jobId = Interlocked.Increment(ref _nextJobId);
            var jobData = Activator.CreateInstance(dispatcherType, jobId, this, Client, serverService.CancellationToken) as JobDispatcherBase;
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
