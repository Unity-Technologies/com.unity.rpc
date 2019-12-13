using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using StreamRpc;

namespace Unity.Rpc
{
    public interface IRegistration
    {
        void RegisterLocalTarget(object instance);
        void RegisterRemoteTarget<TType>() where TType : class;
        void RegisterRemoteTarget(Type type);
        void RegisterRemoteTarget(object remoteProxy);
    }

    public class Rpc<T> : Rpc
        where T : Rpc<T>, IRequestContext
    {
        public Rpc(Configuration configuration, CancellationToken token)
            : base(configuration, token)
        {}

        public new T Attach(Stream stream)
        {
            Attach(stream);
            return (T)this;
        }

        public T Starting(Action<IRegistration, IRequestContext> onStart)
        {
            OnStart += onStart;
            return (T)this;
        }

        public T Stopping(Action<Rpc> onStop)
        {
            OnStop += onStop;
            return (T)this;
        }

        public T Ready(Action<IRequestContext> onReady)
        {
            OnReady += onReady;
            return (T)this;
        }

        public T Disconnected(Action<JsonRpcDisconnectedEventArgs> onDisconnected)
        {
            OnDisconnected += onDisconnected;
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
    }

    /// <summary>
    /// An ipc sender/receiver
    /// Call <seealso cref="Attach"/> first, then register receivers and senders
    /// with <seealso cref="RegisterLocalTarget" /> and <seealso cref="RegisterRemoteTarget" />
    /// and then call <seealso cref="StartListening" /> to listen to remote calls.
    /// </summary>
    public class Rpc : RpcContext, IRegistration
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


        public event Action<JsonRpcDisconnectedEventArgs> OnDisconnected;
        public event Action<IRegistration, IRequestContext> OnStart;
        public event Action<IRequestContext> OnReady;
        public event Action<Rpc> OnStop;

        private readonly TaskCompletionSource<bool> initTask = new TaskCompletionSource<bool>();
        private readonly TaskCompletionSource<bool> startTask = new TaskCompletionSource<bool>();
        private readonly TaskCompletionSource<bool> stopTask = new TaskCompletionSource<bool>();

        public Rpc(Configuration configuration, CancellationToken token)
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
            return initTask.Task;
        }

        /// <summary>
        /// Initializes and starts everything. Once this is done, Get* methods can be called.
        /// </summary>
        /// <returns></returns>
        public virtual async Task Start()
        {
            await Initialize();
            await startTask.Task;
        }


        /// <summary>
        /// Starts the ipc host, attaching it to the given stream.
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public virtual bool Start(Stream stream) => Start(stream, true);

        /// <summary>
        /// Runs until stopped or disconnected. This is equivalent to <see cref="Start" /> and <see cref="WaitUntilDone" /> calls.
        /// </summary>
        /// <returns></returns>
        public virtual async Task Run()
        {
            await Initialize();
            await WaitUntilDone();
        }

        /// <summary>
        /// Stop the server. You can run this on any thread and it'll trigger a shutdown, which will complete both <see cref="Run" /> and <see cref="WaitUntilDone" />
        /// </summary>
        public virtual void Stop()
        {
            RaiseOnStop();
            FinishStop();
        }

        /// <summary>
        /// After Stop has been called, you can wait on this to asynchronously block until shutdown is complete
        /// </summary>
        /// <returns></returns>
        public virtual async Task WaitUntilDone()
        {
            await stopTask.Task;
        }

        /// <summary>
        /// Starts the ipc host, attaching it to the given stream.
        /// </summary>
        /// <param name="stream">The stream to attach to.</param>
        /// <param name="startListening">The RpcServer sets this to false, as the stream here is not for use with the rpc object and should not be listened to. Instead, when clients connect, their sockets will be attached to rpc objects.</param>
        /// <returns></returns>
        protected bool Start(Stream stream, bool startListening)
        {
            try
            {
                if (startListening)
                    Attach(stream);

                // add any instances that were added prior to start getting called
                AddTargets();

                RaiseOnStart();

                // add anything that was registered on the start callback
                AddTargets();

                if (startListening)
                    InternalStartListening();

                RaiseOnReady();

                FinishStart(true);

                return true;
            }
            catch (Exception ex)
            {
                FinishStop(false, ex);
                return false;
            }
        }

        internal void FinishInitialize(bool success = true, Exception ex = null)
        {
            if (success)
            {
                initTask.TrySetResult(success);
            }
            else
            {
                initTask.TrySetException(ex);
                startTask.TrySetException(ex);
                stopTask.TrySetException(ex);
                cts.Cancel();
            }
        }

        internal void FinishStart(bool success = true, Exception ex = null)
        {
            if (success)
            {
                initTask.TrySetResult(success);
                startTask.TrySetResult(success);
            }
            else
            {
                initTask.TrySetException(ex);
                startTask.TrySetException(ex);
                stopTask.TrySetException(ex);
                cts.Cancel();
            }
        }

        internal void FinishStop(bool success = true, Exception ex = null)
        {
            if (success)
            {
                initTask.TrySetResult(success);
                startTask.TrySetResult(success);
                stopTask.TrySetResult(success);
            }
            else
            {
                initTask.TrySetException(ex);
                startTask.TrySetException(ex);
                stopTask.TrySetException(ex);
            }
            cts.Cancel();
        }

        protected virtual void RaiseOnStart()
        {
            OnStart?.Invoke(this, this);
            OnStart = null;
        }

        protected virtual void RaiseOnReady()
        {
            OnReady?.Invoke(this);
            OnReady = null;
        }

        protected virtual void RaiseOnStop()
        {
            OnStop?.Invoke(this);
            OnStop = null;
        }

        protected virtual void RaiseOnDisconnected(JsonRpcDisconnectedEventArgs args)
        {
            OnDisconnected?.Invoke(args);
            Stop();
        }

        /// <summary>
        /// Initialize the ipc sender/receiver object to use this stream for sending/receiving
        /// </summary>
        public void Attach(Stream stream)
        {
            rpc = new JsonRpc(stream);
            rpc.Disconnected += (_, e) => {
                RaiseOnDisconnected(e);
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

                foreach (var obj in remoteProxies)
                {
                    Register(TargetType.Remote, AddRemoteProxy(obj));
                }

                remoteProxies.Clear();
            }

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


        protected void InternalRegisterRemoteProxy(Type type) => remoteTypes.Add(type);
        void IRegistration.RegisterRemoteTarget<TType>() => remoteTypes.Add(typeof(TType));
        void IRegistration.RegisterRemoteTarget(Type type) => remoteTypes.Add(type);

        protected void InternalRegisterLocalTarget(object instance) => localTargets.Add(instance);
        void IRegistration.RegisterLocalTarget(object instance) => localTargets.Add(instance);

        protected void InternalRegisterRemoteProxy(object instance) => remoteProxies.Add(instance);
        void IRegistration.RegisterRemoteTarget(object instance) => remoteProxies.Add(instance);

        private bool disposed;
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposed)
                return;
            if (disposing)
            {
                rpc?.Dispose();
                OnDisconnected = null;
            }
            disposed = true;
        }
    }
}
