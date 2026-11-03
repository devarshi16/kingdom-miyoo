using System;
using System.Text;
using System.Threading;
using System.IO;
using System.Runtime.InteropServices;
namespace KNL.Import
{
    static class Digest
    {
        [DllImport("knlimport", CallingConvention = CallingConvention.Cdecl)]
        static extern int knli_hash(string path, StringBuilder output);
        [DllImport("knlimport", CallingConvention = CallingConvention.Cdecl)]
        static extern int knli_hash_progress();
        [DllImport("knlimport", CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong knli_available_bytes(string path);
        public static string FileHash(string path, Action<double> progress = null)
        {
            Audio.knli_reset_cancel();
            var result = new StringBuilder(65);
            if (progress == null)
            {
                if (knli_hash(path, result) == 0) throw new IOException("Could not read " + Path.GetFileName(path));
            }
            else
            {
                bool finished = false;
                int ok = 0;
                Exception error = null;
                var thread = new Thread(() => {
                    try { ok = knli_hash(path, result); }
                    catch (Exception e) { error = e; }
                    finally { Volatile.Write(ref finished, true); }
                });
                thread.Start();
                try
                {
                    while (!Volatile.Read(ref finished))
                    {
                        Thread.Sleep(100);
                        progress(knli_hash_progress() / 10000.0);
                    }
                }
                catch { Audio.knli_cancel(); thread.Join(); throw; }
                thread.Join();
                if (error != null) throw error;
                if (ok == 0) throw new IOException("Could not read " + Path.GetFileName(path));
                progress(1);
            }
            return result.ToString();
        }
    }
}
