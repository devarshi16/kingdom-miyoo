using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading;
using System.Security.Cryptography;
using System.Collections.Generic;
namespace KNL.Import
{
    static class Importer
    {
        internal static readonly string[] Serialized = {
            "globalgamemanagers", "globalgamemanagers.assets", "level0", "level1", "level2",
            "resources.assets", "sharedassets0.assets", "sharedassets1.assets",
            "sharedassets2.assets"
        };
        internal static readonly string[] GameDlls = {
            "Assembly-CSharp.dll", "Assembly-CSharp-firstpass.dll", "UnityEngine.UI.dll",
            "UnityEngine.Timeline.dll", "UnityEngine.Networking.dll", "DOTween.dll",
            "DOTween43.dll", "DOTween46.dll", "DOTween50.dll", "DOTweenPro.dll", "DemiLib.dll",
            "UnityEngine.Analytics.dll", "XInputDotNetPure.dll", "SonyNP.dll",
            "SonyPS4CommonDialog.dll", "SonyPS4SavedGames.dll"
        };
        const string Version = "native-import-1";
        static string source, root, stage, key;
        static Action<string, double> status = (text, progress) =>
            Console.Error.WriteLine("STATUS " + progress.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " " + text);
        static void Status(string text, double progress)
        {
            status(text, progress);
        }
        static Dictionary<string, object> D(object v)
        {
            return Pixels.D(v);
        }
        static string Hash(string file) { return Digest.FileHash(file); }
        static void Durable(string path, Action<FileStream> write)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            using (var s = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None)){
                write(s);
                s.Flush(true);
            }
            if (File.Exists(path)) {
                File.Delete(path);
            }
            File.Move(temp, path);
        }
        static void Text(string path, string value)
        {
            Durable(path, s => {var b = Encoding.UTF8.GetBytes(value);
                                s.Write(b, 0, b.Length);
            });
        }
        static bool Cached(string path)
        {
            string marker = path + ".sha256";
            return File.Exists(path) && File.Exists(marker) &&
                   File.ReadAllText(marker) == Version + "\n" + key + "\n" + Hash(path);
        }
        static void Checkpoint(string path)
        {
            Text(path + ".sha256", Version + "\n" + key + "\n" + Hash(path));
        }
        static string VerifySource(string profile, double baseProgress = 0, double span = .08)
        {
            var profileData = D(KNL.Json.ParseFile(profile));
            var fingerprints = D(profileData["fingerprints"]);
            var all = new SortedDictionary<string, string>();
            var names = new SortedSet<string>(fingerprints.Keys);
            foreach (var path in Directory.GetFiles(source).Where(p => p.EndsWith(".resS") || p.EndsWith(".resource")))
                names.Add(Path.GetFileName(path));
            long total = 0, completed = 0;
            foreach (string name in names)
            {
                string path = Path.Combine(source, name);
                if (!File.Exists(path)) throw new InvalidDataException("Missing " + name + ". Copy the complete Kingdom_Data folder.");
                total += new FileInfo(path).Length;
            }
            foreach (string name in names)
            {
                string path = Path.Combine(source, name);
                long length = new FileInfo(path).Length;
                string text = "Checking " + Path.GetFileName(name);
                all[name] = Digest.FileHash(path, fraction =>
                    Status(text, baseProgress + span * (completed + fraction * length) / Math.Max(1, total)));
                object expected;
                if (fingerprints.TryGetValue(name, out expected) && all[name] != (string)expected)
                    throw new InvalidDataException("Unsupported or damaged game file: " + name +
                        ". This importer requires the verified PC build 1.2.8.");
                completed += length;
            }
            string buildIdentity = Hash(typeof(Importer).Assembly.Location);
            string buildId = Path.Combine(Path.GetDirectoryName(typeof(Importer).Assembly.Location), "build-id.txt");
            if (File.Exists(buildId)) buildIdentity += Hash(buildId);
            using (var hash = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(Version + "\n" + buildIdentity + "\n" +
                    string.Join("\n", all.Select(e => e.Key + "=" + e.Value).ToArray()));
                return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            }
        }
        static object Header(ObjectRecord record, Node schema)
        {
            return record.File.ReadRecord(record, schema, false);
        }
        static string Pointer(SerializedFile f, Dictionary<string, object> ptr)
        {
            int id = Pixels.I(ptr["m_FileID"]);
            return (id == 0?f.Name:f.Externals[id - 1]) + "#" + ptr["m_PathID"];
        }
        static string Need(string relative)
        {
            string p = Path.Combine(stage, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(p));
            return p;
        }
        public static void Prepare(string gameRoot, string original, string resources, Action<string, double> progress)
        {
            root = Path.GetFullPath(gameRoot);
            source = Path.GetFullPath(original);
            if (progress != null) {
                status = progress;
            }
            if (!Directory.Exists(source)) {
                throw new InvalidDataException(
                    "Copy your original Kingdom_Data folder into KingdomNewLands, then launch again.");
            }
            Directory.CreateDirectory(root);
            using (var fileLock = new PreparationLock(Path.Combine(root, ".prepare.lock")))
            {
                Transaction.Recover(root);
                ulong available = Digest.knli_available_bytes(root);
                if (available == ulong.MaxValue) throw new IOException("Could not check SD-card free space.");
                if (available < 192UL * 1024 * 1024)
                    throw new IOException("Free at least 192 MB on the SD card, then launch again. Existing game files and saves were preserved.");
                key = VerifySource(Path.Combine(resources, "supported_build.json"));
                stage = Path.Combine(root, ".prepare-v1", key);
                Directory.CreateDirectory(stage);
                var entries = new Dictionary<string, string>();
                using (var schemas = new Schemas(Path.Combine(resources, "unity2017.knlt"), Path.Combine(source, "Managed")))
                using (var sources = new SourceSet(source)) {
                    var files = sources.Files;
                    var scripts = new Dictionary<string, string>();
                    var textures = new Dictionary<string, ObjectRecord>();
                    var sprites = new Dictionary<string, List<ObjectRecord> >();
                    var audio = new List<ObjectRecord>();
                    foreach (var f in files) {
                        foreach (var o in f.Objects) {
                            if (o.Class == 115) {
                                var d = D(o.Read(schemas.Classes[115]));
                                scripts[f.Name + "#" + o.Id] = d["m_AssemblyName"] + ":" +
                                    (d["m_Namespace"].ToString() ==
                                        ""?"":d["m_Namespace"] + ".") +
                                    d["m_ClassName"];
                            }
                            if (o.Class == 28) {
                                textures[f.Name + "#" + o.Id] = o;
                            }
                            if (o.Class == 83) {
                                audio.Add(o);
                            }
                            if (o.Class == 213) {
                                var d = D(o.Read(schemas.Classes[213]));
                                string tex = Pointer(f, D(D(d["m_RD"])["texture"]));
                                List<ObjectRecord> list;
                                if (!sprites.TryGetValue(tex, out list)) {
                                    list = new List<ObjectRecord>();
                                    sprites[tex] = list;
                                }
                                list.Add(o);
                            }
                        }
                    }
                    int fileIndex = 0;
                    foreach (var f in files) {
                        Status("Preparing objects " + (++fileIndex) + " / " + files.Length,
                            .08 + .17 * (fileIndex - 1) / files.Length);
                        string path = Need("objects/" + f.Name + ".knlb");
                        if (!Cached(path)) {
                            using (var pack = new ObjectPack(path + ".tmp")){
                                int objectIndex = 0;
                                foreach (var o in f.Objects) {
                                    if ((objectIndex++ & 127) == 0)
                                        Status("Preparing objects " + fileIndex + " / " + files.Length,
                                            .08 + .17 * (fileIndex - 1 + (double)objectIndex / f.Objects.Count) / files.Length);
                                    var node = schemas.Classes[o.Class];
                                    string script = "";
                                    if (o.Class == 114) {
                                        var header = D(Header(o, node));
                                        script = scripts[Pointer(f, D(header["m_Script"]))];
                                        int colon = script.IndexOf(':');
                                        node =
                                            schemas.Script(script.Substring(0, colon), script.Substring(colon + 1));
                                    }
                                    var value = o.Read(node);
                                    pack.Add(o.Id, node.Type, script, value);
                                    if (o.Class == 49) {
                                        var d = D(value);
                                        string text = Need("text/" + Audio.Safe(
                                            (string)d["m_Name"]) + "_" + o.Id +
                                                ".bytes");
                                        File.WriteAllBytes(text, Encoding.UTF8.GetBytes((string)d["m_Script"]));
                                        Checkpoint(text);
                                    }
                                }
                                pack.Finish(f.Externals);
                            }
                            if (File.Exists(path)) {
                                File.Delete(path);
                            }
                            File.Move(path + ".tmp", path);
                            Checkpoint(path);
                        }
                        entries["objects/" + f.Name] = path;
                    }
                    string spritePack = Need("sprites");
                    bool rebuildSprites = !Cached(spritePack);
                    int spriteCount = 0, textureCount = 0;
                    BinaryWriter spriteWriter = null;
                    try{
                        if (rebuildSprites) {
                            spriteWriter = new BinaryWriter(File.Create(spritePack + ".tmp"));
                            spriteWriter.Write(Encoding.ASCII.GetBytes("KNLS"));
                            spriteWriter.Write(1);
                            spriteWriter.Write(0);
                        }
                        foreach (var tex in textures) {
                            var o = tex.Value;
                            var d = D(o.Read(schemas.Classes[28]));
                            bool ui = !((string)d["m_Name"]).Contains("SpriteAtlasTexture");
                            string texture = Need("textures/" + o.File.Name + "/" + o.Id + ".raw");
                            bool rebuildUi = ui && !Cached(texture);
                            Status("Preparing graphics " + (++textureCount) + " / " + textures.Count,
                                .25 + .15 * (textureCount - 1) / textures.Count);
                            if (rebuildSprites || rebuildUi) {
                                var image = Pixels.Texture(source, d);
                                if (rebuildUi) {
                                    Pixels.Flip(image);
                                    Pixels.WriteRaw(texture + ".tmp", image);
                                    Pixels.Flip(image);
                                    if (File.Exists(texture)) {
                                        File.Delete(texture);
                                    }
                                    File.Move(texture + ".tmp", texture);
                                    Checkpoint(texture);
                                }
                                List<ObjectRecord> list;
                                if (rebuildSprites &&
                                    sprites.TryGetValue(tex.Key, out list)) {
                                    foreach (var s in list) {
                                        var sd = D(s.Read(schemas.Classes[213]));
                                        PackWriter.Sprite(spriteWriter, s.File, s, sd, Pixels.Sprite(image, sd));
                                        spriteCount++;
                                    }
                                }
                            }
                            if (ui) {
                                entries["textures/" + o.File.Name + "/" + o.Id] = texture;
                            }
                        }
                        if (rebuildSprites) {
                            if (spriteCount != 2205) {
                                throw new InvalidDataException(
                                    "Incomplete sprite conversion");
                            }
                            spriteWriter.BaseStream.Position = 8;
                            spriteWriter.Write(spriteCount);
                            spriteWriter.Flush();
                            spriteWriter.Dispose();
                            spriteWriter = null;
                            if (File.Exists(spritePack)) {
                                File.Delete(spritePack);
                            }
                            File.Move(spritePack + ".tmp", spritePack);
                            Checkpoint(spritePack);
                        }
                    }
                    finally{
                        if (spriteWriter != null) {
                            spriteWriter.Dispose();
                        }
                    }
                    entries["sprites"] = spritePack;
                    foreach (var f in files) {
                        foreach (var o in f.Objects.Where(o => o.Class == 49)) {
                            var d = D(o.Read(schemas.Classes[49]));
                            string text = Need("text/" + Audio.Safe(
                                (string)d["m_Name"]) + "_" + o.Id + ".bytes");
                            if (!Cached(text)) {
                                File.WriteAllBytes(text, Encoding.UTF8.GetBytes((string)d["m_Script"]));
                                Checkpoint(text);
                            }
                        }
                    }
                    string textRoot = Need("text/.keep");
                    foreach (var p in Directory.GetFiles(Path.GetDirectoryName(textRoot), "*.bytes")) {
                        if (!Cached(p)) {
                            throw new InvalidDataException(
                                "Text checkpoint damaged; remove the incomplete preparation folder and retry.");
                        }
                        entries["text/" + Path.GetFileName(p)] = p;
                    }
                    double audioSeconds = audio.Sum(o => Pixels.F(D(o.Read(
                        schemas.Classes[83]))["m_Length"])),
                        doneSeconds = 0;
                    int clipIndex = 0;
                    foreach (var o in audio) {
                        var d = D(o.Read(schemas.Classes[83]));
                        bool music = Audio.IsMusic(d);
                        string fileName = Audio.Safe((string)d["m_Name"]);
                        string path =
                            Need("audio/" + (music?"music/":"sfx/") + fileName +
                                (music?".ogg":".adpcm"));
                        double value = .4 + .53 * doneSeconds / audioSeconds;
                        Status("Preparing sound " + (++clipIndex) + " / " + audio.Count, value);
                        if (!Cached(path)) {
                            Exception error = null;
                            bool finished = false;
                            Audio.knli_reset_cancel();
                            var thread =
                                new Thread(() => {try{Audio.ConvertClip(source, d, Path.GetDirectoryName(Need(
                                    "audio/.keep")));
                                                  }
                                                  catch (Exception e) {error = e;
                                                  }
                                                  finally{Volatile.Write(ref finished, true);
                                                  }
                                });
                            thread.Start();
                            try{
                                while (!Volatile.Read(ref finished)) {
                                    Thread.Sleep(100);
                                    Status("Preparing sound " + clipIndex + " / " + audio.Count, value);
                                }
                            }
                            catch {
                                Audio.knli_cancel();
                                thread.Join();
                                throw;
                            }
                            thread.Join();
                            if (error != null) {
                                throw error;
                            }
                            Checkpoint(path);
                        }
                        if (!music) {
                            entries["sfx/" + fileName] = path;
                        }
                        doneSeconds += Pixels.F(d["m_Length"]);
                    }
                }
                Status("Building game pack", .94);
                string data = Need("data/.keep");
                data = Path.GetDirectoryName(data);
                string archive = Path.Combine(data, "kingdom.knlpack");
                if (!Cached(archive)) {
                    PackWriter.Archive(archive + ".tmp", entries);
                    if (File.Exists(archive)) {
                        File.Delete(archive);
                    }
                    File.Move(archive + ".tmp", archive);
                    Checkpoint(archive);
                }
                PackWriter.Validate(archive);
                string musicDir = Path.Combine(data, "music");
                Directory.CreateDirectory(musicDir);
                foreach (var p in Directory.GetFiles(Path.Combine(stage, "audio", "music"), "*.ogg")) {
                    string target = Path.Combine(musicDir, Path.GetFileName(p));
                    if (!File.Exists(target) || Hash(target) != Hash(p)) {
                        File.Copy(p, target, true);
                    }
                }
                Status("Checking source consistency", .975);
                if (VerifySource(Path.Combine(resources, "supported_build.json"), .975, .005) !=
                    key) {
                    throw new InvalidDataException(
                        "Original files changed during preparation. Finish copying them, then retry.");
                }
                Status("Installing prepared files", .98);
                string managed = Need("managed/.keep");
                managed = Path.GetDirectoryName(managed);
                foreach (var name in new[] { "UnityEngine.dll", "Rewired_Core.dll", "knl.dll" })
                {
                    string runtimeFile = Path.Combine(root, "managed", name);
                    if (!File.Exists(runtimeFile)) throw new InvalidDataException("Port runtime is incomplete. Reinstall the complete release ZIP.");
                    File.Copy(runtimeFile, Path.Combine(managed, name), true);
                }
                foreach (var n in GameDlls) {
                    File.Copy(Path.Combine(source, "Managed", n), Path.Combine(managed, n), true);
                }
                Transaction.Commit(root, stage, key);
                Status("Preparation complete", 1);
            }
        }
    }
}
