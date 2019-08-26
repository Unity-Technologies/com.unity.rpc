using System.Threading.Tasks;

namespace JobServer
{

    public interface IJobServerService
    {
        Task<int> CreateJob(string jobDispatcherUniqueName, string uniqueJobName);
        Task<bool> StartJob(int jobId);
        Task<bool> CancelJob(int jobId);
    }
}
