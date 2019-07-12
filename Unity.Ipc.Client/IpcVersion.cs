using System;
using System.Diagnostics;

namespace Unity.Ipc.Client
{
    [Serializable]
    [DebuggerDisplay("{Major}.{Minor}.{Build}.{ProtocolRevision}")]
    public class IpcVersion
    {
        public readonly int Major;
        public readonly int Minor;
        public readonly int Build;
        public readonly int ProtocolRevision;

        public IpcVersion(int major, int minor, int build, int protocolRevision)
        {
            Major = major;
            Minor = minor;
            Build = build;
            ProtocolRevision = protocolRevision;
        }

        public override string ToString()
        {
            return $"{Major}.{Minor}.{Build}.{ProtocolRevision}";
        }
    }
}