// KNL 2D physics: a small clean-room subset sized for Kingdom (not Box2D).
//  - bodies: dynamic (gravity, drag, constraints, collision response against static/kinematic
//    colliders), kinematic (move by velocity), static
//  - colliders: box, circle, polygon (AABB for contacts, exact point-in-polygon for point
//    queries), edge chains (ground)
//  - triggers + collisions: Enter/Stay/Exit messages with Unity's rules (needs a Rigidbody2D on
//    one side, layer collision matrix, messages also reach disabled behaviours)
//  - queries: OverlapPoint/Area/Circle (+All/NonAlloc), Raycast, LinecastNonAlloc, Collider2D.Cast
//  - broadphase: sweep-and-prune on x (Kingdom's world is essentially one-dimensional)
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using JObj = System.Collections.Generic.Dictionary<string, object>;

namespace UnityEngine
{
    public partial class Rigidbody2D
    {
        internal static readonly List<Rigidbody2D> s_All = new List<Rigidbody2D>();
        internal int m_BodyType;   // 0 dynamic, 1 kinematic, 2 static
        internal Vector2 m_Velocity; internal float m_AngularVelocity;
        internal float m_Mass = 1f, m_Drag, m_AngularDrag = 0.05f, m_GravityScale = 1f;
        internal bool m_Simulated = true; internal RigidbodyConstraints2D m_Constraints;
        internal Vector2 m_CenterOfMass;
        public Vector2 velocity { get { return m_Velocity; } set { m_Velocity = value; } }
        public float angularVelocity { get { return m_AngularVelocity; } set { m_AngularVelocity = value; } }
        public bool isKinematic { get { return m_BodyType == 1; } set { m_BodyType = value ? 1 : 0; } }
        public bool simulated { get { return m_Simulated; } set { m_Simulated = value; } }
        public float mass { get { return m_Mass; } set { m_Mass = Mathf.Max(1e-4f, value); } }
        public float drag { get { return m_Drag; } set { m_Drag = value; } }
        public float angularDrag { get { return m_AngularDrag; } set { m_AngularDrag = value; } }
        public float gravityScale { get { return m_GravityScale; } set { m_GravityScale = value; } }
        public RigidbodyConstraints2D constraints { get { return m_Constraints; } set { m_Constraints = value; } }
        public Vector2 position { get { return transform.position; } set { var p = transform.position; transform.position = new Vector3(value.x, value.y, p.z); } }
        public float rotation { get { return transform.eulerAngles.z; } set { var e = transform.eulerAngles; transform.eulerAngles = new Vector3(e.x, e.y, value); } }
        public Vector2 centerOfMass { get { return m_CenterOfMass; } set { m_CenterOfMass = value; } }
        public Vector2 worldCenterOfMass { get { return transform.TransformPoint(m_CenterOfMass); } }
        public void MovePosition(Vector2 p) { position = p; }
        public void MoveRotation(float a) { rotation = a; }
        public void WakeUp() { }
        public void Sleep() { }
        public bool IsSleeping() { return false; }
        public void AddForce(Vector2 f) { AddForce(f, ForceMode2D.Force); }
        public void AddForce(Vector2 f, ForceMode2D mode)
        {
            if (m_BodyType != 0) return;
            m_Velocity += mode == ForceMode2D.Impulse ? f / m_Mass : f / m_Mass * Time.fixedDeltaTime;
        }
        public void AddForceAtPosition(Vector2 f, Vector2 p) { AddForceAtPosition(f, p, ForceMode2D.Force); }
        public void AddForceAtPosition(Vector2 f, Vector2 p, ForceMode2D mode)
        {
            AddForce(f, mode);
            var r = p - worldCenterOfMass;
            AddTorque(r.x * f.y - r.y * f.x, mode);
        }
        public void AddTorque(float t) { AddTorque(t, ForceMode2D.Force); }
        public void AddTorque(float t, ForceMode2D mode)
        {
            if (m_BodyType != 0) return;
            float inertia = m_Mass * 0.25f;
            m_AngularVelocity += (mode == ForceMode2D.Impulse ? t / inertia : t / inertia * Time.fixedDeltaTime) * Mathf.Rad2Deg;
        }
    }

