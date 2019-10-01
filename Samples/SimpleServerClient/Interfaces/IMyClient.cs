using System.Threading.Tasks;

namespace Interfaces
{
    public interface IMyClient
    {
        Task ServerJobStarted(string id);
        Task ServerJobProgress(string id, string message);
        Task ServerJobDone(string id);
    }
}
