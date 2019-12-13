using System.Threading.Tasks;

namespace Shared
{
    public interface IServerSingleton
    {
        Task Hello(string clientId);
        Task Goodbye(string clientId);
    }
}
