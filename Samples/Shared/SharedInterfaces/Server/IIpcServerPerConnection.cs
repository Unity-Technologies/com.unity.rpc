using System;
using System.Threading.Tasks;

namespace Shared
{
    public interface IIpcServerPerConnection
    {
        Task<JobData> StartJob(string id);
        event EventHandler<bool> ThingDone;
        Task<string> GetClientId();
    }
}
