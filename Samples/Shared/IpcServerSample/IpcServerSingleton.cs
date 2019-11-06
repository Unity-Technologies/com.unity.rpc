using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.Logging;

namespace Shared
{
    public class IpcServerSingleton : IIpcServerSingleton, IIpcClientInfo
    {
        private readonly ILog logger = LogProvider.GetCurrentClassLogger();

        private HashSet<string> clients = new HashSet<string>();

        public event EventHandler<string> OnClientConnected;

        public void AddClient(string clientId)
        {
            clients.Add(clientId);
        }

        public void RemoveClient(string clientId)
        {
            clients.Remove(clientId);
        }

        public void RaiseOnClientConnected(string clientId)
        {
            OnClientConnected?.Invoke(this, clientId);
        }

        public Task Hello(string clientId)
        {
            logger.Debug($"Client {clientId} says hi");
            return Task.CompletedTask;
        }

        public Task Goodbye(string clientId)
        {
            logger.Debug($"Client {clientId} says goodbye");
            return Task.CompletedTask;
        }
    }
}
