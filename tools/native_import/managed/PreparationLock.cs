using System;using System.IO;using System.Runtime.InteropServices;
namespace KNL.Import
{
    sealed class PreparationLock : IDisposable
    {
        [DllImport("libc", SetLastError = true)] static extern int flock(int descriptor, int operation);
        readonly FileStream file;
        public PreparationLock(string path)
        {
            file = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            if (flock(file.SafeFileHandle.DangerousGetHandle().ToInt32(), 2 | 4) != 0)
            {
                file.Dispose();
                throw new IOException("Game preparation is already running. Wait for it to finish.");
            }
        }
        public void Dispose() { file.Dispose(); }
    }
}
