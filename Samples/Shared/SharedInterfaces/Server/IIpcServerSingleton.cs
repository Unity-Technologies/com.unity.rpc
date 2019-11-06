using System.Threading.Tasks;

namespace Shared
{
    public interface IIpcServerSingleton
    {
        Task Hello(string clientId);
        Task Goodbye(string clientId);
    }
}
