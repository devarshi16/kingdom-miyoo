// KNL engine runtime support: logging, paths, reflection helpers, coroutines, invokes, JSON.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace KNL
{
    public static class Log
    {
        public static bool Quiet;
        public static int Errors, Warnings;
        public static readonly Dictionary<string, int> NotImplementedHits = new Dictionary<string, int>();
        public static void Write(string level, object msg)
        {
            if (level == "ERROR" || level == "EXC") Errors++;
            if (level == "WARN") Warnings++;
            if (!Quiet) Console.Error.WriteLine("[" + level + "] " + msg);
        }
        public static void Info(string msg) { Console.Error.WriteLine("[knl] " + msg); }
        // generated stubs call this before throwing, so failures the game swallows in its own try/catch still show up
        public sealed class StubException : NotImplementedException { public StubException(string m) : base(m) { } }
        public static Exception Missing(string what)
        {
            int n; NotImplementedHits.TryGetValue(what, out n); NotImplementedHits[what] = n + 1;
            return new StubException(what);
        }
        public static void NotImplemented(Exception e)
        {
            if (e is StubException) return;     // already counted at the throw site
            var k = e.Message;
            int n; NotImplementedHits.TryGetValue(k, out n); NotImplementedHits[k] = n + 1;
        }
    }

    public static class Paths
    {
        public static string DataDir = ".";       // extracted/ from tools/convert.py
        public static string SaveDir = "./save";
    }

    /// Scripted button input for headless runs: "--input 30-90:right,100:a" (frame ranges).
    public static class InputScript
    {
        static readonly List<KeyValuePair<int[], uint>> steps = new List<KeyValuePair<int[], uint>>();
        public static void Parse(string spec)
        {
            foreach (var part in spec.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split(':'); var r = kv[0].Split('-');
                int a = int.Parse(r[0]), b = r.Length > 1 ? int.Parse(r[1]) : a;
                uint m = 0;
                foreach (var k in kv[1].Split('+'))
                    switch (k.ToLowerInvariant())
                    {
                        case "left": m |= 1; break; case "right": m |= 2; break; case "up": m |= 4; break; case "down": m |= 8; break;
                        case "a": m |= 16; break; case "b": m |= 32; break; case "x": m |= 64; break; case "y": m |= 128; break;
                        case "start": m |= 256; break; case "select": m |= 512; break; case "menu": m |= 4096; break;
                    }
                steps.Add(new KeyValuePair<int[], uint>(new[] { a, b }, m));
            }
        }
        public static uint Next()
        {
            uint m = 0; int f = Time.frameCount;
            foreach (var s in steps) if (f >= s.Key[0] && f <= s.Key[1]) m |= s.Value;
            return m;
        }
    }

    public static class Engine
    {
        public static bool QuitRequested;
        public static void RequestQuit() { QuitRequested = true; }
    }

    public struct Pose
    {
        public Vector3 position; public Quaternion rotation;
        public Pose(Vector3 p, Quaternion r) { position = p; rotation = r; }
    }

    /// Identifies which loaded scene a GameObject lives in (null = prefab/asset).
    public sealed class SceneTag
    {
        public string name, displayName; public int buildIndex = -1; public bool persistent;
        public SceneTag(string n) { name = n; displayName = n; }
    }

    public static class EngineSettings
    {
        public static string[] Layers = new string[32];
        public static string[] Tags = new string[0];
        public static int LayerIndex(string n) { for (int i = 0; i < 32; i++) if (Layers[i] == n) return i; return -1; }
        public static string LayerName(int i) { return i >= 0 && i < 32 ? (Layers[i] ?? "") : ""; }
    }

    public static class Reflect
    {
        const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly Dictionary<Type, Dictionary<string, MethodInfo>> cache = new Dictionary<Type, Dictionary<string, MethodInfo>>();

        public static MethodInfo FindMethod(Type t, string name)
        {
            Dictionary<string, MethodInfo> byName;
            if (!cache.TryGetValue(t, out byName)) cache[t] = byName = new Dictionary<string, MethodInfo>();
            MethodInfo m;
            if (byName.TryGetValue(name, out m)) return m;
            for (var tt = t; tt != null && m == null; tt = tt.BaseType)
                foreach (var mi in tt.GetMethods(All | BindingFlags.DeclaredOnly))
                    if (mi.Name == name && mi.GetParameters().Length <= 1 && !mi.IsGenericMethodDefinition) { m = mi; break; }
            byName[name] = m;
            return m;
        }

        /// Invoke a Unity "message" (Awake, Update, OnTriggerEnter2D, ...). Returns true if found.
        public static bool InvokeMessage(object target, string name, object arg)
        {
            var m = FindMethod(target.GetType(), name);
            if (m == null) return false;
            try
            {
                var r = Call(m, target, arg);
                if (r is IEnumerator && target is MonoBehaviour) Coroutines.Start((MonoBehaviour)target, (IEnumerator)r);
            }
            catch (Exception e) { Report(target, name, e); }
            return true;
        }

        // M5: messages are called through cached open-instance delegates instead of MethodInfo.Invoke
        // (which allocates an argument array per call and is slow under the ARM JIT). Methods with a
        // value-type parameter or return keep using Invoke. Exceptions from the method arrive unwrapped.
        static readonly Dictionary<MethodInfo, Func<object, object, object>> invokers = new Dictionary<MethodInfo, Func<object, object, object>>();
        /// m.Invoke(target, m has a parameter ? [arg] : null), through a cached delegate.
        public static object Call(MethodInfo m, object target, object arg)
        {
            Func<object, object, object> f;
            if (!invokers.TryGetValue(m, out f)) invokers[m] = f = MakeInvoker(m);
            return f(target, arg);
        }
        static Func<object, object, object> MakeInvoker(MethodInfo m)
        {
            var ps = m.GetParameters(); var dt = m.DeclaringType; var rt = m.ReturnType;
            bool ret = rt != typeof(void);
            if (m.IsStatic || dt.IsValueType || (ps.Length == 1 && ps[0].ParameterType.IsValueType) || (ret && rt.IsValueType) || dt.ContainsGenericParameters)
                return (o, a) =>
                {
                    try { return m.Invoke(o, ps.Length == 0 ? null : new[] { a }); }
                    catch (TargetInvocationException e) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException ?? e).Throw(); return null; }
                };
            var targs = new List<Type> { dt };
            if (ps.Length == 1) targs.Add(ps[0].ParameterType);
            if (ret) targs.Add(rt);
            var g = typeof(Reflect).GetMethod((ps.Length == 0 ? "W0" : "W1") + (ret ? "R" : "V"), BindingFlags.NonPublic | BindingFlags.Static);
            return (Func<object, object, object>)g.MakeGenericMethod(targs.ToArray()).Invoke(null, new object[] { m });
        }
        static Func<object, object, object> W0V<T>(MethodInfo m) { var d = (Action<T>)Delegate.CreateDelegate(typeof(Action<T>), m); return (o, a) => { d((T)o); return null; }; }
        static Func<object, object, object> W1V<T, A>(MethodInfo m) { var d = (Action<T, A>)Delegate.CreateDelegate(typeof(Action<T, A>), m); return (o, a) => { d((T)o, (A)a); return null; }; }
        static Func<object, object, object> W0R<T, R>(MethodInfo m) { var d = (Func<T, R>)Delegate.CreateDelegate(typeof(Func<T, R>), m); return (o, a) => d((T)o); }
        static Func<object, object, object> W1R<T, A, R>(MethodInfo m) { var d = (Func<T, A, R>)Delegate.CreateDelegate(typeof(Func<T, A, R>), m); return (o, a) => d((T)o, (A)a); }

        public static void Report(object target, string where, Exception e)
        {
            if (e is NotImplementedException) Log.NotImplemented(e);
            else if (e is MissingMethodException || e is MissingFieldException || e is TypeLoadException) Log.NotImplemented(new NotImplementedException("missing: " + e.Message));
            Log.Write("EXC", target.GetType().Name + "." + where + ": " + e.GetType().Name + ": " + e.Message);
        }
    }

    // ---------------------------------------------------------------- coroutines
    public sealed class CoroutineState
    {
        public MonoBehaviour owner; public IEnumerator routine; public string methodName;
        public float waitUntil = -1f; public CoroutineState waitFor; public bool done;
        public AsyncOperation waitOp; public CustomYieldInstruction custom;
        public int phase, resumeFrame, resumeFixedStep;
        public Coroutine handle;
    }

    public static class Coroutines
    {
        static readonly List<CoroutineState> active = new List<CoroutineState>();
        static int dispatchPhase; // 0: Update, 1: after physics, 2: after rendering

        public static Coroutine Start(MonoBehaviour owner, IEnumerator routine, string methodName = null)
        {
            if (routine == null || owner == null || !owner.gameObject.activeInHierarchy) return null;
            var st = new CoroutineState { owner = owner, routine = routine, methodName = methodName };
            st.handle = new Coroutine(); st.handle.state = st;
            // Register before MoveNext: SetActive(false) inside the iterator must cancel it too.
            active.Add(st);
            Step(st);
            return st.handle;
        }

        static void Finish(CoroutineState st)
        {
            if (st.done) return;
            st.done = true;
            var disposable = st.routine as IDisposable;
            if (disposable != null)
                try { disposable.Dispose(); }
                catch (Exception e) { Lifecycle.Fail(st.owner, "coroutine dispose", e); }
        }

        static void Step(CoroutineState st)
        {
            try
            {
                if (!st.routine.MoveNext()) { Finish(st); return; }
                if (st.done) return;
                if (st.owner == null || !st.owner.gameObject.activeInHierarchy) { Finish(st); return; }
                var y = st.routine.Current;
                st.waitUntil = -1f; st.waitFor = null; st.waitOp = null; st.custom = null;
                st.phase = 0; st.resumeFrame = Time.frameCount + 1;
                st.waitOp = y as AsyncOperation;
                st.custom = y as CustomYieldInstruction;
                if (y is WaitForFixedUpdate)
                {
                    st.phase = 1; st.resumeFixedStep = Time.s_FixedStepCount + 1;
                }
                else if (y is WaitForEndOfFrame)
                {
                    st.phase = 2; st.resumeFrame = Time.frameCount + (dispatchPhase == 2 ? 1 : 0);
                }
                var ws = y as WaitForSeconds;
                if (ws != null) st.waitUntil = Time.time + ws.m_Seconds;
                var co = y as Coroutine;
                if (co != null) st.waitFor = co.state;
                var nested = y as IEnumerator;
                if (nested != null) { var c = Start(st.owner, nested); st.waitFor = c != null ? c.state : null; }
            }
            catch (Exception e)
            {
                Finish(st);
                Lifecycle.Fail(st.owner, "coroutine " + (st.methodName ?? st.routine.GetType().Name), e);
            }
        }

        public static void Tick() { TickPhase(0); }
        public static void TickFixed() { TickPhase(1); }
        public static void TickEndOfFrame() { TickPhase(2); }

        static void TickPhase(int phase)
        {
            int previousPhase = dispatchPhase; dispatchPhase = phase;
            try
            {
                int count = active.Count;
                for (int i = 0; i < count; i++)
                {
                    var st = active[i];
                    if (st.done) continue;
                    if (st.owner == null || !st.owner.gameObject.activeInHierarchy) { Finish(st); continue; }
                    if (st.phase != phase) continue;
                    if (phase == 1 ? Time.s_FixedStepCount < st.resumeFixedStep : Time.frameCount < st.resumeFrame) continue;
                    if (st.waitUntil >= 0f && Time.time < st.waitUntil) continue;
                    if (st.waitFor != null && !st.waitFor.done) continue;
                    if (st.waitOp != null && !st.waitOp.isDone) continue;
                    if (st.custom != null && st.custom.keepWaiting) continue;
                    long tC = Prof.Level > 1 ? Prof.Now() : 0;
                    Step(st);
                    if (Prof.Level > 1) Prof.Add("    coroutine " + st.owner.GetType().Name + "." + (st.methodName ?? st.routine.GetType().Name), tC);
                }
            }
            finally { dispatchPhase = previousPhase; }
            // In-place compaction includes iterators started during this dispatch.
            active.RemoveAll(s => s.done);
        }

        public static void Stop(MonoBehaviour o, IEnumerator r) { foreach (var s in active.ToArray()) if (s.owner == o && s.routine == r) Finish(s); }
        public static void Stop(MonoBehaviour o, Coroutine c) { if (c != null && c.state != null && c.state.owner == o) Finish(c.state); }
        public static void Stop(MonoBehaviour o, string m) { foreach (var s in active.ToArray()) if (s.owner == o && s.methodName == m) Finish(s); }
        public static void StopAll(MonoBehaviour o) { foreach (var s in active.ToArray()) if (ReferenceEquals(s.owner, o)) Finish(s); }
    }

    public static class Invokes
    {
        sealed class Entry { public MonoBehaviour o; public string m; public float at, repeat; public bool dead; }
        static readonly List<Entry> list = new List<Entry>();
        public static void Add(MonoBehaviour o, string m, float t, float r) { list.Add(new Entry { o = o, m = m, at = Time.time + t, repeat = r }); }
        public static void Cancel(MonoBehaviour o, string m) { foreach (var e in list) if (e.o == o && (m == null || e.m == m)) e.dead = true; }
        public static bool IsInvoking(MonoBehaviour o, string m) { foreach (var e in list) if (!e.dead && e.o == o && e.m == m) return true; return false; }
        public static void Tick()
        {
            int count = list.Count;
            for (int i = 0; i < count; i++)
            {
                var e = list[i];
                if (e.dead || Time.time < e.at) continue;
                if (e.o == null) { e.dead = true; continue; }
                long tI = Prof.Level > 1 ? Prof.Now() : 0;
                Reflect.InvokeMessage(e.o, e.m, null);
                if (Prof.Level > 1) Prof.Add("    invoke " + e.o.GetType().Name + "." + e.m, tI);
                if (e.repeat > 0f) e.at += e.repeat; else e.dead = true;
            }
            int write = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (!e.dead)
                {
                    if (write != i) list[write] = e;
                    write++;
                }
            }
            if (write < list.Count) list.RemoveRange(write, list.Count - write);
        }
    }

    // ---------------------------------------------------------------------- JSON
    /// Minimal JSON reader for the converter's output: objects -> Dictionary<string,object>,
    /// arrays -> List<object>, numbers -> double, plus string/bool/null.
    public static class Json
    {
        public static object Parse(string s) { int i = 0; var v = Value(s, ref i); return v; }
        public static object ParseFile(string path) { return Parse(File.ReadAllText(path, Encoding.UTF8)); }

        static void Ws(string s, ref int i) { while (i < s.Length && s[i] <= ' ') i++; }

        static object Value(string s, ref int i)
        {
            Ws(s, ref i);
            char c = s[i];
            if (c == '{')
            {
                var d = new Dictionary<string, object>(); i++; Ws(s, ref i);
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    Ws(s, ref i); string k = Str(s, ref i); Ws(s, ref i); i++; // ':'
                    d[k] = Value(s, ref i); Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    i++; return d;
                }
            }
            if (c == '[')
            {
                var l = new List<object>(); i++; Ws(s, ref i);
                if (s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Value(s, ref i)); Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    i++; return l;
                }
            }
            if (c == '"') return Str(s, ref i);
            if (c == 't') { i += 4; return true; }
            if (c == 'f') { i += 5; return false; }
            if (c == 'n') { i += 4; return null; }
            int st = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return double.Parse(s.Substring(st, i - st), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        static string Str(string s, ref int i)
        {
            i++; var sb = new StringBuilder();
            while (true)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u': sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                    default: sb.Append(e); break;
                }
            }
        }

        // convenience accessors
        public static Dictionary<string, object> D(object o, string k) { return (Dictionary<string, object>)((Dictionary<string, object>)o)[k]; }
        public static float F(Dictionary<string, object> d, string k, float def = 0f) { object v; return d.TryGetValue(k, out v) && v is double ? (float)(double)v : def; }
        public static long L(Dictionary<string, object> d, string k) { object v; return d.TryGetValue(k, out v) && v is double ? (long)(double)v : 0; }
        public static bool B(Dictionary<string, object> d, string k)
        {
            object v; if (!d.TryGetValue(k, out v)) return false;
            if (v is bool) return (bool)v;
            return v is double && (double)v != 0;
        }
        public static string S(Dictionary<string, object> d, string k) { object v; return d.TryGetValue(k, out v) ? v as string : null; }
        public static Vector3 V3(object o) { var d = (Dictionary<string, object>)o; return new Vector3(F(d, "x"), F(d, "y"), F(d, "z")); }
        public static Vector2 V2(object o) { var d = (Dictionary<string, object>)o; return new Vector2(F(d, "x"), F(d, "y")); }
        public static Quaternion Q(object o) { var d = (Dictionary<string, object>)o; return new Quaternion(F(d, "x"), F(d, "y"), F(d, "z"), F(d, "w", 1f)); }
        public static Color C(object o) { var d = (Dictionary<string, object>)o; return new Color(F(d, "r"), F(d, "g"), F(d, "b"), F(d, "a", 1f)); }
    }
}
