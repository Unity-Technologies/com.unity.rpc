using System;

namespace Shared
{
    public interface IIpcClientInfo
    {
        event EventHandler<string> OnClientConnected;
    }
}
