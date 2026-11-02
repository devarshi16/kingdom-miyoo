// QA instrumentation for the owned ORIGINAL Unity player; never a replacement-engine shim.
using System;using System.Collections;using System.Collections.Generic;using System.Reflection;using System.IO;using UnityEngine;
public sealed class OriginalEnemyVisibility:MonoBehaviour {
 static OriginalEnemyVisibility self;const BindingFlags B=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;Assembly asm;List<GameObject> foes=new List<GameObject>();float wallX;string dir;object director;int frame;
 public static void Ensure(){if(self!=null)return;var g=new GameObject("OriginalEnemyVisibility");DontDestroyOnLoad(g);self=g.AddComponent<OriginalEnemyVisibility>();Debug.Log("[enemy-original] BOOT "+Application.unityVersion);}
 object F(object x,string n){return x.GetType().GetField(n,B).GetValue(x);} void S(object x,string n,object v){x.GetType().GetField(n,B).SetValue(x,v);} object Call(object x,string n,params object[] a){return x.GetType().GetMethod(n,B).Invoke(x,a);}
 Component First(string type){foreach(var x in UnityEngine.Object.FindObjectsOfType(asm.GetType(type))){var c=(Component)x;if(c.gameObject.activeInHierarchy)return c;}return null;}
 IEnumerator Start(){
  asm=Assembly.Load("Assembly-CSharp");dir=Path.Combine(Application.dataPath,"../enemy-shots");Directory.CreateDirectory(dir);
  float deadline=Time.realtimeSinceStartup+60;Component pl=null;
  while(Time.realtimeSinceStartup<deadline){pl=First("Player");director=First("Director");if(pl!=null&&director!=null&&First("EnemyManager")!=null)break;yield return null;}
  if(pl==null||director==null){Debug.LogError("[enemy-original] NO_READY_PLAYER");Application.Quit();yield break;}
  Time.captureFramerate=30;QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;Time.timeScale=1;
  if(Environment.GetEnvironmentVariable("KNL_ORIGINAL_POOL_REUSE")=="1"){
   S(director,"_currentTime",22f);yield return StartCoroutine(PoolReuseTrial(pl));Application.Quit();yield break;
  }
  for(int i=0;i<180;i++){S(director,"_currentTime",22f);yield return null;}
  var walls=UnityEngine.Object.FindObjectsOfType(asm.GetType("Wall"));Component wall=null;float best=float.MaxValue;float target=pl.transform.position.x;string requested=Environment.GetEnvironmentVariable("KNL_ENEMY_WALL_X");if(!string.IsNullOrEmpty(requested))target=float.Parse(requested,System.Globalization.CultureInfo.InvariantCulture);
  foreach(var x in walls){var c=(Component)x;Debug.Log("[enemy-original] WALL "+c.name+" "+c.transform.position);if(c.gameObject.activeInHierarchy&&Mathf.Abs(c.transform.position.x-target)<best){best=Mathf.Abs(c.transform.position.x-target);wall=c;}}
  if(wall==null){Debug.LogError("[enemy-original] NO_WALL");Application.Quit();yield break;}
  wallX=wall.transform.position.x;float side=wallX<0?-1:1;pl.transform.position=new Vector3(wallX-side*2,pl.transform.position.y,pl.transform.position.z);
  var wallet=pl.GetComponent(asm.GetType("Wallet"));wallet.GetType().GetProperty("coins",B).SetValue(wallet,1000,null);
  var manager=First("EnemyManager");var prefabObject=F(manager,"trollPrefab");var prefab=prefabObject as GameObject??((Component)prefabObject).gameObject;var pool=asm.GetType("Pool");MethodInfo spawn=null;foreach(var m in pool.GetMethods(BindingFlags.Static|BindingFlags.Public))if(m.Name=="SpawnGO"&&m.GetParameters().Length==6)spawn=m;
  for(int i=0;i<24;i++){var go=(GameObject)spawn.Invoke(null,new object[]{prefab,new Vector3(wallX+side*(1.5f+i*.18f),.875f,1.5f),Quaternion.identity,pl.transform.parent,true,true});go.name="qa-original-greed-"+i;foes.Add(go);if(i%2==0)Call(go.GetComponent(asm.GetType("Troll")),"MakeToughTroll");}
  Debug.Log("[enemy-original] ARENA "+wallX+" player="+pl.transform.position+" save="+Application.persistentDataPath);
  for(frame=0;frame<=300;frame++){S(director,"_currentTime",22f);if(frame%30==0)foreach(var go in foes)if(go!=null&&go.activeInHierarchy){var r=go.GetComponent<SpriteRenderer>();var damage=go.GetComponent(asm.GetType("Damageable"));Debug.Log("[enemy-original] ACTOR "+frame+" "+go.name+" pos="+go.transform.position+" alpha="+r.color.a+" dead="+damage.GetType().GetProperty("isDead",B).GetValue(damage,null)+" sprite="+(r.sprite!=null?r.sprite.name:"NULL"));}
   yield return new WaitForEndOfFrame();if(frame==0||frame==120||frame==240)Capture();}
  Debug.Log("[enemy-original] COMPLETE");Application.Quit();
 }
 IEnumerator PoolReuseTrial(Component pl){
  var manager=First("EnemyManager");var prefabObject=F(manager,"trollPrefab");var prefab=prefabObject as GameObject??((Component)prefabObject).gameObject;var pool=asm.GetType("Pool");
  MethodInfo spawn=null,despawn=null,damageMethod=null;
  foreach(var m in pool.GetMethods(BindingFlags.Static|BindingFlags.Public))if(m.Name=="SpawnGO"&&m.GetParameters().Length==6)spawn=m;
  foreach(var m in pool.GetMethods(BindingFlags.Static|BindingFlags.Public))if(m.Name=="Despawn"&&m.GetParameters().Length==1&&m.GetParameters()[0].ParameterType==typeof(GameObject))despawn=m;
  var parent=pl.transform.parent;var target=(GameObject)spawn.Invoke(null,new object[]{prefab,new Vector3(pl.transform.position.x+3f,.875f,1.5f),Quaternion.identity,parent,true,true});
  target.name="qa-original-death-reuse";var troll=target.GetComponent(asm.GetType("Troll"));Call(troll,"MakeToughTroll");
  for(int i=0;i<4;i++)yield return new WaitForEndOfFrame();
  var damage=target.GetComponent(asm.GetType("Damageable"));foreach(var m in damage.GetType().GetMethods(B))if(m.Name=="ReceiveDamage"&&m.GetParameters().Length==3)damageMethod=m;
  var args=new object[3];var pars=damageMethod.GetParameters();args[0]=Convert.ChangeType(100f,pars[0].ParameterType);for(int i=1;i<args.Length;i++)args[i]=pars[i].ParameterType.IsValueType?Activator.CreateInstance(pars[i].ParameterType):null;
  Debug.Log("[enemy-original] POOLTEST damage_method="+damageMethod+" initial="+Snap(target));damageMethod.Invoke(damage,args);
  int wait=0;while(target.activeInHierarchy&&wait<240){if(wait%30==0)Debug.Log("[enemy-original] DEATH_WAIT frame="+wait+" "+Snap(target));wait++;yield return new WaitForEndOfFrame();}
  Debug.Log("[enemy-original] DEATH_END wait="+wait+" active="+target.activeInHierarchy+" "+Snap(target));
  if(target.activeInHierarchy||despawn==null){Debug.LogError("[enemy-original] POOLTEST_TARGET_NOT_POOLED");yield break;}
  for(int cycle=0;cycle<20;cycle++){
   var held=new List<GameObject>();GameObject next=null;
   for(int i=0;i<80;i++){
    next=(GameObject)spawn.Invoke(null,new object[]{prefab,new Vector3(pl.transform.position.x+3f,.875f,1.5f),Quaternion.identity,parent,false,false});
    if(next==null)break;if(object.ReferenceEquals(next,target))break;held.Add(next);
   }
   foreach(var g in held)despawn.Invoke(null,new object[]{g});
   if(!object.ReferenceEquals(next,target)){Debug.LogError("[enemy-original] POOLTEST_TARGET_NOT_REUSED cycle="+cycle+" got="+(next!=null?next.GetInstanceID().ToString():"null"));yield break;}
   Debug.Log("[enemy-original] REUSE cycle="+cycle+" "+Snap(target));yield return new WaitForEndOfFrame();
   despawn.Invoke(null,new object[]{target});yield return new WaitForEndOfFrame();
  }
  Debug.Log("[enemy-original] POOLTEST_COMPLETE id="+target.GetInstanceID());
 }
 string Snap(GameObject go){
  var sr=go.GetComponent<SpriteRenderer>();var damage=go.GetComponent(asm.GetType("Damageable"));var anim=go.GetComponent<Animator>();var mask=F(go.GetComponent(asm.GetType("Troll")),"mask") as SpriteRenderer;
  var d=damage.GetType().GetProperty("hitPoints",B).GetValue(damage,null);var dead=damage.GetType().GetProperty("isDead",B).GetValue(damage,null);var st=anim.GetCurrentAnimatorStateInfo(0);
  return "id="+go.GetInstanceID()+" active="+go.activeInHierarchy+" dead="+dead+" hp="+d+" alpha="+sr.color.a+" enabled="+sr.enabled+" sprite="+(sr.sprite!=null?sr.sprite.name:"NULL")+" mask="+(mask!=null?(mask.sprite!=null?mask.sprite.name:"NULL")+"/"+mask.enabled:"null")+" anim="+st.shortNameHash+" norm="+st.normalizedTime+" bounds="+sr.bounds;
 }
 void Capture(){var cam=Camera.main;if(cam==null){Debug.LogError("[enemy-original] NO_MAIN_CAMERA");return;}Debug.Log("[enemy-original] CAPTURE "+frame+" camera="+cam.name+" ortho="+cam.orthographic+" sort="+cam.transparencySortMode+" axis="+cam.transparencySortAxis+" ambient="+RenderSettings.ambientSkyColor);
  var oldTarget=cam.targetTexture;var oldPosition=cam.transform.position;var rt=new RenderTexture(320,240,24);rt.Create();cam.targetTexture=rt;cam.transform.position=new Vector3(wallX,1.88f,oldPosition.z);
  Save(cam,rt,Path.Combine(dir,"f"+frame+"-all.png"));
  foreach(var go in foes)if(go!=null&&go.activeInHierarchy){var rr=go.GetComponentsInChildren<Renderer>(true);var flags=new bool[rr.Length];for(int i=0;i<rr.Length;i++){flags[i]=rr[i].enabled;rr[i].enabled=false;}Save(cam,rt,Path.Combine(dir,"f"+frame+"-without-"+go.name+".png"));for(int i=0;i<rr.Length;i++)rr[i].enabled=flags[i];}
  if(frame==120){
   var renderers=new List<Renderer>();var flags=new List<bool>();foreach(var go in foes)if(go!=null&&go.activeInHierarchy)foreach(var r in go.GetComponentsInChildren<Renderer>(true)){renderers.Add(r);flags.Add(r.enabled);r.enabled=false;}
   Save(cam,rt,Path.Combine(dir,"f"+frame+"-no-qa-enemies.png"));
   foreach(var go in foes)if(go!=null&&go.activeInHierarchy){var own=go.GetComponentsInChildren<Renderer>(true);foreach(var r in own){int index=renderers.IndexOf(r);r.enabled=index>=0&&flags[index];}var sr=go.GetComponent<SpriteRenderer>();var lo=cam.WorldToScreenPoint(sr.bounds.min);var hi=cam.WorldToScreenPoint(sr.bounds.max);Debug.Log("[enemy-original] BOUNDS "+frame+" "+go.name+" low="+lo+" high="+hi);Save(cam,rt,Path.Combine(dir,"f"+frame+"-solo-"+go.name+".png"));foreach(var r in own)r.enabled=false;}
   for(int i=0;i<renderers.Count;i++)renderers[i].enabled=flags[i];
  }
  cam.targetTexture=oldTarget;cam.transform.position=oldPosition;rt.Release();Destroy(rt);
 }
 void Save(Camera cam,RenderTexture rt,string p){cam.Render();var old=RenderTexture.active;RenderTexture.active=rt;var t=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);t.Apply();File.WriteAllBytes(p,t.EncodeToPNG());Destroy(t);RenderTexture.active=old;}
}
