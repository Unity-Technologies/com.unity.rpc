using System;

namespace Unity.Ipc.Client
{
    public class NotHostedByDefaultTaskSchedulerException : Exception
    {
        public NotHostedByDefaultTaskSchedulerException()
        {

        }

        public NotHostedByDefaultTaskSchedulerException(string message) : base(message)
        {

        }
    }

    public class BadExecRequestException : Exception
    {

        public BadExecRequestException(string message) : base(message) { }

        public BadExecRequestException(string message, Exception innerException) : base(message, innerException)
        {

        }
    }

    public class IpcClientAlreadyStartedException : Exception
    {

    }

    public class IpcServerNotFoundException : Exception
    {

    }

    public class IpcClientNotAvailableException : Exception
    {

    }
}
