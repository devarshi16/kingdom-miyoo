// Clean-room scene management, async ops, yield instructions, resources, prefs and input.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.SceneManagement;

namespace UnityEngine
{
    public partial class AsyncOperation
    {
        internal float m_Progress; internal bool m_IsDone;
        bool m_AllowActivation = true;
        public bool isDone { get { return m_IsDone; } }
        public float progress { get { return m_Progress; } }
        public bool allowSceneActivation { get { return m_AllowActivation; } set { m_AllowActivation = value; } }
        public int priority { get; set; }
    }
    public sealed partial class ResourceRequest
    {
        internal Object m_Asset;
        public Object asset { get { return m_Asset; } }
    }

    public abstract partial class CustomYieldInstruction
    {
        public abstract bool keepWaiting { get; }
    }
    public partial class WaitForSecondsRealtime
    {
        float m_Until;
        public WaitForSecondsRealtime(float time) { m_Until = Time.unscaledTime + time; }
        public override bool keepWaiting { get { return Time.unscaledTime < m_Until; } }
    }
    public partial class WWW { public override bool keepWaiting { get { return false; } } }
    public sealed partial class WaitForEndOfFrame { public WaitForEndOfFrame() { } }
    public sealed partial class WaitForFixedUpdate { public WaitForFixedUpdate() { } }

    public sealed partial class Resources
    {
        static Dictionary<string, object> s_Map;   // lower-case path -> PPtr dict (globalgamemanagers ResourceManager)
        static void Init()
        {
            if (s_Map != null) return;
            s_Map = new Dictionary<string, object>();
            var gg = KNL.World.File("globalgamemanagers");
            foreach (var e in gg.objects.Values)
            {
                if ((string)e["type"] != "ResourceManager" || !e.ContainsKey("data")) continue;
                foreach (List<object> kv in (List<object>)((Dictionary<string, object>)e["data"])["m_Container"])
                {
                    string p = ((string)kv[0]).ToLowerInvariant();
                    if (!s_Map.ContainsKey(p)) s_Map[p] = kv[1];
                }
            }
        }
        static IEnumerable<Object> Candidates(string path)
        {
            Init();
            var gg = KNL.World.File("globalgamemanagers");
            string p = path.ToLowerInvariant();
            object pptr;
            if (s_Map.TryGetValue(p, out pptr)) { var o = KNL.World.Resolve(gg, pptr); if (o != null) yield return o; }
        }
        public static Object Load(string path) { return Candidates(path).FirstOrDefault(); }
        public static Object Load(string path, Type t) { return Candidates(path).FirstOrDefault(o => t.IsInstanceOfType(o)); }
        public static T Load<T>(string path) where T : Object
        {
            var o = Load(path);
            if (o is T) return (T)o;
            var go = o as GameObject;   // Unity lets Load<Component>() return a prefab's component
            if (go != null && typeof(Component).IsAssignableFrom(typeof(T))) return (T)(object)go.GetComponent(typeof(T));
            return null;
        }
        public static T[] LoadAll<T>(string path) where T : Object
        {
            Init();
            var gg = KNL.World.File("globalgamemanagers");
            string pre = path.ToLowerInvariant().TrimEnd('/') + "/";
            var r = new List<T>();
            foreach (var kv in s_Map)
                if (kv.Key.StartsWith(pre) || kv.Key == path.ToLowerInvariant())
                {
                    var o = KNL.World.Resolve(gg, kv.Value);
                    if (o is T) r.Add((T)o);
                    else if (o is GameObject && typeof(Component).IsAssignableFrom(typeof(T))) { var c = ((GameObject)o).GetComponent(typeof(T)); if (c != null) r.Add((T)(object)c); }
                }
            return r.ToArray();
        }
        public static ResourceRequest LoadAsync(string path)
        { var rq = new ResourceRequest(); rq.m_Asset = Load(path); rq.m_Progress = 1f; rq.m_IsDone = true; return rq; }
        /// Unlike FindObjectsOfType this includes inactive objects and loaded assets (Unity semantics).
        public static T[] FindObjectsOfTypeAll<T>() where T : Object { return KNL.World.FindAllObjects(typeof(T)).OfType<T>().ToArray(); }
        public static Object[] FindObjectsOfTypeAll(Type t) { return KNL.World.FindAllObjects(t).ToArray(); }
        public static AsyncOperation UnloadUnusedAssets() { var op = new AsyncOperation(); op.m_IsDone = true; op.m_Progress = 1; return op; }
        public static T GetBuiltinResource<T>(string path) where T : Object { return null; }
        public static void UnloadAsset(Object o) { }
    }

