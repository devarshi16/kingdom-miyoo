// Clean-room: shader globals, system info, UnityEvents, particle systems (state only; Kingdom drives
// most particles through Particle Playground's SetParticles, which we keep for the renderer),
// lights/trails/gizmos/JSON/cursor/quality helpers.
using System;
using System.Collections.Generic;
using UnityEngine.Events;

namespace UnityEngine
{
    public sealed partial class Shader
    {
        internal static readonly Dictionary<string, float> s_Floats = new Dictionary<string, float>();
        internal static readonly Dictionary<string, Vector4[]> s_VectorArrays = new Dictionary<string, Vector4[]>();
        internal static readonly Dictionary<string, Vector4> s_Vectors = new Dictionary<string, Vector4>();
        internal static readonly Dictionary<string, Color> s_Colors = new Dictionary<string, Color>();
        public static void SetGlobalFloat(string n, float v) { s_Floats[n] = v; }
        public static void SetGlobalFloat(int id, float v) { s_Floats[Shader.NameOf(id)] = v; }
        public static void SetGlobalVectorArray(string n, Vector4[] v) { s_VectorArrays[n] = v; }
        public static void SetGlobalVector(string n, Vector4 v) { s_Vectors[n] = v; }
        public static void SetGlobalColor(string n, Color c) { s_Colors[n] = c; }
        public static float GetGlobalFloat(string n) { float v; return s_Floats.TryGetValue(n, out v) ? v : 0f; }
    }

    public sealed partial class SystemInfo
    {
        public static int processorCount { get { return 2; } }          // Miyoo Mini Plus: 2x Cortex-A7
        public static string deviceModel { get { return "Miyoo Mini Plus (KNL)"; } }
        public static string deviceName { get { return "knl"; } }
        public static OperatingSystemFamily operatingSystemFamily { get { return OperatingSystemFamily.Linux; } }
        public static int systemMemorySize { get { return 128; } }
        public static int graphicsMemorySize { get { return 0; } }
        public static string operatingSystem { get { return "Linux"; } }
    }

    public sealed partial class Camera
    {
        public void SetReplacementShader(Shader s, string tag) { }
        public int targetDisplay { get { return 0; } set { } }
        public Vector3 ScreenToViewportPoint(Vector3 p) { return new Vector3(p.x / pixelWidth, p.y / pixelHeight, p.z); }
        public Vector3 ViewportToScreenPoint(Vector3 p) { return new Vector3(p.x * pixelWidth, p.y * pixelHeight, p.z); }
        public Ray ScreenPointToRay(Vector3 p) { var w = ScreenToWorldPoint(new Vector3(p.x, p.y, 0)); return new Ray(new Vector3(w.x, w.y, transform.position.z), transform.forward); }
        internal int m_CullingMask = -1; internal float m_Fov = 60f; internal Rect m_ViewportRect = new Rect(0, 0, 1, 1);
        public int cullingMask { get { return m_CullingMask; } set { m_CullingMask = value; } }
        public float fieldOfView { get { return m_Fov; } set { m_Fov = value; } }
        public Rect rect { get { return m_ViewportRect; } set { m_ViewportRect = value; } }
        public Rect pixelRect { get { return new Rect(0, 0, pixelWidth, pixelHeight); } set { } }
        public Matrix4x4 worldToCameraMatrix { get { var m = transform.worldToLocalMatrix; m.m20 = -m.m20; m.m21 = -m.m21; m.m22 = -m.m22; m.m23 = -m.m23; return m; } set { } }
        public Matrix4x4 projectionMatrix
        {
            get
            {   // orthographic, OpenGL convention (near 0.3, far 1000)
                float h = orthographicSize, w = h * pixelWidth / Mathf.Max(1, pixelHeight), n = 0.3f, f = 1000f;
                var m = Matrix4x4.identity; m.m00 = 1f / w; m.m11 = 1f / h; m.m22 = -2f / (f - n); m.m23 = -(f + n) / (f - n); return m;
            }
            set { }
        }
        public void ResetReplacementShader() { }
    }

