using System.Threading.Tasks;
using JsonRpc.Contracts;
using JsonRpc.Server;
using Unity.Ipc.Server;

namespace JobServer
{
    public class JobServerService : IpcServerServiceBase<JobServerSession>
    {
        public JobServerService()
        {
        }

        [JsonRpcMethod]
        public Task<int> CreateJob(string jobDispatcherUniqueName, string uniqueJobName)
        {
            return ServerSession.CreateJob(jobDispatcherUniqueName, uniqueJobName, this);
        }

        [JsonRpcMethod]
        public Task<bool> StartJob(int jobId)
        {
            return ServerSession.StartJob(jobId);
        }

        [JsonRpcMethod]
        public Task<bool> CancelJob(int jobId)
        {
            return ServerSession.CancelJob(jobId);
        }

    }
}
