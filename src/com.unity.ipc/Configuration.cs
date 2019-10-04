using System;
using System.Collections.Generic;
using MessagePack;
using MessagePack.Resolvers;

namespace Unity.Ipc
{
    public class Configuration
    {
        public static int DefaultPort { get; set; } = 59595;
        public int Port { get; protected set; }
        public const string DefaultProtocolVersion = "1.0";
        public IpcVersion ProtocolVersion { get; protected set; }

        public IEnumerable<Type> LocalTypes { get; } = new List<Type>();
        public IEnumerable<Type> RemoteTypes { get; } = new List<Type>();

        private static IFormatterResolver[] defaultResolvers;

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
        {
        }

        public Configuration(params IFormatterResolver[] resolvers)
        {
            CompositeResolver.RegisterAndSetAsDefault(resolvers);
        }

        /// <summary>
        /// Add type representing a remote proxy
        /// </summary>
        public Configuration AddRemoteTarget<T>()
            where T : class
        {
            ((List<Type>)RemoteTypes).Add(typeof(T));
            return this;
        }

        /// <summary>
        /// Add type representing a local ipc target
        /// </summary>
        public Configuration AddLocalTarget<T>()
            where T : class
        {
            ((List<Type>)LocalTypes).Add(typeof(T));
            return this;
        }
    }
}
