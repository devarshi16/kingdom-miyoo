// Point-to-point 2D hinge subset used by the original catapult's free wheel axles.
// Uses the same approximate body inertia as Rigidbody2D.AddTorque. Motors, limits and
// break thresholds are outside this subset; the shipped wheel joints use none of them.
using System;
using System.Collections.Generic;
using UnityEngine;
namespace UnityEngine {
public partial class Joint2D {
    internal Rigidbody2D m_ConnectedBody;
    internal bool m_EnableCollision;
    public Rigidbody2D connectedBody { get { return m_ConnectedBody; } set { m_ConnectedBody = value; } }
    public bool enableCollision { get { return m_EnableCollision; } set { m_EnableCollision = value; } }
}
public partial class AnchoredJoint2D {
    internal Vector2 m_Anchor, m_ConnectedAnchor;
    internal bool m_AutoConfigureConnectedAnchor;
    public Vector2 anchor { get { return m_Anchor; } set { m_Anchor = value; } }
    public Vector2 connectedAnchor { get { return m_ConnectedAnchor; } set { m_ConnectedAnchor = value; } }
    public bool autoConfigureConnectedAnchor { get { return m_AutoConfigureConnectedAnchor; } set { m_AutoConfigureConnectedAnchor = value; } }
}
public sealed partial class HingeJoint2D {
    internal static readonly List<HingeJoint2D> s_All = new List<HingeJoint2D>();
    internal bool m_UseMotor, m_UseLimits;
    public bool useMotor { get { return m_UseMotor; } set { m_UseMotor = value; } }
    public bool useLimits { get { return m_UseLimits; } set { m_UseLimits = value; } }
}
}
namespace KNL {
public static class HingePhysics {
    sealed class Body {
        public Rigidbody2D rb;
        public Vector2 center;
        public Quaternion rotation;
        public Matrix4x4 matrix;
        public float angle, mx, my, inertia;
        public int depth;
        public void Capture() {
            var t=rb.transform;
            center=rb.worldCenterOfMass; rotation=t.rotation; matrix=t.localToWorldMatrix; angle=0;
            bool movable=rb.m_BodyType==0 && rb.m_Simulated;
            float mass=movable?1f/rb.m_Mass:0;
            mx=(rb.m_Constraints&RigidbodyConstraints2D.FreezePositionX)==0?mass:0;
            my=(rb.m_Constraints&RigidbodyConstraints2D.FreezePositionY)==0?mass:0;
            inertia=(rb.m_Constraints&RigidbodyConstraints2D.FreezeRotation)==0?4f*mass:0;
        }
        public Vector2 Offset(Vector2 local) {
            Vector2 v=matrix.MultiplyVector(local);
            if(angle==0)return v;
            float c=Mathf.Cos(angle),s=Mathf.Sin(angle);
            return new Vector2(c*v.x-s*v.y,s*v.x+c*v.y);
        }
        public void Commit() {
            var p=rb.transform.position;var root=center-Offset(rb.m_CenterOfMass);
            rb.transform.SetPositionAndRotation(new Vector3(root.x,root.y,p.z),rotation*Quaternion.Euler(0,0,angle*Mathf.Rad2Deg));
        }
    }
    sealed class Link { public HingeJoint2D joint; public Body a,b; }
    static readonly List<Body> bodies=new List<Body>();
    static readonly List<Body> pool=new List<Body>();
    static readonly Dictionary<Rigidbody2D,Body> byBody=new Dictionary<Rigidbody2D,Body>();
    static readonly List<Link> links=new List<Link>(),linkPool=new List<Link>();
    static Body Get(Rigidbody2D rb) {
        if(rb==null)return null;
        Body b;if(byBody.TryGetValue(rb,out b))return b;
        if(pool.Count==bodies.Count)pool.Add(new Body());b=pool[bodies.Count];b.rb=rb;b.Capture();
        b.depth=0;for(var t=rb.transform.parent;t!=null;t=t.parent)b.depth++;
        bodies.Add(b);byBody.Add(rb,b);return b;
    }
    static bool Live(HingeJoint2D j) {
        return j!=null&&!j.m_Destroyed&&j.enabled&&j.gameObject.activeInHierarchy;
    }
    public static void Prepare() {
        bodies.Clear();byBody.Clear();links.Clear();
        foreach(var j in HingeJoint2D.s_All) {
            if(!Live(j))continue;
            var a=j.GetComponent<Rigidbody2D>();var b=j.m_ConnectedBody;
            if(a==null||!a.simulated||(b!=null&&(!b.simulated||!b.gameObject.activeInHierarchy)))continue;
            if(linkPool.Count==links.Count)linkPool.Add(new Link());var l=linkPool[links.Count];
            l.joint=j;l.a=Get(a);l.b=Get(b);links.Add(l);
        }
        // Parent rigidbodies commit first. A wheel body retains an independent world pose
        // even though its Transform is a child of the moving chassis.
        bodies.Sort((a,b)=>a.depth.CompareTo(b.depth));
    }
    public static void BeforeIntegrate(Rigidbody2D rb) { Body b;if(byBody.TryGetValue(rb,out b))b.Commit(); }
    public static void AfterIntegrate(Rigidbody2D rb) { Body b;if(byBody.TryGetValue(rb,out b))b.Capture(); }
    public static void FinishIntegrate() { foreach(var b in bodies)b.Commit(); }
    public static bool SuppressCollision(Rigidbody2D a,Rigidbody2D b) {
        if(a==null||b==null)return false;
        foreach(var l in links)if(!l.joint.enableCollision&&l.b!=null&&
            ((ReferenceEquals(l.a.rb,a)&&ReferenceEquals(l.b.rb,b))||(ReferenceEquals(l.a.rb,b)&&ReferenceEquals(l.b.rb,a))))return true;
        return false;
    }
    public static bool ContainsBody(Rigidbody2D body) { return body != null && byBody.ContainsKey(body); }
    static float Cross(Vector2 a,Vector2 b){return a.x*b.y-a.y*b.x;}
    static Vector2 Spin(float w,Vector2 r){return new Vector2(-w*r.y,w*r.x);}
    static Vector2 SolveMatrix(Body a,Body b,Vector2 ra,Vector2 rb,Vector2 c) {
        float ia=a.inertia,ib=b!=null?b.inertia:0;
        float xx=a.mx+(b!=null?b.mx:0)+ia*ra.y*ra.y+ib*rb.y*rb.y;
        float yy=a.my+(b!=null?b.my:0)+ia*ra.x*ra.x+ib*rb.x*rb.x;
        float xy=-ia*ra.x*ra.y-ib*rb.x*rb.y,det=xx*yy-xy*xy;
        if(det>1e-10f)return new Vector2((xy*c.y-yy*c.x)/det,(xy*c.x-xx*c.y)/det);
        return new Vector2(xx>1e-10f?-c.x/xx:0,yy>1e-10f?-c.y/yy:0);
    }
    public static void Solve(int positionIterations=12,int velocityIterations=8) {
        if(links.Count==0)return;
        foreach(var b in bodies)b.Capture();
        for(int iteration=0;iteration<positionIterations;iteration++)foreach(var l in links) {
            var a=l.a;var b=l.b;
            var ra=a.Offset(l.joint.anchor-a.rb.m_CenterOfMass);
            var rb=b!=null?b.Offset(l.joint.connectedAnchor-b.rb.m_CenterOfMass):Vector2.zero;
            var pa=a.center+ra;var pb=b!=null?b.center+rb:l.joint.connectedAnchor;
            var error=pa-pb;float length=error.magnitude;if(length>.2f)error*=.2f/length;
            var impulse=SolveMatrix(a,b,ra,rb,error);
            a.center+=new Vector2(impulse.x*a.mx,impulse.y*a.my);a.angle+=a.inertia*Cross(ra,impulse);
            if(b!=null){b.center-=new Vector2(impulse.x*b.mx,impulse.y*b.my);b.angle-=b.inertia*Cross(rb,impulse);}
        }
        for(int iteration=0;iteration<velocityIterations;iteration++)foreach(var l in links) {
            var a=l.a;var b=l.b;
            var ra=a.Offset(l.joint.anchor-a.rb.m_CenterOfMass);var rb=b!=null?b.Offset(l.joint.connectedAnchor-b.rb.m_CenterOfMass):Vector2.zero;
            var va=a.rb.velocity+Spin(a.rb.angularVelocity*Mathf.Deg2Rad,ra);
            var vb=b!=null?b.rb.velocity+Spin(b.rb.angularVelocity*Mathf.Deg2Rad,rb):Vector2.zero;
            var impulse=SolveMatrix(a,b,ra,rb,va-vb);
            a.rb.velocity+=new Vector2(impulse.x*a.mx,impulse.y*a.my);a.rb.angularVelocity+=a.inertia*Cross(ra,impulse)*Mathf.Rad2Deg;
            if(b!=null){b.rb.velocity-=new Vector2(impulse.x*b.mx,impulse.y*b.my);b.rb.angularVelocity-=b.inertia*Cross(rb,impulse)*Mathf.Rad2Deg;}
        }
        foreach(var b in bodies)b.Commit();
    }
}
}
