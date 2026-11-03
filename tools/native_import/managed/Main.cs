using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
namespace KNL.Import
{
    static class MainProgram
    {
        static string Arg(string[] args, string key, string fallback)
        {
            int i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length?args[i + 1]:fallback;
        }
        static int Main(string[] args)
        {
            string resources = Path.GetDirectoryName(typeof(MainProgram).Assembly.Location);
            string root = Path.GetFullPath(Arg(args, "--root", Path.Combine(resources, "..")));
            var elapsed = Stopwatch.StartNew();
            try{
                if (Array.IndexOf(args, "--check") >= 0) {
                    using (var fileLock = new PreparationLock(Path.Combine(root, ".prepare.lock"))) {
                    Transaction.Recover(root);
                    RuntimeIntegrity.Verify(root, resources);
                    return Transaction.IsReady(root) && RuntimeIntegrity.GameLogicMatches(root, resources) ? 0 : 10;
                    }
                }
                using (var screen = new Screen(Array.IndexOf(args, "--window") >= 0)){
                    while (true) {
                        try{
                            screen.Progress("Checking original game files", 0);
                            string shot = Arg(args, "--status-shot", "");
                            if (shot.Length > 0) {
                                screen.Snapshot(shot);
                            }
                            if (Array.IndexOf(args, "--preview") >= 0) {
                                return 0;
                            }
                            using (var fileLock = new PreparationLock(Path.Combine(root, ".prepare.lock"))) {
                                Transaction.Recover(root);
                                RuntimeIntegrity.Verify(root, resources);
                            }
                            Importer.Prepare(root, Arg(args, "--source", Path.Combine(root,
                                "Kingdom_Data")), resources,
                                screen.Progress);
                            return 0;
                        }
                        catch (Exception e) {
                            if (e is OperationCanceledException) {
                                Console.Error.WriteLine(e.Message);
                                return 11;
                            }
                            if (!screen.Error(e)) {
                                return 1;
                            }
                        }
                    }
                }
            }
            catch (Exception e) {
                Console.Error.WriteLine("PREPARATION ERROR: " + e);
                return 1;
            }
            finally {
                Console.Error.WriteLine("PREPARATION elapsed_seconds=" + elapsed.Elapsed.TotalSeconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                try {
                    foreach (string line in File.ReadAllLines("/proc/self/status"))
                        if (line.StartsWith("VmHWM:") || line.StartsWith("VmRSS:") || line.StartsWith("VmSwap:"))
                            Console.Error.WriteLine("PREPARATION " + line);
                } catch (IOException) { }
            }
        }
    }
}
