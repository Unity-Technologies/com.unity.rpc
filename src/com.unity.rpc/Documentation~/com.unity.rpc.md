## About com.unity.rpc

Unity RPC is a C# RPC library using JSON-RPC+MessagePack as the message protocol encoded in binary packages for fast, reliable, minimal-allocation local RPC.

`RpcClient` and `RpcServer` are the two main entry points for this library, depending on whether you're implementing a client or a server. The connection between clients and server is bidirectional, so once a connection is established, both can send and receives method calls and event calls.

Example:

```
var configuration = new Configuration { Port = 44444 };

var server = new RpcServer(configuration);
server.ClientConnecting((_, c) => Debug.Log($"Client {c.Id} connecting"));
server.ClientDisconnecting((c, _) => Debug.Log($"Client {c.Id} disconnecting"));
await server.Start();

var client1 = new RpcClient(configuration);
var client2 = new RpcClient(configuration);
await client1.Start();
await client2.Start();

// IServerInformation is a builtin rpc proxy type that provides basic information about the server
var serverVersion = await client1.GetRemoteTarget<IServerInformation>().GetVersion();
Debug.Log(serverVersion.Version);

serverVersion = await client2.GetRemoteTarget<IServerInformation>().GetVersion();
Debug.Log(serverVersion.Version);

// signal the clients to stop
client1.Stop();
client2.Stop();

// wait until the clients have stopped
await Task.WhenAll(client1.WaitUntilDone(), client2.WaitUntilDone());

// tell the server to stop
server.Stop();

// wait until the server is stopped
await server.WaitUntilDone();
```