    public partial class Collider2D
    {
        internal static readonly List<Collider2D> s_All = new List<Collider2D>();
        internal bool m_IsTrigger; internal Vector2 m_Offset; internal PhysicsMaterial2D m_Material;
        internal Rigidbody2D m_CachedRb;
        internal bool m_RbCached;
        public bool isTrigger { get { return m_IsTrigger; } set { m_IsTrigger = value; } }
        public Vector2 offset { get { return m_Offset; } set { m_Offset = value; } }
        public PhysicsMaterial2D sharedMaterial { get { return m_Material; } set { m_Material = value; } }
        public Rigidbody2D attachedRigidbody
        {
            get
            {
                if (m_RbCached && (m_CachedRb == null || !m_CachedRb.m_Destroyed)) return m_CachedRb;
                m_CachedRb = gameObject != null ? gameObject.GetComponentInParent<Rigidbody2D>() : null;
                m_RbCached = true;
                return m_CachedRb;
            }
        }
        public Bounds bounds { get { var a = KNL.Physics2DSystem.AabbOf(this); return new Bounds(new Vector3((a.x0 + a.x1) / 2, (a.y0 + a.y1) / 2, transform.position.z), new Vector3(a.x1 - a.x0, a.y1 - a.y0, 0)); } }
        public bool OverlapPoint(Vector2 p) { return KNL.Physics2DSystem.ContainsPoint(this, p); }
        public bool IsTouching(Collider2D other) { return KNL.Physics2DSystem.Overlaps(this, other); }
        public int Cast(Vector2 dir, RaycastHit2D[] results) { return Cast(dir, results, float.PositiveInfinity); }
        public int Cast(Vector2 dir, RaycastHit2D[] results, float distance) { return KNL.Physics2DSystem.Cast(this, dir, results, distance); }
        internal virtual void LocalPoints(List<Vector2> pts) { }
    }

    public sealed partial class BoxCollider2D
    {
        internal Vector2 m_Size = Vector2.one;
        public Vector2 size { get { return m_Size; } set { m_Size = value; } }
        internal override void LocalPoints(List<Vector2> p)
        {
            var h = m_Size * 0.5f;
            p.Add(m_Offset + new Vector2(-h.x, -h.y)); p.Add(m_Offset + new Vector2(h.x, -h.y));
            p.Add(m_Offset + new Vector2(h.x, h.y)); p.Add(m_Offset + new Vector2(-h.x, h.y));
        }
    }
    public sealed partial class CircleCollider2D
    {
        internal float m_Radius = 0.5f;
        public float radius { get { return m_Radius; } set { m_Radius = value; } }
        internal override void LocalPoints(List<Vector2> p)
        {
            p.Add(m_Offset + new Vector2(-m_Radius, -m_Radius)); p.Add(m_Offset + new Vector2(m_Radius, -m_Radius));
            p.Add(m_Offset + new Vector2(m_Radius, m_Radius)); p.Add(m_Offset + new Vector2(-m_Radius, m_Radius));
        }
    }
    public sealed partial class PolygonCollider2D
    {
        internal List<Vector2[]> m_Paths = new List<Vector2[]>();
        public int pathCount { get { return m_Paths.Count; } set { while (m_Paths.Count < value) m_Paths.Add(new Vector2[0]); while (m_Paths.Count > value) m_Paths.RemoveAt(m_Paths.Count - 1); } }
        public Vector2[] GetPath(int i) { return i < m_Paths.Count ? (Vector2[])m_Paths[i].Clone() : new Vector2[0]; }
        public void SetPath(int i, Vector2[] pts) { pathCount = Mathf.Max(pathCount, i + 1); m_Paths[i] = (Vector2[])pts.Clone(); }
        public Vector2[] points { get { return GetPath(0); } set { SetPath(0, value); } }
        internal override void LocalPoints(List<Vector2> p) { foreach (var path in m_Paths) foreach (var v in path) p.Add(v + m_Offset); }
    }
    public sealed partial class EdgeCollider2D
    {
        internal Vector2[] m_Points = { new Vector2(-0.5f, 0), new Vector2(0.5f, 0) };
        public Vector2[] points { get { return (Vector2[])m_Points.Clone(); } set { m_Points = value ?? new Vector2[0]; } }
        public int pointCount { get { return m_Points.Length; } }
        internal override void LocalPoints(List<Vector2> p) { foreach (var v in m_Points) p.Add(v + m_Offset); }
    }

    public sealed partial class PhysicsMaterial2D
    {
        internal float m_Friction = 0.4f, m_Bounciness;
        public float friction { get { return m_Friction; } set { m_Friction = value; } }
        public float bounciness { get { return m_Bounciness; } set { m_Bounciness = value; } }
    }

    public partial class Joint2D { }
    public partial class AnchoredJoint2D { }
    public sealed partial class TargetJoint2D
    {
        internal Vector2 m_Anchor, m_Target; internal float m_MaxForce = 1000f, m_Frequency = 5f, m_Damping = 1f;
        public Vector2 anchor { get { return m_Anchor; } set { m_Anchor = value; } }
        public Vector2 target { get { return m_Target; } set { m_Target = value; } }
        public float maxForce { get { return m_MaxForce; } set { m_MaxForce = value; } }
        public float frequency { get { return m_Frequency; } set { m_Frequency = value; } }
        public float dampingRatio { get { return m_Damping; } set { m_Damping = value; } }
    }

    public partial struct ContactPoint2D
    {
        internal Vector2 m_Point, m_Normal; internal Collider2D m_Collider, m_OtherCollider;
        public Vector2 point { get { return m_Point; } }
        public Vector2 normal { get { return m_Normal; } }
        public Collider2D collider { get { return m_Collider; } }
        public Collider2D otherCollider { get { return m_OtherCollider; } }
    }

