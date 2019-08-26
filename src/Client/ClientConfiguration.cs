using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Unity.Ipc
{
    public class ClientConfiguration : Unity.Ipc.Configuration
    {
        public ClientConfiguration Configure(HostBuilderContext context, IServiceCollection services)
        {
            Port = context.Configuration.GetValue("port", DefaultPort);
            ProtocolVersion = IpcVersion.Parse(context.Configuration.GetValue("version", "1.0"));

            services.AddSingleton<IRequestContext, ProxyContainer>();
            services.AddSingleton<ILocalTargets, ProxyContainer>();

            foreach (var type in LocalTypes)
            {
                services.AddSingleton(type);
            }
            return this;
        }
    }
}
