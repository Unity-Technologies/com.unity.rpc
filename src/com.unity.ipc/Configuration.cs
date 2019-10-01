using System;
using System.Collections.Generic;

namespace Unity.Ipc
{
    public class Configuration
    {
        public const int DefaultPort = 59595;
        public int Port { get; protected set; }
        public const string DefaultProtocolVersion = "1.0";
        public IpcVersion ProtocolVersion { get; protected set; }

        public IEnumerable<Type> LocalTypes { get; } = new List<Type>();
        public IEnumerable<Type> RemoteTypes { get; } = new List<Type>();

        /// <summary>
        /// Add type representing a remote proxy
        /// </summary>
        public Configuration AddRemoteTarget<T>()
            where T : class
        {
            ((List<Type>)RemoteTypes).Add(typeof(T));
            return this;
        }

        /// <summary>
        /// Add type representing a local ipc target
        /// </summary>
        public Configuration AddLocalTarget<T>()
            where T : class
        {
            ((List<Type>)LocalTypes).Add(typeof(T));
            return this;
        }
    }
}
