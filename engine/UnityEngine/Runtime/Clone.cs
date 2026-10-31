// Instantiate: Unity-style deep copy of a GameObject hierarchy.
//  - native components: memberwise copy of engine state
//  - MonoBehaviours: new instance (constructor runs) + copy of *serialized* fields only
//    (public / [SerializeField], non-static, non-readonly, serializable types), deep-copying
//    serializable classes/arrays/lists like Unity does
//  - any UnityEngine.Object reference into the copied hierarchy is remapped to the clone
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace KNL
{
    public static partial class World
    {
        public static UnityEngine.Object Instantiate(UnityEngine.Object original, Transform parent, bool worldStays, Pose? pose)
        {
            if (original == null) throw new ArgumentException("The Object you want to instantiate is null.");
            var srcGo = original as GameObject ?? (original is Component ? ((Component)original).gameObject : null);
            if (srcGo == null)
            {
                // plain asset (ScriptableObject, Material, ...): shallow copy + serialized fields
                var copy = CloneLoose(original);
                copy.name = original.name + "(Clone)";
                return copy;
            }

            var map = new Dictionary<UnityEngine.Object, UnityEngine.Object>(ReferenceEqualityComparer.Instance);
            var gos = new List<GameObject>();
            Collect(srcGo, gos);
            var scene = ActiveScene;
            foreach (var g in gos)
            {
                var ng = new GameObject(scene, g.name) { };
                ng.m_IsActive = g.m_IsActive; ng.m_Layer = g.m_Layer; ng.m_Tag = g.m_Tag;
                ng.EnsureId();
                map[g] = ng;
            }
            ((GameObject)map[srcGo]).name = srcGo.name + "(Clone)";
            // components: create first, copy data after the map is complete
            var pairs = new List<KeyValuePair<Component, Component>>();
            foreach (var g in gos)
            {
                var ng = (GameObject)map[g];
                foreach (var c in g.m_Components)
                {
                    Component nc;
                    if (c is MonoBehaviour) nc = (Component)CreateScriptInstance(c.GetType());
                    else nc = (Component)MemberwiseCloneOf(c);
                    nc.m_Destroyed = false; nc.m_InstanceID = 0; nc.EnsureId();
                    nc.m_GameObject = ng; nc.name = ng.name;
                    ng.m_Components.Add(nc);
                    var tr = nc as Transform;
                    if (tr != null) { ng.m_Transform = tr; tr.m_Children = new List<Transform>(); tr.m_Parent = null; }
                    map[c] = nc;
                    pairs.Add(new KeyValuePair<Component, Component>(c, nc));
                }
            }
            // hierarchy
            foreach (var g in gos)
            {
                if (g == srcGo) continue;
                var nt = ((GameObject)map[g]).m_Transform;
                var np = (Transform)map[g.m_Transform.m_Parent];
                nt.m_Parent = np; np.m_Children.Add(nt);
            }
            // data
            foreach (var kv in pairs)
            {
                if (kv.Key is MonoBehaviour) CopySerialized(kv.Key, kv.Value, map);
                else if (!(kv.Key is Transform)) RemapNative(kv.Value, map);
                var b = kv.Key as Behaviour;
                if (b != null) ((Behaviour)kv.Value).m_Enabled = b.m_Enabled;
            }
            var root = ((GameObject)map[srcGo]).m_Transform;
            if (pose.HasValue) { root.m_LocalPosition = pose.Value.position; root.m_LocalRotation = pose.Value.rotation; }
            else if (!worldStays && parent == null)
            {   // keep the original's world placement
                var st = srcGo.m_Transform;
                root.m_LocalPosition = st.position; root.m_LocalRotation = st.rotation; root.m_LocalScale = st.lossyScale;
            }
            if (parent != null)
            {
                if (pose.HasValue || worldStays)
                {
                    var wp = root.m_LocalPosition; var wr = root.m_LocalRotation;
                    root.m_Parent = parent; parent.m_Children.Add(root);
                    root.position = wp; root.rotation = wr;
                }
                else { root.m_Parent = parent; parent.m_Children.Add(root); }
            }
            foreach (var g in gos) SceneObjects.Add((GameObject)map[g]);
            Count(Stats, "instantiate");
            if (SceneLoader.Trace) Console.Error.WriteLine("[inst] " + srcGo.name + " (" + gos.Count + " objs) active=" + ((GameObject)map[srcGo]).activeInHierarchy + " parent=" + (parent != null ? parent.name : "-"));
            // Unity runs Awake/OnEnable inside Instantiate when the result is active
            Lifecycle.Activate(gos.Select(g => (GameObject)map[g]).ToList());
            return map[original];
        }

        static void Collect(GameObject g, List<GameObject> l)
        {
            l.Add(g);
            foreach (var t in g.m_Transform.m_Children) Collect(t.m_GameObject, l);
        }

        static readonly MethodInfo MemberwiseCloneMI = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
        static object MemberwiseCloneOf(object o) { return MemberwiseCloneMI.Invoke(o, null); }

        static UnityEngine.Object CloneLoose(UnityEngine.Object o)
        {
            UnityEngine.Object copy;
            if (o is ScriptableObject)
            {
                copy = (UnityEngine.Object)CreateScriptInstance(o.GetType());
                CopySerialized(o, copy, new Dictionary<UnityEngine.Object, UnityEngine.Object>(ReferenceEqualityComparer.Instance));
            }
            else copy = (UnityEngine.Object)MemberwiseCloneOf(o);
            copy.m_Destroyed = false; copy.m_InstanceID = 0; copy.EnsureId();
            var m = copy as Material;
            if (m != null) { var src = (Material)o; var nm = new Material(src); nm.EnsureId(); return nm; }
            return copy;
        }

        // ---------------------------------------------------------- serialized copy
        const BindingFlags Inst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        static readonly Dictionary<Type, FieldInfo[]> serFields = new Dictionary<Type, FieldInfo[]>();

        public static FieldInfo[] SerializedFields(Type t)
        {
            FieldInfo[] r;
            if (serFields.TryGetValue(t, out r)) return r;
            var l = new List<FieldInfo>();
            for (var tt = t; tt != null && tt != typeof(MonoBehaviour) && tt != typeof(ScriptableObject) && tt != typeof(object) && tt != typeof(UnityEngine.Object); tt = tt.BaseType)
                foreach (var f in tt.GetFields(Inst))
                {
                    if (f.IsStatic || f.IsInitOnly || f.IsLiteral || f.IsNotSerialized) continue;
                    if (!f.IsPublic && !f.IsDefined(typeof(SerializeField), true)) continue;
                    if (!IsSerializable(f.FieldType, 0)) continue;
                    l.Add(f);
                }
            r = l.ToArray();
            serFields[t] = r;
            return r;
        }

        static bool IsSerializable(Type t, int depth)
        {
            if (depth > 6) return false;
            if (t.IsPrimitive || t.IsEnum || t == typeof(string)) return true;
            if (typeof(UnityEngine.Object).IsAssignableFrom(t)) return true;
            if (t.IsArray) return t.GetArrayRank() == 1 && !t.GetElementType().IsArray && IsSerializable(t.GetElementType(), depth + 1);
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)) return IsSerializable(t.GetGenericArguments()[0], depth + 1);
            if (t.Assembly == typeof(GameObject).Assembly)   // Unity built-in serializable types
                return t.IsValueType || t == typeof(AnimationCurve) || t == typeof(Gradient) || t == typeof(RectOffset)
                       || typeof(UnityEngine.Events.UnityEventBase).IsAssignableFrom(t);
            if (t.IsGenericType || t.IsAbstract || t.IsInterface) return false;
            return t.IsSerializable;
        }

        static void CopySerialized(object src, object dst, Dictionary<UnityEngine.Object, UnityEngine.Object> map)
        {
            foreach (var f in SerializedFields(src.GetType()))
            {
                try { f.SetValue(dst, DeepCopy(f.GetValue(src), f.FieldType, map, 0)); }
                catch (Exception e) { Log.Write("WARN", "clone " + src.GetType().Name + "." + f.Name + ": " + e.Message); }
            }
            var so = src as UnityEngine.Object;
            if (so != null && src is ScriptableObject) ((UnityEngine.Object)dst).name = so.name;
        }

        static object DeepCopy(object v, Type t, Dictionary<UnityEngine.Object, UnityEngine.Object> map, int depth)
        {
            if (v == null) return null;
            var uo = v as UnityEngine.Object;
            if (uo != null) { UnityEngine.Object m; return map.TryGetValue(uo, out m) ? m : uo; }
            var vt = v.GetType();
            if (vt.IsPrimitive || vt.IsEnum || v is string) return v;
            if (depth > 8) return v;
            var arr = v as Array;
            if (arr != null)
            {
                var et = vt.GetElementType();
                var na = Array.CreateInstance(et, arr.Length);
                for (int i = 0; i < arr.Length; i++) na.SetValue(DeepCopy(arr.GetValue(i), et, map, depth + 1), i);
                return na;
            }
            if (vt.IsGenericType && vt.GetGenericTypeDefinition() == typeof(List<>))
            {
                var et = vt.GetGenericArguments()[0];
                var nl = (IList)Activator.CreateInstance(vt);
                foreach (var x in (IList)v) nl.Add(DeepCopy(x, et, map, depth + 1));
                return nl;
            }
            if (vt == typeof(AnimationCurve)) { return new AnimationCurve(((AnimationCurve)v).keys) { preWrapMode = ((AnimationCurve)v).preWrapMode, postWrapMode = ((AnimationCurve)v).postWrapMode }; }
            if (vt == typeof(Gradient)) { var g = new Gradient(); g.SetKeys(((Gradient)v).colorKeys, ((Gradient)v).alphaKeys); g.m_Mode = ((Gradient)v).m_Mode; return g; }
            if (vt.IsValueType && vt.Assembly == typeof(GameObject).Assembly) return v;   // Unity structs: value copy
            if (vt.Assembly == typeof(GameObject).Assembly) return v;                    // UnityEvent etc: share
            // [Serializable] game class / struct: copy its serialized fields
            object n;
            try { n = vt.IsValueType ? Activator.CreateInstance(vt) : Activator.CreateInstance(vt, true); }
            catch { n = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(vt); }
            for (var tt = vt; tt != null && tt != typeof(object); tt = tt.BaseType)
                foreach (var f in tt.GetFields(Inst))
                {
                    if (f.IsStatic || f.IsLiteral || f.IsNotSerialized) continue;
                    if (!f.IsPublic && !f.IsDefined(typeof(SerializeField), true)) continue;
                    f.SetValue(n, DeepCopy(f.GetValue(v), f.FieldType, map, depth + 1));
                }
            return n;
        }

        /// After a memberwise copy of a native component, point references into the cloned
        /// hierarchy at the clones (e.g. a joint's connected body).
        static void RemapNative(Component c, Dictionary<UnityEngine.Object, UnityEngine.Object> map)
        {
            for (var tt = c.GetType(); tt != null && tt != typeof(Component); tt = tt.BaseType)
                foreach (var f in tt.GetFields(Inst))
                {
                    if (f.IsStatic || f.IsLiteral) continue;
                    if (typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType))
                    {
                        var v = f.GetValue(c) as UnityEngine.Object;
                        UnityEngine.Object m;
                        if (v != null && map.TryGetValue(v, out m)) f.SetValue(c, m);
                    }
                    else if (f.FieldType.IsArray && f.GetValue(c) != null && !f.FieldType.GetElementType().IsValueType)
                        f.SetValue(c, ((Array)f.GetValue(c)).Clone());   // never share mutable arrays
                    else if (f.FieldType.IsGenericType && f.GetValue(c) is IList && !f.IsInitOnly)
                        f.SetValue(c, Activator.CreateInstance(f.FieldType, f.GetValue(c)));
                }
            NativeCloneHooks.After(c);
        }
    }

    public static class NativeCloneHooks
    {
        public static event Action<Component> AfterClone;
        public static void After(Component c) { if (AfterClone != null) AfterClone(c); }
    }

    public sealed class ReferenceEqualityComparer : IEqualityComparer<UnityEngine.Object>
    {
        public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
        public bool Equals(UnityEngine.Object a, UnityEngine.Object b) { return ReferenceEquals(a, b); }
        public int GetHashCode(UnityEngine.Object o) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o); }
    }
}
