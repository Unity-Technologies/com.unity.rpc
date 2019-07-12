using System;
using System.Net;
using System.Threading.Tasks;
using JsonRpc.Server;
using Microsoft.Extensions.Logging;
using Unity.Ipc.Client;

namespace Unity.Ipc.Server
{
    /// <summary>
    /// Ipc Server class to use if you don't need a server and client session
    /// </summary>
    /// <typeparam name="TService">Type of the Json Rpc service</typeparam>
    public class IpcServer<TService> : IpcServerBase
        where TService : IpcServerServiceBase
    {
        public IpcServer(string fileMutexPath, ILogger logger = null) : base(logger)
        {
            _fileMutexPath = fileMutexPath;
            _serviceType = typeof(TService);
        }

        /// <summary>
        /// Start hosting the server
        /// </summary>
        /// <param name="serverUniqueName">Unique name of the server, mainly for information/debugging purpose. Should be unique though because we may rely on this to locate/identify the server in future version</param>
        /// <param name="baseTcpPort">The base hosting TCP port, this WON'T be the actual port used to host the server, the ProtocolVersion specified in <paramref name="version"/> will also take into consideration, see remarks for more information </param>
        /// <param name="version">The version of the server, Major, Minor & Build are following the SemVer rules, the ProtocolVersion must be a number that is incremented each time there's a new version of the client/server protocol.</param>
        /// <returns>A instance of the task object that runs the hosting routine of the server. WARNING: waiting for this task completion means waiting for the server to end!</returns>
        public Task StartLocalTcp(string serverUniqueName, int baseTcpPort, IpcVersion version)
        {
            return StartLocalTcpInternal(serverUniqueName, baseTcpPort, version);
        }
    }

    /// <summary>
    /// Ipc Server class to use if you need a session object at the server level (shared by all client services)
    /// </summary>
    /// <typeparam name="TService">Type of the Json Rpc service</typeparam>
    /// <typeparam name="TServerSession">Type of the server session object</typeparam>
    public class IpcServer<TService, TServerSession> : IpcServerBase
        where TService : IpcServerServiceBase<TServerSession>
        where TServerSession : ServerSession
    {
        public IpcServer(string fileMutexPath, ILogger logger = null) : base(logger)
        {
            _fileMutexPath = fileMutexPath;
            _serviceType = typeof(TService);
        }

        /// <summary>
        /// Start hosting the server
        /// </summary>
        /// <param name="serverUniqueName">Unique name of the server, mainly for information/debugging purpose. Should be unique though because we may rely on this to locate/identify the server in future version</param>
        /// <param name="baseTcpPort">The base hosting TCP port, this WON'T be the actual port used to host the server, the ProtocolVersion specified in <paramref name="version"/> will also take into consideration, see remarks for more information </param>
        /// <param name="version">The version of the server, Major, Minor & Build are following the SemVer rules, the ProtocolVersion must be a number that is incremented each time there's a new version of the client/server protocol.</param>
        /// <param name="serverSessionFactory">A factory used to access to an object that will be associated with this server instance and acting as the session.
        /// From the server side service you can call <code>RequestContext.Features.Get()</code> to retrieve your session object.</param>
        /// <returns>A instance of the task object that runs the hosting routine of the server. WARNING: waiting for this task completion means waiting for the server to end!</returns>
        public Task StartLocalTcp(string serverUniqueName, int baseTcpPort, IpcVersion version, Func<TServerSession> serverSessionFactory)
        {
            if (serverSessionFactory == null)
            {
                throw new ArgumentException("Server Session Factory can't be null, use the IpcServer<TService> class instead", nameof(serverSessionFactory));
            }

            _serverSessionObject = serverSessionFactory();
            _serverSessionObjectType = typeof(TServerSession);

            return StartLocalTcpInternal(serverUniqueName, baseTcpPort, version);
        }

    }

    /// <summary>
    /// Ipc Server class to use if you need a session object at the server level (shared by all client services) and one session object dedicated for each connected client
    /// </summary>
    /// <typeparam name="TService">Type of the Json Rpc service</typeparam>
    /// <typeparam name="TServerSession">Type of the server session object</typeparam>
    /// <typeparam name="TClientSession"></typeparam>
    public class IpcServer<TService, TServerSession, TClientSession> : IpcServerBase
        where TService : IpcServerServiceBase<TServerSession, TClientSession>
        where TServerSession : ServerSession
        where TClientSession : ClientSession<TServerSession>
    {
        readonly string _fileMutexPath;

        public IpcServer(string fileMutexPath, ILogger logger = null) : base(logger)
        {
            _fileMutexPath = fileMutexPath;
            _serviceType = typeof(TService);
        }

        /// <summary>
        /// Start hosting the server
        /// </summary>
        /// <param name="serverUniqueName">Unique name of the server, mainly for information/debugging purpose. Should be unique though because we may rely on this to locate/identify the server in future version</param>
        /// <param name="baseTcpPort">The base hosting TCP port, this WON'T be the actual port used to host the server, the ProtocolVersion specified in <paramref name="version"/> will also take into consideration, see remarks for more information </param>
        /// <param name="version">The version of the server, Major, Minor & Build are following the SemVer rules, the ProtocolVersion must be a number that is incremented each time there's a new version of the client/server protocol.</param>
        /// <param name="serverSessionFactory">A factory used to access to an object that will be associated with this server instance and acting as the session.
        /// From the server side service you can call <code>RequestContext.Features.Get()</code> to retrieve your session object.</param>
        /// <param name="clientSessionFactory">A factory used to create an object that will be dedicated for each connected client, acting as a session to them</param>
        public Task StartLocalTcp(string serverUniqueName, int baseTcpPort, IpcVersion version, Func<TServerSession> serverSessionFactory, Func<TClientSession> clientSessionFactory)
        {
            if (serverSessionFactory != null)
            {
                _serverSessionObject = serverSessionFactory();
                _serverSessionObjectType = typeof(TServerSession);
            }

            _clientSessionFactory = clientSessionFactory;

            return StartLocalTcpInternal(serverUniqueName, baseTcpPort, version);
        }

        protected override ClientSessionBase CreateClientWideSession()
        {
            return _clientSessionFactory();
        }

        private Func<TClientSession> _clientSessionFactory;
    }
}
