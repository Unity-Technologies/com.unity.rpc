using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Unity.Ipc
{
	public interface IServerInformation
	{
		Task<IpcVersion> GetVersion();
	}

	public class ProtocolVersionMismatchException : Exception {
		public ProtocolVersionMismatchException(string message) : base(message) {}
		public ProtocolVersionMismatchException(IpcVersion clientVersion, IpcVersion serverVersion) : base("Client version " + clientVersion + " does not match server version " + serverVersion) { }
		public ProtocolVersionMismatchException(SerializationInfo info, StreamingContext context) : base(info, context) { }
	}
}
