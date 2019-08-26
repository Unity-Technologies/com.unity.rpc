using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Unity.Ipc.Extensions
{
    static class HelperExtensions
    {
        public static void Forget(this Task task) { }
    }

    public static class HostExtensions
    {
        public static T GetHostedService<T>(this IServiceProvider serviceProvider) where T : class =>
            serviceProvider.GetServices<IHostedService>().FirstOrDefault(x => x is T) as T;
    }

}
