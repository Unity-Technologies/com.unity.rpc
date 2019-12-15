using NUnit.Framework;

namespace Unity.Rpc.Tests
{
    using System.Threading.Tasks;
    using BaseTests;

    // Unity does not support async/await tests, but it does
    // have a special type of test with a [CustomUnityTest] attribute
    // which mimicks a coroutine in EditMode. This attribute is
    // defined here so the tests can be compiled without
    // referencing Unity, and nunit on the command line
    // outside of Unity can execute the tests. Basically I don't
    // want to keep two copies of all the tests so I run the
    // UnityTest from here

    partial class ClientServerConnectionTests : BaseTest
    {
        [Test]
        public async Task StartAndStop()
        {
            using (var test = StartTest())
            {
                var configuration = new Configuration { Port = 44444 };

                var server = new RpcServer(configuration);
                server.ClientConnecting((_, c) => test.Logger.Info($"Client {c.Id} connecting"));
                server.ClientDisconnecting((c, _) => test.Logger.Info($"Client {c.Id} disconnecting"));
                await server.Start();

                var client1 = new RpcClient(configuration);
                var client2 = new RpcClient(configuration);
                await client1.Start();
                await client2.Start();

                // IServerInformation is a builtin rpc proxy that provides basic information about the server
                var serverVersion = await client1.GetRemoteTarget<IServerInformation>().GetVersion();
                test.Logger.Info(serverVersion.Version);

                serverVersion = await client2.GetRemoteTarget<IServerInformation>().GetVersion();
                test.Logger.Info(serverVersion.Version);

                client1.Stop();
                client2.Stop();

                await Task.WhenAll(client1.WaitUntilDone(), client2.WaitUntilDone());

                server.Stop();

                await server.WaitUntilDone();
            }
        }
    }
}
