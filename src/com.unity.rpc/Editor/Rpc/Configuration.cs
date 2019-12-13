using MessagePack;
using MessagePack.Resolvers;

namespace Unity.Rpc
{
    public class Configuration
    {
        private static IFormatterResolver[] defaultResolvers;

        public const int DefaultPort = 0;
        public const string DefaultProtocolVersion = "1.0";

        public static IFormatterResolver[] DefaultResolvers
        {
            get => defaultResolvers ?? (defaultResolvers = new[]
            {
                BuiltinResolver.Instance,
                AttributeFormatterResolver.Instance,

                // replace enum resolver
                DynamicEnumAsStringResolver.Instance,

                DynamicGenericResolver.Instance,
                DynamicUnionResolver.Instance,
                DynamicObjectResolver.Instance,

                PrimitiveObjectResolver.Instance,

                ContractlessStandardResolver.Instance,

                // final fallback(last priority)
                DynamicContractlessObjectResolver.Instance
            });
            set => defaultResolvers = value;
        }

        /// <summary>
        /// The default constructor registers a set of default resolvers for
        /// message pack serialization. If you want to change the defaults,
        /// set <see cref="DefaultResolvers"/>
        /// </summary>
        public Configuration() : this(DefaultResolvers)
        {}

        protected Configuration(params IFormatterResolver[] resolvers)
        {
            CompositeResolver.RegisterAndSetAsDefault(resolvers);
        }

        public virtual IServerInformation GetServerInformation()
        {
            return new ServerInformation { Version = ProtocolVersion };
        }

        /// <summary>
        /// Default port is 0.
        /// </summary>
        public int Port { get; set; } = DefaultPort;
        public RpcVersion ProtocolVersion { get; set; } = RpcVersion.Parse(DefaultProtocolVersion);
        public string Version { get => ProtocolVersion.Version; set => ProtocolVersion = RpcVersion.Parse(value); }
    }
}
