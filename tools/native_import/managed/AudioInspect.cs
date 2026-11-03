using System;
using System.IO;
using System.Linq;
namespace KNL.Import
{
    static class AudioInspect
    {
        static void Main(string[] args)
        {
            var names = new[] {
                "resources.assets", "sharedassets0.assets", "sharedassets1.assets", "sharedassets2.assets"
            };
            using (var schemas = new Schemas(args[1], Path.Combine(args[0], "Managed"))){
                int count = 0;
                foreach (var name in names) {
                    var f = new SerializedFile(Path.Combine(args[0], name));
                    foreach (var o in f.Objects.Where(o => o.Class == 83)) {
                        var d = Pixels.D(o.Read(schemas.Classes[83]));
                        Console.WriteLine("audio " + (++count) + " " + Audio.ConvertClip(args[0], d, args[2]));
                    }
                }
            }
        }
    }
}
