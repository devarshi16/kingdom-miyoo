using System;using System.IO;using System.Linq;using System.Security.Cryptography;using System.Collections.Generic;
namespace KNL.Import
{
    static class RuntimeIntegrity
    {
        public static readonly string[] Files = {
            "managed/UnityEngine.dll", "managed/Rewired_Core.dll", "managed/knl.dll",
            "lib/libknlcore.so", "lib/libknlimport.so", "importer/Mono.Cecil.dll",
            "importer/unity2017.knlt", "importer/NativeImporter.exe"
        };
        public static void Verify(string root, string resources)
        {
            string manifest = Path.Combine(resources, "runtime.json");
            if (!File.Exists(manifest)) throw new InvalidDataException("Port files are incomplete. Re-extract the complete release ZIP.");
            var hashes = (Dictionary<string, object>)KNL.Json.ParseFile(manifest);
            foreach (string name in Files)
            {
                object expected;
                string path = Path.Combine(root, name);
                if (!hashes.TryGetValue(name, out expected) || !File.Exists(path))
                    throw new InvalidDataException("Port file missing: " + name + ". Re-extract the complete release ZIP.");
                using (var hash = SHA256.Create()) using (var file = File.OpenRead(path))
                {
                    string value = BitConverter.ToString(hash.ComputeHash(file)).Replace("-", "").ToLowerInvariant();
                    if (value != (string)expected)
                        throw new InvalidDataException("Port file damaged or mixed with another release: " + name + ". Re-extract the complete release ZIP.");
                }
            }
        }
        public static bool GameLogicMatches(string root, string resources)
        {
            var profile = (Dictionary<string, object>)KNL.Json.ParseFile(Path.Combine(resources, "supported_build.json"));
            var hashes = (Dictionary<string, object>)profile["fingerprints"];
            return Importer.GameDlls.All(name => File.Exists(Path.Combine(root, "managed", name)) &&
                Digest.FileHash(Path.Combine(root, "managed", name)) == (string)hashes["Managed/" + name]);
        }
    }
}
