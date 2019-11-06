using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using StreamRpc;

namespace Unity.Ipc
{
    public interface IIpcRegistration
    {
        void RegisterLocalTarget(object instance);
        void RegisterRemoteTarget<TType>() where TType : class;
        void RegisterRemoteTarget(Type type);
        void RegisterRemoteTarget(object remoteProxy);
    }

    public class Ipc<T> : Ipc, IIpcRegistration
        where T : Ipc<T>, IRequestContext
    {
        public event Action<IIpcRegistration, IRequestContext> OnStart;
        public event Action<T> OnStop;
        public event Action<IRequestContext> OnReady;

        public Ipc(Configuration configuration, CancellationToken token)
            : base(configuration, token)
        {}

        public T Attach(Stream stream)
        {
            InternalAttach(stream);
            return (T)this;
        }

        public T Starting(Action<IIpcRegistration, IRequestContext> onStart)
        {
            OnStart += onStart;
            return (T)this;
        }

        public T Stopping(Action<T> onStop)
        {
            OnStop += onStop;
            return (T)this;
        }

        public T Ready(Action<IRequestContext> onReady)
        {
            OnReady += onReady;
            return (T)this;
        }

        public T RegisterLocalTarget(object instance)
        {
            InternalRegisterLocalTarget(instance);
            return (T)this;
        }

        public T RegisterRemoteTarget<TType>() where TType : class => RegisterRemoteTarget(typeof(TType));

        public T RegisterRemoteTarget(Type type)
        {
            InternalRegisterRemoteProxy(type);
            return (T)this;
        }

        public T RegisterRemoteTarget(object remoteProxy)
        {
            InternalRegisterRemoteProxy(remoteProxy);
            return (T)this;
        }

        public T StartListening()
        {
            InternalStartListening();
            return (T)this;
        }

        protected void RaiseOnStart()
        {
            OnStart?.Invoke(this, this);
            OnStart = null;
        }

        protected void RaiseOnStop()
        {
            OnStop?.Invoke((T)this);
            OnStop = null;
        }

        protected void RaiseOnReady()
        {
            OnReady?.Invoke(this);
            OnReady = null;
        }

        void IIpcRegistration.RegisterLocalTarget(object instance)
        {
            RegisterLocalTarget(instance);
        }

        void IIpcRegistration.RegisterRemoteTarget<TType>()
        {
            RegisterRemoteTarget<TType>();
        }

        void IIpcRegistration.RegisterRemoteTarget(Type type)
        {
            RegisterRemoteTarget(type);
        }

        void IIpcRegistration.RegisterRemoteTarget(object remoteProxy)
        {
            RegisterRemoteTarget(remoteProxy);
        }
    }

    /// <summary>
    /// An ipc sender/receiver
    /// Call <seealso cref="Attach"/> first, then register receivers and senders
    /// with <seealso cref="RegisterLocalTarget" /> and <seealso cref="RegisterRemoteTarget" />
    /// and then call <seealso cref="StartListening" /> to listen to remote calls.
    /// </summary>
    public class Ipc : IpcContext
    {
        private JsonRpc rpc;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

        private List<Type> remoteTypes = new List<Type>();
        private List<object> remoteProxies = new List<object>();
        private List<object> localTargets = new List<object>();

        private bool starting = false;

        protected IEnumerable<Type> RemoteTypes => remoteTypes;

        public CancellationToken Token => cts.Token;
        public Configuration Configuration { get; private set; }


        internal event Action<JsonRpcDisconnectedEventArgs> Disconnected;

        public Ipc(Configuration configuration, CancellationToken token)
            : base(Guid.NewGuid().ToString())
        {
            Configuration = configuration;
            token.Register(() => Stop());
        }

        public void Reconfigure(Configuration configuration)
        {
            if (starting)
                throw new InvalidOperationException("Cannot reconfigure after calling Initialize/Start/Run, sorry!");

            Configuration = configuration;
        }

        public virtual Task Initialize()
        {
            starting = true;
            return Task.CompletedTask;
        }

        public virtual Task Run()
        {
            return Task.CompletedTask;
        }

        public virtual void Stop()
        {
            cts.Cancel();
        }

        /// <summary>
        /// Initialize the ipc sender/receiver object to use this stream for sending/receiving
        /// </summary>
        protected void InternalAttach(Stream stream)
        {
            rpc = new JsonRpc(stream);
            rpc.Disconnected += (_, e) => {
                Disconnected?.Invoke(e);
                Dispose();
            };
        }

        internal void AddTargets()
        {
            if (rpc != null)
            {
                // generate and register proxies
                foreach (var type in RemoteTypes)
                {
                    Register(TargetType.Remote, GenerateAndAddProxy(type));
                }

                remoteTypes.Clear();
            }

            foreach (var obj in remoteProxies)
            {
                Register(TargetType.Remote, AddRemoteProxy(obj));
            }

            remoteProxies.Clear();

            foreach (var obj in localTargets)
            {
                Register(TargetType.Local, AddLocalTarget(obj));
            }

            localTargets.Clear();
        }

        /// <summary>
        /// Register a type to be used to call methods/events/properties on the remote. This creates
        /// a proxy that is suitable for making calls.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>A proxy instance of the type</returns>
        public T GenerateAndAddProxy<T>() where T : class
        {
            return rpc.Attach<T>();
        }

        /// <summary>
        /// Register a type to be used to call methods/events/properties on the remote. This creates
        /// a proxy that is suitable for making calls.
        /// </summary>
        /// <param name="type"></param>
        /// <returns>A proxy instance of the type</returns>
        public object GenerateAndAddProxy(Type type)
        {
            return rpc.Attach(type, null);
        }

        /// <summary>
        /// Register a pregenerated proxy object suitable for calling methods/events/properties on the remote.
        /// </summary>
        /// <param name="proxyInstance">The proxy instance</param>
        protected object AddRemoteProxy(object proxyInstance)
        {
            rpc?.AddLocalRpcTarget(proxyInstance);
            return proxyInstance;
        }

        /// <summary>
        /// Register an object that is used to receive remote method/events/property calls
        /// </summary>
        /// <param name="instance"></param>
        protected object AddLocalTarget(object instance)
        {
            rpc?.AddLocalRpcTarget(instance);
            return instance;
        }

        protected void InternalRegisterRemoteProxy(Type type)
        {
            remoteTypes.Add(type);
        }

        protected void InternalRegisterLocalTarget(object instance)
        {
            localTargets.Add(instance);
        }

        protected void InternalRegisterRemoteProxy(object instance)
        {
            remoteProxies.Add(instance);
        }

        /// <summary>
        /// Start listening to remote calls.
        /// </summary>
        protected void InternalStartListening()
        {
            if (rpc == null)
            {
                throw new InvalidOperationException();
            }
            rpc.StartListening();
        }


        private bool disposed;
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposed)
                return;
            if (disposing)
            {
                rpc?.Dispose();
                Disconnected = null;
            }
            disposed = true;
        }
    }
}
