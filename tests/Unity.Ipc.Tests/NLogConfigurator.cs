using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Seq;
using NLog.Targets.Wrappers;

namespace Unity.Ipc.Tests
{
    public static class NLogConfigurator
    {
        public static void ConfigureSeq(LogLevel level, string url = "http://localhost:5341", string apiKey = "", int bufferSize = 1000, int flushTimeout = 2000)
        {
            // Step 1. Create configuration object 
            var config = new LoggingConfiguration();
            var seqTarget = new SeqTarget
            {
                ServerUrl = url,
                ApiKey = apiKey
            };

            var bufferWrapper = new BufferingTargetWrapper
            {
                Name = "seq",
                BufferSize = bufferSize,
                FlushTimeout = flushTimeout,
                WrappedTarget = seqTarget
            };

            config.AddTarget(bufferWrapper);

            // Step 4. Define rules
            var rule1 = new LoggingRule("*", level, bufferWrapper);
            config.LoggingRules.Add(rule1);

            // Step 5. Activate the configuration
            LogManager.Configuration = config;
        }
    }
}