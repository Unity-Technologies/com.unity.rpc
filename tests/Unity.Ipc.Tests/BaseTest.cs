using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Unity.Editor.Tasks;

namespace BaseTests
{
    public class NUnitLogger : ILogging
    {
        private readonly string context;

        public NUnitLogger(string context)
        {
            this.context = context;
        }

        public void Error(string message, object[] format)
        {
            if (format != null)
                message = string.Format(message, format);
            WriteLine(context, message);
        }

        public void Info(string message, object[] format)
        {
            if (format != null)
                message = string.Format(message, format);
            WriteLine(context, message);
        }

        public void Trace(string message, object[] format)
        {
            if (format != null)
                message = string.Format(message, format);
            WriteLine(context, message);
        }

        public void Warn(string message, object[] format)
        {
            if (format != null)
                message = string.Format(message, format);
            WriteLine(context, message);
        }

        private string GetMessage(string context, string message)
        {
            var time = DateTime.Now.ToString("HH:mm:ss.fff tt");
            var threadId = Thread.CurrentThread.ManagedThreadId;
            return string.Format("{0} [{1,2}] {2} {3}", time, threadId, context, message);
        }

        private void WriteLine(string context, string message)
        {
            NUnit.Framework.TestContext.Progress.WriteLine(GetMessage(context, message));
        }
    }

    public partial class BaseTest
	{
		public const bool TracingEnabled = false;

		internal TestData StartTest([CallerMemberName] string testName = "test") => new TestData(testName, new NUnitLogger(testName));

        protected async Task RunTest(Func<IEnumerator> testMethodToRun)
		{
			var scheduler = ThreadingHelper.GetUIScheduler(new ThreadSynchronizationContext(default));
			var taskStart = new Task<IEnumerator>(testMethodToRun);
			taskStart.Start(scheduler);
			var e = await RunOn(testMethodToRun, scheduler);
			while (await RunOn(s => ((IEnumerator)s).MoveNext(), e, scheduler))
			{ }
		}

		private Task<T> RunOn<T>(Func<T> method, TaskScheduler scheduler)
		{
			return Task<T>.Factory.StartNew(method, CancellationToken.None, TaskCreationOptions.None, scheduler);
		}

		private Task<T> RunOn<T>(Func<object, T> method, object state, TaskScheduler scheduler)
		{
			return Task<T>.Factory.StartNew(method, state, CancellationToken.None, TaskCreationOptions.None, scheduler);
		}
    }
}
