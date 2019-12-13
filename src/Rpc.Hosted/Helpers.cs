using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Unity.Rpc.Hosted.Extensions
{
    static class HelperExtensions
    {
        public static void Forget(this Task task) { }
    }

    public static class HostExtensions
    {
        public static T GetHostedService<T>(this IServiceProvider serviceProvider) where T : class =>
            serviceProvider.GetServices<IHostedService>().FirstOrDefault(x => x is T) as T;

        public static IRequestContext GetRequestContext(this IServiceProvider serviceProvider) => serviceProvider.GetService<IRequestContext>();
        public static IRegistration GetRegistration(this IServiceProvider serviceProvider) => serviceProvider.GetService<IRegistration>();
        public static ILogger<T> GetLogger<T>(this IServiceProvider serviceProvider) => serviceProvider.GetService<ILogger<T>>();
    }
}
