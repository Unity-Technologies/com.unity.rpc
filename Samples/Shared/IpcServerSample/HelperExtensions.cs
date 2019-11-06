using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace Shared
{
    static class ExceptionExtensions
    {
        public static void Forget(this Task task) { }
        public static void Rethrow(this Exception exception)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }
}
