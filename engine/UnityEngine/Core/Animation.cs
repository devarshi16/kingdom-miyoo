// Clean-room Animator for Kingdom: plays the game's compiled AnimatorControllers and clips.
//  - clips: decodes the build-time MuscleClip (streamed / dense / constant curves) and its
//    generic bindings (path + attribute CRC32 hashes); PPtr curves index the clip's sprite table
//  - controllers: compiled state machines (states, transitions, conditions, any-state,
//    exit time, triggers, 1D blend trees); AnimatorOverrideController clip swaps
//  - no cross-fading: transitions switch instantly (pixel-art sprite animation)
using System;
using System.Collections.Generic;
using System.Linq;
using KNL;
using UnityEngine;
using JObj = System.Collections.Generic.Dictionary<string, object>;

namespace UnityEngine
{
    public partial class RuntimeAnimatorController
    {
        internal virtual KNL.AnimControllerData Data { get { return null; } }
        internal virtual AnimationClip Remap(AnimationClip c) { return c; }
    }

    public sealed partial class AnimatorOverrideController
    {
        internal RuntimeAnimatorController m_Base;
        internal readonly Dictionary<AnimationClip, AnimationClip> m_Overrides = new Dictionary<AnimationClip, AnimationClip>();
        internal override KNL.AnimControllerData Data { get { return m_Base != null ? m_Base.Data : null; } }
        internal override AnimationClip Remap(AnimationClip c)
        {
            AnimationClip o;
            if (c != null && m_Overrides.TryGetValue(c, out o) && o != null) return o;
            return m_Base != null ? m_Base.Remap(c) : c;
        }
        public RuntimeAnimatorController runtimeAnimatorController { get { return m_Base; } set { m_Base = value; } }
    }

    public sealed partial class AnimationClip
    {
        internal KNL.ClipData m_Data; internal KNL.AssetFile m_File; internal Dictionary<string, object> m_Raw;
        public float length { get { return m_Data != null ? m_Data.length : 0f; } }
        public float frameRate { get { return m_Data != null ? m_Data.sampleRate : 30f; } }
        public bool isLooping { get { return m_Data != null && m_Data.loop; } }
    }

    public partial struct AnimatorStateInfo
    {
        internal int m_FullPath, m_Name, m_Tag; internal float m_NormalizedTime, m_Length, m_Speed; internal bool m_Loop;
        public float normalizedTime { get { return m_NormalizedTime; } }
        public float length { get { return m_Length; } }
        public int fullPathHash { get { return m_FullPath; } }
        public int shortNameHash { get { return m_Name; } }
        public int nameHash { get { return m_FullPath; } }
        public int tagHash { get { return m_Tag; } }
        public float speed { get { return m_Speed; } }
        public bool loop { get { return m_Loop; } }
        public bool IsName(string n) { int h = Animator.StringToHash(n); return h == m_Name || h == m_FullPath; }
        public bool IsTag(string t) { return Animator.StringToHash(t) == m_Tag; }
    }

    public partial class StateMachineBehaviour
    {
        public virtual void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) { }
        public virtual void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) { }
        public virtual void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) { }
    }

    public partial class Animator
    {
        internal static readonly List<Animator> s_All = new List<Animator>();
        internal RuntimeAnimatorController m_Controller;
        internal float m_Speed = 1f;
        internal AnimatorCullingMode m_CullingMode; internal AnimatorUpdateMode m_UpdateMode;
        internal KNL.AnimatorRuntime m_Rt;

        public RuntimeAnimatorController runtimeAnimatorController
        {
            get { return m_Controller; }
            set { if (m_Controller == value) return; m_Controller = value; m_Rt = null; }
        }
        public bool hasBoundPlayables { get { return m_Controller != null; } }
        public bool isInitialized { get { return m_Controller != null; } }
        public float speed { get { return m_Speed; } set { m_Speed = value; } }
        public AnimatorCullingMode cullingMode { get { return m_CullingMode; } set { m_CullingMode = value; } }
        public AnimatorUpdateMode updateMode { get { return m_UpdateMode; } set { m_UpdateMode = value; } }

        internal KNL.AnimatorRuntime Rt
        {
            get
            {
                if (m_Rt == null && m_Controller != null && m_Controller.Data != null) m_Rt = new KNL.AnimatorRuntime(this);
                return m_Rt;
            }
        }
        public void SetBool(int id, bool v) { var r = Rt; if (r != null) r.SetParam(id, v ? 1f : 0f); }
        public void SetBool(string n, bool v) { SetBool(StringToHash(n), v); }
        public bool GetBool(int id) { var r = Rt; return r != null && r.GetParam(id) != 0f; }
        public bool GetBool(string n) { return GetBool(StringToHash(n)); }
        public void SetFloat(int id, float v) { var r = Rt; if (r != null) r.SetParam(id, v); }
        public void SetFloat(string n, float v) { SetFloat(StringToHash(n), v); }
        public float GetFloat(int id) { var r = Rt; return r != null ? r.GetParam(id) : 0f; }
        public float GetFloat(string n) { return GetFloat(StringToHash(n)); }
        public void SetInteger(int id, int v) { var r = Rt; if (r != null) r.SetParam(id, v); }
        public void SetInteger(string n, int v) { SetInteger(StringToHash(n), v); }
        public int GetInteger(int id) { var r = Rt; return r != null ? (int)r.GetParam(id) : 0; }
        public void SetTrigger(int id) { var r = Rt; if (r != null) r.SetParam(id, 1f); }
        public void SetTrigger(string n) { SetTrigger(StringToHash(n)); }
        public void ResetTrigger(int id) { var r = Rt; if (r != null) r.SetParam(id, 0f); }
        public void ResetTrigger(string n) { ResetTrigger(StringToHash(n)); }
        public void Play(int hash) { Play(hash, -1, float.NegativeInfinity); }
        public void Play(string n) { Play(StringToHash(n), -1, float.NegativeInfinity); }
        public void Play(string n, int layer, float t) { Play(StringToHash(n), layer, t); }
        public void Play(int hash, int layer, float t) { var r = Rt; if (r != null) r.Play(hash, layer, t); }
        public void CrossFade(string n, float d) { Play(n); }
        public void CrossFade(int h, float d) { Play(h); }
        public AnimatorStateInfo GetCurrentAnimatorStateInfo(int layer) { var r = Rt; return r != null ? r.Info(layer) : default(AnimatorStateInfo); }
        public void Update(float dt) { var r = Rt; if (r != null) r.Step(dt); }
        public void Rebind() { m_Rt = null; }
    }
}