    public partial class Material
    {
        internal readonly Dictionary<string, Vector2> m_TexOffsets = new Dictionary<string, Vector2>();
        internal readonly Dictionary<string, Vector2> m_TexScales = new Dictionary<string, Vector2>();
        internal readonly HashSet<string> m_Keywords = new HashSet<string>();
        public Color GetColor(int id) { return GetColor(Shader.NameOf(id)); }
        public void SetColor(int id, Color c) { SetColor(Shader.NameOf(id), c); }
        public float GetFloat(int id) { return GetFloat(Shader.NameOf(id)); }
        public void SetFloat(int id, float f) { SetFloat(Shader.NameOf(id), f); }
        public void SetInt(string n, int v) { SetFloat(n, v); }
        public Vector4 GetVector(string n) { return GetColor(n); }
        public Vector4 GetVector(int id) { return GetColor(Shader.NameOf(id)); }
        public void SetVector(int id, Vector4 v) { SetColor(Shader.NameOf(id), v); }
        public Vector2 GetTextureOffset(string n) { Vector2 v; return m_TexOffsets.TryGetValue(n, out v) ? v : Vector2.zero; }
        public Vector2 GetTextureScale(string n) { Vector2 v; return m_TexScales.TryGetValue(n, out v) ? v : Vector2.one; }
        public void SetTextureScale(string n, Vector2 v) { m_TexScales[n] = v; }
        public Vector2 mainTextureOffset { get { return GetTextureOffset("_MainTex"); } set { m_TexOffsets["_MainTex"] = value; } }
        public Vector2 mainTextureScale { get { return GetTextureScale("_MainTex"); } set { m_TexScales["_MainTex"] = value; } }
        public void EnableKeyword(string k) { m_Keywords.Add(k); m_Shading = null; }
        public void DisableKeyword(string k) { m_Keywords.Remove(k); m_Shading = null; }
        public bool IsKeywordEnabled(string k) { return m_Keywords.Contains(k); }
    }

    public sealed partial class Texture2D
    {
        public Texture2D(int w, int h, TextureFormat f, bool mip) { m_Width = w; m_Height = h; }
        public void Apply() { }
        public void SetPixels(Color[] c) { }
        public Color32[] GetPixels32() { return new Color32[m_Width * m_Height]; }
        public Color GetPixelBilinear(float u, float v) { return Color.white; }
        public void ReadPixels(Rect r, int x, int y) { }
    }

    public partial class RenderTexture
    {
        public RenderTexture(int w, int h, int depth, RenderTextureFormat f) { m_Width = w; m_Height = h; }
        public void DiscardContents() { }
        public void Release() { }
        public bool Create() { return true; }
    }

    public sealed partial class Graphics
    {
        public static void Blit(Texture src, RenderTexture dst, Material m) { }
        public static void Blit(Texture src, RenderTexture dst) { }
    }

    public sealed partial class QualitySettings
    {
        public static void DecreaseLevel() { }
        public static void IncreaseLevel() { }
    }

    public sealed partial class Light
    {
        internal Color m_Color = Color.white; internal float m_Intensity = 1f, m_Range = 10f, m_Shadow = 1f; internal LightType m_Type = LightType.Point;
        public LightType type { get { return m_Type; } set { m_Type = value; } }
        public Color color { get { return m_Color; } set { m_Color = value; } }
        public float intensity { get { return m_Intensity; } set { m_Intensity = value; } }
        public float range { get { return m_Range; } set { m_Range = value; } }
        public float shadowStrength { get { return m_Shadow; } set { m_Shadow = value; } }
    }

    public sealed partial class TrailRenderer
    {
        float m_Start = 1f, m_End = 0f, m_Time = 1f;
        public float startWidth { get { return m_Start; } set { m_Start = value; } }
        public float endWidth { get { return m_End; } set { m_End = value; } }
        public float time { get { return m_Time; } set { m_Time = value; } }
        public void Clear() { }
    }

    public sealed partial class Gizmos
    {
        public static Color color { get; set; }
        public static void DrawCube(Vector3 c, Vector3 s) { }
        public static void DrawWireCube(Vector3 c, Vector3 s) { }
        public static void DrawLine(Vector3 a, Vector3 b) { }
        public static void DrawRay(Vector3 a, Vector3 d) { }
        public static void DrawSphere(Vector3 c, float r) { }
        public static void DrawWireSphere(Vector3 c, float r) { }
    }

    public enum LightType { Spot = 0, Directional = 1, Point = 2, Area = 3 }

    public sealed partial class Cursor
    {
        public static bool visible { get; set; }
        public static CursorLockMode lockState { get; set; }
    }

}

namespace UnityEngine.Events
{
    // persistent (inspector-wired) calls, serialized exactly like Unity's PersistentCallGroup
    [Serializable] public class ArgumentCache
    {
        [SerializeField] internal Object m_ObjectArgument; [SerializeField] internal string m_ObjectArgumentAssemblyTypeName;
        [SerializeField] internal int m_IntArgument; [SerializeField] internal float m_FloatArgument;
        [SerializeField] internal string m_StringArgument; [SerializeField] internal bool m_BoolArgument;
    }
    [Serializable] public class PersistentCall
    {
        [SerializeField] internal Object m_Target; [SerializeField] internal string m_MethodName;
        [SerializeField] internal int m_Mode; [SerializeField] internal ArgumentCache m_Arguments = new ArgumentCache();
        [SerializeField] internal int m_CallState = 2;
    }
    [Serializable] public class PersistentCallGroup { [SerializeField] internal List<PersistentCall> m_Calls = new List<PersistentCall>(); }

