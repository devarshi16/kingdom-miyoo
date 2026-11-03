using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Collections.Generic;
namespace KNL.Import
{
    static class Audio
    {
        [DllImport("knlimport", CallingConvention = CallingConvention.Cdecl)] static extern int knli_audio(
            string input,
            string output,
            int music,
            StringBuilder error,
            int size);
        [DllImport("knlimport", CallingConvention = CallingConvention.Cdecl)] public static extern void knli_cancel();
        [DllImport("knlimport",
            CallingConvention = CallingConvention.Cdecl)] public static extern void knli_reset_cancel();
        [DllImport("knlimport", CallingConvention = CallingConvention.Cdecl)] static extern int knli_cancelled();
        internal static void CopyResource(string source, Dictionary<string, object> stream, string output)
        {
            long size = Convert.ToInt64(stream["m_Size"]), offset = Convert.ToInt64(stream["m_Offset"]);
            string name = Path.GetFileName(Convert.ToString(stream["m_Source"]).Replace('\\', '/'));
            if (string.IsNullOrEmpty(name) || size < 1 || offset < 0)
                throw new InvalidDataException("Invalid audio resource range");
            using (var input = File.OpenRead(Path.Combine(source, name)))
            {
                if (offset > input.Length || size > input.Length - offset)
                    throw new InvalidDataException("Audio resource is missing or truncated: " + name);
                input.Position = offset;
                using (var target = File.Create(output))
                {
                    var buffer = new byte[65536];
                    while (size > 0)
                    {
                        if (knli_cancelled() != 0) throw new OperationCanceledException("Preparation paused. Launch again to resume.");
                        int got = input.Read(buffer, 0, (int)Math.Min(size, buffer.Length));
                        if (got == 0) throw new EndOfStreamException();
                        target.Write(buffer, 0, got);
                        size -= got;
                    }
                }
            }
        }
        public static string Safe(string s)
        {
            s = Regex.Replace(s, "[^A-Za-z0-9._ -]+", "_").Trim();
            return s.Length == 0?"unnamed":s;
        }
        public static bool IsMusic(Dictionary<string, object> data)
        {
            return Pixels.I(data["m_LoadType"]) == 2 || Pixels.F(data["m_Length"]) > 20;
        }
        public static string ConvertClip(string source, Dictionary<string, object> data, string directory)
        {
            bool music = IsMusic(data);
            string path = Path.Combine(directory, music?"music":"sfx");
            Directory.CreateDirectory(path);
            string output = Path.Combine(path, Safe((string)data["m_Name"]) + (music?".ogg":".adpcm"));
            string temp = Path.Combine(directory, "clip-input.fsb");
            try{
                // Some clips are almost 10 MB. Copy their bounded FSB range with a
                // fixed buffer instead of retaining another whole clip in Mono's heap.
                CopyResource(source, Pixels.D(data["m_Resource"]), temp);
                var error = new StringBuilder(512);
                if (knli_audio(temp, output, music?1:0, error, 512) == 0) {
                    throw new InvalidDataException(
                        "Audio " + data["m_Name"] + ": " + error);
                }
                return output;
            }
            finally{
                if (File.Exists(temp)) {
                    File.Delete(temp);
                }
            }
        }
    }
}
