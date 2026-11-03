// Developer-only parity driver. Outputs belong in private work, never in a release.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
namespace KNL.Import
{
    static class PixelInspect
    {
        static string Key(SerializedFile f, Dictionary<string, object> ptr)
        {
            int fid = Pixels.I(ptr["m_FileID"]);
            return (fid == 0?f.Name:f.Externals[fid - 1]) + "#" + ptr["m_PathID"];
        }
        static void Main(string[] args)
        {
            var names = new[] {
                "globalgamemanagers", "globalgamemanagers.assets", "level0", "level1", "level2",
                "resources.assets", "sharedassets0.assets", "sharedassets1.assets",
                "sharedassets2.assets"
            };
            using (var schemas = new Schemas(args[1], Path.Combine(args[0], "Managed"))){
                var files = names.Select(n => new SerializedFile(Path.Combine(args[0], n))).ToArray();
                var textures = new Dictionary<string, ObjectRecord>();
                var sprites = new Dictionary<string, List<ObjectRecord> >();
                foreach (var f in files) {
                    foreach (var o in f.Objects) {
                        if (o.Class == 28) {
                            textures.Add(f.Name + "#" + o.Id, o);
                        }
                        if (o.Class == 213) {
                            var d = Pixels.D(o.Read(schemas.Classes[213]));
                            string key = Key(f, Pixels.D(Pixels.D(d["m_RD"])["texture"]));
                            List<ObjectRecord> list;
                            if (!sprites.TryGetValue(key, out list)) {
                                list = new List<ObjectRecord>();
                                sprites.Add(key, list);
                            }
                            list.Add(o);
                        }
                    }
                }
                int nt = 0, ns = 0;
                foreach (var pair in textures) {
                    var o = pair.Value;
                    var d = Pixels.D(o.Read(schemas.Classes[28]));
                    var im = Pixels.Texture(args[0], d);
                    string dir = Path.Combine(args[2], "textures", o.File.Name);
                    Directory.CreateDirectory(dir);
                    Pixels.Flip(im);
                    Pixels.WriteRaw(Path.Combine(dir, o.Id + ".raw"), im);
                    Pixels.Flip(im);
                    nt++;
                    List<ObjectRecord> list;
                    if (sprites.TryGetValue(pair.Key, out list)) {
                        foreach (var s in list) {
                            var sd = Pixels.D(s.Read(schemas.Classes[213]));
                            var sim = Pixels.Sprite(im, sd);
                            dir = Path.Combine(args[2], "sprites", s.File.Name);
                            Directory.CreateDirectory(dir);
                            Pixels.WriteRaw(Path.Combine(dir, s.Id + ".raw"), sim);
                            ns++;
                        }
                    }
                    Console.WriteLine("pixels textures=" + nt + " sprites=" + ns);
                }
            }
        }
    }
}
