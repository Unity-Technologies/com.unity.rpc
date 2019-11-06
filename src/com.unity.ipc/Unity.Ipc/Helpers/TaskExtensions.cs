using System.Threading;
using System.Threading.Tasks;

namespace Unity.Ipc.Extensions
{
    public static class TaskExtensions
    {
        public static async Task<(T, bool)> Await<T>(this Task<T> task, CancellationToken cancellationToken, int msTimeout = -1)
        {
            if ((await Task.WhenAny(task, Task.Delay(msTimeout, cancellationToken))) == task)
                return (task.Result, true);
            return (default(T), false);
        }
    }
}
