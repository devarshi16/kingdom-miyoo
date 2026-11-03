using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
namespace KNL.Import
{
    sealed class TreeReader
    {
        static readonly bool Trace = Environment.GetEnvironmentVariable("KNL_IMPORT_TRACE") == "1";
        readonly BinaryReader r;
        readonly long end;
        public TreeReader(BinaryReader r, long end)
        {
            this.r = r;
            this.end = end;
        }
        public static void Align(BinaryReader r)
        {
            long p = (r.BaseStream.Position + 3) & ~3L;
            if (p > r.BaseStream.Length) {
                throw new EndOfStreamException();
            }
            r.BaseStream.Position = p;
        }
        int Count(int limit)
        {
            int n = r.ReadInt32();
            if (n < 0 || n > limit || n > end - r.BaseStream.Position) {
                throw new InvalidDataException(
                    "Invalid array/string length " + n);
            }
            return n;
        }
        public object Read(Node n)
        {
            if (Trace) {
                Console.Error.WriteLine(
                    r.BaseStream.Position + " " + n.Type + " " + n.Name);
            }
            object v;
            bool align = (n.Flags & 0x4000) != 0;
            switch (n.Type) {
            case "SInt8": v = (long)r.ReadSByte();
                break;
            case "UInt8": v = (long)r.ReadByte();
                break;
            case "char": v = n.Size == 1?(long)r.ReadByte():(long)r.ReadUInt16();
                break;
            case "SInt16": case "short": v = (long)r.ReadInt16();
                break;
            case "UInt16": case "unsigned short": v = (long)r.ReadUInt16();
                break;
            case "int": case "SInt32": v = (long)r.ReadInt32();
                break;
            case "UInt32": case "unsigned int": case "Type*": v = (long)r.ReadUInt32();
                break;
            case "SInt64": case "long long": v = r.ReadInt64();
                break;
            case "UInt64": case "unsigned long long": case "FileSize": v = r.ReadUInt64();
                break;
            case "float": v = r.ReadSingle();
                break;
            case "double": v = r.ReadDouble();
                break;
            case "bool": v = r.ReadBoolean();
                break;
            case "string": {
                int size = Count(16 * 1024 * 1024);
                v = Encoding.UTF8.GetString(r.ReadBytes(size));
                Align(r);
                break;
            }
            case "TypelessData": {
                int size = Count(64 * 1024 * 1024);
                v = r.ReadBytes(size);
                break;
            }
            default:
                if (n.Children.Count > 0 && n.Children[0].Type == "Array") {
                    var a = n.Children[0];
                    align |= (a.Flags & 0x4000) != 0;
                    int size = Count(4000000);
                    var data = a.Children[1];
                    var list = new List<object>(size);
                    for (int i = 0; i < size; i++) {
                        var item = Read(data);
                        if (n.Type == "map") {
                            if (item is Dictionary<string, object>) {
                                var pair = (Dictionary<string, object>)item;
                                item = new List<object> {
                                    pair["first"], pair["second"]
                                };
                            }
                        }
                        list.Add(item);
                    }
                    v = list;
                }
                else{
                    var d = new Dictionary<string, object>();
                    foreach (var c in n.Children) {
                        d[c.Name] = Read(c);
                    }
                    v = n.Type == "pair"?(object)new List<object> {
                        d["first"], d["second"]
                    }
                                                                                                                                                              :
                    d;
                }
                break;
            }
            if (align) {
                Align(r);
            }
            if (r.BaseStream.Position > end) {
                throw new EndOfStreamException(
                    "Object overread: " + n.Type + " " + n.Name);
            }
            return v;
        }
    }
    sealed class ObjectRecord
    {
        public long Id, Offset;
        public int Size, Class;
        public SerializedFile File;
        public object Read(Node schema) { return File.ReadRecord(this, schema, true); }
    }

