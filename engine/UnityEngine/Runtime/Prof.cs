// Phase profiler for M5: KNL_PROF=1 times engine phases (and bytes allocated in each: allocations drive
// the GC, which is expensive on the device), KNL_PROF=2 also each script type's FixedUpdate/Update/
// LateUpdate. Report() prints the top entries per frame and resets.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace KNL
{
    public static class Prof
    {
        public static readonly int Level = int.Parse(Environment.GetEnvironmentVariable("KNL_PROF") ?? "0");
        static readonly Dictionary<string, long> ticks = new Dictionary<string, long>();
        static readonly Dictionary<string, int> calls = new Dictionary<string, int>();
        static readonly Dictionary<string, long> bytes = new Dictionary<string, long>();
        static long s_Alloc;

        public static long Now() { if (Level == 0) return 0; s_Alloc = GC.GetAllocatedBytesForCurrentThread(); return Stopwatch.GetTimestamp(); }
        /// Adds the time since t0 to `name`; returns now so phases can be chained.
        public static long Add(string name, long t0)
        {
            if (Level == 0) return 0;
            long t = Stopwatch.GetTimestamp(), v; int n;
            ticks.TryGetValue(name, out v); ticks[name] = v + (t - t0);
            calls.TryGetValue(name, out n); calls[name] = n + 1;
            long a = GC.GetAllocatedBytesForCurrentThread();
            bytes.TryGetValue(name, out v); bytes[name] = v + (a - s_Alloc); s_Alloc = a;
            return t;
        }

        static int s_Gc0, s_Gc1;
        public static void Report(int frames, int top)
        {
            if (Level == 0 || frames <= 0) return;
            int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1);
            Console.Error.WriteLine(string.Format("[prof] GC: {0} minor, {1} major over {2} frames, heap {3:F1} MB",
                g0 - s_Gc0, g1 - s_Gc1, frames, GC.GetTotalMemory(false) / 1048576.0));
            s_Gc0 = g0; s_Gc1 = g1;
            foreach (var kv in ticks.OrderByDescending(kv => kv.Value).Take(top))
                Console.Error.WriteLine(string.Format("[prof] {0,8:F2} ms/frame {1,7:F1} calls/frame {3,8:F1} KB/frame  {2}",
                    kv.Value * 1000.0 / Stopwatch.Frequency / frames, calls[kv.Key] / (double)frames, kv.Key, bytes[kv.Key] / 1024.0 / frames));
            ticks.Clear(); calls.Clear(); bytes.Clear();
        }
    }
}