namespace KNL
{
    // ======================================================================= clips
    public sealed class ClipBinding
    {
        public uint path, attribute; public int typeID, customType, curveIndex, curveCount; public bool pptr;
        public UnityEngine.Object script;
    }

    public sealed class ClipData
    {
        public float start, stop, length, sampleRate = 30f; public bool loop;
        public int streamedCount, denseCount, constCount;
        public List<float> streamTimes = new List<float>();        // per frame
        public List<int[]> streamIdx = new List<int[]>();          // per frame: curve indices
        public List<float[]> streamCoef = new List<float[]>();     // per frame: 4 coeffs per key
        public int denseFrames; public float denseRate, denseBegin; public float[] dense = new float[0];
        public float[] constants = new float[0];
        public List<ClipBinding> bindings = new List<ClipBinding>();
        public UnityEngine.Object[] pptrs = new UnityEngine.Object[0];
        public List<KeyValuePair<float, JObj>> events = new List<KeyValuePair<float, JObj>>();
        public AssetFile file;
        int totalCurves;
        float[] lastKeyTime; float[][] lastCoef;

        public static ClipData Decode(AssetFile f, JObj d)
        {
            var c = new ClipData { file = f };
            var mc = (JObj)d["m_MuscleClip"];
            c.start = Json.F(mc, "m_StartTime"); c.stop = Json.F(mc, "m_StopTime");
            c.length = Mathf.Max(0f, c.stop - c.start); c.loop = Json.B(mc, "m_LoopTime");
            c.sampleRate = Json.F(d, "m_SampleRate", 30f);
            var clip = (JObj)((JObj)mc["m_Clip"])["data"];
            var sc = (JObj)clip["m_StreamedClip"];
            c.streamedCount = (int)Json.L(sc, "curveCount");
            var raw = ((List<object>)sc["data"]).Select(x => (uint)(double)x).ToArray();
            int p = 0;
            while (p + 1 < raw.Length)
            {
                float t = BitConverter.ToSingle(BitConverter.GetBytes(raw[p]), 0); int n = (int)raw[p + 1]; p += 2;
                var idx = new int[n]; var coef = new float[n * 4];
                for (int k = 0; k < n && p + 4 < raw.Length; k++)
                {
                    idx[k] = (int)raw[p];
                    for (int j = 0; j < 4; j++) coef[k * 4 + j] = BitConverter.ToSingle(BitConverter.GetBytes(raw[p + 1 + j]), 0);
                    p += 5;
                }
                c.streamTimes.Add(t); c.streamIdx.Add(idx); c.streamCoef.Add(coef);
            }
            var dc = (JObj)clip["m_DenseClip"];
            c.denseFrames = (int)Json.L(dc, "m_FrameCount"); c.denseCount = (int)Json.L(dc, "m_CurveCount");
            c.denseRate = Json.F(dc, "m_SampleRate", 30f); c.denseBegin = Json.F(dc, "m_BeginTime");
            var sa = dc["m_SampleArray"] as List<object>;
            if (sa != null) c.dense = sa.Select(x => (float)(double)x).ToArray();
            var cc = clip["m_ConstantClip"] as JObj;
            var cd = cc != null ? cc["data"] as List<object> : null;
            if (cd != null) { c.constants = cd.Select(x => (float)(double)x).ToArray(); }
            c.constCount = c.constants.Length;
            c.totalCurves = c.streamedCount + c.denseCount + c.constCount;

            var b = (JObj)d["m_ClipBindingConstant"];
            int ci = 0;
            foreach (JObj gb in (List<object>)b["genericBindings"])
            {
                var cb = new ClipBinding
                {
                    path = (uint)Json.L(gb, "path"), attribute = (uint)Json.L(gb, "attribute"),
                    typeID = (int)Json.L(gb, "typeID"), customType = (int)Json.L(gb, "customType"),
                    pptr = Json.B(gb, "isPPtrCurve"), curveIndex = ci,
                };
                cb.curveCount = cb.typeID == 4 ? (cb.attribute == 2 ? 4 : 3) : 1;
                cb.script = World.Resolve(f, gb["script"]);
                ci += cb.curveCount;
                c.bindings.Add(cb);
            }
            c.pptrs = ((List<object>)b["pptrCurveMapping"]).Select(x => World.Resolve(f, x)).ToArray();
            var evs = d["m_Events"] as List<object>;
            if (evs != null) foreach (JObj e in evs) c.events.Add(new KeyValuePair<float, JObj>(Json.F(e, "time"), e));
            return c;
        }

