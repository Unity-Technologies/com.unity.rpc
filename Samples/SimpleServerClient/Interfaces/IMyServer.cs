using System;
using System.Threading.Tasks;

namespace Interfaces
{
    public interface IMyServer
    {
        Task<string> StartJob(string id);
        event EventHandler<bool> ThingDone;
    }
}
