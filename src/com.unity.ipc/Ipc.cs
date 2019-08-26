using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using StreamRpc;

namespace Unity.Ipc
{
    /// <summary>
    /// An ipc sender/receiver
    /// Call <seealso cref="Attach"/> first, then register receivers and senders
    /// with <seealso cref="RegisterLocalTarget" /> and <seealso cref="RegisterRemoteTarget" />
    /// and then call <seealso cref="StartListening" /> to listen to remote calls.
    /// </summary>
    public class Ipc : IDisposable
    {
        private readonly IRequestContext remoteTargets = new ProxyContainer();
        private readonly ILocalTargets localTargets = new ProxyContainer();
        private JsonRpc rpc;
        protected CancellationToken Token { get; }
        internal event EventHandler<JsonRpcDisconnectedEventArgs> Disconnected;

        public IEnumerable<object> RemoteTargets => remoteTargets.Instances;
        public IEnumerable<object> LocalTargets => localTargets.Instances;
        public string Id { get; }

        public Ipc(CancellationToken token = default(CancellationToken))
        {
            this.Token = token;
            Id = Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Initialize the ipc sender/receiver object to use this stream for sending/receiving
        /// </summary>
        public void Attach(Stream stream)
        {
            rpc = new JsonRpc(stream);
        }

        /// <summary>
        /// Register a type to be used to call methods/events/properties on the remote. This creates
        /// a proxy that is suitable for making calls.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>A proxy instance of the type</returns>
        public T RegisterRemoteTarget<T>()
            where T : class
        {
            if (rpc == null)
            {
                throw new InvalidOperationException();
            }
            var ret = rpc.Attach<T>();
            remoteTargets.Register(ret);
            return ret;
        }

        /// <summary>
        /// Register a type to be used to call methods/events/properties on the remote. This creates
        /// a proxy that is suitable for making calls.
        /// </summary>
        /// <param name="type"></param>
        /// <returns>A proxy instance of the type</returns>
        public object RegisterRemoteTarget(Type type)
        {
            if (rpc == null)
            {
                throw new InvalidOperationException();
            }
            var ret = rpc.Attach(type, null);
            remoteTargets.Register(ret);
            return ret;
        }

        public void RegisterRemoteTargets(IEnumerable<Type> types)
        {
            foreach (var type in types)
            {
                RegisterRemoteTarget(type);
            }
        }

        /// <summary>
        /// Register an object that is used to receive remote method/events/property calls
        /// </summary>
        /// <param name="instance"></param>
        public void RegisterLocalTarget(object instance)
        {
            rpc?.AddLocalRpcTarget(instance);
            localTargets.Register(instance);
        }

        public void RegisterLocalTargets(ILocalTargets targets)
        {
            foreach (var target in targets.Instances)
            {
                RegisterLocalTarget(target);
            }
        }

        public T GetLocalTarget<T>()
            where T : class
        {
            return localTargets.Get<T>();
        }

        public T GetRemoteTarget<T>()
            where T : class
        {
            return remoteTargets.Get<T>();
        }

        /// <summary>
        /// Start listening to remote calls.
        /// </summary>
        public void StartListening()
        {
            if (rpc == null)
            {
                throw new InvalidOperationException();
            }
            rpc.Disconnected += (sender, args) => Disconnected?.Invoke(sender, args);
            rpc.StartListening(); ;
        }


        private bool disposed;
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;
            if (disposing)
            {
                rpc?.Dispose();
            }
            disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