        /// Sample all curves at clip-local time t (seconds from m_StartTime).
        public void Sample(float t, float[] outv)
        {
            float time = start + t;
            // streamed: last key at or before t per curve, cubic in (t - keyTime)
            for (int i = 0; i < streamedCount && i < outv.Length; i++) outv[i] = 0f;
            for (int fr = 0; fr < streamTimes.Count; fr++)
            {
                float ft = streamTimes[fr];
                if (ft > time) break;
                var idx = streamIdx[fr]; var co = streamCoef[fr];
                float dt = ft < -1e30f ? 0f : time - ft;
                for (int k = 0; k < idx.Length; k++)
                {
                    int ci = idx[k]; if (ci >= outv.Length) continue;
                    outv[ci] = ((co[k * 4] * dt + co[k * 4 + 1]) * dt + co[k * 4 + 2]) * dt + co[k * 4 + 3];
                }
            }
            // dense: linear between sampled frames
            if (denseCount > 0 && denseFrames > 0)
            {
                float fpos = Mathf.Clamp((time - denseBegin) * denseRate, 0f, denseFrames - 1);
                int f0 = (int)fpos, f1 = Mathf.Min(f0 + 1, denseFrames - 1); float u = fpos - f0;
                for (int i = 0; i < denseCount; i++)
                {
                    float a = dense[f0 * denseCount + i], b2 = dense[f1 * denseCount + i];
                    outv[streamedCount + i] = a + (b2 - a) * u;
                }
            }
            for (int i = 0; i < constCount; i++) outv[streamedCount + denseCount + i] = constants[i];
        }
        public int CurveCount { get { return totalCurves; } }
    }

    // ================================================================= controller
    public sealed class AnimTransition
    {
        public int dest; public bool hasExitTime; public float exitTime, offset;
        public List<int> condMode = new List<int>(); public List<int> condParam = new List<int>(); public List<float> condThreshold = new List<float>();
        public bool canSelf = true;
    }
    public sealed class AnimState
    {
        public int nameId, fullPathId, tagId, speedParam; public float speed = 1f; public bool loop;
        public List<AnimTransition> transitions = new List<AnimTransition>();
        public int clipIndex = -1;                 // single leaf
        public List<int> blendClips = new List<int>(); public List<float> blendThresholds = new List<float>(); public int blendParam;
        public List<int> behaviours = new List<int>();
    }
    public sealed class AnimLayer { public List<AnimState> states = new List<AnimState>(); public List<AnimTransition> any = new List<AnimTransition>(); public int defaultState; }

    public sealed class AnimControllerData
    {
        public List<AnimLayer> layers = new List<AnimLayer>();
        public Dictionary<int, int> paramType = new Dictionary<int, int>();   // id -> 1 float, 3 int, 4 bool, 9 trigger
        public Dictionary<int, float> paramDefault = new Dictionary<int, float>();
        public List<AnimationClip> clips = new List<AnimationClip>();
        public Dictionary<uint, string> tos = new Dictionary<uint, string>();
        public List<StateMachineBehaviour> behaviours = new List<StateMachineBehaviour>();