    public abstract partial class UnityEventBase
    {
        internal readonly List<Delegate> m_Runtime = new List<Delegate>();
        [SerializeField] internal PersistentCallGroup m_PersistentCalls = new PersistentCallGroup();
        public void RemoveAllListeners() { m_Runtime.Clear(); }
        public int GetPersistentEventCount() { return m_PersistentCalls != null ? m_PersistentCalls.m_Calls.Count : 0; }
        public Object GetPersistentTarget(int i) { return m_PersistentCalls.m_Calls[i].m_Target; }
        public string GetPersistentMethodName(int i) { return m_PersistentCalls.m_Calls[i].m_MethodName; }

        void InvokePersistent(object[] args)
        {
            if (m_PersistentCalls == null) return;
            foreach (var pc in m_PersistentCalls.m_Calls.ToArray())
            {
                if (pc.m_CallState == 0 || pc.m_Target == null || string.IsNullOrEmpty(pc.m_MethodName)) continue;
                object target = pc.m_Target; var a = pc.m_Arguments ?? new ArgumentCache();
                object[] callArgs; Type[] sig;
                switch (pc.m_Mode)
                {
                    case 1: callArgs = new object[0]; sig = Type.EmptyTypes; break;
                    case 2: callArgs = new object[] { a.m_ObjectArgument }; sig = null; break;
                    case 3: callArgs = new object[] { a.m_IntArgument }; sig = new[] { typeof(int) }; break;
                    case 4: callArgs = new object[] { a.m_FloatArgument }; sig = new[] { typeof(float) }; break;
                    case 5: callArgs = new object[] { a.m_StringArgument }; sig = new[] { typeof(string) }; break;
                    case 6: callArgs = new object[] { a.m_BoolArgument }; sig = new[] { typeof(bool) }; break;
                    default: callArgs = args ?? new object[0]; sig = null; break;      // event-defined arguments
                }
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
                System.Reflection.MethodInfo m = null;
                for (var t = target.GetType(); t != null && m == null; t = t.BaseType)
                    foreach (var cand in t.GetMethods(flags | System.Reflection.BindingFlags.DeclaredOnly))
                    {
                        if (cand.Name != pc.m_MethodName) continue;
                        var ps = cand.GetParameters();
                        if (ps.Length != callArgs.Length) continue;
                        if (sig != null) { bool ok = true; for (int i = 0; i < ps.Length; i++) if (ps[i].ParameterType != sig[i]) ok = false; if (!ok) continue; }
                        m = cand; break;
                    }
                if (m == null)
                {   // property setters appear as set_X in Unity's method names; also try those
                    if (KNL.Lifecycle.Verbose) KNL.Log.Write("WARN", "UnityEvent: no method " + pc.m_MethodName + " on " + target.GetType().Name);
                    continue;
                }
                try { m.Invoke(m.IsStatic ? null : target, callArgs); }
                catch (System.Reflection.TargetInvocationException e) { KNL.Lifecycle.Fail(target, "UnityEvent " + pc.m_MethodName, e.InnerException ?? e); }
            }
        }

        internal void InvokeAll(object[] args)
        {
            InvokePersistent(args);
            foreach (var d in m_Runtime.ToArray())
            {
                try { d.DynamicInvoke(args); }
                catch (System.Reflection.TargetInvocationException e) { KNL.Lifecycle.Fail(d.Target ?? this, "UnityEvent", e.InnerException ?? e); }
            }
        }
    }
    public partial class UnityEvent
    {
        public UnityEvent() { }
        public void AddListener(UnityAction a) { if (a != null) m_Runtime.Add(a); }
        public void RemoveListener(UnityAction a) { m_Runtime.Remove(a); }
        public void Invoke() { InvokeAll(null); }
    }
    public abstract partial class UnityEvent<T0>
    {
        public void AddListener(UnityAction<T0> a) { if (a != null) m_Runtime.Add(a); }
        public void RemoveListener(UnityAction<T0> a) { m_Runtime.Remove(a); }
        public void Invoke(T0 arg) { InvokeAll(new object[] { arg }); }
    }
}