    public partial class Collision2D
    {
        internal Collider2D m_Collider, m_OtherCollider; internal Rigidbody2D m_Rigidbody; internal Vector2 m_RelVel;
        internal ContactPoint2D[] m_Contacts = new ContactPoint2D[0];
        public Collider2D collider { get { return m_Collider; } }
        public Collider2D otherCollider { get { return m_OtherCollider; } }
        public Rigidbody2D rigidbody { get { return m_Rigidbody; } }
        public GameObject gameObject { get { return m_Collider != null ? m_Collider.gameObject : null; } }
        public Transform transform { get { return m_Collider != null ? m_Collider.transform : null; } }
        public ContactPoint2D[] contacts { get { return m_Contacts; } }
        public Vector2 relativeVelocity { get { return m_RelVel; } }
    }

    public partial struct RaycastHit2D
    {
        internal Collider2D m_Collider; internal Vector2 m_Point, m_Normal; internal float m_Distance, m_Fraction;
        public Collider2D collider { get { return m_Collider; } }
        public Rigidbody2D rigidbody { get { return m_Collider != null ? m_Collider.attachedRigidbody : null; } }
        public Transform transform { get { return m_Collider != null ? m_Collider.transform : null; } }
        public Vector2 point { get { return m_Point; } }
        public Vector2 normal { get { return m_Normal; } }
        public float distance { get { return m_Distance; } }
        public float fraction { get { return m_Fraction; } }
        public static implicit operator bool(RaycastHit2D h) { return h.m_Collider != null; }
    }

    public partial class Physics2D
    {
        public static Vector2 gravity { get { return KNL.Physics2DSystem.Gravity; } set { KNL.Physics2DSystem.Gravity = value; } }
        public static bool queriesHitTriggers { get; set; }
        public const int DefaultRaycastLayers = -5, AllLayers = -1, IgnoreRaycastLayer = 4;
        public static Collider2D OverlapPoint(Vector2 p) { return OverlapPoint(p, DefaultRaycastLayers); }
        public static Collider2D OverlapPoint(Vector2 p, int mask) { var l = KNL.Physics2DSystem.QueryPoint(p, mask); return l.Count > 0 ? l[0] : null; }
        public static int OverlapPointNonAlloc(Vector2 p, Collider2D[] r) { return OverlapPointNonAlloc(p, r, DefaultRaycastLayers); }
        public static int OverlapPointNonAlloc(Vector2 p, Collider2D[] r, int mask) { return Fill(KNL.Physics2DSystem.QueryPoint(p, mask), r); }
        public static Collider2D OverlapArea(Vector2 a, Vector2 b) { return OverlapArea(a, b, DefaultRaycastLayers); }
        public static Collider2D OverlapArea(Vector2 a, Vector2 b, int mask) { var l = KNL.Physics2DSystem.QueryArea(a, b, mask); return l.Count > 0 ? l[0] : null; }
        public static Collider2D[] OverlapAreaAll(Vector2 a, Vector2 b) { return OverlapAreaAll(a, b, DefaultRaycastLayers); }
        public static Collider2D[] OverlapAreaAll(Vector2 a, Vector2 b, int mask) { return KNL.Physics2DSystem.QueryArea(a, b, mask).ToArray(); }
        public static int OverlapAreaNonAlloc(Vector2 a, Vector2 b, Collider2D[] r) { return OverlapAreaNonAlloc(a, b, r, DefaultRaycastLayers); }
        public static int OverlapAreaNonAlloc(Vector2 a, Vector2 b, Collider2D[] r, int mask) { return Fill(KNL.Physics2DSystem.QueryArea(a, b, mask), r); }
        public static Collider2D OverlapCircle(Vector2 c, float rad) { return OverlapCircle(c, rad, DefaultRaycastLayers); }
        public static Collider2D OverlapCircle(Vector2 c, float rad, int mask) { var l = KNL.Physics2DSystem.QueryCircle(c, rad, mask); return l.Count > 0 ? l[0] : null; }
        public static Collider2D[] OverlapCircleAll(Vector2 c, float rad, int mask) { return KNL.Physics2DSystem.QueryCircle(c, rad, mask).ToArray(); }
        public static RaycastHit2D Raycast(Vector2 o, Vector2 d) { return Raycast(o, d, float.PositiveInfinity, DefaultRaycastLayers, float.NegativeInfinity, float.PositiveInfinity); }
        public static RaycastHit2D Raycast(Vector2 o, Vector2 d, float dist) { return Raycast(o, d, dist, DefaultRaycastLayers, float.NegativeInfinity, float.PositiveInfinity); }
        public static RaycastHit2D Raycast(Vector2 o, Vector2 d, float dist, int mask) { return Raycast(o, d, dist, mask, float.NegativeInfinity, float.PositiveInfinity); }
        public static RaycastHit2D Raycast(Vector2 o, Vector2 d, float dist, int mask, float minDepth, float maxDepth)
        { var l = KNL.Physics2DSystem.RayAll(o, d, dist, mask, minDepth, maxDepth); return l.Count > 0 ? l[0] : default(RaycastHit2D); }
        public static int LinecastNonAlloc(Vector2 a, Vector2 b, RaycastHit2D[] r) { return LinecastNonAlloc(a, b, r, DefaultRaycastLayers); }
        public static int LinecastNonAlloc(Vector2 a, Vector2 b, RaycastHit2D[] r, int mask)
        {
            var d = b - a; var l = KNL.Physics2DSystem.RayAll(a, d.normalized, d.magnitude, mask, float.NegativeInfinity, float.PositiveInfinity);
            int n = Mathf.Min(l.Count, r.Length); for (int i = 0; i < n; i++) r[i] = l[i]; return n;
        }
        public static bool GetIgnoreLayerCollision(int a, int b) { return !KNL.Physics2DSystem.LayersCollide(a, b); }
        static int Fill(List<Collider2D> l, Collider2D[] r) { int n = Mathf.Min(l.Count, r.Length); for (int i = 0; i < n; i++) r[i] = l[i]; return n; }
    }

