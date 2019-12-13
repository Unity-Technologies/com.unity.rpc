using System;

namespace Shared
{
    public interface IClientInfo
    {
        event EventHandler<string> OnClientConnected;
    }
}
