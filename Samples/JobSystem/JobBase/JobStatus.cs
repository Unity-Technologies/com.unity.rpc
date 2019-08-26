using System;

namespace JobServer
{
    [Serializable]
    public enum JobStatus
    {
        Undefined = 0,
        Created = 1,
        Running = 2,
        Completed = 3
    }
}
