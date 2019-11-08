﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Ipc;

#if !UNITY
using Microsoft.Extensions.Logging;
#if NETCOREAPP
using System.Composition;
#else
using System.ComponentModel.Composition;
#endif
#endif

namespace Shared
{
    public class IpcClientSample : IIpcJobProgress
    {
        private readonly ILogger logger;
        private Dictionary<string, TaskCompletionSource<bool>> jobs = new Dictionary<string, TaskCompletionSource<bool>>();
        private readonly IIpcServerPerConnection connectionServer;
        private readonly IIpcServerSingleton globalServer;
        private readonly IIpcClientInfo clientInformation;

        private string clientId;

        private TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>();
        public Task Completion => completion.Task;

#if !UNITY
        [ImportingConstructor]
        public IpcClientSample(IRequestContext context, ILogger<IpcClientSample> logger)
            : this(context, (ILogger)logger)
        {
        }
#endif

        public IpcClientSample(IRequestContext context, ILogger logger)
        {
            this.logger = logger;
            connectionServer = context.GetRemoteTarget<IIpcServerPerConnection>();
            globalServer = context.GetRemoteTarget<IIpcServerSingleton>();
            clientInformation = context.GetRemoteTarget<IIpcClientInfo>();

            connectionServer.ThingDone += (_, id) =>
            {
                logger.LogDebug($"Job with id {id} done");
            };

            clientInformation.OnClientConnected += (_, id) => {
                logger.LogDebug($"Server says a new client connected with id {id}");
            };
        }

        public async Task RunUntilQuit(CancellationToken exiting)
        {
            try
            {

                // say hi to the server
                await Start();

                while (!exiting.IsCancellationRequested)
                {
                    var ret = await StartJob();
                    await WaitForJobDone(ret);
                    logger.LogDebug("Done " + ret.ID);
                }

                await Stop();
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }

            completion.TrySetResult(true);
        }

        private async Task Start()
        {
            clientId = await connectionServer.GetClientId();

            logger.LogDebug($"Looks like I have id {clientId}! Saying hi to the server...");

            await globalServer.Hello(clientId);
        }

        private async Task Stop()
        {
            logger.LogDebug($"Saying goodbye to the server...");

            await globalServer.Goodbye(clientId);
        }

        private Task<JobData> StartJob()
        {
            string id = Guid.NewGuid().ToString();
            var job = new TaskCompletionSource<bool>();
            jobs.Add(id, job);
            return connectionServer.StartJob(id);
        }

        private Task WaitForJobDone(JobData id)
        {
            return jobs[id.ID].Task;
        }


        // IIpcJobProgress implementation, to be called by the server
        public Task JobStarted(string id)
        {
            return Task.CompletedTask;
        }

        public Task JobProgress(string id, string message)
        {
            logger.LogDebug(message);
            return Task.CompletedTask;
        }

        public Task JobDone(string id)
        {
            jobs[id].SetResult(true);
            return Task.CompletedTask;
        }
    }
}