        public static AnimControllerData Decode(AssetFile f, JObj d)
        {
            var c = new AnimControllerData();
            foreach (List<object> kv in (List<object>)d["m_TOS"]) c.tos[(uint)(double)kv[0]] = (string)kv[1];
            foreach (var cp in (List<object>)d["m_AnimationClips"]) c.clips.Add(World.Resolve(f, cp) as AnimationClip);
            var ctrl = (JObj)d["m_Controller"];
            var vals = (List<object>)((JObj)((JObj)ctrl["m_Values"])["data"])["m_ValueArray"];
            var defs = (JObj)((JObj)ctrl["m_DefaultValues"])["data"];
            foreach (JObj v in vals)
            {
                int id = (int)(uint)Json.L(v, "m_ID"), type = (int)Json.L(v, "m_Type"), index = (int)Json.L(v, "m_Index");
                c.paramType[id] = type;
                float def = 0f;
                try
                {
                    if (type == 1) def = (float)(double)((List<object>)defs["m_FloatValues"])[index];
                    else if (type == 3) def = (float)(double)((List<object>)defs["m_IntValues"])[index];
                    else if (type == 4 || type == 9) { var bv = ((List<object>)defs["m_BoolValues"])[index]; def = bv is bool ? ((bool)bv ? 1f : 0f) : (float)(double)bv; }
                }
                catch { }
                c.paramDefault[id] = def;
            }
            var sms = (List<object>)ctrl["m_StateMachineArray"];
            foreach (JObj lw in (List<object>)ctrl["m_LayerArray"])
            {
                var l = (JObj)lw["data"];
                var sm = (JObj)((JObj)sms[(int)Json.L(l, "m_StateMachineIndex")])["data"];
                var layer = new AnimLayer { defaultState = (int)Json.L(sm, "m_DefaultState") };
                foreach (JObj sw in (List<object>)sm["m_StateConstantArray"])
                {
                    var s = (JObj)sw["data"];
                    var st = new AnimState
                    {
                        nameId = (int)(uint)Json.L(s, "m_NameID"), fullPathId = (int)(uint)Json.L(s, "m_FullPathID"), tagId = (int)(uint)Json.L(s, "m_TagID"),
                        speed = Json.F(s, "m_Speed", 1f), loop = Json.B(s, "m_Loop"), speedParam = (int)(uint)Json.L(s, "m_SpeedParamID"),
                    };
                    foreach (JObj tw in (List<object>)s["m_TransitionConstantArray"]) st.transitions.Add(Tr((JObj)tw["data"]));
                    var bts = (List<object>)s["m_BlendTreeConstantArray"];
                    if (bts.Count > 0)
                    {
                        var nodes = (List<object>)((JObj)((JObj)bts[0])["data"])["m_NodeArray"];
                        var root = (JObj)((JObj)nodes[0])["data"];
                        var kids = (List<object>)root["m_ChildIndices"];
                        if (kids.Count == 0) st.clipIndex = (int)(uint)Json.L(root, "m_ClipID");
                        else
                        {
                            st.blendParam = (int)(uint)Json.L(root, "m_BlendEventID");
                            var th = (List<object>)((JObj)((JObj)root["m_Blend1dData"])["data"])["m_ChildThresholdArray"];
                            for (int i = 0; i < kids.Count; i++)
                            {
                                var child = (JObj)((JObj)nodes[(int)(double)kids[i]])["data"];
                                st.blendClips.Add((int)(uint)Json.L(child, "m_ClipID"));
                                st.blendThresholds.Add(i < th.Count ? (float)(double)th[i] : i);
                            }
                        }
                    }
                    layer.states.Add(st);
                }
                foreach (JObj tw in (List<object>)sm["m_AnyStateTransitionConstantArray"]) layer.any.Add(Tr((JObj)tw["data"]));
                c.layers.Add(layer);
            }
            // StateMachineBehaviours: one instance per controller (Unity clones per Animator; KNL keeps
            // them per controller asset which is enough for the game's single use)
            var smbs = d["m_StateMachineBehaviours"] as List<object>;
            if (smbs != null) foreach (var pp in smbs) c.behaviours.Add(World.Resolve(f, pp) as StateMachineBehaviour);
            var desc = d["m_StateMachineBehaviourVectorDescription"] as JObj;
            if (desc != null && c.behaviours.Count > 0)
            {
                var ranges = desc["m_StateMachineBehaviourRanges"] as List<object>;
                var indices = ((List<object>)desc["m_StateMachineBehaviourIndices"]).Select(x => (int)(double)x).ToList();
                if (ranges != null)
                    foreach (List<object> kv in ranges)
                    {
                        var key = (JObj)kv[0]; var val = (JObj)kv[1];
                        int li = (int)Json.L(key, "m_LayerIndex"), si = (int)(uint)Json.L(key, "m_StateID");
                        int start = (int)Json.L(val, "first"), count = (int)Json.L(val, "second");
                        if (li < c.layers.Count)
                            foreach (var st in c.layers[li].states)
                                if (st.fullPathId == si || st.nameId == si) for (int i = 0; i < count; i++) st.behaviours.Add(indices[start + i]);
                    }
            }
            return c;
        }

        static AnimTransition Tr(JObj t)
        {
            var tr = new AnimTransition
            {
                dest = (int)Json.L(t, "m_DestinationState"), hasExitTime = Json.B(t, "m_HasExitTime"),
                exitTime = Json.F(t, "m_ExitTime"), offset = Json.F(t, "m_TransitionOffset"), canSelf = Json.B(t, "m_CanTransitionToSelf"),
            };
            foreach (JObj cw in (List<object>)t["m_ConditionConstantArray"])
            {
                var c = (JObj)cw["data"];
                tr.condMode.Add((int)Json.L(c, "m_ConditionMode")); tr.condParam.Add((int)(uint)Json.L(c, "m_EventID")); tr.condThreshold.Add(Json.F(c, "m_EventThreshold"));
            }
            return tr;
        }
    }

