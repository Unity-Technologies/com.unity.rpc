using System;

namespace Unity.Ipc.Client
{
    public interface IIpcClientLogger
    {
        void LogError(Exception exception, string message, params object[] args);
        void LogDebug(Exception exception, string message, params object[] args);
        void LogInformation(Exception exception, string message, params object[] args);

        void LogError(string message, params object[] args);
        void LogDebug(string message, params object[] args);
        void LogInformation(string message, params object[] args);
    }
}