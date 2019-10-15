using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.Ipc
{
    public interface IContext
    {
        T Get<T>() where T : class;
        void Register(object instance);
        IEnumerable<object> Instances { get; }
    }

    public interface ILocalTargets : IContext
    { }

    public interface IRequestContext : IContext
    {
        void AddTargets(IEnumerable<object> other);
    }

    public class ProxyContainer : ILocalTargets, IRequestContext
    {
        public IEnumerable<object> Instances { get; } = new List<object>();
        public void Register(object instance) => ((List<object>)Instances).Add(instance);
        public T Get<T>() where T : class => Instances.FirstOrDefault(x => x is T) as T;
        public void AddTargets(IEnumerable<object> targets) => ((List<object>)Instances).AddRange(targets);
    }

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