    public sealed class AnimControllerAsset : RuntimeAnimatorController
    {
        public AssetFile file; public JObj raw; AnimControllerData data;
        internal override AnimControllerData Data { get { if (data == null && raw != null) data = AnimControllerData.Decode(file, raw); return data; } }
    }

    // ==================================================================== runtime
    public sealed class AnimatorRuntime
    {
        readonly Animator anim; readonly AnimControllerData c;
        readonly Dictionary<int, float> param = new Dictionary<int, float>();
        readonly int[] current; readonly float[] time; readonly float[] lastEvalTime;
        readonly Dictionary<uint, Transform> pathCache = new Dictionary<uint, Transform>();
        float[] sample = new float[16];
        static readonly HashSet<string> warned = new HashSet<string>();

        public AnimatorRuntime(Animator a)
        {
            anim = a; c = a.runtimeAnimatorController.Data;
            foreach (var kv in c.paramDefault) param[kv.Key] = kv.Value;
            current = new int[c.layers.Count]; time = new float[c.layers.Count]; lastEvalTime = new float[c.layers.Count];
            for (int i = 0; i < c.layers.Count; i++) { current[i] = c.layers[i].defaultState; EnterState(i); }
        }
        public void SetParam(int id, float v) { param[id] = v; }
        public float GetParam(int id) { float v; return param.TryGetValue(id, out v) ? v : 0f; }

        AnimationClip ClipOf(AnimState s)
        {
            int ci = s.clipIndex;
            if (s.blendClips.Count > 0)
            {   // 1D blend tree without blending: nearest threshold
                float v = GetParam(s.blendParam); float best = float.MaxValue;
                for (int i = 0; i < s.blendClips.Count; i++) { float d = Mathf.Abs(s.blendThresholds[i] - v); if (d < best) { best = d; ci = s.blendClips[i]; } }
            }
            if (ci < 0 || ci >= c.clips.Count) return null;
            var clip = anim.runtimeAnimatorController.Remap(c.clips[ci]);
            if (clip != null && clip.m_Data == null) World.EnsureClip(clip);
            return clip;
        }

        public void Play(int hash, int layer, float normalizedTime)
        {
            for (int li = 0; li < c.layers.Count; li++)
            {
                if (layer >= 0 && li != layer) continue;
                var states = c.layers[li].states;
                for (int s = 0; s < states.Count; s++)
                    if (states[s].nameId == hash || states[s].fullPathId == hash)
                    {
                        bool same = current[li] == s;
                        if (!same) ExitState(li);
                        current[li] = s;
                        if (!same) EnterState(li);
                        var clip = ClipOf(states[s]);
                        if (!float.IsNegativeInfinity(normalizedTime)) time[li] = normalizedTime * (clip != null ? clip.length : 1f);
                        else if (!same) time[li] = 0f;
                        lastEvalTime[li] = time[li];
                        Apply(li);
                        return;
                    }
            }
        }

        public AnimatorStateInfo Info(int li)
        {
            var info = new AnimatorStateInfo();
            if (li >= c.layers.Count) return info;
            var st = c.layers[li].states[current[li]]; var clip = ClipOf(st);
            float len = clip != null && clip.length > 0 ? clip.length : 1f;
            info.m_Name = st.nameId; info.m_FullPath = st.fullPathId; info.m_Tag = st.tagId;
            info.m_Length = len; info.m_NormalizedTime = time[li] / len; info.m_Speed = st.speed; info.m_Loop = clip != null && clip.isLooping;
            return info;
        }

        bool CondsPass(AnimTransition t, float normalized)
        {
            if (t.hasExitTime && normalized < t.exitTime) return false;
            if (!t.hasExitTime && t.condMode.Count == 0) return false;
            for (int i = 0; i < t.condMode.Count; i++)
            {
                float v = GetParam(t.condParam[i]); float th = t.condThreshold[i];
                switch (t.condMode[i])
                {
                    case 1: if (v == 0f) return false; break;        // If (bool true / trigger set)
                    case 2: if (v != 0f) return false; break;        // IfNot
                    case 3: if (!(v > th)) return false; break;      // Greater
                    case 4: if (!(v < th)) return false; break;      // Less
                    case 6: if ((int)v != (int)th) return false; break; // Equals
                    case 7: if ((int)v == (int)th) return false; break; // NotEqual
                }
            }
            return true;
        }
        void ConsumeTriggers(AnimTransition t)
        {
            foreach (var p in t.condParam) { int ty; if (c.paramType.TryGetValue(p, out ty) && ty == 9) param[p] = 0f; }
        }

        void ExitState(int li)
        {
            var st = c.layers[li].states[current[li]];
            foreach (var bi in st.behaviours) { var b = bi < c.behaviours.Count ? c.behaviours[bi] : null; if (b != null) Safe(() => b.OnStateExit(anim, Info(li), li)); }
        }
        void EnterState(int li)
        {
            var st = c.layers[li].states[current[li]];
            foreach (var bi in st.behaviours) { var b = bi < c.behaviours.Count ? c.behaviours[bi] : null; if (b != null) Safe(() => b.OnStateEnter(anim, Info(li), li)); }
        }
        void Safe(Action a) { try { a(); } catch (Exception e) { Lifecycle.Fail(anim, "StateMachineBehaviour", e); } }

