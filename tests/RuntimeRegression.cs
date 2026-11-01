using System;
using System.Collections;
using System.IO;
using System.Threading;
using KNL;
using UnityEngine;
class Owner : MonoBehaviour { public int after; public bool disposed; public bool quit; void OnApplicationQuit(){quit=true;} }
class StartYield : MonoBehaviour { public int first=-1,second=-1; IEnumerator Start(){ first=Time.frameCount;yield return null;second=Time.frameCount;} }
class UpdateYield : MonoBehaviour { public int first=-1,second=-1; void Update(){if(first<0){first=Time.frameCount;StartCoroutine(Run());}} IEnumerator Run(){yield return null;second=Time.frameCount;} }
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
float t=Time.unscaledTime;Lifecycle.Frame(0.05f,0.2f);Check(Math.Abs(Time.unscaledTime-t-0.2f)<0.001f,"Unscaled clock is not capped by simulation");float real=Time.realtimeSinceStartup;Thread.Sleep(25);Check(Time.realtimeSinceStartup>real,"Realtime advances between frames");
var quitting=new GameObject("quit").AddComponent<Owner>();Frame();Lifecycle.ApplicationQuit();Check(quitting.quit,"Application quit dispatched");Directory.Delete(Paths.SaveDir,true);
Console.WriteLine("Runtime regressions passed: "+passed);
}
}
