// Local-only instrumentation. Runs against the OWNED ORIGINAL Unity player, not the port.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using UnityEngine;
public sealed class OriginalPouchTrials : MonoBehaviour {
 static OriginalPouchTrials instance;
 const BindingFlags B=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 object bag,wallet; Type gameAsmType; Component bagComponent; bool measuring; int trial,added,firstLoss=-1; float nextAdd; int frames; List<string> rows=new List<string>();
 static object F(object o,string n) {return o.GetType().GetField(n,B).GetValue(o);}
 static void S(object o,string n,object v) {o.GetType().GetField(n,B).SetValue(o,v);}
 static object Call(object o,string n,params object[] a) {return o.GetType().GetMethod(n,B).Invoke(o,a);}
 public static void Ensure() {
  if(instance!=null)return;
  var go=new GameObject("OriginalPouchTrials");DontDestroyOnLoad(go);instance=go.AddComponent<OriginalPouchTrials>();
  Debug.Log("[original-trial] Harness installed; engine="+Application.unityVersion);
 }
 public static void Fell(object value) {
  if(instance==null||!instance.measuring||!ReferenceEquals(value,instance.bag))return;
  if(instance.firstLoss<0)instance.firstLoss=(int)F(value,"_currentCoins");
  Debug.Log("[original-trial] first_loss="+instance.firstLoss+" trial="+instance.trial+" added="+instance.added);
 }
 IEnumerator Start() {
  var asm=Assembly.Load("Assembly-CSharp"); gameAsmType=asm.GetType("CoinBag");
  float deadline=Time.realtimeSinceStartup+90;
  while(Time.realtimeSinceStartup<deadline) {
   foreach(var c in Resources.FindObjectsOfTypeAll(gameAsmType)) {
    var comp=c as Component;
    if(comp!=null&&comp.gameObject.activeInHierarchy) {
     var prop=gameAsmType.GetProperty("player",B);var player=prop.GetValue(c,null) as Component;
     if(player!=null) {bag=c;bagComponent=comp;wallet=player.GetComponent(asm.GetType("Wallet"));if(wallet!=null)break;}
    }
   }
   if(wallet!=null)break;
   yield return null;
  }
  if(wallet==null){Debug.LogError("[original-trial] NO_READY_BAG");Application.Quit();yield break;}
  float readyAt=Time.realtimeSinceStartup+3f; while(Time.realtimeSinceStartup<readyAt)yield return null;
  var managers=asm.GetType("Managers");
  var poolManager=managers.GetField("pools",BindingFlags.Public|BindingFlags.Static).GetValue(null);
  var holder=managers.GetField("holder",BindingFlags.Public|BindingFlags.Static).GetValue(null);
  var coinPrefab=F(holder,"coinPrefab");if(coinPrefab is Component)coinPrefab=((Component)coinPrefab).gameObject;
  var poolTable=(IDictionary)asm.GetType("Pool").GetField("poolsByPrefab",BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public).GetValue(null);
  if(!poolTable.Contains(coinPrefab))Call(poolManager,"CreatePoolFor",coinPrefab);
  Debug.Log("[original-trial] Native pouch ready. fixedDeltaTime="+Time.fixedDeltaTime+" gravity="+Physics2D.gravity+" root="+bagComponent.transform.position);
  // Freeze unrelated world logic after initialization, preserving the original pouch/coin scripts.
  foreach(var mb in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>()) {
   if(mb==this)continue;string name=mb.GetType().Name;
   if(name!="CoinBag"&&name!="BagCoin"&&name!="SpriteFX"&&name!="DOTweenComponent"&&name!="Pool")mb.enabled=false;
  }
  S(wallet,"canGrabCoins",false);
  Time.timeScale=1;Time.captureFramerate=50;QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
  rows.Add("trial,seed,first_loss_wallet_before,added_at_first_loss,retained_after_settle,fixed_delta,addition_interval");
  for(trial=1;trial<=20;trial++) {
   ((MonoBehaviour)bag).StopAllCoroutines();
   foreach(var c in bagComponent.GetComponentsInChildren<Component>(true))if(c!=null&&c.GetType().Name=="BagCoin")DestroyImmediate(c.gameObject);
   ((IList)F(bag,"_coins")).Clear();Call(F(bag,"_pool"),"Clear");
   S(bag,"_currentCoins",0);S(bag,"_coinsToAdd",0);S(bag,"_addingCoins",false);S(wallet,"_coins",0);
   Call(bag,"HideImmediate");
   int seed=4511+trial*7919;UnityEngine.Random.InitState(seed);
   firstLoss=-1;added=0;measuring=true;nextAdd=Time.time+.5f;
   while(firstLoss<0&&added<350) {
    if(Time.time>=nextAdd) {added++;wallet.GetType().GetProperty("coins",B).SetValue(wallet,(int)F(wallet,"_coins")+1,null);nextAdd=Time.time+.24f;}
    yield return null;
   }
   yield return new WaitForSeconds(1f);measuring=false;
   int retained=(int)F(wallet,"_coins");
   rows.Add(trial+","+seed+","+firstLoss+","+added+","+retained+","+Time.fixedDeltaTime.ToString(System.Globalization.CultureInfo.InvariantCulture)+",0.24");
   string dest=Path.Combine(Path.Combine(Application.dataPath,".."),"pouch-trials.csv");File.WriteAllLines(dest,rows.ToArray());
   Debug.Log("[original-trial] RESULT "+rows[rows.Count-1]);
   yield return new WaitForSeconds(.5f);
  }
  Debug.Log("[original-trial] COMPLETE 20");Application.Quit();
 }
 void Update(){if(++frames%1000==0)Debug.Log("[original-trial] progress frame="+frames+" time="+Time.time+" trial="+trial);}
}
