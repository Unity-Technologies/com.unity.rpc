using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Client;
using JsonRpc.Streams;
using Microsoft.Extensions.Logging;

namespace Unity.Ipc.Server
{
    public class ClientInfo : IDisposable
    {
        public DateTime LastAliveDateTime { get; private set; }
        public bool ToShutDown { get; private set; }
        public bool IsAlive { get; private set; }
        public bool IsDisposed => _isDisposed;
        public bool ShouldBeAvailable => IsAlive && !IsDisposed && _reader != null;
        public EndPoint RemoteEndPoint => _socket.RemoteEndPoint;

        private readonly ILogger _logger;
        private readonly Socket _socket;
        private readonly NetworkStream _netStream;
        private readonly ByLineTextMessageReader _reader;
        private readonly ByLineTextMessageWriter _writer;
        private readonly IDisposable _serverIDisposable;
        internal JsonRpcClient _serverSideClient;
        private bool _isDisposed;
        private int _checkingIsAlive;
        private DateTime _nextClientIsAliveCheckTime;
        private IDisposable _serverSideClientIDisposable;
        private IDisposable _clientWideSession;
        private object _serverSideClientLocker;

        public ClientInfo(ILogger logger, Socket socket, NetworkStream netStream, ByLineTextMessageReader reader,
            ByLineTextMessageWriter writer, IDisposable serverIDisposable, JsonRpcClient serverSideClient, IDisposable clientWideSession, IDisposable serverSideClientIDisposable)
        {
            _logger = logger;
            _socket = socket;
            _netStream = netStream;
            _reader = reader;
            _writer = writer;
            _serverIDisposable = serverIDisposable;
            _serverSideClient = serverSideClient;
            _serverSideClientIDisposable = serverSideClientIDisposable;
            _clientWideSession = clientWideSession;
            _nextClientIsAliveCheckTime = DateTime.UtcNow + IpcServerBase.KeepAliveInterval;
            _serverSideClientLocker = new object();
            IsAlive = true;
            _checkingIsAlive = 0;
        }

        public void RequestForShutDown() => ToShutDown = true;

        public async Task CheckIfClientIsAlive()
        {
            // Check if we reach the point in time to check for the client
            if (DateTime.UtcNow < _nextClientIsAliveCheckTime)
            {
                return;
            }

            // Check if we're already trying to figuring out if the client is alive or not (can take a while...)
            // If _checkingIsAlive is 0, then we're not and we set the field to 1
            // If it's 1, we're already checking, then we quit...
            if (Interlocked.CompareExchange(ref _checkingIsAlive, 1, 0) != 0)
            {
                return;
            }

            // Make the call to the client's isAlive JsonRpc Method and return the result
            // Make sure in the finally clause that we reset the _checkingIsAlive field to 0 to specify that we've done with this check
            try
            {
                var rm = await _serverSideClient.SendRequestAsync("isAlive", null, CancellationToken.None);
                if (rm == null || rm.Error != null)
                {
                    IsAlive = false;
                    return;
                }

                IsAlive = rm.Result.ToObject<bool>();
                if (IsAlive)
                {
                    LastAliveDateTime = DateTime.UtcNow;
                }
            }
            catch (Exception e)
            {
                IsAlive = false;
                ToShutDown = true;
            }
            finally
            {
                _nextClientIsAliveCheckTime = DateTime.UtcNow + IpcServerBase.KeepAliveInterval;
                Interlocked.CompareExchange(ref _checkingIsAlive, 0, 1);
            }
        }

        public void SendServerShutdown()
        {
            try
            {
                // This is fire & forget style
                _ = _serverSideClient.SendRequestAsync("serverIsShuttingDown", null, CancellationToken.None);
            }
            catch (Exception e)
            {
                _logger?.LogDebug(0, e, "Error during sending request of 'serverIsShuttingDown'.");
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _serverSideClientIDisposable?.Dispose();
            _serverIDisposable?.Dispose();
            _reader?.Dispose();
            _writer?.Dispose();
            _netStream?.Dispose();
            _socket?.Dispose();
            _clientWideSession?.Dispose();
            _serverSideClient = null;

            _isDisposed = true;
        }
    }
}
