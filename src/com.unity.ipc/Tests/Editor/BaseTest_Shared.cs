using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace BaseTests
{
    using System.IO;
    using System.Threading;
    using Unity.Editor.Tasks;

    public interface ILogging
    {
        void Info(string message, object[] format = null);
        void Warn(string message, object[] format = null);
        void Error(string message, object[] format = null);
        void Trace(string message, object[] format = null);
    }

    internal class TestData : IDisposable
    {
        public readonly Stopwatch Watch;
        public readonly ILogging Logger;
        public readonly string TestPath;
        public readonly string TestName;
        public readonly ITaskManager TaskManager;

        public TestData(string testName, ILogging logger)
        {
            TestName = testName;
            Logger = logger;
            Watch = new Stopwatch();

            TestPath = BaseTest.GetTemporaryDirectory(testName);

            TaskManager = new TaskManager();
            try
            {
                TaskManager.Initialize();
            }
            catch
            {
                // we're on the nunit sync context, which can't be used to create a task scheduler
                // so use a different context as the main thread. The test won't run on the main nunit thread
                TaskManager.Initialize(new MainThreadSynchronizationContext(TaskManager.Token));
            }

            Logger.Trace($"START {testName}");
            Watch.Start();
        }

        public void Dispose()
        {
            Watch.Stop();
            TaskManager.Dispose();
            if (SynchronizationContext.Current is IMainThreadSynchronizationContext ourContext)
                ourContext.Dispose();
            Logger.Trace($"STOP {TestName} :{Watch.ElapsedMilliseconds}ms");
        }
    }

    public partial class BaseTest
    {
        protected const int Timeout = 30000;
        protected const int RandomSeed = 120938;
        internal string TestAssemblyLocation => Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        public static string GetTemporaryDirectory(string prefix = "")
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), prefix + Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }

        protected void StartTrackTime(Stopwatch watch, ILogging logger, string message = "")
        {
            if (!string.IsNullOrEmpty(message))
                logger.Trace(message);
            watch.Reset();
            watch.Start();
        }

        protected void StopTrackTimeAndLog(Stopwatch watch, ILogging logger)
        {
            watch.Stop();
            logger.Trace($"Time: {watch.ElapsedMilliseconds}");
        }

        protected ActionTask GetTask(ITaskManager taskManager, TaskAffinity affinity, int id, Action<int> body)
        {
            return new ActionTask(taskManager, _ => body(id)) { Affinity = affinity };
        }

        protected static IEnumerable<object> StartAndWait(params ITask[] tasks)
        {
            foreach (var task in tasks) task.Start();
            while (!tasks.All(x => x.Task.IsCompleted)) yield return null;
        }

        protected static IEnumerable<object> StartAndWait(IEnumerable<ITask> tasks)
        {
            foreach (var task in tasks) task.Start();
            while (!tasks.All(x => x.Task.IsCompleted)) yield return null;
        }

        protected static IEnumerable<object> Wait(params ITask[] tasks)
        {
            while (!tasks.All(x => x.Task.IsCompleted)) yield return null;
        }

        protected static IEnumerable<object> Wait(IEnumerable<ITask> tasks)
        {
            while (!tasks.All(x => x.Task.IsCompleted)) yield return null;
        }

        protected static IEnumerable<object> Wait<T>(ITask<T> task)
        {
            while (!task.IsCompleted) yield return null;
        }

        protected static IEnumerable<object> Wait(IEnumerable<Task> tasks)
        {
            while (!tasks.All(x => x.IsCompleted)) yield return null;
        }

        protected static IEnumerable<object> Wait(params Task[] tasks)
        {
            while (!tasks.All(x => x.IsCompleted)) yield return null;
        }
    }


    public static class TestExtensions
    {
        public static IEnumerable<object> Wait(this ITask task)
        {
            while (!task.IsCompleted) yield return null;
        }

        public static ITask<T> FromAsync<T>(this ITaskManager taskManager, Task<T> task)
        {
            return new TPLTask<T>(taskManager, () => task);
        }

        public static ITask FromAsync(this ITaskManager taskManager, Task task)
        {
            return new TPLTask(taskManager, () => task);
        }

        public static ITask<T> FromAsync<T>(this ITaskManager taskManager, Func<Task<T>> task)
        {
            return new TPLTask<T>(taskManager, task);
        }

        public static ITask FromAsync(this ITaskManager taskManager, Func<Task> task)
        {
            return new TPLTask(taskManager, task);
        }

        public static void Matches(this IEnumerable actual, IEnumerable expected)
        {
            CollectionAssert.AreEqual(expected, actual,
                $"{Environment.NewLine}expected:{expected.Join()}{Environment.NewLine}actual  :{actual.Join()}{Environment.NewLine}");
        }

        public static void Matches<T>(this IEnumerable<T> actual, IEnumerable<T> expected)
        {
            CollectionAssert.AreEqual(expected.ToArray(), actual.ToArray(),
                $"{Environment.NewLine}expected:{expected.Join()}{Environment.NewLine}actual  :{actual.Join()}{Environment.NewLine}");
        }

        public static string Join(this IEnumerable array, string separator = ",") => string.Join(separator, array);
    }

    static class KeyValuePair
    {
        public static KeyValuePair<TKey, TValue> Create<TKey, TValue>(TKey key, TValue value)
        {
            return new KeyValuePair<TKey, TValue>(key, value);
        }
    }

}
