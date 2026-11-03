// Differential fixture: run unchanged against a retained runtime and the candidate.
// The digest includes ordered IDs, distances, points and normals, not just hit counts.
using System;using System.IO;using System.Text;using System.Linq;using System.Security.Cryptography;using System.Collections.Generic;using KNL;using UnityEngine;
class ColliderCastRegression {
 static void Main(string[] args){World.ActiveScene=new SceneTag("isolated collider casts");var rng=new System.Random(8543);var cols=new List<BoxCollider2D>();var bodies=new List<Rigidbody2D>();var nodes=new List<GameObject>();
  for(int i=0;i<600;i++){var go=new GameObject("cast obstacle "+i);go.layer=i%6;go.transform.position=new Vector3((i%60)*.5f,(i/60)*.75f,0);if(i%7==0){var rb=go.AddComponent<Rigidbody2D>();rb.gravityScale=0;bodies.Add(rb);}var c=go.AddComponent<BoxCollider2D>();c.size=new Vector2(.25f+(i%5)*.25f,.25f+(i%3)*.5f);c.isTrigger=i%4==0;cols.Add(c);nodes.Add(go);}
  var mine=new GameObject("moving cast body");mine.layer=0;var mover=mine.AddComponent<Rigidbody2D>();mover.gravityScale=0;var caster=mine.AddComponent<BoxCollider2D>();caster.size=new Vector2(.5f,1);var child=new GameObject("same body excluded");child.transform.SetParent(mine.transform,false);child.AddComponent<BoxCollider2D>();
  var dirs=new[]{Vector2.up,Vector2.down,Vector2.right,Vector2.left,Vector2.zero,new Vector2(.3f,1),new Vector2(1e-10f,1),new Vector2(2e-9f,1),new Vector2(-1e-10f,-1)};var distances=new[]{float.PositiveInfinity,0f,.25f,2f,100f,-1f};
  var bytes=new MemoryStream();var writer=new BinaryWriter(bytes);int checks=0;
  for(int frame=0;frame<160;frame++){Lifecycle.Frame(.02f);int n=rng.Next(cols.Count);nodes[n].SetActive(!nodes[n].activeSelf);cols[(n+3)%cols.Count].isTrigger=!cols[(n+3)%cols.Count].isTrigger;nodes[(n+7)%cols.Count].layer=rng.Next(6);cols[(n+11)%cols.Count].enabled=!cols[(n+11)%cols.Count].enabled;nodes[(n+17)%cols.Count].transform.position=new Vector3((float)rng.NextDouble()*30,(float)rng.NextDouble()*8,0);Physics2DSystem.Step();
   for(int q=0;q<12;q++){if(q==6)Physics2DSystem.Step();if(q==7)cols[(n+5)%cols.Count].isTrigger=!cols[(n+5)%cols.Count].isTrigger;mine.transform.position=new Vector3((float)rng.NextDouble()*30,(float)rng.NextDouble()*8,0);var result=new RaycastHit2D[(q*7)%35];int count=caster.Cast(dirs[(frame+q)%dirs.Length],result,distances[(frame+q)%distances.Length]);writer.Write(count);for(int i=0;i<count;i++){var h=result[i];writer.Write(h.collider.GetInstanceID());writer.Write(h.distance);writer.Write(h.point.x);writer.Write(h.point.y);writer.Write(h.normal.x);writer.Write(h.normal.y);}checks++;}
  }
  foreach(float x in new[]{3.9999998f,4f,4.0000005f,-4.0000005f,999999f,1000001f,float.NaN,float.PositiveInfinity}){var go=new GameObject("cast boundary");go.transform.position=new Vector3(x,3,0);var c=go.AddComponent<BoxCollider2D>();c.size=new Vector2(x==999999f?3000000f:.0001f,1);cols.Add(c);}
  for(int phase=0;phase<2;phase++){
   if(phase==1)for(int i=0;i<1100;i++){var go=new GameObject("cast index capacity "+i);go.transform.position=new Vector3(i*8,5,0);go.AddComponent<BoxCollider2D>();}
   Lifecycle.Frame(.02f);Physics2DSystem.Step();
   foreach(float x in new[]{-4.0000005f,-4f,0f,3.9999998f,4f,4.0000005f,999999f,1000001f})foreach(var dir in dirs){mine.transform.position=new Vector3(x,0,0);var result=new RaycastHit2D[64];int count=caster.Cast(dir,result);writer.Write(count);for(int i=0;i<count;i++){var h=result[i];writer.Write(h.collider.GetInstanceID());writer.Write(h.distance);writer.Write(h.point.x);writer.Write(h.point.y);writer.Write(h.normal.x);writer.Write(h.normal.y);}checks++;}
  }
  writer.Flush();using(var hash=SHA256.Create())Console.WriteLine("casts="+checks+" digest="+BitConverter.ToString(hash.ComputeHash(bytes.ToArray())).Replace("-","").ToLowerInvariant());
  if(args.Length>0&&args[0]=="--allocation"){var result=new RaycastHit2D[64];for(int i=0;i<20;i++)caster.Cast(Vector2.up,result);long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<2000;i++)caster.Cast(Vector2.up,result);long allocated=GC.GetAllocatedBytesForCurrentThread()-before;Console.WriteLine("warm_cast_allocated_bytes="+allocated);if(allocated!=0)throw new Exception("Warm collider casts allocate");}
 }
}