        public void Step(float dt)
        {
            for (int li = 0; li < c.layers.Count; li++)
            {
                var layer = c.layers[li];
                var st = layer.states[current[li]];
                var clip = ClipOf(st);
                float len = clip != null && clip.length > 0 ? clip.length : 1f;
                float spd = st.speed * anim.m_Speed * (st.speedParam != 0 ? GetParam(st.speedParam) : 1f);
                float prev = time[li];
                time[li] += dt * spd;
                float norm = time[li] / len;
                // transitions: any-state first, then the current state's
                AnimTransition fire = null;
                foreach (var t in layer.any) if ((t.canSelf || t.dest != current[li]) && CondsPass(t, norm)) { fire = t; break; }
                if (fire == null) foreach (var t in st.transitions) if (CondsPass(t, norm)) { fire = t; break; }
                if (fire != null && fire.dest >= 0 && fire.dest < layer.states.Count)
                {
                    ConsumeTriggers(fire);
                    ExitState(li);
                    current[li] = fire.dest;
                    var nclip = ClipOf(layer.states[fire.dest]);
                    time[li] = fire.offset * (nclip != null ? nclip.length : 1f);
                    prev = time[li];
                    EnterState(li);
                }
                Events(li, prev, time[li]);
                Apply(li);
                foreach (var bi in layer.states[current[li]].behaviours)
                { var b = bi < c.behaviours.Count ? c.behaviours[bi] : null; if (b != null) Safe(() => b.OnStateUpdate(anim, Info(li), li)); }
            }
        }

        float LocalTime(AnimationClip clip, float t)
        {
            float len = clip.length;
            if (len <= 0f) return 0f;
            return clip.isLooping ? Mathf.Repeat(t, len) : Mathf.Clamp(t, 0f, len);
        }

        void Events(int li, float from, float to)
        {
            var clip = ClipOf(c.layers[li].states[current[li]]);
            if (clip == null || clip.m_Data == null || clip.m_Data.events.Count == 0 || to <= from) return;
            float len = clip.length; if (len <= 0) return;
            foreach (var e in clip.m_Data.events)
            {
                // fire every crossing of e.Key in (from, to] over looped time
                float k = e.Key;
                float first = clip.isLooping ? Mathf.Floor((from - k) / len) + 1 : 0;
                for (float n = first; ; n++)
                {
                    float at = clip.isLooping ? k + n * len : k;
                    if (at > to) break;
                    if (at > from || (from == 0 && at == 0)) Fire(e.Value, clip);
                    if (!clip.isLooping) break;
                }
            }
        }
        void Fire(JObj e, AnimationClip clip)
        {
            string fn = Json.S(e, "functionName");
            if (string.IsNullOrEmpty(fn)) return;
            object arg = null;
            var obj = World.Resolve(clip.m_Data.file, e.ContainsKey("objectReferenceParameter") ? e["objectReferenceParameter"] : null);
            foreach (var comp in anim.gameObject.m_Components.ToArray())
            {
                if (!(comp is MonoBehaviour)) continue;
                var m = Reflect.FindMethod(comp.GetType(), fn);
                if (m == null) continue;
                var ps = m.GetParameters();
                if (ps.Length == 1)
                {
                    var pt = ps[0].ParameterType;
                    if (pt == typeof(string)) arg = Json.S(e, "data");
                    else if (pt == typeof(float)) arg = Json.F(e, "floatParameter");
                    else if (pt == typeof(int)) arg = (int)Json.L(e, "intParameter");
                    else if (typeof(UnityEngine.Object).IsAssignableFrom(pt)) arg = obj;
                }
                Reflect.InvokeMessage(comp, fn, arg);
            }
        }

        void Apply(int li)
        {
            var clip = ClipOf(c.layers[li].states[current[li]]);
            if (clip == null || clip.m_Data == null) return;
            var d = clip.m_Data;
            if (sample.Length < d.CurveCount) sample = new float[d.CurveCount + 8];
            d.Sample(LocalTime(clip, time[li]), sample);
            foreach (var b in d.bindings) ApplyBinding(b, d);
            // UI graphics rebuild their mesh after animation writes (UIBehaviour.OnDidApplyAnimationProperties)
            foreach (var mb in touched) Reflect.InvokeMessage(mb, "OnDidApplyAnimationProperties", null);
            touched.Clear();
        }
        readonly HashSet<MonoBehaviour> touched = new HashSet<MonoBehaviour>();

        Transform Target(uint pathHash)
        {
            Transform t;
            if (pathCache.TryGetValue(pathHash, out t)) return t;
            t = pathHash == 0 ? anim.transform : Find(anim.transform, "", pathHash);
            pathCache[pathHash] = t;
            return t;
        }
        static Transform Find(Transform root, string prefix, uint h)
        {
            foreach (var ch in root.m_Children)
            {
                string p = prefix.Length == 0 ? ch.name : prefix + "/" + ch.name;
                if ((uint)Animator.StringToHash(p) == h) return ch;
                var r = Find(ch, p, h);
                if (r != null) return r;
            }
            return null;
        }

