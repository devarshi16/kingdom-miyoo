// Device data pack (tools/pack.py, D19): one archive with binary objects decoded lazily per
// object, 8-bit indexed sprites, UI textures and ADPCM SFX. Music streams from music/*.ogg.
// Without a pack the engine reads convert.py's extracted/ tree (JSON + PNG), as on desktop.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace KNL
{
    using JObj = Dictionary<string, object>;

    public static class Pack
    {
        public static bool Active;
        public static string FilePath;
        static readonly Dictionary<string, KeyValuePair<long, int>> dir = new Dictionary<string, KeyValuePair<long, int>>();
        static FileStream fs;

        [DllImport("knlcore")] static extern int knl_pack_open(string path);

        public static bool TryOpen(string dataDir)
        {
            var p = Path.Combine(dataDir, "kingdom.knlpack");
            if (!File.Exists(p)) return false;
            fs = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.Read, 65536);
            var br = new BinaryReader(fs);
            if (new string(br.ReadChars(4)) != "KNLP" || br.ReadUInt32() != 1) throw new InvalidDataException("not a v1 knlpack: " + p);
            uint n = br.ReadUInt32();
            for (uint i = 0; i < n; i++)
            {
                int ln = br.ReadUInt16();
                string name = Encoding.UTF8.GetString(br.ReadBytes(ln));
                long off = (long)br.ReadUInt64(); int len = (int)br.ReadUInt32();
                dir[name] = new KeyValuePair<long, int>(off, len);
            }
            FilePath = p; Active = true;
            knl_pack_open(p);
            Log.Info(string.Format("data: pack {0} ({1} entries)", p, n));
            return true;
        }
        public static bool Has(string name) { return dir.ContainsKey(name); }
        public static bool Locate(string name, out long offset, out int length)
        {
            KeyValuePair<long, int> e;
            if (!dir.TryGetValue(name, out e)) { offset = 0; length = 0; return false; }
            offset = e.Key; length = e.Value; return true;
        }
        public static byte[] Read(string name)
        {
            long off; int len;
            if (!Locate(name, out off, out len)) return null;
            var b = new byte[len];
            lock (fs) { fs.Seek(off, SeekOrigin.Begin); int got = 0; while (got < len) { int r = fs.Read(b, got, len - got); if (r <= 0) break; got += r; } }
            return b;
        }
    }

    /// The objects of one serialized file. JSON mode keeps parsed trees; pack mode keeps the compact
    /// binary blob and decodes an object's tree on each lookup (callers keep what they need).
    public sealed class ObjectTable : IEnumerable<KeyValuePair<long, JObj>>
    {
        readonly Dictionary<long, JObj> parsed;           // JSON mode
        // pack mode
        readonly Dictionary<long, int> index;
        long[] pids; string[] types, scripts; int[] offs, lens;
        byte[] blob; string[] strings;

        public ObjectTable() { parsed = new Dictionary<long, JObj>(); }
        ObjectTable(bool pack) { index = new Dictionary<long, int>(); }

        public int Count { get { return parsed != null ? parsed.Count : pids.Length; } }
        public void Add(long pid, JObj o) { parsed[pid] = o; }
        public void Clear() { if (parsed != null) parsed.Clear(); else { index.Clear(); pids = new long[0]; blob = null; } }
        public bool ContainsKey(long pid) { return parsed != null ? parsed.ContainsKey(pid) : index.ContainsKey(pid); }
        public JObj this[long pid] { get { JObj o; if (!TryGetValue(pid, out o)) throw new KeyNotFoundException(pid.ToString()); return o; } set { parsed[pid] = value; } }

        /// type name without decoding (pack mode) - used by scans that only filter on type
        public string TypeOf(long pid)
        {
            if (parsed != null) { JObj o; return parsed.TryGetValue(pid, out o) ? o["type"] as string : null; }
            int i; return index.TryGetValue(pid, out i) ? types[i] : null;
        }

        public bool TryGetValue(long pid, out JObj o)
        {
            if (parsed != null) return parsed.TryGetValue(pid, out o);
            int i;
            if (!index.TryGetValue(pid, out i)) { o = null; return false; }
            o = Decode(i); return true;
        }

        public IEnumerable<JObj> Values { get { foreach (var kv in this) yield return kv.Value; } }

        public IEnumerator<KeyValuePair<long, JObj>> GetEnumerator()
        {
            if (parsed != null) { foreach (var kv in parsed) yield return kv; yield break; }
            for (int i = 0; i < pids.Length; i++) yield return new KeyValuePair<long, JObj>(pids[i], Decode(i));
        }
        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

        JObj Decode(int i)
        {
            var o = new JObj(3);
            o["type"] = types[i];
            if (scripts[i].Length > 0) o["script"] = scripts[i];
            int p = offs[i];
            o["data"] = Value(ref p);
            return o;
        }

        int VarInt(ref int p)
        {
            int r = 0, sh = 0;
            while (true) { byte b = blob[p++]; r |= (b & 0x7F) << sh; if ((b & 0x80) == 0) return r; sh += 7; }
        }
        long VarLong(ref int p)
        {
            long r = 0; int sh = 0;
            while (true) { byte b = blob[p++]; r |= (long)(b & 0x7F) << sh; if ((b & 0x80) == 0) return r; sh += 7; }
        }
        // same shapes as Json.Parse: double for numbers, string, bool, null, List<object>, JObj
        object Value(ref int p)
        {
            byte tag = blob[p++];
            switch (tag)
            {
                case 0: return null;
                case 1: return false;
                case 2: return true;
                case 3: { long z = VarLong(ref p); return (double)((z >> 1) ^ -(z & 1)); }
                case 4: { double d = BitConverter.ToDouble(blob, p); p += 8; return d; }
                case 8: { float f = BitConverter.ToSingle(blob, p); p += 4; return (double)f; }
                case 5: return strings[VarInt(ref p)];
                case 6: { int n = VarInt(ref p); var l = new List<object>(n); for (int k = 0; k < n; k++) l.Add(Value(ref p)); return l; }
                case 7: { int n = VarInt(ref p); var d = new JObj(n); for (int k = 0; k < n; k++) { string key = strings[VarInt(ref p)]; d[key] = Value(ref p); } return d; }
            }
            throw new InvalidDataException("bad tag " + tag + " at " + (p - 1));
        }

        /// pack entry "objects/<file>" -> table (externals returned separately)
        public static ObjectTable FromPack(byte[] b, List<string> externals)
        {
            var t = new ObjectTable(true);
            var ms = new MemoryStream(b); var br = new BinaryReader(ms);
            if (new string(br.ReadChars(4)) != "KNLB" || br.ReadUInt32() != 1) throw new InvalidDataException("bad KNLB");
            int ns = (int)br.ReadUInt32();
            t.strings = new string[ns];
            t.blob = b;
            int pos = (int)ms.Position;
            for (int i = 0; i < ns; i++)
            {
                int len = t.VarInt(ref pos);
                t.strings[i] = string.Intern(Encoding.UTF8.GetString(b, pos, len)); pos += len;
            }
            ms.Position = pos;
            int ne = (int)br.ReadUInt32();
            for (int i = 0; i < ne; i++) externals.Add(t.strings[br.ReadUInt32()]);
            int n = (int)br.ReadUInt32();
            t.pids = new long[n]; t.types = new string[n]; t.scripts = new string[n]; t.offs = new int[n]; t.lens = new int[n];
            for (int i = 0; i < n; i++)
            {
                t.pids[i] = br.ReadInt64(); t.types[i] = t.strings[br.ReadUInt32()]; t.scripts[i] = t.strings[br.ReadUInt32()];
                t.offs[i] = (int)br.ReadUInt32(); t.lens[i] = (int)br.ReadUInt32();
                t.index[t.pids[i]] = i;
            }
            int dataStart = (int)ms.Position;
            for (int i = 0; i < n; i++) t.offs[i] += dataStart;
            return t;
        }
    }
}