    public sealed partial class PlayerPrefs
    {
        static Dictionary<string, string> s_Data;
        static string FilePath { get { return Path.Combine(KNL.Paths.SaveDir, "playerprefs.txt"); } }
        static void Init()
        {
            if (s_Data != null) return;
            s_Data = new Dictionary<string, string>();
            if (!File.Exists(FilePath)) return;
            foreach (var line in File.ReadAllLines(FilePath))
            {
                int i = line.IndexOf('\t');
                if (i > 0) s_Data[Uri.UnescapeDataString(line.Substring(0, i))] = Uri.UnescapeDataString(line.Substring(i + 1));
            }
        }
        public static bool HasKey(string k) { Init(); return s_Data.ContainsKey(k); }
        public static void DeleteKey(string k) { Init(); s_Data.Remove(k); }
        public static void DeleteAll() { Init(); s_Data.Clear(); }
        public static string GetString(string k) { return GetString(k, ""); }
        public static string GetString(string k, string d) { Init(); string v; return s_Data.TryGetValue(k, out v) ? v : d; }
        public static int GetInt(string k) { return GetInt(k, 0); }
        public static int GetInt(string k, int d) { Init(); string v; int r; return s_Data.TryGetValue(k, out v) && int.TryParse(v, out r) ? r : d; }
        public static float GetFloat(string k) { return GetFloat(k, 0f); }
        public static float GetFloat(string k, float d) { Init(); string v; float r; return s_Data.TryGetValue(k, out v) && float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out r) ? r : d; }
        public static void SetString(string k, string v) { Init(); s_Data[k] = v ?? ""; }
        public static void SetInt(string k, int v) { Init(); s_Data[k] = v.ToString(); }
        public static void SetFloat(string k, float v) { Init(); s_Data[k] = v.ToString("R", System.Globalization.CultureInfo.InvariantCulture); }
        public static void Save()
        {
            Init();
            Directory.CreateDirectory(KNL.Paths.SaveDir);
            File.WriteAllLines(FilePath, s_Data.Select(kv => Uri.EscapeDataString(kv.Key) + "\t" + Uri.EscapeDataString(kv.Value)).ToArray());
        }
    }

    /// Keyboard/pad state from the native platform layer (desktop: keyboard; device: buttons).
    public sealed partial class Input
    {
        internal const uint Left = 1, Right = 2, Up = 4, Down = 8, A = 16, B = 32, X = 64, Y = 128, Start = 256, Select = 512, L = 1024, R = 2048, Menu = 4096;
        internal static void BeginFrame() { KNL.Buttons.BeginFrame(); }
        internal static bool Held(uint m) { return KNL.Buttons.Held(m); }
        internal static bool Pressed(uint m) { return KNL.Buttons.Pressed(m); }
        internal static bool Released(uint m) { return KNL.Buttons.Released(m); }
        static uint Map(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.LeftArrow: case KeyCode.A: return Left;
                case KeyCode.RightArrow: case KeyCode.D: return Right;
                case KeyCode.UpArrow: case KeyCode.W: return Up;
                case KeyCode.DownArrow: case KeyCode.S: return Down;
                case KeyCode.Space: case KeyCode.Return: case KeyCode.Z: return A;
                case KeyCode.LeftShift: case KeyCode.X: return B;
                case KeyCode.Escape: return Start;
                case KeyCode.Tab: return Select;
                case KeyCode.C: return B;
                default: return 0;
            }
        }
        static uint Map(string k) { try { return Map((KeyCode)Enum.Parse(typeof(KeyCode), k, true)); } catch { return 0; } }
        public static bool GetKey(KeyCode k) { return !Hidden(k) && Held(Map(k)); }
        static bool Hidden(KeyCode k)
        {
            return KNL.Buttons.GamepadMode && (k == KeyCode.LeftArrow || k == KeyCode.RightArrow || k == KeyCode.UpArrow || k == KeyCode.DownArrow
                || k == KeyCode.A || k == KeyCode.D || k == KeyCode.W || k == KeyCode.S);
        }
        public static bool GetKeyDown(KeyCode k) { return !Hidden(k) && Pressed(Map(k)); }
        public static bool GetKeyUp(KeyCode k) { return Released(Map(k)); }
        public static bool GetKey(string k) { return Held(Map(k)); }
        public static bool GetKeyDown(string k) { return Pressed(Map(k)); }
        public static bool GetKeyUp(string k) { return Released(Map(k)); }
        public static bool GetButton(string b) { return false; }
        public static bool GetButtonDown(string b) { return false; }
        public static bool GetButtonUp(string b) { return false; }
        public static float GetAxis(string a) { return GetAxisRaw(a); }
        public static float GetAxisRaw(string a)
        {
            if (a == "Horizontal") return (Held(Right) ? 1f : 0f) - (Held(Left) ? 1f : 0f);
            if (a == "Vertical") return (Held(Up) ? 1f : 0f) - (Held(Down) ? 1f : 0f);
            return 0f;
        }
        public static bool anyKey { get { return KNL.Buttons.s_Now != 0; } }
        public static bool anyKeyDown { get { return (KNL.Buttons.s_Now & ~KNL.Buttons.s_Prev) != 0; } }
        public static Vector3 mousePosition { get { return Vector3.zero; } }
        public static Vector2 mouseScrollDelta { get { return Vector2.zero; } }
        public static bool mousePresent { get { return false; } }
        public static bool GetMouseButton(int b) { return false; }
        public static bool GetMouseButtonDown(int b) { return false; }
        public static bool GetMouseButtonUp(int b) { return false; }
        public static int touchCount { get { return 0; } }
        public static bool touchSupported { get { return false; } }
        public static Touch GetTouch(int i) { return default(Touch); }
        public static Touch[] touches { get { return new Touch[0]; } }
        public static string inputString { get { return ""; } }
        public static string compositionString { get { return ""; } }
        public static Vector2 compositionCursorPos { get; set; }
        public static IMECompositionMode imeCompositionMode { get; set; }
        public static bool imeIsSelected { get { return false; } }
        public static bool multiTouchEnabled { get; set; }
        public static bool simulateMouseWithTouches { get; set; }
        public static bool backButtonLeavesApp { get; set; }
        public static bool compensateSensors { get; set; }
        public static bool eatKeyPressOnTextFieldFocus { get; set; }
        public static bool isGyroAvailable { get { return false; } }
        public static bool stylusTouchSupported { get { return false; } }
        public static bool touchPressureSupported { get { return false; } }
        public static Vector3 acceleration { get { return Vector3.zero; } }
        public static int accelerationEventCount { get { return 0; } }
        public static AccelerationEvent[] accelerationEvents { get { return new AccelerationEvent[0]; } }
        public static AccelerationEvent GetAccelerationEvent(int i) { return default(AccelerationEvent); }
        public static string[] GetJoystickNames() { return new string[0]; }
        public static void ResetInputAxes() { KNL.Buttons.s_Now = KNL.Buttons.s_Prev = 0; }
        public static DeviceOrientation deviceOrientation { get { return DeviceOrientation.LandscapeLeft; } }
    }
}