    public partial class MaterialPropertyBlock
    {
        public void SetColor(int id, Color c) { colors[Shader.NameOf(id)] = c; }
        public void SetFloat(int id, float f) { floats[Shader.NameOf(id)] = f; }
    }
}

namespace KNL
{
    public struct Aabb { public float x0, y0, x1, y1; public bool Overlaps(Aabb o) { return x0 <= o.x1 && x1 >= o.x0 && y0 <= o.y1 && y1 >= o.y0; } }

    public static class Physics2DSystem
    {
        public static Vector2 Gravity = new Vector2(0, -9.81f);
        static readonly uint[] layerMatrix = Enumerable.Repeat(0xFFFFFFFFu, 32).ToArray();
        public static int Steps;

        public static void LoadSettings()
        {
            foreach (var e in World.File("globalgamemanagers").objects.Values)
            {
                if ((string)e["type"] != "Physics2DSettings" || !e.ContainsKey("data")) continue;
                var d = (JObj)e["data"];
                Gravity = Json.V2(d["m_Gravity"]);
                var m = (List<object>)d["m_LayerCollisionMatrix"];
                for (int i = 0; i < 32 && i < m.Count; i++) layerMatrix[i] = (uint)(double)m[i];
            }
        }
        public static bool LayersCollide(int a, int b) { return (layerMatrix[a & 31] & (1u << (b & 31))) != 0; }

        // ---------------------------------------------------------------- shapes
        static readonly List<Vector2> tmp = new List<Vector2>();
        public static Aabb AabbOf(Collider2D c)
        {
            var box = c as BoxCollider2D;
            if (box != null)
            {
                var t = c.transform;
                if (t != null && t.m_LocalRotation.z == 0f && t.m_LocalRotation.w == 1f && (t.m_Parent == null || t.m_Parent.m_LocalRotation.z == 0f))
                {
                    var pos = t.position;
                    var scale = t.lossyScale;
                    float cx = pos.x + box.m_Offset.x * scale.x;
                    float cy = pos.y + box.m_Offset.y * scale.y;
                    float hx = Mathf.Abs(box.m_Size.x * 0.5f * scale.x);
                    float hy = Mathf.Abs(box.m_Size.y * 0.5f * scale.y);
                    return new Aabb { x0 = cx - hx, x1 = cx + hx, y0 = cy - hy, y1 = cy + hy };
                }
            }
            tmp.Clear(); c.LocalPoints(tmp);
            var m = c.transform.localToWorldMatrix;
            var a = new Aabb { x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue };
            if (tmp.Count == 0) { var p = m.MultiplyPoint3x4(Vector3.zero); a.x0 = a.x1 = p.x; a.y0 = a.y1 = p.y; return a; }
            int count = tmp.Count;
            for (int i = 0; i < count; i++)
            {
                var p = m.MultiplyPoint3x4(tmp[i]);
                if (p.x < a.x0) a.x0 = p.x; if (p.x > a.x1) a.x1 = p.x;
                if (p.y < a.y0) a.y0 = p.y; if (p.y > a.y1) a.y1 = p.y;
            }
            return a;
        }

        public static bool ContainsPoint(Collider2D c, Vector2 p)
        {
            var a = AabbOf(c);
            if (p.x < a.x0 || p.x > a.x1 || p.y < a.y0 || p.y > a.y1) return false;
            var poly = c as PolygonCollider2D;
            if (poly != null)
            {
                var lp = (Vector2)c.transform.InverseTransformPoint(p) - poly.m_Offset;
                foreach (var path in poly.m_Paths) if (PointInPoly(path, lp)) return true;
                return false;
            }
            var circ = c as CircleCollider2D;
            if (circ != null)
            {
                var ctr = (Vector2)c.transform.TransformPoint(circ.m_Offset);
                float r = circ.m_Radius * Mathf.Max(Mathf.Abs(c.transform.lossyScale.x), Mathf.Abs(c.transform.lossyScale.y));
                return (p - ctr).sqrMagnitude <= r * r;
            }
            return !(c is EdgeCollider2D);
        }
        static bool PointInPoly(Vector2[] v, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i++)
                if ((v[i].y > p.y) != (v[j].y > p.y) && p.x < (v[j].x - v[i].x) * (p.y - v[i].y) / (v[j].y - v[i].y) + v[i].x) inside = !inside;
            return inside;
        }
        public static bool Overlaps(Collider2D a, Collider2D b) { return AabbOf(a).Overlaps(AabbOf(b)); }

