using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using BaseTests;

namespace Unity.Ipc.Tests
{
    using Editor.Tasks;
    using NUnit.Framework;

    public interface IMathSession
    {
        Task<int> Add(int opA, int opB);
    }

    public class BasicMathService : IMathSession
    {
        public Task<int> Add(int opA, int opB)
        {
            return Task.FromResult(opA + opB);
        }
    }

    public class MathSession
    {
        public int LastResult;
    }

    public class Helpers
    {
        public static Configuration GetConfiguration(int port, int protocol) =>
            new Configuration { Port = port, ProtocolVersion = IpcVersion.Parse(protocol.ToString()) };

        public static ITask<IpcServer> NewServer<T>(ITaskManager taskManager, Configuration configuration, CancellationTokenSource cts)
            where T : class, new()
        {
            return new TPLTask<IpcServer>(taskManager, async () => {
                var inst = new T();
                var server = new IpcServer(configuration, cts.Token).RegisterLocalTarget(inst);
                await server.Start();
                return server;
            }).Start();
        }

        public static ITask<IRequestContext> NewClient<T>(ITaskManager taskManager, Configuration configuration, CancellationTokenSource cts)
            where T : class
        {
            return new TPLTask<IRequestContext>(taskManager, async () => {
                var client = new IpcClient(configuration, cts.Token).RegisterRemoteTarget<T>();
                await client.Start();
                return client;
            }).Start();
        }
    }

    public partial class ClientServerConnectionTests : BaseTest
    {
        [Test]
        public void SingleClientServerHandshake()
        {
            using (var test = StartTest())
            {
                var cts = new CancellationTokenSource();
                var configuration = new Configuration();

                using (var server = Helpers.NewServer<BasicMathService>(test.TaskManager, configuration, cts).RunSynchronously())
                using (var clientA = Helpers.NewClient<IMathSession>(test.TaskManager, configuration, cts).RunSynchronously())
                using (var clientB = Helpers.NewClient<IMathSession>(test.TaskManager, configuration, cts).RunSynchronously())
                {
                    var mathSessionA = clientA.GetRemoteTarget<IMathSession>();
                    var mathSessionB = clientB.GetRemoteTarget<IMathSession>();

                    int retAdd = mathSessionA.Add(9, 11).Result;
                    Assert.AreEqual(9 + 11, retAdd);

                    retAdd = mathSessionB.Add(3, 12).Result;
                    Assert.AreEqual(3 + 12, retAdd);
                }
            }
        }

        [Test]
        public void SingleClientNoServer()
        {
            using (var test = StartTest())
            {
                var client = new IpcClient(new Configuration());
                Assert.Throws<SocketException>(() => {
                    try
                    {
                        client.Run().Wait();

                    }
                    catch (AggregateException e)
                    {
                        e.InnerException.Rethrow();
                    }
                    catch
                    {
                        throw;
                    }
                });
            }
        }
    }
}
