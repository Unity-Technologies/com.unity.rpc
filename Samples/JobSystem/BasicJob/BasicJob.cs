using System.Threading;
using System.Threading.Tasks;
using JobServer;

namespace BasicJob
{
    public class BasicJob : JobDispatcherBase
    {
        public BasicJob(int jobId,
            IJobServerService server,
            IJobClientService client,
            CancellationToken cancellationToken)
            : base(jobId, server, client, cancellationToken)
        { }

        public override async Task<bool> StartJob()
        {
            if (await base.StartJob() == false)
            {
                return false;
            }

            _ = Task.Run(async () => {
                for (float i = 0; i < 1.0f; i += 0.01f)
                {
                    await UpdateJobProgress(i);
                    Thread.Sleep(10);
                }

                await CompleteSuccessfully();
            });

            return true;
        }
    }
}
