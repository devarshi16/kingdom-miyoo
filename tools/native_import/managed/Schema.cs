// Generic serialization metadata reader. Original game assemblies are inspected with
// Mono.Cecil only; importing does not load or execute their code.
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Mono.Cecil;
namespace KNL.Import
{
    sealed class Node
    {
        public string Type, Name;
        public int Flags, Size;
        public List<Node> Children = new List<Node>();
        public Node(string t, string n, int flags = 0)
        {
            Type = t;
            Name = n;
            Flags = flags;
            Size = -1;
        }
        public Node Copy(string name)
        {
            var n = new Node(Type, name, Flags){
                Size = Size
            };
            n.Children.AddRange(Children);
            return n;
        }
    }
    sealed class OriginalResolver : DefaultAssemblyResolver
    {
        public void Remember(AssemblyDefinition assembly) { RegisterAssembly(assembly); }
    }
    sealed class Schemas : IDisposable
    {
        public readonly Dictionary<int, Node> Classes = new Dictionary<int, Node>();
        readonly Dictionary<string, Node> values = new Dictionary<string, Node>(),
            scripts = new Dictionary<string, Node>();
        readonly OriginalResolver resolver = new OriginalResolver();
        readonly string managedDirectory;
        readonly Dictionary<string, AssemblyDefinition> assemblies = new Dictionary<string,
                AssemblyDefinition>();
        public Schemas(string database, string managed)
        {
            using (var r = new BinaryReader(File.OpenRead(database))){
                if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "KNLT" ||
                    r.ReadUInt32() != 1) {
                    throw new InvalidDataException(
                        "Invalid generic schema database");
                }
                int count = r.ReadInt32();
                if (count < 1 || count > 10000) {
                    throw new InvalidDataException();
                }
                for (int c = 0; c < count; c++) {
                    int id = r.ReadInt32(), nn = r.ReadInt32();
                    if (nn < 1 || nn > 100000) {
                        throw new InvalidDataException();
                    }
                    var stack = new Stack<Node>();
                    Node root = null;
                    for (int i = 0; i < nn; i++) {
                        string type = Text(r), name = Text(r);
                        int level = r.ReadInt32(), flags = r.ReadInt32(), size = r.ReadInt32();
                        var n = new Node(type, name, flags){
                            Size = size
                        };
                        while (stack.Count > level) {
                            stack.Pop();
                        }
                        if (level == 0) {
                            root = n;
                        }
                        else {
                            if (stack.Count != level) {
                                throw new InvalidDataException();
                            }
                            stack.Peek().Children.Add(n);
                        }
                        stack.Push(n);
                    }
                    Classes.Add(id, root);
                    Index(root);
                }
            }
            managedDirectory = managed;
            // Never resolve user metadata against the engine replacement in the working
            // directory. Original game/Unity metadata comes first; bundled Mono supplies
            // only the standard CLR metadata that was not shipped with the game.
            foreach (var directory in resolver.GetSearchDirectories()) resolver.RemoveSearchDirectory(directory);
            resolver.AddSearchDirectory(managed);
            resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location));
        }
        static string Text(BinaryReader r)
        {
            int n = r.ReadInt32();
            if (n < 0 || n > 1048576) {
                throw new InvalidDataException();
            }
            var b = r.ReadBytes(n);
            if (b.Length != n) {
                throw new EndOfStreamException();
            }
            return Encoding.UTF8.GetString(b);
        }
        void Index(Node n)
        {
            if (!values.ContainsKey(n.Type)) {
                values[n.Type] = n;
            }
            foreach (var ch in n.Children) {
                Index(ch);
            }
        }
        static bool Attribute(ICustomAttributeProvider p, string name)
        {
            return p.HasCustomAttributes && p.CustomAttributes.Any(a => a.AttributeType.FullName == name);
        }
        static bool IsObject(TypeReference t)
        {
            for (var d = t.Resolve(); d != null;
                d = d.BaseType ==
                null?null:d.BaseType.Resolve()) {
                if (d.FullName == "UnityEngine.Object") {
                    return true;
                }
            }
            return false;
        }
        static TypeReference Resolve(TypeReference t, Dictionary<string, TypeReference> generic)
        {
            var gp = t as GenericParameter;
            TypeReference mapped;
            if (gp != null && generic.TryGetValue(gp.Name, out mapped)) {
                return mapped;
            }
            var arr = t as ArrayType;
            if (arr != null) {
                return new ArrayType(Resolve(arr.ElementType, generic), arr.Rank);
            }
            var gi = t as GenericInstanceType;
            if (gi != null) {
                var g = new GenericInstanceType(gi.ElementType);
                foreach (var arg in gi.GenericArguments) {
                    g.GenericArguments.Add(Resolve(arg, generic));
                }
                return g;
            }
            return t;
        }
        Node Value(TypeReference t, string name, bool element, int depth)
        {
            if (depth > 32) {
                throw new InvalidDataException("Serialization nesting too deep: " + t.FullName);
            }
            string prim = null;
            bool align = false;
            switch (t.MetadataType) {
            case MetadataType.Boolean: prim = "bool";
                align = true;
                break;
            case MetadataType.Char: prim = "char";
                align = true;
                break;
            case MetadataType.SByte: prim = "SInt8";
                align = true;
                break;
            case MetadataType.Byte: prim = "UInt8";
                align = true;
                break;
            case MetadataType.Int16: prim = "short";
                align = true;
                break;
            case MetadataType.UInt16: prim = "UInt16";
                align = true;
                break;
            case MetadataType.Int32: prim = "int";
                break;
            case MetadataType.UInt32: prim = "UInt32";
                break;
            case MetadataType.Int64: prim = "SInt64";
                break;
            case MetadataType.UInt64: prim = "UInt64";
                break;
            case MetadataType.Single: prim = "float";
                break;
            case MetadataType.Double: prim = "double";
                break;
            case MetadataType.String: return values["string"].Copy(name);
            }
            if (prim != null) {
                return new Node(prim, name, align && !element?0x4000:0);
            }
            var ar = t as ArrayType;
            var gi = t as GenericInstanceType;
            if (ar != null ||
                (gi != null && gi.ElementType.FullName == "System.Collections.Generic.List`1")) {
                if (ar != null && ar.Rank != 1) {
                    return null;
                }
                var et = ar != null?ar.ElementType:gi.GenericArguments[0];
                var data = Value(et, "data", true, depth + 1);
                if (data == null) {
                    return null;
                }
                var v = new Node("vector", name);
                var a = new Node("Array", "Array", 0x4000);
                a.Children.Add(new Node("int", "size"));
                a.Children.Add(data);
                v.Children.Add(a);
                return v;
            }
            var d = t.Resolve();
            if (d == null) {
                throw new InvalidDataException("Cannot resolve " + t.FullName);
            }
            if (d.IsEnum) {
                return new Node("int", name);
            }
            if (IsObject(t)) {
                var ptr = values["PPtr<GameObject>"].Copy(name);
                ptr.Type = "PPtr<" + d.Name + ">";
                return ptr;
            }
            if (d.Namespace == "UnityEngine") {
                if (d.Name == "Color") {
                    var color = new Node("ColorRGBA", name);
                    foreach (var c in new[] {"r", "g", "b", "a"}) {
                        color.Children.Add(new Node("float", c));
                    }
                    return color;
                }
                if (d.Name == "Matrix4x4") {
                    var matrix = new Node("Matrix4x4f", name);
                    for (int col = 0; col < 4; col++) {
                        for (int row = 0; row < 4; row++) {
                            matrix.Children.Add(new Node(
                                "float", "m"
                                +
                                row
                                +
                                col));
                        }
                    }
                    return matrix;
                }
                if (d.Name == "RectOffset") {
                    var rect = new Node("RectOffset", name);
                    foreach (var f in new[] {"m_Left", "m_Right", "m_Top", "m_Bottom"}) {
                        rect.Children.Add(new Node(
                            "int", f));
                    }
                    return rect;
                }
                if (d.Name == "Gradient") {
                    var gradient = new Node("Gradient", name);
                    for (int i = 0; i < 8; i++) {
                        var c = new Node("ColorRGBA", "key" + i);
                        foreach (var f in new[] {"r", "g", "b", "a"}) {
                            c.Children.Add(new Node("float", f));
                        }
                        gradient.Children.Add(c);
                    }
                    for (int i = 0; i < 8; i++) {
                        gradient.Children.Add(new Node("UInt16", "ctime" + i));
                    }
                    for (int i = 0; i < 8; i++) {
                        gradient.Children.Add(new Node("UInt16", "atime" + i));
                    }
                    gradient.Children.Add(new Node("int", "m_Mode"));
                    gradient.Children.Add(new Node("UInt8", "m_NumColorKeys"));
                    gradient.Children.Add(new Node("UInt8", "m_NumAlphaKeys", 0x4000));
                    return gradient;
                }
                if (d.Name == "AnimationCurve") {
                    var curve = new Node("AnimationCurve", name);
                    var vector = new Node("vector", "m_Curve");
                    var array = new Node("Array", "Array", 0x4000);
                    array.Children.Add(new Node("int", "size"));
                    var keyframe = new Node("Keyframe", "data");
                    foreach (var f in new[] {"time", "value", "inSlope", "outSlope"}) {
                        keyframe.Children.Add(new Node(
                            "float", f));
                    }
                    array.Children.Add(keyframe);
                    vector.Children.Add(array);
                    curve.Children.Add(vector);
                    foreach (var f in new[] {"m_PreInfinity", "m_PostInfinity",
                                             "m_RotationOrder"}) {
                        curve.Children.Add(new Node("int", f));
                    }
                    return curve;
                }
                string key = d.Name;
                switch (key) {
                case "Vector2": key = "Vector2f";
                    break;
                case "Vector3": key = "Vector3f";
                    break;
                case "Vector4": key = "Vector4f";
                    break;
                case "Quaternion": key = "Quaternionf";
                    break;
                case "Matrix4x4": key = "Matrix4x4f";
                    break;
                case "Rect": key = "Rectf";
                    break;
                case "Bounds": key = "AABB";
                    break;
                case "Color": case "Color32": key = "ColorRGBA";
                    break;
                case "LayerMask": key = "BitField";
                    break;
                }
                Node native;
                if (values.TryGetValue(key, out native)) {
                    if (d.Name == "Color32") {
                        native = new Node("ColorRGBA", name);
                        native.Children.Add(new Node("UInt32", "rgba"));
                        return native;
                    }
                    return native.Copy(name);
                }
            }
            if (d.Namespace.StartsWith("System") ||
                (!d.IsSerializable && !Attribute(d, "System.SerializableAttribute"))) {
                return null;
            }
            var node = new Node(d.Name, name, d.IsValueType && !element?0x4000:0);
            Fields(t, node, depth + 1);
            return node;
        }
        void Fields(TypeReference t, Node target, int depth)
        {
            var d = t.Resolve();
            if (d == null) {
                throw new InvalidDataException("Cannot resolve " + t.FullName);
            }
            var generic = new Dictionary<string, TypeReference>();
            var gi = t as GenericInstanceType;
            if (gi !=
                null) {
                for (int i = 0; i < d.GenericParameters.Count;
                    i++) {
                    generic[d.GenericParameters[i].Name] = gi.GenericArguments[i];
                }
            }
            if (d.BaseType != null && d.BaseType.FullName != "System.Object" &&
                d.BaseType.FullName != "System.ValueType" &&
                d.BaseType.FullName != "UnityEngine.MonoBehaviour" &&
                d.BaseType.FullName != "UnityEngine.ScriptableObject" &&
                d.BaseType.FullName != "UnityEngine.Object") {
                Fields(Resolve(d.BaseType, generic), target, depth + 1);
            }
            foreach (var f in d.Fields) {
                if (f.IsStatic || f.IsLiteral || f.IsInitOnly || f.IsNotSerialized ||
                    (!f.IsPublic && !Attribute(f, "UnityEngine.SerializeField"))) {
                    continue;
                }
                var ft = Resolve(f.FieldType, generic);
                if (ft.FullName == d.FullName && !IsObject(ft)) {
                    continue;
                }
                var n = Value(ft, f.Name, false, depth + 1);
                if (n != null) {
                    target.Children.RemoveAll(c => c.Name == n.Name);
                    target.Children.Add(n);
                }
            }
        }
        public Node Script(string assembly, string type)
        {
            string key = assembly + ":" + type;
            Node n;
            if (scripts.TryGetValue(key, out n)) {
                return n;
            }
            AssemblyDefinition a;
            if (!assemblies.TryGetValue(Path.GetFileNameWithoutExtension(assembly), out a)) {
                string path = Path.Combine(managedDirectory, Path.GetFileName(assembly));
                if (!File.Exists(path)) throw new InvalidDataException("Missing original assembly " + assembly);
                a = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { AssemblyResolver = resolver });
                resolver.Remember(a);
                assemblies.Add(a.Name.Name, a);
            }
            var d = a.MainModule.GetType(type.Replace('+', '/'));
            if (d == null) {
                throw new InvalidDataException("Missing original type " + key);
            }
            n = Classes[114].Copy("Base");
            Fields(d, n, 0);
            scripts.Add(key, n);
            return n;
        }
        public void Dispose()
        {
            resolver.Dispose();
        }
    }
}
