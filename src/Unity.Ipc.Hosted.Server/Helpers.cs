using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace Unity.Ipc.Hosted.Server
{
    static class HelperExtensions
    {
        public static void Forget(this Task task) { }
        public static void Rethrow(this Exception exception)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        public static T GetHostedService<T>(this IServiceProvider serviceProvider) where T : class =>
            serviceProvider.GetServices<IHostedService>().FirstOrDefault(x => x is T) as T;
    }

    class DisposableDictionary<TKey, TValue> : ConcurrentDictionary<TKey, TValue>, IDisposable
        where TValue : IDisposable
    {
        private bool disposed;
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                var list = Values.ToArray();
                foreach (var disp in list)
                {
                    try
                    {
                        disp.Dispose();
                    }
                    catch { }
                }
                Clear();
            }
            disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
