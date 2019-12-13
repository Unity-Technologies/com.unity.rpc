using System;
using System.Runtime.Serialization;

namespace Unity.Rpc
{
    public class ProtocolVersionMismatchException : Exception {
		public ProtocolVersionMismatchException(string message) : base(message) {}
		public ProtocolVersionMismatchException(RpcVersion clientVersion, RpcVersion serverVersion) : base("Client version " + clientVersion + " does not match server version " + serverVersion) { }
		public ProtocolVersionMismatchException(SerializationInfo info, StreamingContext context) : base(info, context) { }
	}
}
