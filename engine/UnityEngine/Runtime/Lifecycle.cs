// KNL lifecycle + frame loop: Unity's message ordering (Awake/OnEnable/Start/FixedUpdate/Update/
// LateUpdate/OnDisable/OnDestroy), script execution order from the game's MonoScript data,
// activation semantics, and Instantiate (deep copy with in-hierarchy reference remapping).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using JObj = System.Collections.Generic.Dictionary<string, object>;

namespace KNL
{
    /// Per-type cache of the Unity messages a script implements.
    public sealed class ScriptInfo
    {
        public int order; public string name;
        public MethodInfo Awake, OnEnable, OnDisable, Start, Update, LateUpdate, FixedUpdate, OnDestroy;
        public MethodInfo OnTriggerEnter2D, OnTriggerStay2D, OnTriggerExit2D;
        public MethodInfo OnCollisionEnter2D, OnCollisionStay2D, OnCollisionExit2D;
        public byte physicsMask; // 1: TriggerEnter, 2: TriggerStay, 4: TriggerExit, 8: CollisionEnter, 16: CollisionStay, 32: CollisionExit
        static readonly Dictionary<Type, ScriptInfo> cache = new Dictionary<Type, ScriptInfo>();
        public static readonly Dictionary<string, int> ExecutionOrder = new Dictionary<string, int>();

