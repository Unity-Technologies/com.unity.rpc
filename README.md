# About the Unity RPC package

Unity RPC is a C# RPC library using JSON-RPC+MessagePack as the message protocol encoded in binary packages for fast, reliable, minimal-allocation local RPC. This library makes it easy for Unity editor plugins to run local server processes and send/receive data using C# async/await methods and `EventHandler<T>` events.

Samples of client/server implementations can be found in the [Samples](https://github.com/Unity-Technologies/com.unity.rpc/tree/master/Samples) folder, and a real world usage of this library in Unity is the [Process Server](https://github.com/Unity-Technologies/com.unity.process-server/).

## API

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

## Build

## How to build

Check [How to Build](https://raw.githubusercontent.com/Unity-Technologies/Git-for-Unity/master/BUILD.md) for all the build, packaging and versioning details.

### Release build 

`build[.sh|cmd] -r`

### Release build and package

`pack[.sh|cmd] -r -b`

### Release build and test

`test[.sh|cmd] -r -b`


### Where are the build artifacts?

Packages sources are in `build/packages`.

Nuget packages are in `build/nuget`.

Packman (npm) packages are in `upm-ci~/packages`.

Binaries for each project are in `build/bin` for the main projects and `build/tests` for the tests.

### How to bump the major or minor parts of the version

The `version.json` file in the root of the repo controls the version for all packages.
Set the major and/or minor number in it and **commit the change** so that the next build uses the new version.
The patch part of the version is the height of the commit tree since the last manual change of the `version.json`
file, so once you commit a change to the major or minor parts, the patch will reset back to 0.