        // ------------------------------------------------------------- registry
        sealed class Entry { public Collider2D c; public Rigidbody2D rb; public Aabb box; public int layer; }
        static readonly List<Entry> entries = new List<Entry>();
        static readonly List<Entry> entryPool = new List<Entry>();   // reused every step (M5: no per-step garbage)
        static readonly List<Rigidbody2D> bodies = new List<Rigidbody2D>();

        static readonly Comparison<Entry> s_EntryComparison = (a, b) => a.box.x0.CompareTo(b.box.x0);

        static void Gather()
        {
            entries.Clear(); bodies.Clear();
            int pass = GameObject.NewPass();
            var cols = Collider2D.s_All;
            int colCount = cols.Count;
            for (int i = 0; i < colCount; i++)
            {
                var c = cols[i];
                if (c == null || c.m_Destroyed || !c.m_Enabled) continue;
                var g = c.m_GameObject;
                if (g == null || g.m_Destroyed || !g.ActiveInPass(pass)) continue;
                if (entryPool.Count == entries.Count) entryPool.Add(new Entry());
                var e = entryPool[entries.Count];
                e.c = c; e.rb = BodyOf(c, pass); e.box = AabbOf(c); e.layer = g.m_Layer;
                entries.Add(e);
            }
            var bds = Rigidbody2D.s_All;
            int bdCount = bds.Count;
            for (int i = 0; i < bdCount; i++)
            {
                var rb = bds[i];
                if (rb == null || rb.m_Destroyed || !rb.m_Simulated) continue;
                var g = rb.m_GameObject;
                if (g == null || g.m_Destroyed || !g.ActiveInPass(pass)) continue;
                bodies.Add(rb);
            }
            entries.Sort(s_EntryComparison);
        }

        static Rigidbody2D BodyOf(Collider2D c, int pass)
        {
            var rb = c.attachedRigidbody;
            if (rb != null && rb.gameObject.ActiveInPass(pass)) return rb;
            return null;
        }

        // --------------------------------------------------------------- stepping
        struct PairKey : IEquatable<PairKey>
        {
            public Collider2D a, b;
            public bool Equals(PairKey o) { return ReferenceEquals(a, o.a) && ReferenceEquals(b, o.b); }
            public override int GetHashCode() { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(a) * 31 + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(b); }
        }
        static HashSet<PairKey> triggersPrev = new HashSet<PairKey>(), triggersNow = new HashSet<PairKey>();
        static HashSet<PairKey> contactsPrev = new HashSet<PairKey>(), contactsNow = new HashSet<PairKey>();
        static readonly Dictionary<PairKey, Collision2D> contactInfo = new Dictionary<PairKey, Collision2D>();

        public static void Step()
        {
            Steps++;
            float dt = Time.fixedDeltaTime;
            long pt = Prof.Level > 1 ? Prof.Now() : 0;
            // integrate
            int bCount = bodies.Count;
            for (int i = 0; i < bCount; i++)
            {
                var rb = bodies[i];
                if (rb == null || rb.m_Destroyed || rb.m_BodyType == 2) continue;
                if (rb.m_BodyType == 0)
                {
                    rb.m_Velocity += Gravity * rb.m_GravityScale * dt;
                    rb.m_Velocity *= 1f / (1f + dt * rb.m_Drag);
                    rb.m_AngularVelocity *= 1f / (1f + dt * rb.m_AngularDrag);
                    TargetJoints(rb, dt);
                }
                var v = rb.m_Velocity;
                if ((rb.m_Constraints & RigidbodyConstraints2D.FreezePositionX) != 0) v.x = 0;
                if ((rb.m_Constraints & RigidbodyConstraints2D.FreezePositionY) != 0) v.y = 0;
                if (v.x != 0 || v.y != 0)
                {
                    var t = rb.transform; var p = t.position;
                    t.position = new Vector3(p.x + v.x * dt, p.y + v.y * dt, p.z);
                }
                if (rb.m_AngularVelocity != 0 && (rb.m_Constraints & RigidbodyConstraints2D.FreezeRotation) == 0)
                    rb.transform.Rotate(0, 0, rb.m_AngularVelocity * dt);
            }
            if (Prof.Level > 1) pt = Prof.Add("  physics integrate", pt);
            Gather();
            if (Prof.Level > 1) pt = Prof.Add("  physics gather", pt);
            triggersNow.Clear(); contactsNow.Clear();
            // sweep-and-prune on x
            for (int i = 0; i < entries.Count; i++)
            {
                var A = entries[i];
                for (int j = i + 1; j < entries.Count; j++)
                {
                    var B = entries[j];
                    if (B.box.x0 > A.box.x1) break;
                    if (!A.box.Overlaps(B.box)) continue;
                    if (A.rb != null && ReferenceEquals(A.rb, B.rb)) continue;   // same body
                    if (A.rb == null && B.rb == null) continue;                  // static vs static
                    if (!LayersCollide(A.layer, B.layer)) continue;
                    if (A.c.m_IsTrigger || B.c.m_IsTrigger)
                    {
                        if (A.c is EdgeCollider2D || B.c is EdgeCollider2D)
                        { if (!EdgeTouch(A, B)) continue; }
                        triggersNow.Add(Key(A.c, B.c));
                        continue;
                    }
                    Resolve(A, B);
                }
            }
            if (Prof.Level > 1) pt = Prof.Add("  physics pairs", pt);
            Dispatch();
            if (Prof.Level > 1) Prof.Add("  physics messages", pt);
        }