    sealed class SerializedFile : IDisposable
    {
        BinaryReader payloadReader;
        public object ReadRecord(ObjectRecord record, Node schema, bool exact)
        {
            if (payloadReader == null) payloadReader = new BinaryReader(new FileStream(Path,
                FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.RandomAccess));
            payloadReader.BaseStream.Position = record.Offset;
            var value = new TreeReader(payloadReader, record.Offset + record.Size).Read(schema);
            if (exact && payloadReader.BaseStream.Position != record.Offset + record.Size)
                throw new InvalidDataException(Name + "#" + record.Id + " " + schema.Type +
                    " consumed " + (payloadReader.BaseStream.Position - record.Offset) + " of " + record.Size);
            return value;
        }
        public void Dispose() { if (payloadReader != null) { payloadReader.Dispose(); payloadReader = null; } }
        public string Path, Name;
        public List<string> Externals = new List<string>();
        public List<ObjectRecord> Objects = new List<ObjectRecord>();
        static uint Be(BinaryReader r)
        {
            var b = r.ReadBytes(4);
            if (b.Length != 4) {
                throw new EndOfStreamException();
            }
            return (uint)b[0] << 24 | (uint)b[1] << 16 | (uint)b[2] << 8 | b[3];
        }
        static int Count(BinaryReader r, int limit)
        {
            int n = r.ReadInt32();
            if (n < 0 || n > limit) {
                throw new InvalidDataException("Metadata count out of range");
            }
            return n;
        }
        static void Skip(BinaryReader r, long n)
        {
            if (n < 0 || r.BaseStream.Position + n > r.BaseStream.Length) {
                throw new EndOfStreamException();
            }
            r.BaseStream.Position += n;
        }
        static string CStr(BinaryReader r)
        {
            var b = new List<byte>();
            byte c;
            while ((c = r.ReadByte()) != 0) {
                if (b.Count >= 4096) {
                    throw new InvalidDataException("Long metadata string");
                }
                b.Add(c);
            }
            return Encoding.UTF8.GetString(b.ToArray());
        }
        public SerializedFile(string path)
        {
            Path = path;
            Name = System.IO.Path.GetFileName(path);
            using (var r = new BinaryReader(System.IO.File.OpenRead(path))){
                uint meta = Be(r), size = Be(r), version = Be(r), data = Be(r);
                byte endian = r.ReadByte();
                Skip(r, 3);
                if (version != 17 || endian != 0 || size != r.BaseStream.Length || data > size ||
                    meta > size - 20) {
                    throw new InvalidDataException(
                        "Unsupported Unity file header: " + Name);
                }
                if (CStr(r) != "2017.1.2p1") {
                    throw new InvalidDataException("Unsupported Unity version");
                }
                r.ReadInt32();
                bool tree = r.ReadBoolean();
                int count = Count(r, 10000);
                var classes = new List<int>();
                for (int i = 0; i < count; i++) {
                    int id = r.ReadInt32();
                    classes.Add(id);
                    r.ReadByte();
                    r.ReadInt16();
                    if (id == 114) {
                        Skip(r, 16);
                    }
                    Skip(r, 16);
                    if (tree) {
                        throw new InvalidDataException("Embedded type-tree build not supported");
                    }
                }
                count = Count(r, 1000000);
                for (int i = 0; i < count; i++) {
                    TreeReader.Align(r);
                    long id = r.ReadInt64();
                    uint off = r.ReadUInt32();
                    int len = r.ReadInt32(), type = r.ReadInt32();
                    if (type < 0 || type >= classes.Count || len < 0 ||
                        (long)data + off + len > size) {
                        throw new InvalidDataException(
                            "Object outside source file");
                    }
                    Objects.Add(new ObjectRecord {
                        File = this, Id = id, Offset = (long)data + off, Size = len,
                        Class = classes[type]
                    });
                }
                count = Count(r, 1000000);
                for (int i = 0; i < count; i++) {
                    r.ReadInt32();
                    TreeReader.Align(r);
                    r.ReadInt64();
                }
                count = Count(r, 10000);
                for (int i = 0; i < count; i++) {
                    CStr(r);
                    Skip(r, 20);
                    Externals.Add(System.IO.Path.GetFileName(CStr(r).Replace('\\', '/')));
                }
                CStr(r);
                if (r.BaseStream.Position > 20 + meta) {
                    throw new InvalidDataException("Metadata overread");
                }
            }
        }
    }
    sealed class SourceSet : IDisposable
    {
        public readonly SerializedFile[] Files;
        public SourceSet(string source) { Files = Importer.Serialized.Select(n => new SerializedFile(System.IO.Path.Combine(source, n))).ToArray(); }
        public void Dispose() { foreach (var file in Files) file.Dispose(); }
    }

}
