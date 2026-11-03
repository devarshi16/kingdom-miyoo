using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
namespace KNL.Import
{
    static class PackWriter
    {
        public static void Sprite(BinaryWriter w, SerializedFile f, ObjectRecord record, Dictionary<string,
            object> d, Image image)
        {
            ObjectPack.Text(w, f.Name);
            w.Write(record.Id);
            ObjectPack.Text(w, (string)d["m_Name"]);
            var r = Pixels.D(d["m_Rect"]);
            foreach (var k in new[] {"x", "y", "width", "height"}) {
                w.Write(Convert.ToSingle(r[k]));
            }
            var p = Pixels.D(d["m_Pivot"]);
            w.Write(Convert.ToSingle(p["x"]));
            w.Write(Convert.ToSingle(p["y"]));
            w.Write(Convert.ToSingle(d["m_PixelsToUnits"]));
            w.Write(image.Width);
            w.Write(image.Height);
            var offset = Pixels.D(Pixels.D(d["m_RD"])["textureRectOffset"]);
            w.Write((int)Math.Round(Pixels.F(offset["x"])));
            w.Write((int)Math.Round(Pixels.F(offset["y"])));
            var colors = new SortedSet<uint>();
            var b = image.Pixels;
            for (int i = 0; i < b.Length && colors.Count <= 256; i += 4) {
                colors.Add(
                    (uint)b[i] << 24 | (uint)b[i + 1] << 16 | (uint)b[i + 2] << 8 | b[i + 3]);
            }
            if (colors.Count <= 256) {
                w.Write((byte)0);
                w.Write((ushort)colors.Count);
                var ids = new Dictionary<uint, byte>();
                int n = 0;
                foreach (uint c in colors) {
                    ids[c] = (byte)n++;
                    w.Write((byte)(c >> 24));
                    w.Write((byte)(c >> 16));
                    w.Write((byte)(c >> 8));
                    w.Write((byte)c);
                }
                for (int i = 0; i < b.Length;
                    i +=
                    4) {
                    w.Write(ids[(uint)b[i] << 24 | (uint)b[i + 1] << 16 | (uint)b[i + 2] <<
                        8 | b[i + 3]]);
                }
            }
            else{
                w.Write((byte)1);
                w.Write((ushort)0);
                w.Write(b);
            }
        }
        public static void Archive(string output, Dictionary<string, string> entries)
        {
            long offset = 12;
            foreach (var e in entries) {
                offset += 2 + Encoding.UTF8.GetByteCount(e.Key) + 12;
            }
            using (var w = new BinaryWriter(File.Create(output))){
                w.Write(Encoding.ASCII.GetBytes("KNLP"));
                w.Write(1);
                w.Write(entries.Count);
                foreach (var e in entries) {
                    byte[] name = Encoding.UTF8.GetBytes(e.Key);
                    long size = new FileInfo(e.Value).Length;
                    if (name.Length < 1 || name.Length > 4096 || size < 1 ||
                        size > int.MaxValue) {
                        throw new InvalidDataException(
                            "Invalid pack entry " + e.Key);
                    }
                    w.Write((ushort)name.Length);
                    w.Write(name);
                    w.Write((ulong)offset);
                    w.Write((uint)size);
                    offset += size;
                }
                foreach (var e in entries) {
                    using (var r = File.OpenRead(e.Value)) r.CopyTo(w.BaseStream);
                }
                w.Flush();
                w.BaseStream.Flush();
            }
            Validate(output);
        }
        public static void Validate(string path)
        {
            using (var r = new BinaryReader(File.OpenRead(path))){
                if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "KNLP" ||
                    r.ReadInt32() != 1) {
                    throw new InvalidDataException("Invalid game pack header");
                }
                int count = r.ReadInt32();
                if (count < 1 || count > 100000) {
                    throw new InvalidDataException("Invalid pack entry count");
                }
                var entries = new Dictionary<string, long[]>();
                for (int i = 0; i < count; i++) {
                    int length = r.ReadUInt16();
                    if (length < 1 ||
                        length > 4096) {
                        throw new InvalidDataException("Invalid pack entry name");
                    }
                    var b = r.ReadBytes(length);
                    if (b.Length != length) {
                        throw new EndOfStreamException();
                    }
                    string name = Encoding.UTF8.GetString(b);
                    ulong off = r.ReadUInt64();
                    uint size = r.ReadUInt32();
                    if (off > (ulong)r.BaseStream.Length || size < 1 ||
                        off + size > (ulong)r.BaseStream.Length ||
                        entries.ContainsKey(name)) {
                        throw new InvalidDataException(
                            "Pack entry outside archive: " + name);
                    }
                    entries[name] = new[] {
                        (long)off, (long)size
                    };
                }
                long end = r.BaseStream.Position;
                foreach (var entry in entries.OrderBy(e => e.Value[0])) {
                    if (entry.Value[0] <
                        end) {
                        throw new InvalidDataException("Overlapping game pack entries");
                    }
                    end = entry.Value[0] + entry.Value[1];
                }
                foreach (var n in Importer.Serialized.Concat(new[] {"sprites"})) {
                    string name = n == "sprites"?n:"objects/" + n;
                    if (!entries.ContainsKey(name)) {
                        throw new InvalidDataException("Pack missing " + name);
                    }
                }
                var sprite = entries["sprites"];
                r.BaseStream.Position = sprite[0];
                if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "KNLS" || r.ReadInt32() != 1 ||
                    r.ReadInt32() != 2205) {
                    throw new InvalidDataException("Incomplete sprite pack");
                }
            }
        }
    }
}
