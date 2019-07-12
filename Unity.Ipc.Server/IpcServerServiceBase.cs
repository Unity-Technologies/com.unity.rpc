using System;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Client;
using JsonRpc.Contracts;
using JsonRpc.Server;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Unity.Ipc.Client;

namespace Unity.Ipc.Server
{
    public abstract class IpcServerServiceBase<TServerSession> : IpcServerServiceBase
    {
        public TServerSession ServerSession => RequestContext.Features.Get<TServerSession>();
    }

    public abstract class IpcServerServiceBase<TServerSession, TClientSession> : IpcServerServiceBase<TServerSession>
    {
        public TClientSession ClientSession => RequestContext.Features.Get<TClientSession>();
    }

    public abstract class IpcServerServiceBase : JsonRpcService
    {
        [JsonRpcMethod]
        public Task<IpcVersion> Handshake()
        {
            var version = RequestContext.Features.Get<IpcVersion>();
            return Task.FromResult(version);
        }

        [JsonRpcMethod]
        public Task ClientIsShuttingDown()
        {
            try
            {
                var ci = RequestContext.Features.Get<ClientInfo>();
                ci.RequestForShutDown();
            }
            catch (Exception)
            {
            }
            return Task.CompletedTask;
        }

        public async Task<TRes> ExecClientRequest<TRes>(string requestName, object arg, CancellationToken cancellationToken)
        {
            if (!TryGetJsonRpcClient(out var client))
            {
                throw new BadExecRequestException($"Error during sending the request {requestName}, couldn't access the JsonRpcClient object, are you executing ExecClientRequest outside the scope of a Server JsonRpcMethod, after an await maybe? Rely on the ClientSession object to perform client requests out of the scope of a server JsonRpcMethod.");
            }

            try
            {
                var response = await client.SendRequestAsync(requestName, JToken.FromObject(arg), cancellationToken).ConfigureAwait(false);
                if (response.Error == null)
                {
                    return response.Result.ToObject<TRes>();
                }

                var logger = RequestContext.Features.Get<ILogger>();
                logger?.LogError("Error during sending the request {requestName}, error message: {errorMessage}", requestName, response.Error.Message.ToString());
                throw new BadExecRequestException($"Error during sending the request {requestName}, error message: {response.Error.Message}");
            }
            catch (Exception e)
            {
                var logger = RequestContext.Features.Get<ILogger>();
                logger?.LogError(0, e, "Thread: {threadId}, Error while executing request {r}", Thread.CurrentThread.ManagedThreadId, requestName);
                throw new BadExecRequestException($"Error during sending the request {requestName}, see inner exception for more information", e);
            }
        }

        public async Task ExecClientRequest(string requestName, object arg, CancellationToken cancellationToken)
        {
            if (!TryGetJsonRpcClient(out var client))
            {
                throw new BadExecRequestException($"Error during sending the request {requestName}, couldn't access the JsonRpcClient object, are you executing ExecClientRequest outside the scope of a Server JsonRpcMethod, after an await maybe? Rely on the ClientSession object to perform client requests out of the scope of a server JsonRpcMethod.");
            }

            try
            {
                var response = await client.SendRequestAsync(requestName, JToken.FromObject(arg), cancellationToken).ConfigureAwait(false);
                if (response.Error == null)
                {
                    return;
                }

                var logger = RequestContext.Features.Get<ILogger>();
                logger?.LogError("Error during sending the request {requestName}, error message: {errorMessage}", requestName, response.Error.Message.ToString());
                throw new BadExecRequestException($"Error during sending the request {requestName}, error message: {response.Error.Message}");
            }
            catch (Exception e)
            {
                var logger = RequestContext.Features.Get<ILogger>();
                logger?.LogError(0, e, "Thread: {threadId}, Error while executing request {r}", Thread.CurrentThread.ManagedThreadId, requestName);
                throw new BadExecRequestException($"Error during sending the request {requestName}, see inner exception for more information", e);
            }
        }

        public bool TryGetJsonRpcClient(out JsonRpcClient client)
        {
            client = null;
            var ci = RequestContext?.Features?.Get<ClientInfo>();
            if (ci == null || !ci.ShouldBeAvailable)
            {
                return false;
            }

            client = ci._serverSideClient;
            return client != null;
        }
    }
}
