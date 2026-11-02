using System;
using KNL;
using UnityEngine;
class HingeRegression {
    static int passed;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
    static float Error(HingeJoint2D h){Vector2 a=h.transform.TransformPoint(h.anchor);Vector2 b=h.connectedBody!=null?(Vector2)h.connectedBody.transform.TransformPoint(h.connectedAnchor):h.connectedAnchor;return(a-b).magnitude;}
    static void Step(int count){for(int i=0;i<count;i++)Physics2DSystem.Step();}
    static void Main(){
        World.ActiveScene=new SceneTag("hinge regression");
        var floor=new GameObject("ground").AddComponent<EdgeCollider2D>();floor.points=new[]{new Vector2(-200,0),new Vector2(200,0)};
        var chassis=new GameObject("chassis");chassis.transform.position=new Vector3(0,.328125f,0);var root=chassis.AddComponent<Rigidbody2D>();root.mass=5;root.drag=1;root.angularDrag=0;
        HingeJoint2D[] joints=new HingeJoint2D[2];Rigidbody2D[] wheels=new Rigidbody2D[2];
        for(int i=0;i<2;i++){float x=i==0?-.421875f:.421875f;var w=new GameObject("wheel"+i);w.transform.SetParent(chassis.transform,false);w.transform.localPosition=new Vector3(x,.171875f,0);wheels[i]=w.AddComponent<Rigidbody2D>();wheels[i].angularDrag=0;w.AddComponent<CircleCollider2D>().radius=.5f;joints[i]=w.AddComponent<HingeJoint2D>();joints[i].connectedBody=root;joints[i].connectedAnchor=new Vector2(x,.171875f);}
        Step(1000);
        Check(Error(joints[0])<.003f&&Error(joints[1])<.003f,"both axles remain attached after 1000 gravity steps");
        Check(Math.Abs(root.position.y-.328125f)<.025f,"chassis supported above the ground by its wheels");
        Check(wheels[0].position.y>.47f&&wheels[1].position.y>.47f,"wheel contacts support assembly without sinking");
        wheels[0].angularVelocity=180;Step(1);Check(Math.Abs(wheels[0].rotation-root.rotation)>1f,"free axle permits wheel rotation independently of chassis");Step(99);
        root.AddForce(new Vector2(7,0),ForceMode2D.Impulse);Step(300);
        Check(root.position.x>.01f&&Error(joints[0])<.003f&&Error(joints[1])<.003f,"pushed chassis moves with both attached wheels");
        var clone=UnityEngine.Object.Instantiate(chassis);var cloneRoot=clone.GetComponent<Rigidbody2D>();
        Check(clone.GetComponentInChildren<HingeJoint2D>().connectedBody==cloneRoot,"cloned axle connects to cloned chassis");
        UnityEngine.Object.DestroyImmediate(clone);
        var free=new GameObject("disabled hinge");free.transform.position=new Vector3(20,5,0);var fr=free.AddComponent<Rigidbody2D>();var fh=free.AddComponent<HingeJoint2D>();fh.connectedAnchor=new Vector2(20,5);fh.enabled=false;Step(20);Check(fr.position.y<4.8f,"disabled hinge does not suspend a falling body");
        UnityEngine.Object.DestroyImmediate(free);
        var pinned=new GameObject("world hinge");pinned.transform.position=new Vector3(25,5,0);var pr=pinned.AddComponent<Rigidbody2D>();var ph=pinned.AddComponent<HingeJoint2D>();ph.connectedAnchor=new Vector2(25,5);Step(100);Check(Error(ph)<.001f&&pr.velocity.magnitude<.001f,"unconnected axle uses fixed world anchor");
        Check(Lifecycle.Exceptions==0,"hinge fixtures have no lifecycle exceptions");
        Console.WriteLine("Hinge checks passed: "+passed);
    }
}
