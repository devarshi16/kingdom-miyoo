// Only data/ and managed/ participate in installation. save/ is never staged,
// renamed, deleted or enumerated. A journal permits rollback before the receipt
// is durable, and verifies a completed commit before removing that journal.
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace KNL.Import
{
    static class Transaction
    {
        const string Journal = ".install-journal", Ready = ".native-ready", Tag = ".native-install-id";
        [DllImport("libc", EntryPoint = "sync")] static extern void Sync();
        static void Flush() { if (Environment.OSVersion.Platform == PlatformID.Unix) Sync(); }
        static bool Hex(string value, int length)
        {
            return value.Length == length && value.All(Uri.IsHexDigit);
        }
        static void Write(string path, string text)
        {
            using (var file = new FileStream(path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var bytes = Encoding.UTF8.GetBytes(text);
                file.Write(bytes, 0, bytes.Length);
                file.Flush(true);
            }
            if (File.Exists(path)) File.Delete(path);
            File.Move(path + ".tmp", path);
        }
        static bool Tagged(string folder, string id)
        {
            string path = Path.Combine(folder, Tag);
            return File.Exists(path) && File.ReadAllText(path) == id;
        }
        static bool Owned(string relative)
        {
            return relative.StartsWith("data/", StringComparison.Ordinal) ||
                (relative.StartsWith("managed/", StringComparison.Ordinal) &&
                 Importer.GameDlls.Contains(relative.Substring(8)));
        }
        static bool ReceiptValid(string root, string[] rows, bool full)
        {
            if (rows.Length < 4 || !Hex(rows[0], 32) || !Hex(rows[1], 64) ||
                !Tagged(Path.Combine(root, "data"), rows[0]) ||
                !Tagged(Path.Combine(root, "managed"), rows[0])) return false;
            var seen = new HashSet<string>();
            foreach (string row in rows.Skip(2))
            {
                string[] fields = row.Split('|');
                long length;
                if (fields.Length != 3 || fields[0].Contains("..") || Path.IsPathRooted(fields[0]) ||
                    !Hex(fields[2], 64) || !long.TryParse(fields[1], out length) || length < 1) return false;
                // Public engine files are validated against the current release's runtime
                // manifest. Updating them must not force reimport of unchanged owned data.
                if (!Owned(fields[0])) continue;
                string path = Path.Combine(root, fields[0]);
                if (!seen.Add(fields[0]) || !File.Exists(path) || new FileInfo(path).Length != length) return false;
                if ((full || fields[0].StartsWith("managed/", StringComparison.Ordinal)) &&
                    Digest.FileHash(path) != fields[2]) return false;
            }
            if (!seen.Contains("data/kingdom.knlpack") ||
                !Importer.GameDlls.All(n => seen.Contains("managed/" + n))) return false;
            try { PackWriter.Validate(Path.Combine(root, "data", "kingdom.knlpack")); }
            catch (IOException) { return false; }
            return true;
        }
        public static void Recover(string root)
        {
            string journal = Path.Combine(root, Journal);
            if (!File.Exists(journal)) return;
            string[] transaction = File.ReadAllLines(journal);
            if (transaction.Length != 2 || !Hex(transaction[0], 32) || !Hex(transaction[1], 64))
                throw new InvalidDataException("Installation journal is damaged. Existing files were left in place.");
            string id = transaction[0], key = transaction[1];
            string backup = Path.Combine(root, ".install-rollback", id);
            string stage = Path.Combine(root, ".prepare-v1", key);
            string ready = Path.Combine(root, Ready);
            bool complete = File.Exists(ready) && File.ReadLines(ready).FirstOrDefault() == id &&
                ReceiptValid(root, File.ReadAllLines(ready), true);
            if (!complete)
            {
                foreach (string name in new[] { "data", "managed" })
                {
                    string target = Path.Combine(root, name), old = Path.Combine(backup, name);
                    string pending = Path.Combine(stage, name);
                    if (Directory.Exists(target) && Tagged(target, id))
                    {
                        Directory.CreateDirectory(stage);
                        if (Directory.Exists(pending))
                            throw new IOException("Both staged and installed files exist. Recovery refused to overwrite either copy.");
                        Directory.Move(target, pending);
                    }
                    if (Directory.Exists(old))
                    {
                        if (Directory.Exists(target))
                            throw new IOException("Recovery destination already exists: " + name);
                        Directory.Move(old, target);
                    }
                }
                if (File.Exists(ready) && File.ReadLines(ready).FirstOrDefault() == id) File.Delete(ready);
                string prior = Path.Combine(backup, Ready);
                if (File.Exists(prior))
                {
                    if (File.Exists(ready)) throw new IOException("Both installation receipts exist. Recovery preserved both.");
                    File.Move(prior, ready);
                }
            }
            Flush();
            File.Delete(journal);
            Flush();
        }
        public static bool IsReady(string root)
        {
            Recover(root);
            string ready = Path.Combine(root, Ready);
            if (File.Exists(ready)) return ReceiptValid(root, File.ReadAllLines(ready), false);
            // Existing PC-prepared installations continue to work without raw input files.
            if (!File.Exists(Path.Combine(root, "data", "kingdom.knlpack")) ||
                !Importer.GameDlls.All(n => File.Exists(Path.Combine(root, "managed", n)))) return false;
            PackWriter.Validate(Path.Combine(root, "data", "kingdom.knlpack"));
            return true;
        }
        public static void Commit(string root, string stage, string key)
        {
            Recover(root);
            string id = Guid.NewGuid().ToString("N");
            string backup = Path.Combine(root, ".install-rollback", id);
            Directory.CreateDirectory(backup);
            var receipt = new List<string> { id, key };
            foreach (string name in new[] { "data", "managed" })
            {
                string folder = Path.Combine(stage, name);
                Write(Path.Combine(folder, Tag), id);
                foreach (string path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories).OrderBy(p => p))
                {
                    string relative = name + "/" + path.Substring(folder.Length + 1).Replace('\\', '/');
                    if (path.EndsWith(".sha256") || Path.GetFileName(path) == Tag || !Owned(relative)) continue;
                    receipt.Add(relative + "|" + new FileInfo(path).Length + "|" + Digest.FileHash(path));
                }
            }
            Write(Path.Combine(root, Journal), id + "\n" + key);
            Flush();
            try
            {
                string ready = Path.Combine(root, Ready);
                if (File.Exists(ready)) File.Move(ready, Path.Combine(backup, Ready));
                foreach (string name in new[] { "data", "managed" })
                {
                    string target = Path.Combine(root, name);
                    if (Directory.Exists(target)) Directory.Move(target, Path.Combine(backup, name));
                    Directory.Move(Path.Combine(stage, name), target);
                    Flush();
                }
                if (!ReceiptValid(root, receipt.ToArray(), true))
                    throw new InvalidDataException("Installed game validation failed.");
                Write(ready, string.Join("\n", receipt.ToArray()));
                Flush();
                File.Delete(Path.Combine(root, Journal));
                Flush();
            }
            catch { Recover(root); throw; }
        }
    }
}
