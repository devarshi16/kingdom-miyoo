// Clean-room JsonUtility: Unity's serialization rules (public or [SerializeField] instance fields of
// serializable types), UnityEngine.Object references as {"instanceID":n}.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace UnityEngine
{
    public partial class JsonUtility
    {
        public static string ToJson(object obj) { return ToJson(obj, false); }
        public static string ToJson(object obj, bool pretty)
        {
            if (obj == null) return "";
            var sb = new StringBuilder(); WriteObject(sb, obj, obj.GetType(), 0); return sb.ToString();
        }
        public static T FromJson<T>(string json) { return (T)FromJson(json, typeof(T)); }
        public static object FromJson(string json, Type t)
        {
            if (string.IsNullOrEmpty(json)) return null;
            object o;
            try { o = t.IsValueType ? Activator.CreateInstance(t) : Activator.CreateInstance(t, true); }
            catch { o = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(t); }
            FromJsonOverwrite(json, o);
            return o;
        }
        public static void FromJsonOverwrite(string json, object target)
        {
            if (string.IsNullOrEmpty(json) || target == null) return;
            var d = KNL.Json.Parse(json) as Dictionary<string, object>;
            if (d != null) Apply(d, target);
        }

        static IEnumerable<System.Reflection.FieldInfo> Fields(Type t)
        {
            if (typeof(Object).IsAssignableFrom(t)) return KNL.World.SerializedFields(t);
            var l = new List<System.Reflection.FieldInfo>();
            for (var tt = t; tt != null && tt != typeof(object) && tt != typeof(ValueType); tt = tt.BaseType)
                foreach (var f in tt.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
                    if (!f.IsNotSerialized && !f.IsInitOnly && (f.IsPublic || f.IsDefined(typeof(SerializeField), true))) l.Add(f);
            return l;
        }

        static void WriteValue(StringBuilder sb, object v, Type t, int depth)
        {
            if (v == null) { sb.Append(typeof(Object).IsAssignableFrom(t) ? "{\"instanceID\":0}" : (t == typeof(string) ? "\"\"" : "null")); return; }
            if (v is string) { WriteString(sb, (string)v); return; }
            if (v is bool) { sb.Append((bool)v ? "true" : "false"); return; }
            if (v is Enum) { sb.Append(global::System.Convert.ToInt64(v).ToString(CultureInfo.InvariantCulture)); return; }
            if (v is float) { sb.Append(((float)v).ToString("R", CultureInfo.InvariantCulture)); return; }
            if (v is double) { sb.Append(((double)v).ToString("R", CultureInfo.InvariantCulture)); return; }
            if (v.GetType().IsPrimitive) { sb.Append(global::System.Convert.ToString(v, CultureInfo.InvariantCulture)); return; }
            var uo = v as Object;
            if (uo != null) { sb.Append("{\"instanceID\":").Append(uo.GetInstanceID()).Append('}'); return; }
            var list = v as IList;
            if (list != null)
            {
                var et = t.IsArray ? t.GetElementType() : (t.IsGenericType ? t.GetGenericArguments()[0] : typeof(object));
                sb.Append('[');
                for (int i = 0; i < list.Count; i++) { if (i > 0) sb.Append(','); WriteValue(sb, list[i], et, depth + 1); }
                sb.Append(']'); return;
            }
            if (depth > 10) { sb.Append("{}"); return; }
            WriteObject(sb, v, v.GetType(), depth + 1);
        }
        static void WriteObject(StringBuilder sb, object o, Type t, int depth)
        {
            if (t == typeof(Vector2)) { var v = (Vector2)o; sb.AppendFormat(CultureInfo.InvariantCulture, "{{\"x\":{0},\"y\":{1}}}", v.x, v.y); return; }
            if (t == typeof(Vector3)) { var v = (Vector3)o; sb.AppendFormat(CultureInfo.InvariantCulture, "{{\"x\":{0},\"y\":{1},\"z\":{2}}}", v.x, v.y, v.z); return; }
            if (t == typeof(Color)) { var c = (Color)o; sb.AppendFormat(CultureInfo.InvariantCulture, "{{\"r\":{0},\"g\":{1},\"b\":{2},\"a\":{3}}}", c.r, c.g, c.b, c.a); return; }
            sb.Append('{'); bool first = true;
            foreach (var f in Fields(t))
            {
                if (!first) sb.Append(','); first = false;
                WriteString(sb, f.Name); sb.Append(':'); WriteValue(sb, f.GetValue(o), f.FieldType, depth);
            }
            sb.Append('}');
        }
        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (var ch in s)
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: if (ch < 32) sb.AppendFormat("\\u{0:x4}", (int)ch); else sb.Append(ch); break;
                }
            sb.Append('"');
        }

        static void Apply(Dictionary<string, object> d, object target)
        {
            foreach (var f in Fields(target.GetType()))
            {
                object v;
                if (!d.TryGetValue(f.Name, out v)) continue;
                try { f.SetValue(target, Convert(v, f.FieldType)); } catch { }
            }
        }
        static object Convert(object v, Type t)
        {
            if (typeof(Object).IsAssignableFrom(t)) return null;   // references are not restored across sessions
            if (v == null) return t.IsValueType ? Activator.CreateInstance(t) : null;
            if (t == typeof(string)) return v as string ?? v.ToString();
            if (t.IsEnum) return Enum.ToObject(t, global::System.Convert.ToInt64(v));
            if (t == typeof(bool)) return v is bool ? (bool)v : global::System.Convert.ToDouble(v) != 0;
            if (t.IsPrimitive) return System.Convert.ChangeType(v, t, CultureInfo.InvariantCulture);
            var l = v as List<object>;
            if (l != null)
            {
                if (t.IsArray)
                {
                    var et = t.GetElementType(); var arr = Array.CreateInstance(et, l.Count);
                    for (int i = 0; i < l.Count; i++) arr.SetValue(Convert(l[i], et), i);
                    return arr;
                }
                if (t.IsGenericType)
                {
                    var et = t.GetGenericArguments()[0]; var list = (IList)Activator.CreateInstance(t);
                    foreach (var x in l) list.Add(Convert(x, et));
                    return list;
                }
                return null;
            }
            var dd = v as Dictionary<string, object>;
            if (dd == null) return null;
            object o;
            try { o = t.IsValueType ? Activator.CreateInstance(t) : Activator.CreateInstance(t, true); }
            catch { o = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(t); }
            if (t == typeof(Vector2) || t == typeof(Vector3) || t == typeof(Color) || t == typeof(Vector4) || t == typeof(Quaternion))
            {
                foreach (var kv in dd)
                {
                    var f = t.GetField(kv.Key);
                    if (f != null) { object boxed = o; f.SetValue(boxed, global::System.Convert.ToSingle(kv.Value, CultureInfo.InvariantCulture)); o = boxed; }
                }
                return o;
            }
            object b = o; Apply(dd, b); return b;
        }
    }
}
