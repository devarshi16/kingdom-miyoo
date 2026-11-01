// Compatibility island for Kingdom's physical coin pouch. Original serialized shapes, layers,
// spawn positions and game scripts are retained; Box2D replaces the circular contact approximation.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
namespace KNL {
public static class PouchPhysics {
    const string Lib="knlcore";
    [DllImport(Lib)] static extern IntPtr knl_pouch_create(float gx,float gy);
    [DllImport(Lib)] static extern void knl_pouch_destroy(IntPtr world);
    [DllImport(Lib)] static extern IntPtr knl_pouch_filter(IntPtr world);
    [DllImport(Lib)] static extern void knl_pouch_filter_destroy(IntPtr filter);
    [DllImport(Lib)] static extern IntPtr knl_pouch_body(IntPtr world,int dynamic,float x,float y,float angle,float drag,float adrag,float gravity);
    [DllImport(Lib)] static extern int knl_pouch_polygon(IntPtr body,float[] xy,int count,int id,int sensor,float friction,float bounce);
    [DllImport(Lib)] static extern void knl_pouch_remove(IntPtr world,IntPtr filter,IntPtr body);
    [DllImport(Lib)] static extern void knl_pouch_mass(IntPtr body,float mass);
    [DllImport(Lib)] static extern void knl_pouch_layer(IntPtr filter,IntPtr body,int id,int layer,uint mask);
    [DllImport(Lib)] static extern void knl_pouch_set(IntPtr body,int active,int type,float x,float y,float angle,float vx,float vy,float av);
    [DllImport(Lib)] static extern void knl_pouch_get(IntPtr body,float[] values);
    [DllImport(Lib)] static extern void knl_pouch_step(IntPtr world,float dt);
    [DllImport(Lib)] static extern int knl_pouch_contacts(IntPtr world,int[] ids,float[] points,int capacity);
    sealed class Body { public Rigidbody2D rb;public PolygonCollider2D col;public IntPtr handle; }
    static IntPtr world,filter,wall;
    static Transform root;
    static readonly Dictionary<Rigidbody2D,Body> owned=new Dictionary<Rigidbody2D,Body>();
    static readonly Dictionary<int,Collider2D> shapes=new Dictionary<int,Collider2D>();
    static readonly HashSet<Rigidbody2D> checkedBodies=new HashSet<Rigidbody2D>();
    static readonly int[] ids=new int[2048];static readonly float[] points=new float[4096],values=new float[6];
    static float Angle(Transform t) {
        var q=t.rotation;
        return Mathf.Atan2(2f*(q.w*q.z+q.x*q.y),1f-2f*(q.x*q.x+q.z*q.z));
    }
    static bool IsBagCoin(GameObject g) { foreach(var c in g.m_Components)if(c!=null&&c.GetType().Name=="BagCoin")return true;return false; }
    static Transform FindBag(Transform t) {
        for(var p=t.parent;p!=null;p=p.parent)foreach(var c in p.gameObject.m_Components)if(c!=null&&c.GetType().Name=="CoinBag")return p;
        return null;
    }
    static void Shape(IntPtr body,PolygonCollider2D c,Transform origin) {
        float angle=Angle(origin),cs=Mathf.Cos(angle),sn=Mathf.Sin(angle);var pos=origin.position;
        foreach(var path in c.m_Paths) {
            var xy=new float[path.Length*2];
            for(int i=0;i<path.Length;i++) { var p=c.transform.TransformPoint(path[i]+c.offset)-pos;xy[i*2]=cs*p.x+sn*p.y;xy[i*2+1]=-sn*p.x+cs*p.y; }
            var m=c.sharedMaterial;
            if(knl_pouch_polygon(body,xy,path.Length,c.GetInstanceID(),c.isTrigger?1:0,m!=null?m.friction:0.4f,m!=null?m.bounciness:0f)==0)throw new InvalidOperationException("Cannot decompose pouch collider "+c.name);
        }
        shapes[c.GetInstanceID()]=c;
        Layer(body,c);
    }
    static void Layer(IntPtr handle,Collider2D c) { knl_pouch_layer(filter,handle,c.GetInstanceID(),c.gameObject.layer,Physics2DSystem.CollisionMask(c.gameObject.layer)); }
    public static void Reset() {
        if(world!=IntPtr.Zero)knl_pouch_destroy(world);
        if(filter!=IntPtr.Zero)knl_pouch_filter_destroy(filter);
        world=filter=wall=IntPtr.Zero;root=null;owned.Clear();shapes.Clear();checkedBodies.Clear();
    }
    public static void Prepare(List<Rigidbody2D> bodies) {
        if(!ReferenceEquals(root,null)&&root.m_Destroyed)Reset();
        foreach(var rb in bodies) {
            if(rb==null||owned.ContainsKey(rb)||checkedBodies.Contains(rb))continue;
            if(!IsBagCoin(rb.gameObject)){checkedBodies.Add(rb);continue;}
            var bag=FindBag(rb.transform);if(bag==null)continue;
            if(root==null) {
                root=bag;world=knl_pouch_create(Physics2DSystem.Gravity.x,Physics2DSystem.Gravity.y);filter=knl_pouch_filter(world);
                var p=root.position;wall=knl_pouch_body(world,0,p.x,p.y,Angle(root),0,0,0);
                foreach(var c in root.GetComponentsInChildren<PolygonCollider2D>(true))if(c.attachedRigidbody==null)Shape(wall,c,root);
            }
            if(bag!=root)throw new InvalidOperationException("Multiple simultaneous coin pouches are not supported");
            var col=rb.GetComponentInChildren<PolygonCollider2D>();if(col==null)throw new InvalidOperationException("Bag coin has no polygon collider");
            var pos=rb.position;var h=knl_pouch_body(world,1,pos.x,pos.y,Angle(rb.transform),rb.drag,rb.angularDrag,rb.gravityScale);
            Shape(h,col,rb.transform);knl_pouch_mass(h,rb.mass);owned.Add(rb,new Body {rb=rb,col=col,handle=h});
        }
    }
    internal static void OnDestroyed(GameObject go) {
        var rb=go.GetComponent<Rigidbody2D>();if(ReferenceEquals(rb,null))return;
        checkedBodies.Remove(rb);Body b;
        if(owned.TryGetValue(rb,out b)){knl_pouch_remove(world,filter,b.handle);owned.Remove(rb);shapes.Remove(b.col.GetInstanceID());}
    }
    public static bool Owns(Rigidbody2D rb) { return !ReferenceEquals(rb,null)&&owned.ContainsKey(rb); }
    public static bool Handles(Collider2D a,Collider2D b) { return shapes.ContainsKey(a.GetInstanceID())&&shapes.ContainsKey(b.GetInstanceID()); }
    public static void Step(float dt) {
        if(world==IntPtr.Zero)return;
        var rp=root.position;knl_pouch_set(wall,root.gameObject.activeInHierarchy?1:0,2,rp.x,rp.y,Angle(root),0,0,0);
        foreach(var b in owned.Values) {
            var rb=b.rb;
            bool active=rb!=null&&rb.simulated&&rb.gameObject.activeInHierarchy&&b.col!=null&&b.col.enabled&&b.col.gameObject.activeInHierarchy;
            if(!active){knl_pouch_set(b.handle,0,2,0,0,0,0,0,0);continue;}
            Layer(b.handle,b.col);var p=rb.position;var v=rb.velocity;
            knl_pouch_set(b.handle,1,rb.m_BodyType,p.x,p.y,Angle(rb.transform),v.x,v.y,rb.angularVelocity*Mathf.Deg2Rad);
            knl_pouch_mass(b.handle,rb.mass); // reactivation/type changes recompute native fixture mass
        }
        knl_pouch_step(world,dt);
        foreach(var b in owned.Values)if(b.rb!=null&&b.rb.simulated&&b.rb.gameObject.activeInHierarchy&&b.col!=null&&b.col.enabled) {
            knl_pouch_get(b.handle,values);b.rb.position=new Vector2(values[0],values[1]);b.rb.rotation=values[2]*Mathf.Rad2Deg;
            b.rb.m_Velocity=new Vector2(values[3],values[4]);b.rb.m_AngularVelocity=values[5]*Mathf.Rad2Deg;
        }
    }
    public static void Contacts() {
        if(world==IntPtr.Zero)return;
        int n=knl_pouch_contacts(world,ids,points,ids.Length/2);
        if(n==ids.Length/2)throw new InvalidOperationException("Pouch contact buffer exhausted");
        for(int i=0;i<n;i++) {
            Collider2D a,b;if(!shapes.TryGetValue(ids[i*2],out a)||!shapes.TryGetValue(ids[i*2+1],out b)||a==null||b==null)continue;
            Physics2DSystem.PouchContact(a,b,new Vector2(points[i*4],points[i*4+1]),new Vector2(points[i*4+2],points[i*4+3]));
        }
    }
}
}
