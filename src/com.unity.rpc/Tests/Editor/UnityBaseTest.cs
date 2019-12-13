using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

namespace BaseTests
{
	using System;
    using System.Threading;
    using Unity.Editor.Tasks;
    using Unity.Editor.Tasks.Logging;

    // Unity does not support async/await tests, but it does
	// have a special type of test with a [CustomUnityTest] attribute
	// which mimicks a coroutine in EditMode. This attribute is
	// defined here so the tests can be compiled without
	// referencing Unity, and nunit on the command line
	// outside of Unity can execute the tests. Basically I don't
	// want to keep two copies of all the tests.
	public class CustomUnityTestAttribute : UnityTestAttribute
	{ }

    public class UnityLogger : ILogging
    {
        private readonly string context;

        public UnityLogger(string context)
        {
            this.context = context;
        }

        public void Info(string message, object[] format)
        {
            message = string.Format(message, format);
            Debug.Log(GetMessage(context, message));
        }

        public void Warn(string message, object[] format)
        {
            message = string.Format(message, format);
            Debug.LogWarning(GetMessage(context, message));
        }

        public void Error(string message, object[] format)
        {
            message = string.Format(message, format);
            Debug.LogError(GetMessage(context, message));
        }

        public void Trace(string message, object[] format)
        {
        }

        private string GetMessage(string context, string message)
        {
            var time = DateTime.Now.ToString("HH:mm:ss.fff tt");
            var threadId = Thread.CurrentThread.ManagedThreadId;
            return string.Format("{0} [{1,2}] {2} {3}", time, threadId, context, message);
        }
    }


    public partial class BaseTest
	{
		private LogAdapterBase existingLogger;
		private bool existingTracing;

        internal TestData StartTest([CallerMemberName] string testName = "test") => new TestData(testName, new UnityLogger(testName));
    }
}
