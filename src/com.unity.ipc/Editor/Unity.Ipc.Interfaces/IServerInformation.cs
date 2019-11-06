using System.Threading.Tasks;

namespace Unity.Ipc
{
    public interface IServerInformation
    {
        Task<IpcVersion> GetVersion();
    }
}
