using System;

namespace Unity.Ipc.Server
{
    public class IpcServerConnectException : Exception
    {
        public IpcServerConnectException(string message, Exception innerException) : base(message, innerException)
        {

        }
    }
    public class IpcServerAlreadyExistsException : Exception
    {
        public IpcServerAlreadyExistsException(string message) : base(message)
        {

        }
    }
}
