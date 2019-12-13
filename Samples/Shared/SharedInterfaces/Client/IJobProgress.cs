using System.Threading.Tasks;

namespace Shared
{
    public interface IJobProgress
    {
        Task JobStarted(string id);
        Task JobProgress(string id, string message);
        Task JobDone(string id);
    }
}
