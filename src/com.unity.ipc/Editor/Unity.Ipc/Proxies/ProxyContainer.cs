using System;
using System.Collections.Generic;
using System.Linq;

namespace Unity.Ipc
{
    public enum TargetType
    {
        Local,
        Remote
    }

    public interface IRequestContext : IDisposable
    {
        string Id { get; }
        T Get<T>(TargetType targetType) where T : class;
        T GetLocalTarget<T>() where T : class;
        T GetRemoteTarget<T>() where T : class;

        IEnumerable<object> LocalTargets { get; }
        IEnumerable<object> RemoteTargets { get; }
    }

    public class IpcContext : IRequestContext
    {
        public string Id { get; }
        public IEnumerable<object> LocalTargets { get; } = new List<object>();
        public IEnumerable<object> RemoteTargets { get; } = new List<object>();

        public IpcContext(string id)
        {
            Id = id;
        }
        
        public IRequestContext Register(TargetType targetType, object instance)
        {
            GetTargetsList(targetType).Add(instance);
            return this;
        }

        public T Get<T>(TargetType targetType) where T : class => GetTargetsList(targetType).FirstOrDefault(x => x is T) as T;
        public T GetLocalTarget<T>() where T : class => Get<T>(TargetType.Local);
        public T GetRemoteTarget<T>() where T : class => Get<T>(TargetType.Remote);

        private List<object> GetTargetsList(TargetType targetType) => (List<object>)(targetType == TargetType.Local ? LocalTargets : RemoteTargets);

        protected virtual void Dispose(bool disposing)
        {}

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
