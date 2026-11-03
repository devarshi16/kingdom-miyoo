// Developer validation entry point. Produces no distributable assets.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
namespace KNL.Import
{
    static class Inspect
    {
        static Dictionary<string, object> D(object v)
        {
            return (Dictionary<string, object>)v;
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
                var scripts = new Dictionary<string, string>();
                foreach (var f in files) {
                    foreach (var o in f.Objects.Where(x => x.Class == 115)) {
                        var d = D(o.Read(schemas.Classes[115]));
                        scripts[f.Name + "#" + o.Id] = d["m_AssemblyName"] + ":" +
                            (d["m_Namespace"].ToString() ==
                                ""?"":d["m_Namespace"] + ".") + d["m_ClassName"];
                    }
                }
                int ok = 0, bad = 0;
                foreach (var f in files) {
                    ObjectPack pack = null;
                    if (args.Length > 3) {
                        Directory.CreateDirectory(args[3]);
                        pack = new ObjectPack(Path.Combine(args[3], f.Name + ".knlb"));
                    }
                    foreach (var o in f.Objects) {
                        if (args.Length > 2 && args[2] != "all" && f.Name + "#" + o.Id != args[2]) {
                            continue;
                        }
                        string scriptName = "";
                        try{
                            var node = schemas.Classes[o.Class];
                            if (o.Class == 114) {
                                var h = D(ReadHeader(o, node));
                                var ptr = D(h["m_Script"]);
                                long file = Convert.ToInt64(ptr["m_FileID"]),
                                    id = Convert.ToInt64(ptr["m_PathID"]);
                                string target = file == 0?f.Name:f.Externals[(int)file - 1];
                                string script = scripts[target + "#" + id];
                                scriptName = script;
                                int colon = script.IndexOf(':');
                                node = schemas.Script(script.Substring(0, colon), script.Substring(colon + 1));
                            }
                            var value = o.Read(node);
                            if (pack != null) {
                                pack.Add(o.Id, node.Type, scriptName, value);
                            }
                            ok++;
                        }
                        catch (Exception e) {
                            bad++;
                            Console.Error.WriteLine(
                                "FAIL " + f.Name + "#" + o.Id + " class=" + o.Class + " script=" + scriptName + " " + e.GetType().Name + ": " +
                                e.Message);
                        }
                    }
                    if (pack != null) {
                        pack.Finish(f.Externals);
                        pack.Dispose();
                    }
                }
                Console.WriteLine("decoded=" + ok + " failed=" + bad);
                if (bad > 0) {
                    Environment.ExitCode = 1;
                }
            }
        }
        static object ReadHeader(ObjectRecord o, Node n)
        {
            using (var r = new BinaryReader(File.OpenRead(o.File.Path))){
                r.BaseStream.Position = o.Offset;
                return new TreeReader(r, o.Offset + o.Size).Read(n);
            }
        }
    }
}