        public static ScriptInfo Of(Type t)
        {
            ScriptInfo s;
            if (cache.TryGetValue(t, out s)) return s;
            s = new ScriptInfo { name = t.Name };
            int o;
            for (var tt = t; tt != null && tt != typeof(MonoBehaviour); tt = tt.BaseType)
                if (ExecutionOrder.TryGetValue(tt.FullName, out o)) { s.order = o; break; }
            s.Awake = M(t, "Awake"); s.OnEnable = M(t, "OnEnable"); s.OnDisable = M(t, "OnDisable");
            s.Start = M(t, "Start"); s.Update = M(t, "Update"); s.LateUpdate = M(t, "LateUpdate");
            s.FixedUpdate = M(t, "FixedUpdate"); s.OnDestroy = M(t, "OnDestroy");

            s.OnTriggerEnter2D = M1(t, "OnTriggerEnter2D", typeof(Collider2D));
            s.OnTriggerStay2D = M1(t, "OnTriggerStay2D", typeof(Collider2D));
            s.OnTriggerExit2D = M1(t, "OnTriggerExit2D", typeof(Collider2D));
            s.OnCollisionEnter2D = M1(t, "OnCollisionEnter2D", typeof(Collision2D));
            s.OnCollisionStay2D = M1(t, "OnCollisionStay2D", typeof(Collision2D));
            s.OnCollisionExit2D = M1(t, "OnCollisionExit2D", typeof(Collision2D));
            if (s.OnTriggerEnter2D != null) s.physicsMask |= 1;
            if (s.OnTriggerStay2D != null) s.physicsMask |= 2;
            if (s.OnTriggerExit2D != null) s.physicsMask |= 4;
            if (s.OnCollisionEnter2D != null) s.physicsMask |= 8;
            if (s.OnCollisionStay2D != null) s.physicsMask |= 16;
            if (s.OnCollisionExit2D != null) s.physicsMask |= 32;

            cache[t] = s;
            return s;
        }
        static MethodInfo M(Type t, string n)
        {
            for (var tt = t; tt != null && tt != typeof(MonoBehaviour) && tt != typeof(object); tt = tt.BaseType)
            {
                var m = tt.GetMethod(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                if (m != null) return m;
            }
            return null;
        }
        static MethodInfo M1(Type t, string n, Type paramType)
        {
            for (var tt = t; tt != null && tt != typeof(MonoBehaviour) && tt != typeof(object); tt = tt.BaseType)
            {
                var m = tt.GetMethod(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (m != null)
                {
                    var ps = m.GetParameters();
                    if (ps.Length == 1 && ps[0].ParameterType.IsAssignableFrom(paramType)) return m;
                    if (ps.Length == 0) return m;
                }
            }
            return null;
        }
    }

    /// Engine-side state attached to every live (scene) MonoBehaviour.
    public sealed class BehaviourState
    {
        public MonoBehaviour mb; public ScriptInfo info; public long seq;
        public bool awoken, started, enabledCalled, destroyed;
        public Action update, lateUpdate, fixedUpdate;
    }

    public static class Lifecycle
    {
        static readonly Dictionary<MonoBehaviour, BehaviourState> states = new Dictionary<MonoBehaviour, BehaviourState>();
        static readonly List<BehaviourState> all = new List<BehaviourState>();
        static readonly List<BehaviourState> unstarted = new List<BehaviourState>();   // RunStarts' candidates (subset of all)
        static bool dirtyOrder;
        static long seq;
        public static int Exceptions;
        public static readonly Dictionary<string, int> ExceptionSites = new Dictionary<string, int>();

        public static void LoadExecutionOrder()
        {
            foreach (var fn in new[] { "globalgamemanagers.assets" })
            {
                var f = World.File(fn);
                foreach (var e in f.objects.Values)
                {
                    if ((string)e["type"] != "MonoScript" || !e.ContainsKey("data")) continue;
                    var d = (JObj)e["data"];
                    int o = (int)Json.L(d, "m_ExecutionOrder");
                    if (o == 0) continue;
                    string ns = Json.S(d, "m_Namespace");
                    ScriptInfo.ExecutionOrder[(string.IsNullOrEmpty(ns) ? "" : ns + ".") + Json.S(d, "m_ClassName")] = o;
                }
            }
        }

        static BehaviourState State(MonoBehaviour mb)
        {
            BehaviourState s;
            if (states.TryGetValue(mb, out s)) return s;
            s = new BehaviourState { mb = mb, info = ScriptInfo.Of(mb.GetType()), seq = seq++ };
            states[mb] = s;
            all.Add(s); allVersion++; unstarted.Add(s);
            dirtyOrder = true;
            return s;
        }

        static void Call(BehaviourState s, MethodInfo m, string what)
        {
            if (m == null) return;
            try
            {
                var r = m.Invoke(s.mb, null);
                var e = r as IEnumerator;
                if (e != null) Coroutines.Start(s.mb, e, what);
            }
            catch (TargetInvocationException ex) { Fail(s.mb, what, ex.InnerException ?? ex); }
            catch (Exception ex) { Fail(s.mb, what, ex); }
        }
        public static void Fail(object target, string what, Exception e)
        {
            Exceptions++;
            while ((e is TypeInitializationException || e is TargetInvocationException) && e.InnerException != null) e = e.InnerException;
            string key = target.GetType().Name + "." + what + ": " + e.GetType().Name + ": " + e.Message;
            int n; ExceptionSites.TryGetValue(key, out n); ExceptionSites[key] = n + 1;
            Reflect.Report(target, what, e);
            if (n == 0 && Verbose) Console.Error.WriteLine("[TRACE] " + key + "\n" + e.StackTrace);
        }
        public static bool Verbose;

        // ------------------------------------------------------- activation
        /// Run Awake/OnEnable for every MonoBehaviour under these roots that is active in the
        /// hierarchy, sorted by script execution order (Unity sorts a scene load this way).
        public static void Activate(IEnumerable<GameObject> gos)
        {
            var list = new List<BehaviourState>();
            foreach (var go in gos)
            {
                if (!go.activeInHierarchy) continue;
                foreach (var c in go.m_Components)
                {
                    var au = c as AudioSource;
                    if (au != null && au.m_Enabled && !au.m_Destroyed) pendingAudio.Add(au);
                    var mb = c as MonoBehaviour;
                    if (mb == null || mb.m_Destroyed) continue;
                    var s = State(mb);
                    if (!s.awoken || (mb.m_Enabled && !s.enabledCalled)) list.Add(s);
                }
            }
            foreach (var s in list.OrderBy(x => x.info.order).ThenBy(x => x.seq))
            {
                if (!s.awoken) CoinCounter.BeforeAwake(s.mb);
                if (s.mb.m_Destroyed || !s.mb.gameObject.activeInHierarchy) continue;
                if (!s.awoken) { s.awoken = true; Call(s, s.info.Awake, "Awake"); }
                if (s.mb.m_Destroyed || !s.mb.gameObject.activeInHierarchy) continue;
                if (s.mb.m_Enabled && !s.enabledCalled) { s.enabledCalled = true; Call(s, s.info.OnEnable, "OnEnable"); }
            }
        }

        static IEnumerable<GameObject> Subtree(GameObject go)
        {
            yield return go;
            foreach (var t in go.m_Transform.m_Children.ToArray())
                foreach (var g in Subtree(t.m_GameObject)) yield return g;
            // AudioSource.playOnAwake starts when the source becomes active (Unity: on enable)
            if (pendingAudio.Count > 0) { var pa = pendingAudio.ToArray(); pendingAudio.Clear(); foreach (var au in pa) AudioSystem.OnEnable(au); }
        }
        static readonly List<AudioSource> pendingAudio = new List<AudioSource>();

        public static void OnActiveChanged(GameObject go)
        {
            if (go.m_Scene == null) return;
            if (go.activeInHierarchy) Activate(Subtree(go));
            else Deactivate(Subtree(go));
        }

        static void Deactivate(IEnumerable<GameObject> gos)
        {
            foreach (var g in gos)
                foreach (var c in g.m_Components.ToArray())
                {
                    var mb = c as MonoBehaviour;
                    BehaviourState s;
                    if (mb == null) continue;
                    Coroutines.StopAll(mb);
                    if (!states.TryGetValue(mb, out s) || !s.enabledCalled) continue;
                    s.enabledCalled = false;
                    Call(s, s.info.OnDisable, "OnDisable");
                }
        }

        public static void OnBehaviourEnabledChanged(Behaviour b)
        {
            var mb = b as MonoBehaviour;
            if (mb == null || mb.gameObject == null || mb.gameObject.m_Scene == null) return;
            var s = State(mb);
            if (!s.awoken) CoinCounter.BeforeAwake(mb);
            if (!mb.gameObject.activeInHierarchy) return;
            if (mb.m_Enabled && !s.enabledCalled)
            {
                if (!s.awoken) { s.awoken = true; Call(s, s.info.Awake, "Awake"); }
                s.enabledCalled = true; Call(s, s.info.OnEnable, "OnEnable");
            }
            else if (!mb.m_Enabled && s.enabledCalled) { s.enabledCalled = false; Call(s, s.info.OnDisable, "OnDisable"); }
        }

        public static void OnComponentAdded(Component c)
        {
            var mb = c as MonoBehaviour;
            if (mb == null || c.m_GameObject.m_Scene == null) return;
            Activate(new[] { c.m_GameObject });
        }

        public static void OnDestroyed(MonoBehaviour mb)
        {
            Coroutines.StopAll(mb);
            BehaviourState s;
            if (!states.TryGetValue(mb, out s) || s.destroyed) return;
            if (s.enabledCalled) { s.enabledCalled = false; Call(s, s.info.OnDisable, "OnDisable"); }
            if (s.awoken) Call(s, s.info.OnDestroy, "OnDestroy");
            s.destroyed = true;
            states.Remove(mb);
        }

        // -------------------------------------------------------- frame
        static bool Live(BehaviourState s) { return !s.destroyed && !s.mb.m_Destroyed && s.mb.m_Enabled && s.enabledCalled && s.mb.gameObject.activeInHierarchy; }

        static Action Bind(BehaviourState s, MethodInfo m) { return (Action)Delegate.CreateDelegate(typeof(Action), s.mb, m); }

        static void RunStarts()
        {
            // Start runs once, before the first Update, for every enabled behaviour (exec order)
            for (int guard = 0; guard < 8; guard++)
            {
                unstarted.RemoveAll(s => s.started || s.destroyed);
                var pending = unstarted.Where(s => Live(s)).ToList();
                if (pending.Count == 0) return;
                foreach (var s in pending.OrderBy(x => x.info.order).ThenBy(x => x.seq))
                {
                    if (s.started || !Live(s)) continue;
                    s.started = true;
                    Call(s, s.info.Start, "Start");
                }
            }
        }

        static void SortIfNeeded()
        {
            if (!dirtyOrder) return;
            all.RemoveAll(s => s.destroyed);
            all.Sort((a, b) => a.info.order != b.info.order ? a.info.order.CompareTo(b.info.order) : a.seq.CompareTo(b.seq));
            allVersion++;
            dirtyOrder = false;
        }

        // Per-message snapshots of `all` (only behaviours that implement the message), rebuilt when `all`
        // changes: the same states in the same order as all.ToArray() + skip, without copying 6,000
        // references three times a frame (M5: allocation drove the GC on the device).
        static int allVersion;
        static readonly BehaviourState[][] snap = new BehaviourState[3][];
        static readonly int[] snapVersion = { -1, -1, -1 };
        static BehaviourState[] With(int kind)   // 0 FixedUpdate, 1 Update, 2 LateUpdate
        {
            if (snapVersion[kind] == allVersion) return snap[kind];
            var l = new List<BehaviourState>();
            foreach (var s in all)
                if ((kind == 0 ? s.info.FixedUpdate : kind == 1 ? s.info.Update : s.info.LateUpdate) != null) l.Add(s);
            snapVersion[kind] = allVersion;
            return snap[kind] = l.ToArray();
        }

        static float fixedAccumulator;
        public static Action PhysicsStep;   // set by Physics2D
        public static Action AnimatorStep;  // set by Animator system

        public static void Frame(float dt) { Frame(dt, dt); }
        public static void Frame(float dt, float wallDt)
        {
            dt = Mathf.Min(dt, 1f / 3f);   // TimeManager 'Maximum Allowed Timestep'
            Time.s_UnscaledDeltaTime = wallDt; Time.s_UnscaledTime += wallDt;
            Time.s_DeltaTime = dt * Time.s_TimeScale;
            Time.s_FrameCount++;

            long pt = Prof.Now();
            SceneLoader.ProcessPending();
            pt = Prof.Add("frame: scene loads", pt);
            SortIfNeeded();
            RunStarts();
            pt = Prof.Add("frame: Start", pt);

            // fixed steps (50 Hz in this game)
            fixedAccumulator += Time.s_DeltaTime;
            float fdt = Time.s_FixedDeltaTime;
            int steps = 0;
            float frameStartTime = Time.s_Time;
            while (Time.timeScale > 0f && fdt > 0f && fixedAccumulator >= fdt && steps < 5)
            {
                fixedAccumulator -= fdt; steps++;
                Time.s_FixedTime += fdt; Time.s_FixedStepCount++;
                Time.s_Time = Time.s_FixedTime;
                float saved = Time.s_DeltaTime; Time.s_DeltaTime = fdt;
                SortIfNeeded();
                foreach (var s in With(0))
                {
                    if (!Live(s) || !s.started) continue;
                    if (s.fixedUpdate == null) s.fixedUpdate = Bind(s, s.info.FixedUpdate);
                    long t1 = Prof.Level > 1 ? Prof.Now() : 0;
                    try { s.fixedUpdate(); } catch (Exception e) { Fail(s.mb, "FixedUpdate", e); }
                    if (Prof.Level > 1) Prof.Add("  FixedUpdate " + s.info.name, t1);
                }
                pt = Prof.Add("logic: FixedUpdate scripts", pt);
                if (PhysicsStep != null) PhysicsStep();
                Coroutines.TickFixed();
                pt = Prof.Add("logic: physics", pt);
                Time.s_DeltaTime = saved;
            }
            if (steps == 5) fixedAccumulator = 0f;
            Time.s_Time = frameStartTime + Time.s_DeltaTime;

            pt = Prof.Now();
            Input.BeginFrame();
            RunStarts();
            SortIfNeeded();
            foreach (var s in With(1))
            {
                if (!Live(s) || !s.started) continue;
                if (s.update == null) s.update = Bind(s, s.info.Update);
                long t1 = Prof.Level > 1 ? Prof.Now() : 0;
                try { s.update(); } catch (Exception e) { Fail(s.mb, "Update", e); }
                if (Prof.Level > 1) Prof.Add("  Update " + s.info.name, t1);
            }
            pt = Prof.Add("logic: Update scripts", pt);
            long tInv = Prof.Level > 0 ? Prof.Now() : 0;
            Invokes.Tick();
            if (Prof.Level > 0) tInv = Prof.Add("  logic: invokes", tInv);
            Coroutines.Tick();
            CoinCounter.Tick();
            if (Prof.Level > 0) Prof.Add("  logic: coroutines", tInv);
            pt = Prof.Add("logic: invokes + coroutines", pt);
            if (AnimatorStep != null) AnimatorStep();
            pt = Prof.Add("logic: animator", pt);
            foreach (var s in With(2))
            {
                if (!Live(s) || !s.started) continue;
                if (s.lateUpdate == null) s.lateUpdate = VisualQuality.WrapLateUpdate(s.mb, Bind(s, s.info.LateUpdate));
                long t1 = Prof.Level > 1 ? Prof.Now() : 0;
                try { s.lateUpdate(); } catch (Exception e) { Fail(s.mb, "LateUpdate", e); }
                if (Prof.Level > 1) Prof.Add("  LateUpdate " + s.info.name, t1);
            }
            pt = Prof.Add("logic: LateUpdate scripts", pt);
            World.FlushDestroyed();
            Prof.Add("logic: destroy flush", pt);
        }

        public static int LiveCount { get { return all.Count(Live); } }

        static bool quitSent;
        public static void ApplicationQuit()
        {
            if (quitSent) return;
            quitSent = true;
            foreach (var s in all.ToArray())
                if (!s.destroyed && !s.mb.m_Destroyed && s.awoken)
                    Reflect.InvokeMessage(s.mb, "OnApplicationQuit", null);
            PlayerPrefs.Save();
        }
    }

    // =================================================================== scenes
    public static class SceneLoader
    {
        public sealed class Pending { public string name; public bool additive; public AsyncOperation op; }
        static readonly List<Pending> pending = new List<Pending>();
        public static readonly List<SceneTag> Loaded = new List<SceneTag>();
        public static string[] BuildScenes = new string[0];    // index -> scene name
        public static Action<SceneTag, int> SceneLoadedHook;

        public static void LoadBuildSettings()
        {
            foreach (var e in World.File("globalgamemanagers").objects.Values)
            {
                if ((string)e["type"] != "BuildSettings" || !e.ContainsKey("data")) continue;
                var sc = (List<object>)((JObj)e["data"])["scenes"];
                BuildScenes = sc.Select(s => System.IO.Path.GetFileNameWithoutExtension((string)s)).ToArray();
            }
        }
        public static int IndexOf(string name)
        {
            for (int i = 0; i < BuildScenes.Length; i++) if (BuildScenes[i] == name) return i;
            int k; return name.StartsWith("level") && int.TryParse(name.Substring(5), out k) ? k : -1;
        }

        public static AsyncOperation Request(string name, bool additive)
        {
            var op = new AsyncOperation();
            pending.Add(new Pending { name = name, additive = additive, op = op });
            return op;
        }

        public static bool Trace = Environment.GetEnvironmentVariable("KNL_TRACE") != null;
        public static SceneTag LoadNow(string name, bool additive)
        {
            if (Trace) Console.Error.WriteLine("[scene] load " + name + (additive ? " (additive)" : "") + " @frame " + Time.frameCount);
            int idx = IndexOf(name);
            if (idx < 0) { Log.Write("ERROR", "unknown scene " + name); return null; }
            if (!additive)
            {
                foreach (var g in World.SceneObjects.ToArray())
                    if (!g.m_Destroyed && g.m_Scene != null && !World.IsPersistent(g) && g.m_Transform.m_Parent == null) World.DestroyNow(g);
                World.SceneObjects.RemoveAll(g => g.m_Destroyed);
                World.FlushDestroyed();
                Loaded.RemoveAll(t => !t.persistent);
            }
            var tag = World.LoadScene("level" + idx, BuildScenes.Length > idx ? BuildScenes[idx] : name);
            if (!additive) Time.s_LevelLoadTime = Time.s_Time;
            tag.buildIndex = idx;
            Loaded.Add(tag);
            if (!additive || World.ActiveScene == null) World.ActiveScene = tag;
            Lifecycle.Activate(World.SceneObjects.Where(g => g.m_Scene == tag).ToList());
            if (SceneLoadedHook != null) SceneLoadedHook(tag, additive ? 1 : 0);
            return tag;
        }

        public static bool Unload(string name)
        {
            if (Trace) Console.Error.WriteLine("[scene] unload " + name + " @frame " + Time.frameCount);
            var tag = Loaded.FirstOrDefault(t => t.displayName == name || t.name == name);
            if (tag == null) return false;
            foreach (var g in World.SceneObjects.ToArray())
                if (!g.m_Destroyed && g.m_Scene == tag && g.m_Transform.m_Parent == null) World.DestroyNow(g);
            World.SceneObjects.RemoveAll(g => g.m_Destroyed);
            World.FlushDestroyed();
            Loaded.Remove(tag);
            if (World.ActiveScene == tag) World.ActiveScene = Loaded.FirstOrDefault();
            return true;
        }

        public static void ProcessPending()
        {
            foreach (var p in pending.ToArray())
            {
                if (!p.op.allowSceneActivation) { p.op.m_Progress = 0.9f; continue; }
                pending.Remove(p);
                LoadNow(p.name, p.additive);
                p.op.m_Progress = 1f; p.op.m_IsDone = true;
            }
        }
    }
}
