using System;
using System.Collections;
using System.IO;
using System.Threading;
using KNL;
using UnityEngine;
class Owner : MonoBehaviour { public int after; public bool disposed; public bool quit; void OnApplicationQuit(){quit=true;} }
class StartYield : MonoBehaviour { public int first=-1,second=-1; IEnumerator Start(){ first=Time.frameCount;yield return null;second=Time.frameCount;} }
class UpdateYield : MonoBehaviour { public int first=-1,second=-1; void Update(){if(first<0){first=Time.frameCount;StartCoroutine(Run());}} IEnumerator Run(){yield return null;second=Time.frameCount;} }
class TriggerTracker : MonoBehaviour { public int hits;void OnTriggerEnter2D(Collider2D other){hits++;} }
class RuntimeRegression {
static int passed;
static void Check(bool b,string name){if(!b)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);passed++;}
static IEnumerator Wait(Owner o,object instruction){yield return instruction;o.after++;}
static IEnumerator Long(Owner o){try{while(true)yield return null;}finally{o.disposed=true;}}
static IEnumerator Child(Owner o){yield return null;o.after++;}
static IEnumerator Parent(Owner o){yield return Child(o);o.after+=10;}
static void Frame(float dt=0.033333f){Lifecycle.Frame(dt);}
static void Main(){
World.ActiveScene=new SceneTag("regression");Paths.SaveDir=Path.Combine(Path.GetTempPath(),"knl-prefs-"+Guid.NewGuid());
var owner=new GameObject("owner").AddComponent<Owner>();
var start=new GameObject("start").AddComponent<StartYield>();Frame();Check(start.second<0,"Start yield null waits for next frame");Frame();Check(start.second>start.first,"Start resumes next frame");
var update=new GameObject("update").AddComponent<UpdateYield>();Frame();Check(update.second<0,"Update-started yield null waits");Frame();Check(update.second>update.first,"Update iterator resumes next frame");
Time.timeScale=0;Coroutines.Start(owner,Wait(owner,new WaitForFixedUpdate()));Frame();Check(owner.after==0,"Fixed wait stays suspended while paused");Time.timeScale=1;Frame();Check(owner.after==1,"Fixed wait resumes after physics");
owner.after=0;Coroutines.Start(owner,Wait(owner,new WaitForEndOfFrame()));Frame();Check(owner.after==0,"End-of-frame wait stays suspended during Update");Coroutines.TickEndOfFrame();Check(owner.after==1,"End-of-frame wait resumes after render");
owner.after=0;Coroutines.Start(owner,Wait(owner,null));owner.gameObject.SetActive(false);owner.gameObject.SetActive(true);Frame();Check(owner.after==0,"Deactivation cancels coroutine permanently");
Coroutines.Start(owner,Long(owner));owner.gameObject.SetActive(false);Check(owner.disposed,"Cancellation disposes iterator");owner.gameObject.SetActive(true);
owner.after=0;owner.enabled=false;Coroutines.Start(owner,Wait(owner,null));Frame();Check(owner.after==1,"Disabling MonoBehaviour alone does not cancel coroutine");owner.enabled=true;
owner.after=0;Coroutines.Start(owner,Parent(owner));Frame();Frame();Check(owner.after==11,"Nested iterators complete in order");
var p1=new GameObject("p1");var rb1=p1.AddComponent<Rigidbody2D>();var p2=new GameObject("p2");var rb2=p2.AddComponent<Rigidbody2D>();var child=new GameObject("child");child.transform.parent=p1.transform;var col=child.AddComponent<BoxCollider2D>();Check(col.attachedRigidbody==rb1,"Initial parent body");child.transform.parent=p2.transform;Check(col.attachedRigidbody==rb2,"Reparent invalidates body cache");
var added=new GameObject("addbody");var ac=added.AddComponent<BoxCollider2D>();Check(ac.attachedRigidbody==null,"Initial body absent");var ar=added.AddComponent<Rigidbody2D>();Check(ac.attachedRigidbody==ar,"Adding body invalidates cached null");UnityEngine.Object.DestroyImmediate(ar);Check(ac.attachedRigidbody==null,"Removing body invalidates cache");
var trRoot=new GameObject("matrix root");var trLeaf=new GameObject("matrix leaf");trLeaf.transform.SetParent(trRoot.transform,false);trLeaf.transform.localPosition=new Vector3(1,2,0);var cached=trLeaf.transform.localToWorldMatrix;trRoot.transform.localPosition=new Vector3(3,4,0);Check(trLeaf.transform.TransformPoint(Vector3.zero)==new Vector3(4,6,0),"Parent motion invalidates descendant matrix in same frame");
trRoot.transform.localScale=new Vector3(2,3,1);Check(trLeaf.transform.TransformPoint(Vector3.zero)==new Vector3(5,10,0),"Parent scale invalidates descendant matrix");trLeaf.transform.localPosition=new Vector3(2,1,0);Check(trLeaf.transform.TransformPoint(Vector3.zero)==new Vector3(7,7,0),"Local writes invalidate previously read matrix");
var trOther=new GameObject("matrix other");trOther.transform.localPosition=new Vector3(10,0,0);trLeaf.transform.SetParent(trOther.transform,false);Check(trLeaf.transform.TransformPoint(Vector3.zero)==new Vector3(12,1,0),"Reparent without pose preservation invalidates matrix");var clone=UnityEngine.Object.Instantiate(trOther);Check(clone.transform.GetChild(0).TransformPoint(Vector3.zero)==new Vector3(12,1,0),"Clone resets inherited matrix cache");clone.transform.localPosition=new Vector3(20,0,0);Check(clone.transform.GetChild(0).TransformPoint(Vector3.zero)==new Vector3(22,1,0),"Cloned descendants follow cloned parent");
var ma=new Matrix4x4();var mbx=new Matrix4x4();var rng=new System.Random(83);bool matrixOk=true;for(int sample=0;sample<100;sample++){for(int i=0;i<16;i++){ma[i]=(float)(rng.NextDouble()*20-10);mbx[i]=(float)(rng.NextDouble()*20-10);}var actual=ma*mbx;for(int r=0;r<4;r++)for(int c=0;c<4;c++){float expected=ma[r,0]*mbx[0,c]+ma[r,1]*mbx[1,c]+ma[r,2]*mbx[2,c]+ma[r,3]*mbx[3,c];if(actual[r,c]!=expected)matrixOk=false;}}Check(matrixOk,"Unrolled matrices match indexed multiplication across 100 random pairs");
var activeParent=new GameObject("active parent");var activeChild=new GameObject("active child");activeChild.transform.SetParent(activeParent.transform,false);Check(activeChild.activeInHierarchy,"Child initially active");activeParent.SetActive(false);Check(!activeChild.activeInHierarchy,"Parent activation invalidates cached child immediately");activeParent.SetActive(true);Check(activeChild.activeInHierarchy,"Parent reactivation refreshes cached child immediately");
var thinGo=new GameObject("thin polygon");thinGo.transform.position=new Vector3(100,0,0);var thinRb=thinGo.AddComponent<Rigidbody2D>();thinRb.gravityScale=0;var thin=thinGo.AddComponent<PolygonCollider2D>();thin.points=new[]{new Vector2(-.21f,-.19f),new Vector2(-.19f,-.21f),new Vector2(.21f,.19f),new Vector2(.19f,.21f)};var tracker=thinGo.AddComponent<TriggerTracker>();
var nearGo=new GameObject("near polygon trigger");nearGo.transform.position=new Vector3(100.13f,-.13f,0);var near=nearGo.AddComponent<PolygonCollider2D>();near.isTrigger=true;near.points=new[]{new Vector2(-.03f,-.03f),new Vector2(.03f,-.03f),new Vector2(.03f,.03f),new Vector2(-.03f,.03f)};Frame();Physics2DSystem.Step();Check(tracker.hits==0,"Thin polygon does not trigger on bounding-circle overlap");nearGo.transform.position=new Vector3(100,0,0);Physics2DSystem.Step();Check(tracker.hits>0,"Crossing polygon edges generate a trigger");
bool planarOk=true;for(int i=0;i<150;i++){float angle=(float)(rng.NextDouble()*14400-7200);var expected=Quaternion.AngleAxis(0,Vector3.up)*Quaternion.AngleAxis(0,Vector3.right)*Quaternion.AngleAxis(angle,Vector3.forward);var actual=Quaternion.Euler(0,0,angle);if(actual.x!=expected.x||actual.y!=expected.y||actual.z!=expected.z||actual.w!=expected.w)planarOk=false;}Check(planarOk,"Planar Euler path equals identity-product reference across 150 angles");
var bodyParent=new GameObject("body parent");bodyParent.transform.rotation=Quaternion.Euler(0,0,20);var bodyChild=new GameObject("body child");bodyChild.transform.SetParent(bodyParent.transform,false);var planarBody=bodyChild.AddComponent<Rigidbody2D>();planarBody.rotation=35;Check(Math.Abs(planarBody.rotation-35)<.0001f,"Planar rigidbody rotation retains world-space semantics under a parent");
float t=Time.unscaledTime;Lifecycle.Frame(0.05f,0.2f);Check(Math.Abs(Time.unscaledTime-t-0.2f)<0.001f,"Unscaled clock is not capped by simulation");float real=Time.realtimeSinceStartup;Thread.Sleep(25);Check(Time.realtimeSinceStartup>real,"Realtime advances between frames");
var quitting=new GameObject("quit").AddComponent<Owner>();Frame();Lifecycle.ApplicationQuit();Check(quitting.quit,"Application quit dispatched");Directory.Delete(Paths.SaveDir,true);
Console.WriteLine("Runtime regressions passed: "+passed);
}
}