        static readonly string[] SrFloatProps = { "m_Color.r", "m_Color.g", "m_Color.b", "m_Color.a" };
        void ApplyBinding(ClipBinding b, ClipData d)
        {
            var tr = Target(b.path);
            if (tr == null) return;
            float v = sample[b.curveIndex];
            switch (b.typeID)
            {
                case 212:   // SpriteRenderer
                {
                    var sr = tr.gameObject.GetComponent<SpriteRenderer>();
                    if (sr == null) return;
                    if (b.pptr) { int i = (int)(v + 0.5f); if (i >= 0 && i < d.pptrs.Length) sr.m_Sprite = d.pptrs[i] as Sprite; return; }
                    if (b.attribute == H("m_Color.r")) sr.m_Color.r = v;
                    else if (b.attribute == H("m_Color.g")) sr.m_Color.g = v;
                    else if (b.attribute == H("m_Color.b")) sr.m_Color.b = v;
                    else if (b.attribute == H("m_Color.a")) sr.m_Color.a = v;
                    else if (b.attribute == H("m_FlipX")) sr.m_FlipX = v > 0.5f;
                    else if (b.attribute == H("m_FlipY")) sr.m_FlipY = v > 0.5f;
                    else if (b.attribute == H("m_Enabled")) sr.m_RendererEnabled = v > 0.5f;
                    else Warn("SpriteRenderer attr " + b.attribute);
                    return;
                }
                case 4:     // Transform
                {
                    float x = sample[b.curveIndex], y = sample[b.curveIndex + 1], z = sample[b.curveIndex + 2];
                    if (b.attribute == 1) tr.m_LocalPosition = new Vector3(x, y, z);
                    else if (b.attribute == 2) tr.m_LocalRotation = new Quaternion(x, y, z, sample[b.curveIndex + 3]);
                    else if (b.attribute == 3) tr.m_LocalScale = new Vector3(x, y, z);
                    else if (b.attribute == 4) tr.m_LocalRotation = Quaternion.Euler(x, y, z);
                    return;
                }
                case 1:     // GameObject
                    if (b.attribute == H("m_IsActive")) { bool on = v > 0.5f; if (tr.gameObject.m_IsActive != on) tr.gameObject.SetActive(on); }
                    return;
                case 114:   // MonoBehaviour field (plain, struct component like m_Color.r, or object reference curve)
                {
                    foreach (var comp in tr.gameObject.m_Components)
                    {
                        var mb = comp as MonoBehaviour;
                        if (mb == null) continue;
                        if (b.attribute == H("m_Enabled")) { mb.enabled = v > 0.5f; return; }
                        var fp = FieldFor(mb.GetType(), b.attribute);
                        if (fp.f == null) continue;
                        if (b.pptr)
                        {
                            int i = (int)(v + 0.5f);
                            if (i >= 0 && i < d.pptrs.Length && (d.pptrs[i] == null || fp.f.FieldType.IsInstanceOfType(d.pptrs[i]))) fp.f.SetValue(mb, d.pptrs[i]);
                        }
                        else if (fp.comp >= 0) SetComponent(fp.f, mb, fp.comp, v);
                        else if (fp.f.FieldType == typeof(float)) fp.f.SetValue(mb, v);
                        else if (fp.f.FieldType == typeof(bool)) fp.f.SetValue(mb, v > 0.5f);
                        else if (fp.f.FieldType == typeof(int)) fp.f.SetValue(mb, (int)v);
                        touched.Add(mb);
                        return;
                    }
                    return;
                }
                case 224:   // RectTransform
                {
                    var rt = tr as RectTransform;
                    if (rt == null) return;
                    uint a = b.attribute;
                    if (a == H("m_AnchoredPosition.x")) { var ap = rt.anchoredPosition; ap.x = v; rt.anchoredPosition = ap; }
                    else if (a == H("m_AnchoredPosition.y")) { var ap = rt.anchoredPosition; ap.y = v; rt.anchoredPosition = ap; }
                    else if (a == H("m_SizeDelta.x")) { var sd = rt.m_SizeDelta; sd.x = v; rt.sizeDelta = sd; }
                    else if (a == H("m_SizeDelta.y")) { var sd = rt.m_SizeDelta; sd.y = v; rt.sizeDelta = sd; }
                    else if (a == H("m_AnchorMin.x")) { var q = rt.m_AnchorMin; q.x = v; rt.anchorMin = q; }
                    else if (a == H("m_AnchorMin.y")) { var q = rt.m_AnchorMin; q.y = v; rt.anchorMin = q; }
                    else if (a == H("m_AnchorMax.x")) { var q = rt.m_AnchorMax; q.x = v; rt.anchorMax = q; }
                    else if (a == H("m_AnchorMax.y")) { var q = rt.m_AnchorMax; q.y = v; rt.anchorMax = q; }
                    else if (a == H("m_Pivot.x")) { var q = rt.m_Pivot; q.x = v; rt.pivot = q; }
                    else if (a == H("m_Pivot.y")) { var q = rt.m_Pivot; q.y = v; rt.pivot = q; }
                    else if (a == H("m_LocalScale.x")) { var q = rt.m_LocalScale; q.x = v; rt.m_LocalScale = q; }
                    else if (a == H("m_LocalScale.y")) { var q = rt.m_LocalScale; q.y = v; rt.m_LocalScale = q; }
                    else if (a == H("m_LocalScale.z")) { var q = rt.m_LocalScale; q.z = v; rt.m_LocalScale = q; }
                    else if (a == H("m_LocalPosition.z")) { var q = rt.m_LocalPosition; q.z = v; rt.m_LocalPosition = q; }
                    else if (a == 1 || a == 3) { float x = sample[b.curveIndex], y = sample[b.curveIndex + 1], z = sample[b.curveIndex + 2]; if (a == 1) rt.m_LocalPosition = new Vector3(x, y, z); else rt.m_LocalScale = new Vector3(x, y, z); }
                    else Warn("RectTransform attr " + a);
                    return;
                }
                default:
                {
                    // Behaviour enable flags on other components (Collider2D, etc.)
                    if (b.attribute == H("m_Enabled"))
                        foreach (var comp in tr.gameObject.m_Components)
                        {
                            var bh = comp as Behaviour;
                            if (bh != null && World.ClassIdOf(comp) == b.typeID) { bh.enabled = v > 0.5f; return; }
                        }
                    Warn("binding type " + b.typeID);
                    return;
                }
            }
        }

