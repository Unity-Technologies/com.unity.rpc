using System;
using System.Threading.Tasks;

namespace JobServer
{
    public interface IJobClientService
    {
        Task<bool> UpdateJobProgress(int jobId, float jobProgress);
        Task<bool> JobCompleted(int jobId, JobCompletion jobCompletion, DateTime jobCompletedTime);
    }
}