namespace UnityEngine.SceneManagement
{
    public partial struct Scene
    {
        internal KNL.SceneTag m_Tag;
        internal Scene(KNL.SceneTag t) { m_Tag = t; }
        public string name { get { return m_Tag != null ? m_Tag.displayName : ""; } }
        public int buildIndex { get { return m_Tag != null ? m_Tag.buildIndex : -1; } }
        public bool isLoaded { get { return m_Tag != null && KNL.SceneLoader.Loaded.Contains(m_Tag); } }
        public bool IsValid() { return m_Tag != null; }
        public int handle { get { return m_Tag != null ? m_Tag.GetHashCode() : 0; } }
        public GameObject[] GetRootGameObjects() { var t = m_Tag; return KNL.World.Roots().Where(g => g.m_Scene == t).ToArray(); }
        public int rootCount { get { return GetRootGameObjects().Length; } }
        public static bool operator ==(Scene a, Scene b) { return a.m_Tag == b.m_Tag; }
        public static bool operator !=(Scene a, Scene b) { return a.m_Tag != b.m_Tag; }
        public override bool Equals(object o) { return o is Scene && ((Scene)o).m_Tag == m_Tag; }
        public override int GetHashCode() { return handle; }
    }

    public partial class SceneManager
    {
        public static int sceneCount { get { return KNL.SceneLoader.Loaded.Count; } }
        public static int sceneCountInBuildSettings { get { return KNL.SceneLoader.BuildScenes.Length; } }
        public static Scene GetActiveScene() { return new Scene(KNL.World.ActiveScene); }
        public static bool SetActiveScene(Scene s) { if (s.m_Tag == null) return false; KNL.World.ActiveScene = s.m_Tag; return true; }
        public static Scene GetSceneAt(int i) { return new Scene(KNL.SceneLoader.Loaded[i]); }
        public static Scene GetSceneByName(string n) { return new Scene(KNL.SceneLoader.Loaded.FirstOrDefault(t => t.displayName == n)); }
        public static Scene GetSceneByBuildIndex(int i) { return new Scene(KNL.SceneLoader.Loaded.FirstOrDefault(t => t.buildIndex == i)); }
        public static void LoadScene(string n) { LoadScene(n, LoadSceneMode.Single); }
        public static void LoadScene(string n, LoadSceneMode m) { KNL.SceneLoader.Request(n, m == LoadSceneMode.Additive); }
        public static void LoadScene(int i) { LoadScene(KNL.SceneLoader.BuildScenes[i], LoadSceneMode.Single); }
        public static void LoadScene(int i, LoadSceneMode m) { LoadScene(KNL.SceneLoader.BuildScenes[i], m); }
        public static AsyncOperation LoadSceneAsync(string n) { return LoadSceneAsync(n, LoadSceneMode.Single); }
        public static AsyncOperation LoadSceneAsync(string n, LoadSceneMode m) { return KNL.SceneLoader.Request(n, m == LoadSceneMode.Additive); }
        public static AsyncOperation LoadSceneAsync(int i, LoadSceneMode m) { return LoadSceneAsync(KNL.SceneLoader.BuildScenes[i], m); }
        public static bool UnloadScene(string n) { return KNL.SceneLoader.Unload(n); }
        public static AsyncOperation UnloadSceneAsync(string n)
        { KNL.SceneLoader.Unload(n); var op = new AsyncOperation(); op.m_IsDone = true; op.m_Progress = 1; return op; }
    }
}