namespace UnityEngine
{
    public sealed partial class ParticleSystem
    {
        internal bool m_Playing = true, m_Paused, m_EmissionEnabled = true, m_SeparateAxes;
        internal ParticleSystemSimulationSpace m_SimSpace; internal float m_StartSizeMul = 1f;
        internal Particle[] m_Particles = new Particle[0]; internal int m_Count;
        public MainModule main { get { return new MainModule(this); } }
        public EmissionModule emission { get { return new EmissionModule(this); } }
        public RotationOverLifetimeModule rotationOverLifetime { get { return new RotationOverLifetimeModule(this); } }
        public ParticleSystemSimulationSpace simulationSpace { get { return m_SimSpace; } set { m_SimSpace = value; } }
        public bool isPlaying { get { return m_Playing && !m_Paused; } }
        public bool isPaused { get { return m_Paused; } }
        public bool isStopped { get { return !m_Playing; } }
        public int particleCount { get { return m_Count; } }
        public void Play() { m_Playing = true; m_Paused = false; }
        public void Play(bool withChildren) { Play(); }
        public void Stop() { m_Playing = false; }
        public void Stop(bool withChildren) { Stop(); }
        public void Pause() { m_Paused = true; }
        public void Clear() { m_Count = 0; }
        public void Clear(bool withChildren) { m_Count = 0; }
        public void Emit(int n) { }
        public int GetParticles(Particle[] dst)
        {
            int n = Mathf.Min(m_Count, dst != null ? dst.Length : 0);
            Array.Copy(m_Particles, dst, n);
            return n;
        }
        public void SetParticles(Particle[] src, int count)
        {
            count = Mathf.Min(count, src != null ? src.Length : 0);
            if (m_Particles.Length < count) m_Particles = new Particle[count];
            Array.Copy(src, m_Particles, count);
            m_Count = count;
        }

        public partial struct MainModule
        {
            internal ParticleSystem m_PS;
            internal MainModule(ParticleSystem ps) { m_PS = ps; }
            public ParticleSystemSimulationSpace simulationSpace { get { return m_PS.m_SimSpace; } set { m_PS.m_SimSpace = value; } }
            public float startSizeMultiplier { get { return m_PS.m_StartSizeMul; } set { m_PS.m_StartSizeMul = value; } }
        }
        public partial struct EmissionModule
        {
            internal ParticleSystem m_PS;
            internal EmissionModule(ParticleSystem ps) { m_PS = ps; }
            public bool enabled { get { return m_PS.m_EmissionEnabled; } set { m_PS.m_EmissionEnabled = value; } }
        }
        public partial struct RotationOverLifetimeModule
        {
            internal ParticleSystem m_PS;
            internal RotationOverLifetimeModule(ParticleSystem ps) { m_PS = ps; }
            public bool separateAxes { get { return m_PS.m_SeparateAxes; } set { m_PS.m_SeparateAxes = value; } }
        }
        public partial struct Particle
        {
            Vector3 m_Position, m_Velocity, m_Rotation3D; Color32 m_StartColor; float m_Lifetime, m_StartLifetime, m_StartSize, m_Rotation;
            public Vector3 position { get { return m_Position; } set { m_Position = value; } }
            public Vector3 velocity { get { return m_Velocity; } set { m_Velocity = value; } }
            public Vector3 rotation3D { get { return m_Rotation3D; } set { m_Rotation3D = value; } }
            public Color32 startColor { get { return m_StartColor; } set { m_StartColor = value; } }
            public float remainingLifetime { get { return m_Lifetime; } set { m_Lifetime = value; } }
            public float startLifetime { get { return m_StartLifetime; } set { m_StartLifetime = value; } }
            public float startSize { get { return m_StartSize; } set { m_StartSize = value; } }
            public float rotation { get { return m_Rotation; } set { m_Rotation = value; } }
        }
    }

    public sealed partial class ParticleSystemRenderer
    {
        internal ParticleSystemRenderMode m_RenderMode;
        public ParticleSystemRenderMode renderMode { get { return m_RenderMode; } set { m_RenderMode = value; } }
    }
}

namespace UnityEngine.Analytics
{
    public partial class Analytics
    {
        public static AnalyticsResult CustomEvent(string name, System.Collections.Generic.IDictionary<string, object> data) { return AnalyticsResult.Ok; }
        public static AnalyticsResult CustomEvent(string name) { return AnalyticsResult.Ok; }
    }
}

namespace UnityEngine
{
    public sealed partial class Display
    {
        // one display, the 320x240 framebuffer; no mouse on the device
        static Display s_Main = new Display();
        public static Display[] displays = new Display[] { s_Main };
        public static Display main { get { return s_Main; } }
        public static Vector3 RelativeMouseAt(Vector3 p) { return new Vector3(p.x, p.y, 0); }
        public int renderingWidth { get { return KNL.RenderSystem.Width; } }
        public int renderingHeight { get { return KNL.RenderSystem.Height; } }
        public int systemWidth { get { return KNL.RenderSystem.Width; } }
        public int systemHeight { get { return KNL.RenderSystem.Height; } }
        public bool active { get { return true; } }
        public void Activate() { }
    }
}
