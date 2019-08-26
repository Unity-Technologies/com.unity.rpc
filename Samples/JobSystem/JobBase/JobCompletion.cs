using System;

namespace JobServer
{
    [Serializable]
    public enum JobCompletion
    {
        Undefined = 0,
        Successful,
        Cancelled,
        Faulted
    }
}
