#pragma warning disable CS0067
using System;
using System.Threading.Tasks;

namespace Unity.Ipc.Tests
{
	public interface ITestServer
	{
		Task Method(string arg);
	}

	class InvalidTestServer : ITestServer
	{
		public event Action OnEvent;
		public Task Method(string arg)
		{
			return Task.CompletedTask;
		}
	}

	//public class ConnectionFailures
	//{
	//	//[Fact]

	//	public async Task InvalidProxyMakesRunThrow()
	//	{
	//		var conf = new Configuration { Port = new Random().Next(44444, 55555) };
	//		var server = new IpcServer(conf).RegisterLocalTarget(new InvalidTestServer());
	//		await Assert.ThrowsAnyAsync<NotSupportedException>(() => server.Run());
	//	}

	//	//[Fact]
	//	public async Task InvalidProxyMakesStartThrow()
	//	{
	//		var conf = new Configuration { Port = new Random().Next(44444, 55555) };
	//		var server = new IpcServer(conf).RegisterLocalTarget(new InvalidTestServer());
	//		await Assert.ThrowsAnyAsync<NotSupportedException>(() => server.Start());
	//	}
	//}
}
