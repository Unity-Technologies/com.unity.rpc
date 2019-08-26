using System;
using System.Threading.Tasks;

namespace TestApp
{
    public interface IMyServer
    {
        Task<string> StartJob(string id);
        event EventHandler<bool> ThingDone;
    }

    public interface IMyClient
    {
        Task ServerJobStarted(string id);
        Task ServerJobProgress(string id, string message);
        Task ServerJobDone(string id);
    }
}
