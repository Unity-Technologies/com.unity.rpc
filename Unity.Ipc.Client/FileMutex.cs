using System;
using System.IO;
using System.Net;

namespace Unity.Ipc.Client
{
    public class FileMutex
    {
        readonly string m_Path;
        readonly string m_MutexName;
        FileStream m_FileStream;

        private const string LockFileName = "unitycompiler";
        private const string LockFileExtension = "lock";
        private const string LockFileText = "lock";
        private bool disposed = false;

        public FileMutex(string path, string mutexName)
        {
            m_Path = path;
            m_MutexName = mutexName;
        }

        ~FileMutex()
        {
            if (!disposed)
            {
                return;
            }

            m_FileStream?.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                m_FileStream.Dispose();
                try { File.Delete(GetFilePath(m_Path, m_MutexName)); }
                catch (Exception) { }
            }

            disposed = true;
        }

        public bool Acquire()
        {
            var filePath = GetFilePath(m_Path, m_MutexName);
            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, LockFileText);
            }

            m_FileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
            return true;
        }

        public static bool IsTaken(string path, string mutexName)
        {
            var filePath = GetFilePath(path, mutexName);
            return IsLocked(filePath);
        }

        private static bool IsLocked(string filePath)
        {
            FileStream stream = null;
            try
            {
                if (!File.Exists(filePath))
                {
                    return false;
                }

                stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch (IOException)
            {
                return true;
            }
            finally
            {
                stream?.Dispose();
            }

            return false;
        }

        private static string GetFilePath(string path, string name)
        {
            return Path.Combine(path, $"{name}.{LockFileExtension}");
        }
    }
}
