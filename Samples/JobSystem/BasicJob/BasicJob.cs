using System.Threading;
using System.Threading.Tasks;
using JobServer;
using JsonRpc.Client;

namespace BasicJob
{
    public class BasicJob : JobDispatcherBase
    {
        public override async Task<bool> StartJob()
        {
            if (await base.StartJob() == false)
            {
                return false;
            }

            _ = Task.Run(() =>
            {
                for (float i = 0; i < 1.0f; i+=0.01f)
                {
                    JobServerSession.UpdateJobProgress(JobId, i);
                    Thread.Sleep(10);
                }

                JobServerSession.CompleteJobSuccessfully(JobId);
            });

            return true;
        }

        public BasicJob(int jobId, JobServerSession jobServerSession, JsonRpcClient serverSideClient) : base(jobId, jobServerSession, serverSideClient)
        {
        }
    }
}