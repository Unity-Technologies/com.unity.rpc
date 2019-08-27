using System;

namespace Unity.Ipc
{
    public interface IIpcHost : IDisposable
    {
        Ipc Ipc { get; }
    }
}
