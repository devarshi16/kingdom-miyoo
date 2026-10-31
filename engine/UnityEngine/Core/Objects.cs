// Clean-room implementation of Unity's object model (public API semantics only).
using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{
    public partial class Object
    {
        static int s_nextId = 1;
        internal int m_InstanceID;
        internal bool m_Destroyed;
        string m_Name = "";

        public Object() { m_InstanceID = s_nextId++; }
        internal void EnsureId() { if (m_InstanceID == 0) m_InstanceID = s_nextId++; }

        public string name { get { return m_Name; } set { m_Name = value ?? ""; } }
        public HideFlags hideFlags { get; set; }
        public int GetInstanceID() { return m_InstanceID; }

        // Unity semantics: a destroyed object compares equal to null.
        static bool CompareBaseObjects(Object a, Object b)
        {
            bool an = ReferenceEquals(a, null) || a.m_Destroyed;
            bool bn = ReferenceEquals(b, null) || b.m_Destroyed;
            if (an && bn) return true;
            if (an || bn) return false;
            return ReferenceEquals(a, b);
        }
        public static bool operator ==(Object a, Object b) { return CompareBaseObjects(a, b); }
        public static bool operator !=(Object a, Object b) { return !CompareBaseObjects(a, b); }
        public static implicit operator bool(Object o) { return !CompareBaseObjects(o, null); }
        public override bool Equals(object o)
        {
            var other = o as Object;
            if (other == null && o != null && !(o is Object)) return false;
            return CompareBaseObjects(this, other);
        }
        public override int GetHashCode() { return m_InstanceID; }
        public override string ToString() { return string.Format("{0} ({1})", name, GetType().FullName); }

        public static void Destroy(Object obj) { Destroy(obj, 0f); }
        public static void Destroy(Object obj, float t) { KNL.World.ScheduleDestroy(obj, t); }
        public static void DestroyImmediate(Object obj) { KNL.World.DestroyNow(obj); }
        public static void DestroyImmediate(Object obj, bool allowAssets) { KNL.World.DestroyNow(obj); }
        public static void DontDestroyOnLoad(Object target) { KNL.World.MarkPersistent(target); }

        public static Object[] FindObjectsOfType(Type type) { return KNL.World.FindObjectsOfType(type).ToArray(); }
        public static T[] FindObjectsOfType<T>() where T : Object { return KNL.World.FindObjectsOfType(typeof(T)).ConvertAll(o => (T)o).ToArray(); }
        public static Object FindObjectOfType(Type type) { var l = KNL.World.FindObjectsOfType(type); return l.Count > 0 ? l[0] : null; }
        public static T FindObjectOfType<T>() where T : Object { return (T)FindObjectOfType(typeof(T)); }

        public static T Instantiate<T>(T original) where T : Object { return (T)KNL.World.Instantiate(original, null, false, null); }
        public static Object Instantiate(Object original) { return KNL.World.Instantiate(original, null, false, null); }
        public static T Instantiate<T>(T original, Transform parent) where T : Object { return (T)KNL.World.Instantiate(original, parent, false, null); }
        public static T Instantiate<T>(T original, Transform parent, bool worldPositionStays) where T : Object { return (T)KNL.World.Instantiate(original, parent, worldPositionStays, null); }
        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation) where T : Object
        { return (T)KNL.World.Instantiate(original, null, false, new KNL.Pose(position, rotation)); }
        public static Object Instantiate(Object original, Vector3 position, Quaternion rotation)
        { return KNL.World.Instantiate(original, null, false, new KNL.Pose(position, rotation)); }
        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation, Transform parent) where T : Object
        { return (T)KNL.World.Instantiate(original, parent, false, new KNL.Pose(position, rotation)); }
    }

    public partial class Component
    {
        internal GameObject m_GameObject;
        public GameObject gameObject { get { return m_GameObject; } }
        public Transform transform { get { return m_GameObject != null ? m_GameObject.transform : null; } }
        public string tag { get { return m_GameObject.tag; } set { m_GameObject.tag = value; } }
        public bool CompareTag(string t) { return m_GameObject.CompareTag(t); }
        public Component GetComponent(Type t) { return m_GameObject.GetComponent(t); }
        public Component GetComponent(string t) { return m_GameObject.GetComponent(t); }
        public T GetComponent<T>() { return m_GameObject.GetComponent<T>(); }
        public T GetComponentInChildren<T>() { return m_GameObject.GetComponentInChildren<T>(); }
        public T GetComponentInChildren<T>(bool includeInactive) { return m_GameObject.GetComponentInChildren<T>(includeInactive); }
        public Component GetComponentInChildren(Type t) { return m_GameObject.GetComponentInChildren(t, false); }
        public T GetComponentInParent<T>() { return m_GameObject.GetComponentInParent<T>(); }
        public Component GetComponentInParent(Type t) { return m_GameObject.GetComponentInParent(t); }
        public T[] GetComponents<T>() { return m_GameObject.GetComponents<T>(); }
        public void GetComponents<T>(List<T> results) { m_GameObject.GetComponents<T>(results); }
        public Component[] GetComponents(Type t) { return m_GameObject.GetComponents(t); }
        public T[] GetComponentsInChildren<T>() { return m_GameObject.GetComponentsInChildren<T>(false); }
        public T[] GetComponentsInChildren<T>(bool includeInactive) { return m_GameObject.GetComponentsInChildren<T>(includeInactive); }
        public void GetComponentsInChildren<T>(List<T> results) { m_GameObject.GetComponentsInChildren<T>(false, results); }
        public void GetComponentsInChildren<T>(bool includeInactive, List<T> results) { m_GameObject.GetComponentsInChildren<T>(includeInactive, results); }
        public Component[] GetComponentsInChildren(Type t, bool includeInactive) { return m_GameObject.GetComponentsInChildren(t, includeInactive); }
        public T[] GetComponentsInParent<T>() { return m_GameObject.GetComponentsInParent<T>(false); }
        public T[] GetComponentsInParent<T>(bool includeInactive) { return m_GameObject.GetComponentsInParent<T>(includeInactive); }
        public void SendMessage(string m) { m_GameObject.SendMessage(m, null, SendMessageOptions.RequireReceiver); }
        public void SendMessage(string m, object v) { m_GameObject.SendMessage(m, v, SendMessageOptions.RequireReceiver); }
        public void SendMessage(string m, object v, SendMessageOptions o) { m_GameObject.SendMessage(m, v, o); }
        public void BroadcastMessage(string m, object v, SendMessageOptions o) { m_GameObject.BroadcastMessage(m, v, o); }
    }

    public partial class Behaviour
    {
        internal bool m_Enabled = true;
        public bool enabled
        {
            get { return m_Enabled; }
            set { if (m_Enabled == value) return; m_Enabled = value; KNL.World.OnBehaviourEnabledChanged(this); }
        }
        public bool isActiveAndEnabled { get { return m_Enabled && m_GameObject != null && m_GameObject.activeInHierarchy; } }
    }

    public partial class MonoBehaviour
    {
        public bool useGUILayout { get; set; }
        public Coroutine StartCoroutine(IEnumerator routine) { return KNL.Coroutines.Start(this, routine); }
        public Coroutine StartCoroutine(string methodName) { return StartCoroutine(methodName, null); }
        public Coroutine StartCoroutine(string methodName, object value)
        {
            var m = KNL.Reflect.FindMethod(GetType(), methodName);
            if (m == null) { Debug.LogError("Coroutine '" + methodName + "' couldn't be started!"); return null; }
            var e = (IEnumerator)m.Invoke(this, m.GetParameters().Length == 0 ? null : new[] { value });
            return KNL.Coroutines.Start(this, e, methodName);
        }
        public void StopCoroutine(IEnumerator routine) { KNL.Coroutines.Stop(this, routine); }
        public void StopCoroutine(Coroutine routine) { KNL.Coroutines.Stop(this, routine); }
        public void StopCoroutine(string methodName) { KNL.Coroutines.Stop(this, methodName); }
        public void StopAllCoroutines() { KNL.Coroutines.StopAll(this); }
        public void Invoke(string methodName, float time) { KNL.Invokes.Add(this, methodName, time, -1f); }
        public void InvokeRepeating(string methodName, float time, float repeatRate) { KNL.Invokes.Add(this, methodName, time, repeatRate); }
        public void CancelInvoke() { KNL.Invokes.Cancel(this, null); }
        public void CancelInvoke(string methodName) { KNL.Invokes.Cancel(this, methodName); }
        public bool IsInvoking(string methodName) { return KNL.Invokes.IsInvoking(this, methodName); }
        public static void print(object message) { Debug.Log(message); }
    }

    public partial class ScriptableObject
    {
        public static ScriptableObject CreateInstance(Type t) { return (ScriptableObject)KNL.World.CreateScriptInstance(t); }
        public static T CreateInstance<T>() where T : ScriptableObject { return (T)CreateInstance(typeof(T)); }
    }

    public partial class YieldInstruction { }
    public sealed partial class Coroutine { internal KNL.CoroutineState state; }
    public sealed partial class WaitForSeconds { internal float m_Seconds; public WaitForSeconds(float seconds) { m_Seconds = seconds; } }

    // ------------------------------------------------------------- GameObject
    public sealed partial class GameObject
    {
        internal readonly List<Component> m_Components = new List<Component>();
        internal bool m_IsActive = true;
        internal int m_Layer;
        internal string m_Tag = "Untagged";
        internal Transform m_Transform;
        internal KNL.SceneTag m_Scene;   // null for prefab/asset GameObjects

        public GameObject() : this("New Game Object") { }
        public GameObject(string name) { this.name = name; KNL.World.AttachNewGameObject(this); }
        public GameObject(string name, params Type[] components) : this(name) { foreach (var t in components) AddComponent(t); }
        internal GameObject(KNL.SceneTag scene, string name) { this.name = name; m_Scene = scene; }

        public Transform transform { get { return m_Transform; } }
        public GameObject gameObject { get { return this; } }
        public int layer { get { return m_Layer; } set { m_Layer = value; } }
        public string tag { get { return m_Tag; } set { m_Tag = value; } }
        public bool CompareTag(string t) { return m_Tag == t; }
        public bool activeSelf { get { return m_IsActive; } }

        internal static int s_ActiveTopologyVersion = 1;
        internal static int NewPass() { return ++s_ActiveTopologyVersion; }
        int c_ActivePass; bool c_Active;
        public bool activeInHierarchy
        {
            get
            {
                if (c_ActivePass == s_ActiveTopologyVersion) return c_Active;
                bool a = m_IsActive && m_Scene != null;
                if (a) { var p = m_Transform != null ? m_Transform.m_Parent : null; a = p == null || p.m_GameObject.activeInHierarchy; }
                c_ActivePass = s_ActiveTopologyVersion; c_Active = a;
                return a;
            }
        }
        internal bool ActiveInPass(int pass) { return activeInHierarchy; }
        public bool isStatic { get; set; }
        public void SetActive(bool value)
        {
            if (m_IsActive == value) return;
            m_IsActive = value;
            s_ActiveTopologyVersion++;
            KNL.World.OnActiveChanged(this);
        }
        public UnityEngine.SceneManagement.Scene scene { get { return default(UnityEngine.SceneManagement.Scene); } }

        public Component AddComponent(Type t) { return KNL.World.AddComponent(this, t); }
        public T AddComponent<T>() where T : Component { return (T)AddComponent(typeof(T)); }

        public Component GetComponent(Type t)
        {
            for (int i = 0; i < m_Components.Count; i++) if (t.IsInstanceOfType(m_Components[i])) return m_Components[i];
            return null;
        }
        public T GetComponent<T>() { return (T)(object)GetComponent(typeof(T)); }
        public Component GetComponent(string typeName)
        {
            foreach (var c in m_Components) if (c.GetType().Name == typeName) return c;
            return null;
        }
        public Component[] GetComponents(Type t) { var l = new List<Component>(); foreach (var c in m_Components) if (t.IsInstanceOfType(c)) l.Add(c); return l.ToArray(); }
        public T[] GetComponents<T>() { var l = new List<T>(); GetComponents(l); return l.ToArray(); }
        public void GetComponents<T>(List<T> results) { results.Clear(); foreach (var c in m_Components) if (c is T) results.Add((T)(object)c); }

        public Component GetComponentInChildren(Type t) { return GetComponentInChildren(t, false); }
        public Component GetComponentInChildren(Type t, bool includeInactive)
        {
            if (!includeInactive && !activeInHierarchy) return null;
            var c = GetComponent(t);
            if (c != null) return c;
            foreach (var ch in m_Transform.m_Children)
            {
                var r = ch.m_GameObject.GetComponentInChildren(t, includeInactive);
                if (r != null) return r;
            }
            return null;
        }
        public T GetComponentInChildren<T>() { return (T)(object)GetComponentInChildren(typeof(T), false); }
        public T GetComponentInChildren<T>(bool includeInactive) { return (T)(object)GetComponentInChildren(typeof(T), includeInactive); }
        public Component GetComponentInParent(Type t)
        {
            for (var tr = m_Transform; tr != null; tr = tr.parent)
            { var c = tr.m_GameObject.activeInHierarchy ? tr.m_GameObject.GetComponent(t) : null; if (c != null) return c; }
            return null;
        }
        public T GetComponentInParent<T>() { return (T)(object)GetComponentInParent(typeof(T)); }
        public Component[] GetComponentsInChildren(Type t, bool includeInactive)
        { var l = new List<Component>(); CollectChildren(t, includeInactive, l); return l.ToArray(); }
        internal void CollectChildren(Type t, bool includeInactive, List<Component> l)
        {
            if (!includeInactive && !activeInHierarchy) return;
            foreach (var c in m_Components) if (t.IsInstanceOfType(c)) l.Add(c);
            foreach (var ch in m_Transform.m_Children) ch.m_GameObject.CollectChildren(t, includeInactive, l);
        }
        public T[] GetComponentsInChildren<T>() { return GetComponentsInChildren<T>(false); }
        public T[] GetComponentsInChildren<T>(bool includeInactive) { var l = new List<T>(); GetComponentsInChildren(includeInactive, l); return l.ToArray(); }
        public void GetComponentsInChildren<T>(bool includeInactive, List<T> results)
        {
            results.Clear(); var l = new List<Component>(); CollectChildren(typeof(T), includeInactive, l);
            foreach (var c in l) results.Add((T)(object)c);
        }
        public void GetComponentsInChildren<T>(List<T> results) { GetComponentsInChildren(false, results); }
        public T[] GetComponentsInParent<T>(bool includeInactive)
        {
            var l = new List<T>();
            for (var tr = m_Transform; tr != null; tr = tr.parent)
                if (includeInactive || tr.m_GameObject.activeInHierarchy)
                    foreach (var c in tr.m_GameObject.m_Components) if (c is T) l.Add((T)(object)c);
            return l.ToArray();
        }

        public void SendMessage(string methodName) { SendMessage(methodName, null, SendMessageOptions.RequireReceiver); }
        public void SendMessage(string methodName, object value) { SendMessage(methodName, value, SendMessageOptions.RequireReceiver); }
        public void SendMessage(string methodName, SendMessageOptions options) { SendMessage(methodName, null, options); }
        public void SendMessage(string methodName, object value, SendMessageOptions options)
        {
            bool any = false;
            foreach (var c in m_Components.ToArray())
                if (c is MonoBehaviour) any |= KNL.Reflect.InvokeMessage(c, methodName, value);
            if (!any && options == SendMessageOptions.RequireReceiver)
                Debug.LogError("SendMessage " + methodName + " has no receiver!");
        }
        public void BroadcastMessage(string methodName, object value, SendMessageOptions options)
        {
            SendMessage(methodName, value, SendMessageOptions.DontRequireReceiver);
            foreach (var ch in m_Transform.m_Children.ToArray()) ch.m_GameObject.BroadcastMessage(methodName, value, SendMessageOptions.DontRequireReceiver);
        }

        public static GameObject Find(string name) { return KNL.World.FindGameObject(name); }
        public static GameObject FindWithTag(string tag) { return KNL.World.FindGameObjectWithTag(tag); }
        public static GameObject FindGameObjectWithTag(string tag) { return KNL.World.FindGameObjectWithTag(tag); }
        public static GameObject[] FindGameObjectsWithTag(string tag) { return KNL.World.FindGameObjectsWithTag(tag); }
        public static GameObject CreatePrimitive(PrimitiveType t) { return new GameObject(t.ToString()); }
    }

    public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }

    // -------------------------------------------------------------- Transform
    public partial class Transform : IEnumerable
    {
        internal Vector3 m_LocalPosition;
        internal Quaternion m_LocalRotation = Quaternion.identity;
        internal Vector3 m_LocalScale = Vector3.one;
        internal Transform m_Parent;
        internal List<Transform> m_Children = new List<Transform>();

        public Vector3 localPosition { get { return m_LocalPosition; } set { m_LocalPosition = value; } }
        public Quaternion localRotation { get { return m_LocalRotation; } set { m_LocalRotation = value; } }
        public Vector3 localScale { get { return m_LocalScale; } set { m_LocalScale = value; } }
        public Vector3 localEulerAngles { get { return m_LocalRotation.eulerAngles; } set { m_LocalRotation = Quaternion.Euler(value); } }

        public Matrix4x4 localToWorldMatrix { get { UpdateWorld(); return c_World; } }
        public Matrix4x4 worldToLocalMatrix
        {
            get
            {
                UpdateWorld();
                if (!c_InvValid) { c_Inv = c_World.inverse; c_InvValid = true; }
                return c_Inv;
            }
        }

        // World matrix cache (M5). The local fields are written directly in many places (loader, animator,
        // Instantiate), so instead of dirty flags the cache is validated by exact comparison of the local
        // values, the parent reference and the parent's stamp; on any difference it is recomputed with the
        // same expression as before (TRS, then parent * local), so results are unchanged.
        Vector3 c_Pos, c_Scale; Quaternion c_Rot; Transform c_Parent; int c_ParentStamp; bool c_Valid, c_InvValid;
        Matrix4x4 c_World, c_Inv;
        internal int m_Stamp;
        static int s_Stamp;
        internal void UpdateWorld()
        {
            var p = m_Parent; int ps = 0;
            if (p != null) { p.UpdateWorld(); ps = p.m_Stamp; }
            if (c_Valid && ReferenceEquals(p, c_Parent) && ps == c_ParentStamp
                && m_LocalPosition.x == c_Pos.x && m_LocalPosition.y == c_Pos.y && m_LocalPosition.z == c_Pos.z
                && m_LocalRotation.x == c_Rot.x && m_LocalRotation.y == c_Rot.y && m_LocalRotation.z == c_Rot.z && m_LocalRotation.w == c_Rot.w
                && m_LocalScale.x == c_Scale.x && m_LocalScale.y == c_Scale.y && m_LocalScale.z == c_Scale.z) return;
            var m = Matrix4x4.TRS(m_LocalPosition, m_LocalRotation, m_LocalScale);
            c_World = p != null ? p.c_World * m : m;
            c_Pos = m_LocalPosition; c_Rot = m_LocalRotation; c_Scale = m_LocalScale;
            c_Parent = p; c_ParentStamp = ps; c_Valid = true; c_InvValid = false;
            m_Stamp = ++s_Stamp;
        }

        public Vector3 position
        {
            get { return m_Parent != null ? m_Parent.localToWorldMatrix.MultiplyPoint3x4(m_LocalPosition) : m_LocalPosition; }
            set { m_LocalPosition = m_Parent != null ? m_Parent.worldToLocalMatrix.MultiplyPoint3x4(value) : value; }
        }
        public Quaternion rotation
        {
            get { return m_Parent != null ? m_Parent.rotation * m_LocalRotation : m_LocalRotation; }
            set { m_LocalRotation = m_Parent != null ? Quaternion.Inverse(m_Parent.rotation) * value : value; }
        }
        public Vector3 eulerAngles { get { return rotation.eulerAngles; } set { rotation = Quaternion.Euler(value); } }
        public Vector3 lossyScale
        {
            get { var s = m_LocalScale; return m_Parent != null ? Vector3.Scale(m_Parent.lossyScale, s) : s; }
        }
        public Vector3 right { get { return rotation * Vector3.right; } set { rotation = Quaternion.FromToRotation(Vector3.right, value); } }
        public Vector3 up { get { return rotation * Vector3.up; } set { rotation = Quaternion.FromToRotation(Vector3.up, value); } }
        public Vector3 forward { get { return rotation * Vector3.forward; } set { rotation = Quaternion.LookRotation(value); } }

        public Transform parent { get { return m_Parent; } set { SetParent(value, true); } }
        public Transform root { get { var t = this; while (t.m_Parent != null) t = t.m_Parent; return t; } }
        public int childCount { get { return m_Children.Count; } }
        public Transform GetChild(int i) { return m_Children[i]; }
        public int GetSiblingIndex() { return m_Parent != null ? m_Parent.m_Children.IndexOf(this) : 0; }
        public void SetSiblingIndex(int i) { if (m_Parent == null) return; var l = m_Parent.m_Children; l.Remove(this); l.Insert(Mathf.Clamp(i, 0, l.Count), this); }
        public void SetAsFirstSibling() { SetSiblingIndex(0); }
        public void SetAsLastSibling() { if (m_Parent != null) SetSiblingIndex(m_Parent.m_Children.Count - 1); }
        public void DetachChildren() { foreach (var c in m_Children.ToArray()) c.SetParent(null, true); }
        public bool IsChildOf(Transform p) { for (var t = this; t != null; t = t.m_Parent) if (t == p) return true; return false; }
        public IEnumerator GetEnumerator() { return m_Children.ToArray().GetEnumerator(); }

        public void SetParent(Transform p) { SetParent(p, true); }
        public void SetParent(Transform p, bool worldPositionStays)
        {
            if (p == m_Parent) return;
            GameObject.s_ActiveTopologyVersion++;
            Vector3 wp = position; Quaternion wr = rotation; Vector3 ws = lossyScale;
            if (m_Parent != null) m_Parent.m_Children.Remove(this);
            m_Parent = p;
            if (p != null) p.m_Children.Add(this);
            if (worldPositionStays)
            {
                position = wp; rotation = wr;
                var ps = p != null ? p.lossyScale : Vector3.one;
                m_LocalScale = new Vector3(ps.x != 0 ? ws.x / ps.x : 0, ps.y != 0 ? ws.y / ps.y : 0, ps.z != 0 ? ws.z / ps.z : 0);
            }
            if (m_GameObject != null) KNL.World.OnParentChanged(this);
        }
        public void SetPositionAndRotation(Vector3 p, Quaternion r) { position = p; rotation = r; }

        public Transform Find(string n)
        {
            if (n.Contains("/"))
            {
                Transform t = this;
                // empty segments ("Level/GameLayer/", saved object paths) are skipped, as in Unity
                foreach (var part in n.Split('/')) { if (part.Length == 0) continue; if (t == null) return null; t = t.Find(part); }
                return t;
            }
            foreach (var c in m_Children) if (c.name == n) return c;
            return null;
        }
        public Transform FindChild(string n) { return Find(n); }

        public void Translate(Vector3 d) { Translate(d, Space.Self); }
        public void Translate(Vector3 d, Space s) { if (s == Space.World) position += d; else position += rotation * d; }
        public void Translate(float x, float y, float z) { Translate(new Vector3(x, y, z)); }
        public void Rotate(Vector3 e) { Rotate(e, Space.Self); }
        public void Rotate(Vector3 e, Space s) { var q = Quaternion.Euler(e); if (s == Space.Self) localRotation = localRotation * q; else rotation = q * rotation; }
        public void Rotate(float x, float y, float z) { Rotate(new Vector3(x, y, z)); }
        public void Rotate(Vector3 axis, float angle) { rotation = rotation * Quaternion.AngleAxis(angle, axis); }
        public void LookAt(Vector3 p) { rotation = Quaternion.LookRotation(p - position); }
        public void LookAt(Transform t) { LookAt(t.position); }
        public Vector3 TransformPoint(Vector3 p) { return localToWorldMatrix.MultiplyPoint3x4(p); }
        public Vector3 TransformPoint(float x, float y, float z) { return TransformPoint(new Vector3(x, y, z)); }
        public Vector3 InverseTransformPoint(Vector3 p) { return worldToLocalMatrix.MultiplyPoint3x4(p); }
        public Vector3 InverseTransformPoint(float x, float y, float z) { return InverseTransformPoint(new Vector3(x, y, z)); }
        public Vector3 TransformDirection(Vector3 d) { return rotation * d; }
        public Vector3 InverseTransformDirection(Vector3 d) { return Quaternion.Inverse(rotation) * d; }
        public Vector3 TransformVector(Vector3 v) { return localToWorldMatrix.MultiplyVector(v); }
        public Vector3 InverseTransformVector(Vector3 v) { return worldToLocalMatrix.MultiplyVector(v); }
        public bool hasChanged { get; set; }
    }

    public sealed partial class RectTransform
    {
        // Unity semantics: localPosition is the truth; anchoredPosition is derived from it and the
        // parent rect. Changing a rect keeps every descendant's anchoredPosition (see RectChange).
        internal Vector2 m_AnchorMin, m_AnchorMax, m_AnchoredPosition, m_SizeDelta, m_Pivot = new Vector2(0.5f, 0.5f);
        public Vector2 anchorMin { get { return m_AnchorMin; } set { if (value != m_AnchorMin) RectChange(() => m_AnchorMin = value); } }
        public Vector2 anchorMax { get { return m_AnchorMax; } set { if (value != m_AnchorMax) RectChange(() => m_AnchorMax = value); } }
        public Vector2 sizeDelta { get { return m_SizeDelta; } set { if (value != m_SizeDelta) RectChange(() => m_SizeDelta = value); } }
        public Vector2 pivot { get { return m_Pivot; } set { if (value != m_Pivot) RectChange(() => m_Pivot = value); } }
        public Vector2 anchoredPosition
        {
            get { var lp = localPosition; var r = AnchorRef(); return new Vector2(lp.x - r.x, lp.y - r.y); }
            set { var lp = localPosition; var r = AnchorRef(); localPosition = new Vector3(r.x + value.x, r.y + value.y, lp.z); }
        }
        public Vector3 anchoredPosition3D
        {
            get { var a = anchoredPosition; return new Vector3(a.x, a.y, localPosition.z); }
            set { anchoredPosition = new Vector2(value.x, value.y); var lp = localPosition; localPosition = new Vector3(lp.x, lp.y, value.z); }
        }
        public Rect rect { get { var s = Size(); return new Rect(-m_Pivot.x * s.x, -m_Pivot.y * s.y, s.x, s.y); } }
        public Vector2 offsetMin
        {
            get { return anchoredPosition - Vector2.Scale(m_SizeDelta, m_Pivot); }
            set { var o = value - offsetMin; var ap = anchoredPosition; sizeDelta = m_SizeDelta - o; anchoredPosition = ap + Vector2.Scale(o, Vector2.one - m_Pivot); }
        }
        public Vector2 offsetMax
        {
            get { return anchoredPosition + Vector2.Scale(m_SizeDelta, Vector2.one - m_Pivot); }
            set { var o = value - offsetMax; var ap = anchoredPosition; sizeDelta = m_SizeDelta + o; anchoredPosition = ap + Vector2.Scale(o, m_Pivot); }
        }
        public void SetSizeWithCurrentAnchors(Axis axis, float size)
        {
            var ps = ParentSize(); var sd = m_SizeDelta;
            if (axis == Axis.Horizontal) sd.x = size - ps.x * (m_AnchorMax.x - m_AnchorMin.x);
            else sd.y = size - ps.y * (m_AnchorMax.y - m_AnchorMin.y);
            sizeDelta = sd;
        }
        public void SetInsetAndSizeFromParentEdge(Edge edge, float inset, float size)
        {
            int axis = (edge == Edge.Top || edge == Edge.Bottom) ? 1 : 0;
            bool end = edge == Edge.Top || edge == Edge.Right;
            float av = end ? 1f : 0f;
            var am = m_AnchorMin; var ax = m_AnchorMax; var sd = m_SizeDelta;
            if (axis == 0) { am.x = av; ax.x = av; sd.x = size; } else { am.y = av; ax.y = av; sd.y = size; }
            anchorMin = am; anchorMax = ax; sizeDelta = sd;
            var ap = anchoredPosition; float pv = axis == 0 ? m_Pivot.x : m_Pivot.y;
            float v = end ? -inset - size * (1f - pv) : inset + size * pv;
            if (axis == 0) ap.x = v; else ap.y = v;
            anchoredPosition = ap;
        }
        public void GetLocalCorners(Vector3[] c)
        {
            var r = rect;
            c[0] = new Vector3(r.xMin, r.yMin, 0); c[1] = new Vector3(r.xMin, r.yMax, 0);
            c[2] = new Vector3(r.xMax, r.yMax, 0); c[3] = new Vector3(r.xMax, r.yMin, 0);
        }
        public void GetWorldCorners(Vector3[] c)
        {
            GetLocalCorners(c); var m = localToWorldMatrix;
            for (int i = 0; i < 4; i++) c[i] = m.MultiplyPoint3x4(c[i]);
        }

        internal Vector2 ParentSize()
        {
            var p = m_Parent as RectTransform;
            return p == null ? Vector2.zero : p.Size();
        }
        internal Vector2 Size()
        {
            var p = m_Parent as RectTransform;
            if (p == null) return m_SizeDelta;
            var ps = p.Size();
            return new Vector2(ps.x * (m_AnchorMax.x - m_AnchorMin.x) + m_SizeDelta.x, ps.y * (m_AnchorMax.y - m_AnchorMin.y) + m_SizeDelta.y);
        }
        internal Vector2 AnchorRef()
        {
            var p = m_Parent as RectTransform;
            if (p == null) return Vector2.zero;
            var pr = p.rect;
            return new Vector2(pr.xMin + pr.width * (m_AnchorMin.x + (m_AnchorMax.x - m_AnchorMin.x) * m_Pivot.x),
                               pr.yMin + pr.height * (m_AnchorMin.y + (m_AnchorMax.y - m_AnchorMin.y) * m_Pivot.y));
        }
        // keep the anchoredPosition of this rect and every RectTransform below it across a change
        static readonly List<KeyValuePair<RectTransform, Vector2>> s_Snap = new List<KeyValuePair<RectTransform, Vector2>>();
        internal void RectChange(System.Action change)
        {
            int start = s_Snap.Count;
            Snapshot(this);
            change();
            for (int i = start; i < s_Snap.Count; i++) s_Snap[i].Key.anchoredPosition = s_Snap[i].Value;
            s_Snap.RemoveRange(start, s_Snap.Count - start);
        }
        static void Snapshot(RectTransform rt)
        {
            s_Snap.Add(new KeyValuePair<RectTransform, Vector2>(rt, rt.anchoredPosition));
            foreach (var c in rt.m_Children) { var crt = c as RectTransform; if (crt != null) Snapshot(crt); }
        }
    }

    public partial class Renderer
    {
        internal bool m_RendererEnabled = true;
        internal Material[] m_Materials = new Material[0];
        internal int m_SortingOrder, m_SortingLayerID;
        public bool enabled { get { return m_RendererEnabled; } set { m_RendererEnabled = value; } }
        public bool isVisible { get { return m_RendererEnabled && m_GameObject != null && m_GameObject.activeInHierarchy; } }
        public int sortingOrder { get { return m_SortingOrder; } set { m_SortingOrder = value; } }
        public int sortingLayerID { get { return m_SortingLayerID; } set { m_SortingLayerID = value; } }
        public string sortingLayerName { get { return "Default"; } set { } }
        public Material material
        {   // Unity: first access gives this renderer its own copy of the material
            get
            {
                if (m_Materials.Length == 0 || m_Materials[0] == null) return null;
                if (!m_OwnsMaterial) { m_Materials[0] = new Material(m_Materials[0]) { name = m_Materials[0].name + " (Instance)" }; m_OwnsMaterial = true; }
                return m_Materials[0];
            }
            set { if (m_Materials.Length == 0) m_Materials = new Material[1]; m_Materials[0] = value; m_OwnsMaterial = false; }
        }
        internal bool m_OwnsMaterial;
        public Material sharedMaterial { get { return m_Materials.Length > 0 ? m_Materials[0] : null; } set { if (m_Materials.Length == 0) m_Materials = new Material[1]; m_Materials[0] = value; m_OwnsMaterial = false; } }
        public Material[] materials { get { if (m_Materials.Length > 0) { var x = material; } return m_Materials; } set { m_Materials = value ?? new Material[0]; } }
        public Material[] sharedMaterials { get { return m_Materials; } set { m_Materials = value ?? new Material[0]; } }
        internal MaterialPropertyBlock m_PropertyBlock;
        public void SetPropertyBlock(MaterialPropertyBlock p) { m_PropertyBlock = p; }
        public void GetPropertyBlock(MaterialPropertyBlock p) { if (m_PropertyBlock != null) m_PropertyBlock.CopyTo(p); else p.Clear(); }
        public Bounds bounds { get { return KNL.RenderSystem.BoundsOf(this); } }
    }

    public sealed partial class SpriteRenderer
    {
        internal Sprite m_Sprite;
        internal Color m_Color = Color.white;
        internal bool m_FlipX, m_FlipY;
        public Sprite sprite { get { return m_Sprite; } set { m_Sprite = value; } }
        public Color color { get { return m_Color; } set { m_Color = value; } }
        public bool flipX { get { return m_FlipX; } set { m_FlipX = value; } }
        public bool flipY { get { return m_FlipY; } set { m_FlipY = value; } }
    }

    public sealed partial class MeshRenderer { }
    public sealed partial class MeshFilter
    {
        internal Mesh m_Mesh; bool m_Instanced;
        /// Unity semantics: reading .mesh gives this filter its own copy ("X Instance").
        public Mesh mesh
        {
            get
            {
                if (m_Mesh != null && !m_Instanced) { m_Mesh = m_Mesh.CloneInstance(); m_Instanced = true; }
                return m_Mesh;
            }
            set { m_Mesh = value; m_Instanced = true; }
        }
        public Mesh sharedMesh { get { return m_Mesh; } set { m_Mesh = value; m_Instanced = false; } }
    }

    public sealed partial class Mesh
    {
        internal Vector3[] m_Vertices = new Vector3[0];
        internal Color[] m_Colors = new Color[0];
        internal Vector2[] m_UV = new Vector2[0], m_UV2 = new Vector2[0];
        internal int[] m_Triangles = new int[0];
        public Vector3[] vertices { get { return (Vector3[])m_Vertices.Clone(); } set { m_Vertices = value ?? new Vector3[0]; } }
        public int vertexCount { get { return m_Vertices.Length; } }
        public Color[] colors { get { return (Color[])m_Colors.Clone(); } set { m_Colors = value ?? new Color[0]; } }
        public Vector2[] uv { get { return (Vector2[])m_UV.Clone(); } set { m_UV = value ?? new Vector2[0]; } }
        public Vector2[] uv2 { get { return (Vector2[])m_UV2.Clone(); } set { m_UV2 = value ?? new Vector2[0]; } }
        public int[] triangles { get { return (int[])m_Triangles.Clone(); } set { m_Triangles = value ?? new int[0]; } }
        public void Clear() { m_Vertices = new Vector3[0]; m_Colors = new Color[0]; m_UV = new Vector2[0]; m_Triangles = new int[0]; }
        public void RecalculateBounds() { }
        public void RecalculateNormals() { }
        public void MarkDynamic() { }
        internal Mesh CloneInstance()
        {
            var m = new Mesh(); m.name = name + " Instance";
            m.m_Vertices = (Vector3[])m_Vertices.Clone(); m.m_Colors = (Color[])m_Colors.Clone(); m.m_UV = (Vector2[])m_UV.Clone();
            m.m_UV2 = (Vector2[])m_UV2.Clone(); m.m_Triangles = (int[])m_Triangles.Clone();
            return m;
        }
    }

    public sealed partial class Sprite
    {
        internal KNL.SpriteData data;
        public Rect rect { get { return data != null ? data.rect : default(Rect); } }
        public Vector2 pivot { get { return data != null ? new Vector2(data.pivot.x * data.rect.width, data.pivot.y * data.rect.height) : default(Vector2); } }
        public float pixelsPerUnit { get { return data != null ? data.ppu : 100f; } }
        public Bounds bounds
        {
            get
            {
                if (data == null) return default(Bounds);
                var size = new Vector3(data.rect.width / data.ppu, data.rect.height / data.ppu, 0.2f);
                var c = new Vector3((0.5f - data.pivot.x) * size.x, (0.5f - data.pivot.y) * size.y, 0);
                return new Bounds(c, size);
            }
        }
        public Texture2D texture { get { return CutTexture(); } }
    }

    public partial class Texture
    {
        internal int m_Width, m_Height;
        public virtual int width { get { return m_Width; } set { m_Width = value; } }
        public virtual int height { get { return m_Height; } set { m_Height = value; } }
        public FilterMode filterMode { get; set; }
        public TextureWrapMode wrapMode { get; set; }
    }
    public sealed partial class Texture2D
    {
        public Texture2D(int width, int height) { m_Width = width; m_Height = height; }
        internal Texture2D() { }
        internal string m_File; internal long m_PathID; internal int m_Format; internal KNL.SpriteData m_Sprite; internal int m_KnlTex = -2;
        internal bool alphaOnly { get { return m_Format == 1; } }     // Alpha8 (font textures)
    }

    public partial class Material
    {
        internal Shader m_Shader;
        internal readonly Dictionary<string, Color> m_ColorProps = new Dictionary<string, Color>();
        internal readonly Dictionary<string, float> m_FloatProps = new Dictionary<string, float>();
        internal Texture m_MainTexture;
        internal Material() { }
        public Material(Material source)
        {
            name = source.name; m_Shader = source.m_Shader; m_MainTexture = source.m_MainTexture;
            foreach (var kv in source.m_ColorProps) m_ColorProps[kv.Key] = kv.Value;
            foreach (var kv in source.m_FloatProps) m_FloatProps[kv.Key] = kv.Value;
            foreach (var kv in source.m_TexOffsets) m_TexOffsets[kv.Key] = kv.Value;
            foreach (var kv in source.m_TexScales) m_TexScales[kv.Key] = kv.Value;
            foreach (var k in source.m_Keywords) m_Keywords.Add(k);
            renderQueue = source.renderQueue; m_Shading = null;
        }
        internal KNL.ShadeInfo m_Shading;      // render-side cache of the shader parameters (see RenderSystem)
        public Material(Shader shader) { m_Shader = shader; }
        public Shader shader { get { return m_Shader; } set { m_Shader = value; } }
        public Color color { get { return GetColor("_Color"); } set { SetColor("_Color", value); } }
        public Texture mainTexture { get { return m_MainTexture; } set { m_MainTexture = value; } }
        public bool HasProperty(string n) { return m_ColorProps.ContainsKey(n) || m_FloatProps.ContainsKey(n); }
        public Color GetColor(string n) { Color c; return m_ColorProps.TryGetValue(n, out c) ? c : Color.white; }
        public void SetColor(string n, Color c) { m_ColorProps[n] = c; m_Shading = null; }
        public float GetFloat(string n) { float f; return m_FloatProps.TryGetValue(n, out f) ? f : 0f; }
        public void SetFloat(string n, float f) { m_FloatProps[n] = f; m_Shading = null; }
        public void SetVector(string n, Vector4 v) { m_ColorProps[n] = v; m_Shading = null; }
        public void SetMatrix(string n, Matrix4x4 m) { }
        public void SetTextureOffset(string n, Vector2 o) { }
        public int renderQueue { get; set; }
    }
    public sealed partial class Shader
    {
        public static Shader Find(string n) { var s = new Shader(); s.name = n; return s; }
        // property ids are a stable registry so int-keyed calls map back to the property name
        static readonly Dictionary<string, int> s_Ids = new Dictionary<string, int>();
        static readonly List<string> s_Names = new List<string>();
        public static int PropertyToID(string n)
        {
            int id; if (s_Ids.TryGetValue(n, out id)) return id;
            id = s_Names.Count; s_Names.Add(n); s_Ids[n] = id; return id;
        }
        internal static string NameOf(int id) { return id >= 0 && id < s_Names.Count ? s_Names[id] : "#" + id; }
    }
    public sealed partial class MaterialPropertyBlock
    {
        internal readonly Dictionary<string, Color> colors = new Dictionary<string, Color>();
        internal readonly Dictionary<string, float> floats = new Dictionary<string, float>();
        public void SetColor(string n, Color c) { colors[n] = c; }
        public void SetFloat(string n, float f) { floats[n] = f; }
        public void Clear() { colors.Clear(); floats.Clear(); }
        public Color GetColor(string n) { Color c; return colors.TryGetValue(n, out c) ? c : new Color(0, 0, 0, 0); }
        public float GetFloat(string n) { float f; return floats.TryGetValue(n, out f) ? f : 0f; }
        public Color GetColor(int id) { return GetColor(Shader.NameOf(id)); }
        public float GetFloat(int id) { return GetFloat(Shader.NameOf(id)); }
        public bool isEmpty { get { return colors.Count == 0 && floats.Count == 0; } }
        internal void CopyTo(MaterialPropertyBlock o) { o.Clear(); foreach (var kv in colors) o.colors[kv.Key] = kv.Value; foreach (var kv in floats) o.floats[kv.Key] = kv.Value; }
    }

    public sealed partial class Camera
    {
        internal float m_OrthographicSize = 5f;
        internal bool m_Orthographic = true;
        internal Color m_BackgroundColor = Color.black;
        internal float m_Depth;
        internal RenderTexture m_TargetTexture;
        public float orthographicSize { get { return m_OrthographicSize; } set { m_OrthographicSize = value; } }
        public bool orthographic { get { return m_Orthographic; } set { m_Orthographic = value; } }
        public Color backgroundColor { get { return m_BackgroundColor; } set { m_BackgroundColor = value; } }
        public float depth { get { return m_Depth; } set { m_Depth = value; } }
        public RenderTexture targetTexture { get { return m_TargetTexture; } set { m_TargetTexture = value; } }
        public float nearClipPlane { get; set; }
        public float farClipPlane { get; set; }
        public float aspect { get { return (float)KNL.RenderSystem.Width / KNL.RenderSystem.Height; } set { } }
        public int pixelWidth { get { return KNL.RenderSystem.Width; } }
        public int pixelHeight { get { return KNL.RenderSystem.Height; } }
        public static Camera main { get { return KNL.RenderSystem.TaggedMainCamera(); } }
        public Vector3 WorldToScreenPoint(Vector3 p) { return KNL.RenderSystem.WorldToScreen(this, p); }
        public Vector3 ScreenToWorldPoint(Vector3 p) { return KNL.RenderSystem.ScreenToWorld(this, p); }
        public Vector3 WorldToViewportPoint(Vector3 p) { var s = WorldToScreenPoint(p); return new Vector3(s.x / pixelWidth, s.y / pixelHeight, s.z); }
        public Vector3 ViewportToWorldPoint(Vector3 p) { return ScreenToWorldPoint(new Vector3(p.x * pixelWidth, p.y * pixelHeight, p.z)); }
        public void Render() { }
    }

    // ------------------------------------------------------------- statics
    public sealed partial class Debug
    {
        public static void Log(object m) { KNL.Log.Write("LOG", m); }
        public static void Log(object m, Object ctx) { KNL.Log.Write("LOG", m); }
        public static void LogWarning(object m) { KNL.Log.Write("WARN", m); }
        public static void LogWarning(object m, Object ctx) { KNL.Log.Write("WARN", m); }
        public static void LogError(object m) { KNL.Log.Write("ERROR", m); }
        public static void LogError(object m, Object ctx) { KNL.Log.Write("ERROR", m); }
        public static void LogException(Exception e) { KNL.Log.Write("EXC", e); }
        public static void LogException(Exception e, Object ctx) { KNL.Log.Write("EXC", e); }
        public static void LogFormat(string f, params object[] a) { KNL.Log.Write("LOG", string.Format(f, a)); }
        public static void LogFormat(Object ctx, string f, params object[] a) { KNL.Log.Write("LOG", string.Format(f, a)); }
        public static void LogWarningFormat(Object ctx, string f, params object[] a) { KNL.Log.Write("WARN", string.Format(f, a)); }
        public static void LogErrorFormat(Object ctx, string f, params object[] a) { KNL.Log.Write("ERROR", string.Format(f, a)); }
        public static void LogWarningFormat(string f, params object[] a) { KNL.Log.Write("WARN", string.Format(f, a)); }
        public static void LogErrorFormat(string f, params object[] a) { KNL.Log.Write("ERROR", string.Format(f, a)); }
        public static void DrawLine(Vector3 a, Vector3 b) { }
        public static void DrawLine(Vector3 a, Vector3 b, Color c) { }
        public static void DrawLine(Vector3 a, Vector3 b, Color c, float d) { }
        public static void DrawRay(Vector3 a, Vector3 d) { }
        public static void DrawRay(Vector3 a, Vector3 d, Color c) { }
        public static void DrawRay(Vector3 a, Vector3 d, Color c, float duration) { }
        public static void Break() { }
        public static bool isDebugBuild { get { return false; } }
    }

    public sealed partial class Time
    {
        internal static float s_Time, s_DeltaTime = 1f / 30f, s_UnscaledTime, s_UnscaledDeltaTime = 1f / 30f;
        internal static float s_FixedDeltaTime = 0.02f, s_TimeScale = 1f;
        internal static int s_FrameCount;
        public static float time { get { return s_Time; } }
        public static float deltaTime { get { return s_DeltaTime; } }
        public static float unscaledTime { get { return s_UnscaledTime; } }
        public static float unscaledDeltaTime { get { return s_UnscaledDeltaTime; } }
        public static float realtimeSinceStartup { get { return s_UnscaledTime; } }
        public static float timeSinceLevelLoad { get { return s_Time; } }
        public static float fixedTime { get { return s_Time; } }
        public static float fixedDeltaTime { get { return s_FixedDeltaTime; } set { s_FixedDeltaTime = value; } }
        public static float smoothDeltaTime { get { return s_DeltaTime; } }
        public static float maximumDeltaTime { get; set; }
        public static float timeScale { get { return s_TimeScale; } set { s_TimeScale = value; } }
        public static int frameCount { get { return s_FrameCount; } }
    }

    public sealed partial class Application
    {
        public static bool isPlaying { get { return true; } }
        public static bool isEditor { get { return false; } }
        public static bool isMobilePlatform { get { return false; } }
        public static bool isConsolePlatform { get { return false; } }
        public static RuntimePlatform platform { get { return RuntimePlatform.LinuxPlayer; } }
        public static string persistentDataPath { get { return KNL.Paths.SaveDir; } }
        public static string dataPath { get { return KNL.Paths.DataDir; } }
        public static string streamingAssetsPath { get { return KNL.Paths.DataDir + "/StreamingAssets"; } }
        public static string version { get { return "1.2.8"; } }
        public static string productName { get { return "Kingdom"; } }
        public static string companyName { get { return "noio"; } }
        public static SystemLanguage systemLanguage { get { return SystemLanguage.English; } }
        public static int targetFrameRate { get; set; }
        public static bool runInBackground { get; set; }
        public static void Quit() { KNL.Engine.RequestQuit(); }
        public static void OpenURL(string url) { }
        public static NetworkReachability internetReachability { get { return NetworkReachability.NotReachable; } }
    }

    public sealed partial class Screen
    {
        public static int width { get { return KNL.RenderSystem.Width; } }
        public static int height { get { return KNL.RenderSystem.Height; } }
        public static bool fullScreen { get { return true; } set { } }
        public static float dpi { get { return 96f; } }
        public static Resolution currentResolution { get { return default(Resolution); } }
        public static Resolution[] resolutions { get { return new Resolution[0]; } }
        public static void SetResolution(int w, int h, bool full) { }
    }
}