        static readonly Dictionary<string, uint> hashCache = new Dictionary<string, uint>();
        static uint H(string s) { uint h; if (!hashCache.TryGetValue(s, out h)) { h = (uint)Animator.StringToHash(s); hashCache[s] = h; } return h; }
        struct FieldPath { public System.Reflection.FieldInfo f; public int comp; }
        static readonly Dictionary<Type, Dictionary<uint, FieldPath>> fieldHashes = new Dictionary<Type, Dictionary<uint, FieldPath>>();
        static readonly string[] Comps = { "x", "y", "z", "w" }, ColComps = { "r", "g", "b", "a" };
        static FieldPath FieldFor(Type t, uint h)
        {
            Dictionary<uint, FieldPath> d;
            if (!fieldHashes.TryGetValue(t, out d))
            {
                d = new Dictionary<uint, FieldPath>();
                foreach (var f in World.SerializedFields(t))
                {
                    d[H(f.Name)] = new FieldPath { f = f, comp = -1 };
                    var ft = f.FieldType;
                    var names = ft == typeof(Color) ? ColComps : (ft == typeof(Vector2) || ft == typeof(Vector3) || ft == typeof(Vector4)) ? Comps : null;
                    if (names != null) for (int i = 0; i < names.Length; i++) d[H(f.Name + "." + names[i])] = new FieldPath { f = f, comp = i };
                }
                fieldHashes[t] = d;
            }
            FieldPath r; d.TryGetValue(h, out r); return r;
        }
        static void SetComponent(System.Reflection.FieldInfo f, object o, int i, float v)
        {
            var ft = f.FieldType; var cur = f.GetValue(o);
            if (ft == typeof(Color)) { var c = (Color)cur; if (i == 0) c.r = v; else if (i == 1) c.g = v; else if (i == 2) c.b = v; else c.a = v; f.SetValue(o, c); }
            else if (ft == typeof(Vector2)) { var c = (Vector2)cur; if (i == 0) c.x = v; else c.y = v; f.SetValue(o, c); }
            else if (ft == typeof(Vector3)) { var c = (Vector3)cur; if (i == 0) c.x = v; else if (i == 1) c.y = v; else c.z = v; f.SetValue(o, c); }
            else if (ft == typeof(Vector4)) { var c = (Vector4)cur; if (i == 0) c.x = v; else if (i == 1) c.y = v; else if (i == 2) c.z = v; else c.w = v; f.SetValue(o, c); }
        }
        static void Warn(string w) { if (warned.Add(w)) Log.Write("WARN", "animator: unhandled " + w); }
    }

    public sealed class MonoScriptRef : UnityEngine.Object { }

    public static class AnimationSystem
    {
        public static void Step()
        {
            float dt = Time.deltaTime;
            var all = Animator.s_All;
            int count = all.Count;
            for (int i = 0; i < count; i++)
            {
                var a = all[i];
                if (a == null || a.m_Destroyed || !a.m_Enabled || a.m_Controller == null) continue;
                var g = a.m_GameObject;
                if (g == null || g.m_Destroyed || !g.activeInHierarchy) continue;
                var rt = a.Rt;
                if (rt != null) { try { rt.Step(a.m_UpdateMode == AnimatorUpdateMode.UnscaledTime ? Time.unscaledDeltaTime : dt); } catch (Exception e) { Lifecycle.Fail(a, "Animator", e); } }
            }
        }
    }
}