        static PairKey Key(Collider2D a, Collider2D b)
        {
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(a) <= System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(b)
                ? new PairKey { a = a, b = b } : new PairKey { a = b, b = a };
        }

        static bool EdgeTouch(Entry A, Entry B)
        {
            var e = A.c as EdgeCollider2D != null ? A : B; var o = ReferenceEquals(e, A) ? B : A;
            float x = (o.box.x0 + o.box.x1) / 2f, y = EdgeY((EdgeCollider2D)e.c, x);
            return !float.IsNaN(y) && o.box.y0 <= y && o.box.y1 >= y;
        }
        static float EdgeY(EdgeCollider2D e, float x)
        {
            var m = e.transform.localToWorldMatrix; var pts = e.m_Points;
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                var a = m.MultiplyPoint3x4(pts[i] + e.m_Offset); var b = m.MultiplyPoint3x4(pts[i + 1] + e.m_Offset);
                float lo = Mathf.Min(a.x, b.x), hi = Mathf.Max(a.x, b.x);
                if (x >= lo && x <= hi) return Mathf.Abs(b.x - a.x) < 1e-6f ? Mathf.Max(a.y, b.y) : a.y + (b.y - a.y) * (x - a.x) / (b.x - a.x);
            }
            return float.NaN;
        }

        static void Resolve(Entry A, Entry B)
        {
            bool aDyn = A.rb != null && A.rb.m_BodyType == 0, bDyn = B.rb != null && B.rb.m_BodyType == 0;
            if (!aDyn && !bDyn) return;   // kinematic/static contacts produce no response
            Entry dyn = aDyn ? A : B, other = aDyn ? B : A;
            if (aDyn && bDyn)
            {   // dynamic vs dynamic: report only (no response), with a contact at the overlap centre
                var kk = Key(A.c, B.c); contactsNow.Add(kk);
                float cx = (Mathf.Max(A.box.x0, B.box.x0) + Mathf.Min(A.box.x1, B.box.x1)) / 2f, cy = (Mathf.Max(A.box.y0, B.box.y0) + Mathf.Min(A.box.y1, B.box.y1)) / 2f;
                var cc = new Collision2D { m_RelVel = A.rb.m_Velocity - B.rb.m_Velocity };
                cc.m_Contacts = new[] { new ContactPoint2D { m_Point = new Vector2(cx, cy), m_Normal = (A.box.x0 + A.box.x1) < (B.box.x0 + B.box.x1) ? Vector2.left : Vector2.right } };
                contactInfo[kk] = cc;
                return;
            }
            Vector2 n; float pen;
            if (other.c is EdgeCollider2D)
            {
                float x = (dyn.box.x0 + dyn.box.x1) / 2f, y = EdgeY((EdgeCollider2D)other.c, x);
                if (float.IsNaN(y) || dyn.box.y0 > y || dyn.box.y1 < y) return;
                n = Vector2.up; pen = y - dyn.box.y0;
                if (pen > (dyn.box.y1 - dyn.box.y0) * 0.75f + 0.25f) return;  // deep: started below the ground
            }
            else
            {
                float px = Mathf.Min(dyn.box.x1 - other.box.x0, other.box.x1 - dyn.box.x0);
                float py = Mathf.Min(dyn.box.y1 - other.box.y0, other.box.y1 - dyn.box.y0);
                if (px < py) { pen = px; n = (dyn.box.x0 + dyn.box.x1) < (other.box.x0 + other.box.x1) ? Vector2.left : Vector2.right; }
                else { pen = py; n = (dyn.box.y0 + dyn.box.y1) < (other.box.y0 + other.box.y1) ? Vector2.down : Vector2.up; }
            }
            var rb = dyn.rb; var t = rb.transform; var p = t.position;
            t.position = new Vector3(p.x + n.x * pen, p.y + n.y * pen, p.z);
            dyn.box.x0 += n.x * pen; dyn.box.x1 += n.x * pen; dyn.box.y0 += n.y * pen; dyn.box.y1 += n.y * pen;
            float vn = Vector2.Dot(rb.m_Velocity, n);
            var relVel = rb.m_Velocity;
            if (vn < 0f)
            {
                float bounce = Mathf.Max(Bounce(dyn.c), Bounce(other.c));
                rb.m_Velocity -= (1f + bounce) * vn * n;
                // friction on the tangent
                var tan = new Vector2(-n.y, n.x); float vt = Vector2.Dot(rb.m_Velocity, tan);
                float fr = Mathf.Sqrt(Friction(dyn.c) * Friction(other.c));
                rb.m_Velocity -= tan * vt * Mathf.Clamp01(fr * 0.5f);
                if (n.y > 0.5f) rb.m_AngularVelocity *= 0.8f;
            }
            var k = Key(A.c, B.c);
            contactsNow.Add(k);
            var col = new Collision2D { m_RelVel = relVel };
            col.m_Contacts = new[] { new ContactPoint2D { m_Point = new Vector2((dyn.box.x0 + dyn.box.x1) / 2, n.y > 0 ? dyn.box.y0 : dyn.box.y1), m_Normal = n } };
            contactInfo[k] = col;
        }
        static float Bounce(Collider2D c) { var m = c.m_Material; return m != null ? m.m_Bounciness : 0f; }
        static float Friction(Collider2D c) { var m = c.m_Material; return m != null ? m.m_Friction : 0.4f; }

        static void TargetJoints(Rigidbody2D rb, float dt)
        {
            foreach (var comp in rb.gameObject.m_Components)
            {
                var tj = comp as TargetJoint2D;
                if (tj == null || !tj.m_Enabled) continue;
                Vector2 anchorW = rb.transform.TransformPoint(tj.m_Anchor);
                float w = 2f * Mathf.PI * tj.m_Frequency;
                Vector2 accel = (tj.m_Target - anchorW) * (w * w) - rb.m_Velocity * (2f * tj.m_Damping * w);
                float maxA = tj.m_MaxForce / rb.m_Mass;
                if (accel.magnitude > maxA) accel = accel.normalized * maxA;
                rb.m_Velocity += accel * dt;
            }
        }

        // -------------------------------------------------------------- messages
        static void Dispatch()
        {
            foreach (var k in triggersNow) Send(k, triggersPrev.Contains(k) ? "OnTriggerStay2D" : "OnTriggerEnter2D", true);
            foreach (var k in triggersPrev) if (!triggersNow.Contains(k)) Send(k, "OnTriggerExit2D", true);
            foreach (var k in contactsNow) Send(k, contactsPrev.Contains(k) ? "OnCollisionStay2D" : "OnCollisionEnter2D", false);
            foreach (var k in contactsPrev) if (!contactsNow.Contains(k)) Send(k, "OnCollisionExit2D", false);
            var t = triggersPrev; triggersPrev = triggersNow; triggersNow = t;
            var c = contactsPrev; contactsPrev = contactsNow; contactsNow = c;
            contactInfo.Clear();
        }

        static void Send(PairKey k, string msg, bool trigger)
        {
            if (k.a == null || k.b == null || k.a.m_Destroyed || k.b.m_Destroyed) return;
            Deliver(k.a, k.b, msg, trigger, k);
            Deliver(k.b, k.a, msg, trigger, k);
        }
        static void Deliver(Collider2D self, Collider2D other, string msg, bool trigger, PairKey k)
        {
            object arg = other;
            if (!trigger)
            {
                Collision2D info; contactInfo.TryGetValue(k, out info);
                var col = new Collision2D { m_Collider = other, m_OtherCollider = self, m_Rigidbody = other.attachedRigidbody };
                if (info != null) { col.m_Contacts = info.m_Contacts; col.m_RelVel = info.m_RelVel; }
                if (col.m_Contacts.Length == 0)
                    col.m_Contacts = new[] { new ContactPoint2D { m_Point = (Vector2)self.transform.position, m_Normal = Vector2.up } };
                arg = col;
            }
            Notify(self.gameObject, msg, arg);
            var rb = self.attachedRigidbody;
            if (rb != null && rb.gameObject != self.gameObject) Notify(rb.gameObject, msg, arg);
        }
        static void Notify(GameObject go, string msg, object arg)
        {
            if (go == null || go.m_Destroyed) return;
            var comps = go.m_Components;
            int count = comps.Count;
            for (int i = 0; i < count; i++)
            {
                var comp = comps[i];
                if (comp is MonoBehaviour && !comp.m_Destroyed)
                {
                    var m = Reflect.FindMethod(comp.GetType(), msg);
                    if (m == null) continue;
                    try { Reflect.Call(m, comp, arg); }
                    catch (Exception e) { Lifecycle.Fail(comp, msg, e); }
                }
            }
        }

        public static void OnDestroyed(GameObject go) { }

        // ---------------------------------------------------------------- queries
        static IEnumerable<Entry> Live(int mask)
        {
            if (entries.Count == 0 || Time.frameCount != gatheredFrame) { Gather(); gatheredFrame = Time.frameCount; }
            foreach (var e in entries)
                if (!e.c.m_Destroyed && ((1 << e.layer) & mask) != 0) yield return e;
        }
        static int gatheredFrame = -1;
        public static void Invalidate() { gatheredFrame = -1; }

        public static List<Collider2D> QueryPoint(Vector2 p, int mask)
        { return Live(mask).Where(e => ContainsPoint(e.c, p)).Select(e => e.c).ToList(); }
        public static List<Collider2D> QueryArea(Vector2 a, Vector2 b, int mask)
        {
            var q = new Aabb { x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x), y0 = Mathf.Min(a.y, b.y), y1 = Mathf.Max(a.y, b.y) };
            return Live(mask).Where(e => e.box.Overlaps(q)).Select(e => e.c).ToList();
        }
        public static List<Collider2D> QueryCircle(Vector2 c, float r, int mask)
        {
            return Live(mask).Where(e =>
            {
                float cx = Mathf.Clamp(c.x, e.box.x0, e.box.x1), cy = Mathf.Clamp(c.y, e.box.y0, e.box.y1);
                return (cx - c.x) * (cx - c.x) + (cy - c.y) * (cy - c.y) <= r * r;
            }).Select(e => e.c).ToList();
        }
        public static List<RaycastHit2D> RayAll(Vector2 o, Vector2 d, float dist, int mask, float minDepth, float maxDepth)
        {
            var hits = new List<RaycastHit2D>();
            if (d.sqrMagnitude < 1e-12f) return hits;
            d = d.normalized;
            foreach (var e in Live(mask))
            {
                float z = e.c.transform.position.z;
                if (z < minDepth || z > maxDepth) continue;
                float t; Vector2 n;
                if (!RayBox(o, d, e.box, out t, out n) || t > dist) continue;
                hits.Add(new RaycastHit2D { m_Collider = e.c, m_Point = o + d * t, m_Normal = n, m_Distance = t, m_Fraction = float.IsInfinity(dist) ? 0 : t / dist });
            }
            hits.Sort((a, b) => a.m_Distance.CompareTo(b.m_Distance));
            return hits;
        }
        static bool RayBox(Vector2 o, Vector2 d, Aabb b, out float tHit, out Vector2 n)
        {
            float tmin = 0f, tmax = float.MaxValue; n = Vector2.zero; tHit = 0;
            for (int ax = 0; ax < 2; ax++)
            {
                float oo = ax == 0 ? o.x : o.y, dd = ax == 0 ? d.x : d.y, lo = ax == 0 ? b.x0 : b.y0, hi = ax == 0 ? b.x1 : b.y1;
                if (Mathf.Abs(dd) < 1e-9f) { if (oo < lo || oo > hi) return false; continue; }
                float t1 = (lo - oo) / dd, t2 = (hi - oo) / dd; float sgn = -1f;
                if (t1 > t2) { var s = t1; t1 = t2; t2 = s; sgn = 1f; }
                if (t1 > tmin) { tmin = t1; n = ax == 0 ? new Vector2(sgn, 0) : new Vector2(0, sgn); }
                if (t2 < tmax) tmax = t2;
                if (tmin > tmax) return false;
            }
            tHit = tmin;
            return true;
        }
        public static int Cast(Collider2D c, Vector2 dir, RaycastHit2D[] results, float distance)
        {
            var me = AabbOf(c); var rb = c.attachedRigidbody;
            var ctr = new Vector2((me.x0 + me.x1) / 2, (me.y0 + me.y1) / 2); var half = new Vector2((me.x1 - me.x0) / 2, (me.y1 - me.y0) / 2);
            var hits = new List<RaycastHit2D>();
            foreach (var e in Live(-1))
            {
                if (ReferenceEquals(e.c, c) || (rb != null && ReferenceEquals(e.rb, rb)) || e.c.m_IsTrigger) continue;
                if (!LayersCollide(c.gameObject.m_Layer, e.layer)) continue;
                var grown = new Aabb { x0 = e.box.x0 - half.x, x1 = e.box.x1 + half.x, y0 = e.box.y0 - half.y, y1 = e.box.y1 + half.y };
                float t; Vector2 n;
                if (RayBox(ctr, dir.normalized, grown, out t, out n) && t <= distance)
                    hits.Add(new RaycastHit2D { m_Collider = e.c, m_Point = ctr + dir.normalized * t, m_Normal = n, m_Distance = t });
            }
            hits.Sort((a, b) => a.m_Distance.CompareTo(b.m_Distance));
            int k = Mathf.Min(hits.Count, results.Length); for (int i = 0; i < k; i++) results[i] = hits[i];
            return k;
        }
    }
}
