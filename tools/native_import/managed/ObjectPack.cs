using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
namespace KNL.Import
{
// Streams each file's values to a temporary blob, retaining only its string table/index.
// The encoding is KNLB v1, shared with the existing desktop packer and runtime reader.
    sealed class ObjectPack : IDisposable
    {
        readonly BinaryWriter blob;
        readonly string path;
        readonly List<string> strings = new List<string>();
        readonly Dictionary<string, int> ids = new Dictionary<string, int>();
        readonly List<Index> index = new List<Index>();
        struct Index {
            public long Id;
            public int Type, Script, Offset, Length;
        }
        public ObjectPack(string path)
        {
            this.path = path;
            blob = new BinaryWriter(File.Create(path + ".blob"));
        }
        int S(string s)
        {
            int id;
            if (!ids.TryGetValue(s, out id)) {
                id = strings.Count;
                strings.Add(s);
                ids.Add(s, id);
            }
            return id;
        }
        public static void Var(BinaryWriter w, ulong n)
        {
            while (n >= 128) {
                w.Write((byte)(n | 128));
                n >>= 7;
            }
            w.Write((byte)n);
        }
        public static void Text(BinaryWriter w, string s)
        {
            var b = Encoding.UTF8.GetBytes(s);
            Var(w, (ulong)b.Length);
            w.Write(b);
        }
        public void Add(long id, string type, string script, object data)
        {
            int off = checked ((int)blob.BaseStream.Position);
            Encode(data, null);
            index.Add(new Index {
                Id = id, Type = S(type), Script = S(script ?? ""), Offset = off,
                Length = checked ((int)blob.BaseStream.Position) - off
            });
        }
        void Encode(object v, string field)
        {
            var raw = v as byte[];
            var dict = v as Dictionary<string, object>;
            var list = v as IList;
            bool stripped = field == "image data" || field == "m_AudioData" || field == "m_FontData" ||
                field == "m_Script_bytes";
            if (dict == null && list != null && stripped) {
                Encode(new Dictionary<string, object> {
                    {"$stripped", list.Count}
                }, null);
                return;
            }
            if (raw != null) {
                Encode(new Dictionary<string, object> {
                    {"$bytes", raw.Length}
                }, null);
                return;
            }
            if (v == null) {
                blob.Write((byte)0);
                return;
            }
            if (v is bool) {
                blob.Write((byte)((bool)v?2:1));
                return;
            }
            if (v is string) {
                blob.Write((byte)5);
                Var(blob, (ulong)S((string)v));
                return;
            }
            if (dict != null) {
                blob.Write((byte)7);
                Var(blob, (ulong)dict.Count);
                foreach (var kv in dict) {
                    Var(blob, (ulong)S(kv.Key));
                    Encode(kv.Value, kv.Key);
                }
                return;
            }
            if (list != null) {
                blob.Write((byte)6);
                Var(blob, (ulong)list.Count);
                foreach (var item in list) {
                    Encode(item, null);
                }
                return;
            }
            if (v is float || v is double) {
                double d = Convert.ToDouble(v);
                if (double.IsNaN(d) || double.IsInfinity(d)) {
                    Encode(double.IsNaN(d)?"nan":d < 0?"-inf":"inf", null);
                    return;
                }
                if ((double)(float)d == d) {
                    blob.Write((byte)8);
                    blob.Write((float)d);
                }
                else{
                    blob.Write((byte)4);
                    blob.Write(d);
                }
                return;
            }
            decimal integer = Convert.ToDecimal(v);
            if (integer > -(1L << 62) && integer < (1L << 62)) {
                long n = (long)integer;
                blob.Write((byte)3);
                Var(blob, unchecked ((ulong)((n << 1) ^ (n >> 63))));
            }
            else{
                blob.Write((byte)4);
                blob.Write(Convert.ToDouble(v));
            }
        }
        public void Finish(List<string> externals)
        {
            var ext = new List<int>();
            foreach (var e in externals) {
                ext.Add(S(e));
            }
            blob.Flush();
            blob.Dispose();
            using (var w = new BinaryWriter(File.Create(path))){
                w.Write(Encoding.ASCII.GetBytes("KNLB"));
                w.Write(1);
                w.Write(strings.Count);
                foreach (var s in strings) {
                    Text(w, s);
                }
                w.Write(ext.Count);
                foreach (int id in ext) {
                    w.Write(id);
                }
                w.Write(index.Count);
                foreach (var i in index) {
                    w.Write(i.Id);
                    w.Write(i.Type);
                    w.Write(i.Script);
                    w.Write(i.Offset);
                    w.Write(i.Length);
                }
                using (var r = File.OpenRead(path + ".blob")) r.CopyTo(w.BaseStream);
            }
            File.Delete(path + ".blob");
        }
        public void Dispose()
        {
            blob.Dispose();
        }
    }
}
