using System;
using System.Threading.Tasks;

namespace Interfaces
{
    public interface IMyServer
    {
        Task<JobData> StartJob(string id);
        event EventHandler<bool> ThingDone;
    }

    public class JobData
    {
        public string ID { get; set; }
    }
}
