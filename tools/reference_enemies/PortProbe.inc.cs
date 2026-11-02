// QA-only members injected into a private copy of Host/Program.cs. Never built into releases.
    static readonly System.Reflection.BindingFlags EnemyFields=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
    static readonly bool EnemyLongProbe=Environment.GetEnvironmentVariable("KNL_ENEMY_LONG_PROBE")=="1";
    static readonly System.Collections.Generic.Dictionary<int,string> EnemyPriorState=new System.Collections.Generic.Dictionary<int,string>();
    static string EnemyObj(object o){return o==null?"null":System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o).ToString("x");}
    static object EnemyField(object o,string n){return o==null?null:o.GetType().GetField(n,EnemyFields).GetValue(o);}
    static string EnemySprite(SpriteRenderer r){return r==null||r.sprite==null?"null":r.sprite.name;}
    static string EnemyFootprint(SpriteRenderer r){if(r==null||r.sprite==null)return "null";var d=r.sprite.data;return r.sprite.name+"[img="+d.imgW+"x"+d.imgH+",off="+d.offX+","+d.offY+",rect="+d.rect.width+"x"+d.rect.height+"]";}
    static string EnemyMaterial(SpriteRenderer r){var m=r==null?null:r.sharedMaterial;return m==null?"null":EnemyObj(m)+":"+m.name+":"+(m.shader==null?"null":m.shader.name);}
    static string EnemyBounds(Renderer r){if(r==null)return "null";var b=r.bounds;return "("+b.center.x.ToString("F3")+","+b.center.y.ToString("F3")+","+b.center.z.ToString("F3")+")/ ("+b.extents.x.ToString("F3")+","+b.extents.y.ToString("F3")+","+b.extents.z.ToString("F3")+")";}
    static bool EnemyLongSpawned;
    static GameObject EnemyDeathReuseTarget;
    static GameObject EnemyReuseGo;
    static int EnemyReuseCycles;
    static void EnemyPoolCycle(int f){
      if(f<3500||EnemyReuseCycles>=20)return;
      var manager=EnemyComponent("EnemyManager");var prefabRaw=EnemyField(manager,"trollPrefab");var prefab=prefabRaw as GameObject??(prefabRaw as Component).gameObject;var pool=manager.GetType().Assembly.GetType("Pool");
      if(EnemyReuseGo==null){
        int offset=f-3500;if(offset<0||(offset%2)!=0)return;
        var pl=World.SceneObjects.First(g=>g.m_Tag=="Player"&&g.activeInHierarchy);var spawn=pool.GetMethods(System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public).First(m=>m.Name=="SpawnGO"&&m.GetParameters().Length==6);
        if(EnemyReuseCycles==0&&EnemyDeathReuseTarget!=null&&!EnemyDeathReuseTarget.activeInHierarchy){
          var held=new System.Collections.Generic.List<GameObject>();
          for(int n=0;n<80;n++){
            var p=new Vector3(pl.transform.position.x-3f,.875f,1.5f);var candidate=(GameObject)spawn.Invoke(null,new object[]{prefab,p,Quaternion.identity,pl.transform.parent,false,false});
            if(candidate==null)break;
            if(System.Object.ReferenceEquals(candidate,EnemyDeathReuseTarget)){EnemyReuseGo=candidate;break;}
            held.Add(candidate);
          }
          foreach(var candidate in held)pool.GetMethod("Despawn",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public,null,new[]{typeof(GameObject)},null).Invoke(null,new object[]{candidate});
          Console.Error.WriteLine("LONGREUSE death_target_acquired="+(EnemyReuseGo!=null)+" id="+EnemyObj(EnemyDeathReuseTarget)+" held_candidates="+held.Count);
        }
        if(EnemyReuseGo==null){var p=new Vector3(pl.transform.position.x-3f,.875f,1.5f);EnemyReuseGo=(GameObject)spawn.Invoke(null,new object[]{prefab,p,Quaternion.identity,pl.transform.parent,true,true});}
        if(EnemyReuseGo==null)return;
        var troll=EnemyReuseGo.m_Components.First(c=>c.GetType().Name=="Troll");troll.GetType().GetMethod("MakeToughTroll").Invoke(troll,null);
        var anim=EnemyReuseGo.GetComponent<Animator>();var rt=anim==null?null:EnemyField(anim,"m_Rt");var info=anim==null?default(AnimatorStateInfo):anim.GetCurrentAnimatorStateInfo(0);
        var fx=EnemyReuseGo.GetComponent("SpriteFX");var body=fx==null?EnemyReuseGo.GetComponent<SpriteRenderer>():EnemyField(fx,"_renderer") as SpriteRenderer;var damage=EnemyReuseGo.GetComponent("Damageable");var mask=EnemyField(troll,"mask") as SpriteRenderer;
        Console.Error.WriteLine("LONGREUSE spawn cycle="+EnemyReuseCycles+" frame="+f+" id="+EnemyObj(EnemyReuseGo)+" active="+EnemyReuseGo.activeInHierarchy+" rt="+EnemyObj(rt)+" state="+info.shortNameHash+" norm="+info.normalizedTime+" body="+EnemyFootprint(body)+" body_bounds="+EnemyBounds(body)+" alpha="+(body==null?-1:body.color.a)+" body_enabled="+(body!=null&&body.enabled)+" mask="+EnemyFootprint(mask)+" mask_bounds="+EnemyBounds(mask)+" mask_enabled="+(mask!=null&&mask.enabled)+" dead="+(damage!=null&&Convert.ToBoolean(damage.GetType().GetProperty("isDead").GetValue(damage,null)))+" hp="+(damage==null?-1:damage.GetType().GetProperty("hitPoints").GetValue(damage,null))+" holding="+(anim!=null&&anim.GetBool("Holding"))+" charge="+(anim!=null&&anim.GetBool("Charge"))+" recover="+(anim!=null&&anim.GetBool("Recover")));
      }else if((f-3500)%2==1){
        var despawn=pool.GetMethods(System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public).First(m=>m.Name=="Despawn"&&m.GetParameters().Length==1);
        var old=EnemyReuseGo;var anim=old.GetComponent<Animator>();var rt=anim==null?null:EnemyField(anim,"m_Rt");var troll=old.m_Components.First(c=>c.GetType().Name=="Troll");var fx=old.GetComponent("SpriteFX");var body=fx==null?old.GetComponent<SpriteRenderer>():EnemyField(fx,"_renderer") as SpriteRenderer;var mask=EnemyField(troll,"mask") as SpriteRenderer;var damage=old.GetComponent("Damageable");var info=anim==null?default(AnimatorStateInfo):anim.GetCurrentAnimatorStateInfo(0);
        Console.Error.WriteLine("LONGREUSE before_despawn cycle="+EnemyReuseCycles+" frame="+f+" id="+EnemyObj(old)+" rt="+EnemyObj(rt)+" state="+info.shortNameHash+" norm="+info.normalizedTime+" body="+EnemyFootprint(body)+" body_bounds="+EnemyBounds(body)+" alpha="+(body==null?-1:body.color.a)+" body_enabled="+(body!=null&&body.enabled)+" mask="+EnemyFootprint(mask)+" mask_bounds="+EnemyBounds(mask)+" mask_enabled="+(mask!=null&&mask.enabled)+" dead="+(damage!=null&&Convert.ToBoolean(damage.GetType().GetProperty("isDead").GetValue(damage,null)))+" hp="+(damage==null?-1:damage.GetType().GetProperty("hitPoints").GetValue(damage,null)));
        despawn.Invoke(null,new object[]{old});
        Console.Error.WriteLine("LONGREUSE despawn cycle="+EnemyReuseCycles+" frame="+f+" id="+EnemyObj(old)+" active="+old.activeInHierarchy+" rt="+EnemyObj(rt));EnemyReuseGo=null;EnemyReuseCycles++;
      }
    }
    static void EnemyLongBefore(int f){
      EnemyPoolCycle(f);
      if(f==200){var director=EnemyComponent("Director");director.GetType().GetField("_currentTime",EnemyFields).SetValue(director,22f);Console.Error.WriteLine("LONGPROBE set starting time to 22:00 once; original clock then advances normally");}
      if(f==600&&!EnemyLongSpawned){
        var pl=World.SceneObjects.First(g=>g.m_Tag=="Player"&&g.activeInHierarchy);var walls=World.SceneObjects.Where(g=>g.activeInHierarchy&&g.m_Components.Any(c=>c.GetType().Name=="Wall")).ToArray();
        var wall=walls.OrderBy(g=>Mathf.Abs(g.transform.position.x-EnemyTargetWall(pl.transform.position.x))).First();float side=wall.transform.position.x<0?-1:1;EnemyWall=wall.transform.position.x;
        var manager=EnemyComponent("EnemyManager");var prefabRaw=EnemyField(manager,"trollPrefab");var prefab=prefabRaw as GameObject??(prefabRaw as Component).gameObject;var pool=manager.GetType().Assembly.GetType("Pool");
        var spawn=pool.GetMethods(System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public).First(m=>m.Name=="SpawnGO"&&m.GetParameters().Length==6);
        for(int i=0;i<20;i++){var p=new Vector3(wall.transform.position.x+side*(1.2f+i*.22f),.875f,1.5f);var go=(GameObject)spawn.Invoke(null,new object[]{prefab,p,Quaternion.identity,pl.transform.parent,true,true});go.name="qa-invisible-"+i;if(i==18)EnemyDeathReuseTarget=go;var troll=go.m_Components.First(c=>c.GetType().Name=="Troll");if(i%2==0)troll.GetType().GetMethod("MakeToughTroll").Invoke(troll,null);}
        EnemyLongSpawned=true;Console.Error.WriteLine("LONGPROBE spawned 20 original Troll actors near wall="+wall.transform.position+" player="+pl.transform.position+" half tough via original MakeToughTroll; no alpha/state overrides");
      }
    }
    static void EnemyLongAfter(int f){
      if(!EnemyLongProbe)return;
      var root=Environment.GetEnvironmentVariable("KNL_ENEMY_SHOTS");
      if(String.IsNullOrEmpty(root))root=Path.Combine(Path.GetTempPath(),"knl-enemy-long-probe");
      if(f==0){Directory.CreateDirectory(root);Console.Error.WriteLine("LONGPROBE start frames=10000 capture_dir="+root+" game_time=natural");}
      var trolls=World.SceneObjects.SelectMany(g=>g.m_Components.Where(c=>c!=null&&c.GetType().Name=="Troll").Select(c=>new{go=g,troll=c})).ToArray();
      var manager=World.SceneObjects.SelectMany(g=>g.m_Components).FirstOrDefault(c=>c!=null&&c.GetType().Name=="EnemyManager"&&c.gameObject.activeInHierarchy);
      var prefabRaw=manager==null?null:EnemyField(manager,"trollPrefab");var prefab=prefabRaw as GameObject??(prefabRaw as Component)?.gameObject;
      var prefabBody=prefab==null?null:prefab.GetComponent<SpriteRenderer>();
      foreach(var entry in trolls){
        var go=entry.go;var troll=entry.troll;int id=System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(go);
        var fx=EnemyField(troll,"_spriteFX") as Component;var body=fx==null?go.GetComponent<SpriteRenderer>():EnemyField(fx,"_renderer") as SpriteRenderer;
        var mask=EnemyField(troll,"mask") as SpriteRenderer;var damage=go.m_Components.FirstOrDefault(c=>c.GetType().Name=="Damageable");var animator=go.GetComponent<Animator>();
        float hp=damage==null?-1:Convert.ToSingle(damage.GetType().GetProperty("hitPoints").GetValue(damage,null));bool dead=damage!=null&&(bool)damage.GetType().GetProperty("isDead").GetValue(damage,null);
        float alpha=body==null?-1:body.color.a;int animHash=animator==null?0:animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
        string state=go.activeInHierarchy+"|"+dead+"|"+hp+"|"+alpha+"|"+(body!=null&&body.enabled)+"|"+EnemySprite(body)+"|"+EnemySprite(mask)+"|"+animHash;
        string prior;bool changed=!EnemyPriorState.TryGetValue(id,out prior)||prior!=state;
        if(changed||f%300==0||alpha<=0.05f){
          EnemyPriorState[id]=state;
          var block=new MaterialPropertyBlock();if(body!=null)body.GetPropertyBlock(block);
          var original=fx==null?null:EnemyField(fx,"_originalAlpha");var capturedMat=fx==null?null:EnemyField(fx,"_sharedMaterial");
          object owns=body==null?null:EnemyField(body,"m_OwnsMaterial");object instanceBlock=body==null?null:EnemyField(body,"m_PropertyBlock");
          var info=animator==null?default(AnimatorStateInfo):animator.GetCurrentAnimatorStateInfo(0);
          Console.Error.WriteLine("LONGTROLL frame="+f+" id="+id.ToString("x")+" changed="+changed+" active="+go.activeInHierarchy+" name="+go.name+" pos="+go.transform.position+" scale="+go.transform.lossyScale+
            " dead="+dead+" hp="+hp+" body_enabled="+(body!=null&&body.enabled)+" body_color="+(body==null?"null":body.color.ToString())+" body_sprite="+EnemySprite(body)+" body_layer="+(body==null?-1:body.gameObject.layer)+" body_sort="+(body==null?-1:body.sortingOrder)+" body_bounds="+EnemyBounds(body)+
            " body_mat="+EnemyMaterial(body)+" captured_mat="+EnemyObj(capturedMat)+" pool_template_mat="+EnemyMaterial(prefabBody)+" shares_template="+(body!=null&&prefabBody!=null&&System.Object.ReferenceEquals(body.sharedMaterial,prefabBody.sharedMaterial))+" owns_mat="+owns+" block_id="+EnemyObj(instanceBlock)+" static_block_id="+EnemyObj(fx==null?null:fx.GetType().Assembly.GetType("SpriteFX").GetField("_materialBlock",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).GetValue(null))+
            " overlay="+block.GetColor("_Overlay")+" recolorA="+block.GetColor("_RecolorA")+" recolorB="+block.GetColor("_RecolorB")+" ambient="+block.GetFloat("_AmbientResponse")+" mask_enabled="+(mask!=null&&mask.enabled)+" mask_color="+(mask==null?"null":mask.color.ToString())+" mask_sprite="+EnemySprite(mask)+" mask_layer="+(mask==null?-1:mask.gameObject.layer)+" mask_sort="+(mask==null?-1:mask.sortingOrder)+" mask_bounds="+EnemyBounds(mask)+" anim="+animHash+" anim_norm="+info.normalizedTime+" anim_rt="+EnemyObj(animator==null?null:EnemyField(animator,"m_Rt"))+" holding="+(animator!=null&&animator.GetBool("Holding"))+" charge="+(animator!=null&&animator.GetBool("Charge"))+" recover="+(animator!=null&&animator.GetBool("Recover"))+" camera_mask="+(RenderSystem.MainCamera==null?-1:RenderSystem.MainCamera.cullingMask));
        }
      }
      if(f%300==0){var director=World.SceneObjects.SelectMany(g=>g.m_Components).FirstOrDefault(c=>c!=null&&c.GetType().Name=="Director"&&c.gameObject.activeInHierarchy);var time=director==null?null:EnemyField(director,"_currentTime");var day=director==null?null:EnemyField(director,"_currentDay");Console.Error.WriteLine("LONGWORLD frame="+f+" day="+day+" hour="+time+" trolls="+trolls.Count(x=>x.go.activeInHierarchy)+" active="+trolls.Count(x=>x.go.activeInHierarchy)+" tracked="+EnemyPriorState.Count+" time="+Time.time);}
      if(f%600==0&&f>0&&trolls.Length>0){
        var candidates=trolls.Where(x=>x.go.activeInHierarchy).ToArray();
        var picks=new[]{candidates.FirstOrDefault(x=>EnemyField(x.troll,"mask")!=null),candidates.FirstOrDefault(x=>EnemyField(x.troll,"mask")==null)};
        for(int pi=0;pi<picks.Length;pi++)if(picks[pi]!=null){
          var selected=picks[pi];var go=selected.go;var fx=EnemyField(selected.troll,"_spriteFX") as Component;var body=fx==null?go.GetComponent<SpriteRenderer>():EnemyField(fx,"_renderer") as SpriteRenderer;var mask=EnemyField(selected.troll,"mask") as SpriteRenderer;
          if(body==null||body.sprite==null)continue;
          var all=trolls.SelectMany(x=>x.go.GetComponentsInChildren<SpriteRenderer>(true)).Distinct().ToArray();var flags=all.Select(r=>r.enabled).ToArray();foreach(var r in all)r.enabled=false;
          body.enabled=true;if(mask!=null)mask.enabled=true;
          string label="f"+f+"-"+(pi==0?"masked":"unmasked")+"-"+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(go).ToString("x");
          var camera=RenderSystem.MainCamera;float oldSize=camera==null?0:camera.orthographicSize;if(camera!=null)camera.orthographicSize=2.25f;
          RenderSystem.CameraOverride=new Vector3(go.transform.position.x,go.transform.position.y+1f,-10f);RenderSystem.RenderFrame();RenderSystem.SavePng(Path.Combine(root,label+"-body-plus-mask.png"),1);
          if(mask!=null){mask.enabled=false;RenderSystem.RenderFrame();RenderSystem.SavePng(Path.Combine(root,label+"-body-only.png"),1);mask.enabled=true;}
          body.enabled=false;if(mask!=null)mask.enabled=true;RenderSystem.RenderFrame();RenderSystem.SavePng(Path.Combine(root,label+"-mask-only.png"),1);
          for(int i=0;i<all.Length;i++)all[i].enabled=flags[i];RenderSystem.CameraOverride=null;if(camera!=null)camera.orthographicSize=oldSize;RenderSystem.RenderFrame();
          Console.Error.WriteLine("LONGCAPTURE frame="+f+" kind="+(pi==0?"masked":"unmasked")+" id="+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(go).ToString("x")+" sprite="+EnemySprite(body)+" alpha="+body.color.a+" mask="+EnemySprite(mask)+" file="+label);
        }
      }
      if(f==9999)Console.Error.WriteLine("LONGPROBE done tracked_gameobjects="+EnemyPriorState.Count+" active_trolls="+trolls.Count(x=>x.go.activeInHierarchy));
    }
    static Component EnemyComponent(string type){return World.SceneObjects.SelectMany(g=>g.m_Components).First(c=>c.GetType().Name==type&&c.gameObject.activeInHierarchy);}
    static float EnemyWall;
    static float EnemyTargetWall(float fallback) {
        string text = Environment.GetEnvironmentVariable("KNL_ENEMY_WALL_X");
        return string.IsNullOrEmpty(text) ? fallback : float.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
    }
    static void EnemyNightBefore(int f){
      if(EnemyLongProbe){EnemyLongBefore(f);return;}
      if(f>=200){var director=EnemyComponent("Director");director.GetType().GetField("_currentTime",EnemyFields).SetValue(director,22f);}
      if(f==600){
        var pl=World.SceneObjects.First(g=>g.m_Tag=="Player"&&g.activeInHierarchy);
        var walls=World.SceneObjects.Where(g=>g.activeInHierarchy&&g.m_Components.Any(c=>c.GetType().Name=="Wall")).ToArray();
        foreach(var w in walls)Console.Error.WriteLine("WALL "+w.name+" "+w.transform.position);
        var wall=walls.OrderBy(g=>Mathf.Abs(g.transform.position.x-EnemyTargetWall(pl.transform.position.x))).First();
        float side=wall.transform.position.x<0?-1:1;
        pl.transform.position=new Vector3(wall.transform.position.x-side*2,pl.transform.position.y,pl.transform.position.z);
        Console.Error.WriteLine("ARENA player="+pl.transform.position+" wall="+wall.transform.position); EnemyWall=wall.transform.position.x;
        var wallet=pl.m_Components.First(c=>c.GetType().Name=="Wallet");wallet.GetType().GetProperty("coins").SetValue(wallet,1000,null);
        var manager=EnemyComponent("EnemyManager");var prefab=(UnityEngine.Object)manager.GetType().GetField("trollPrefab").GetValue(manager);
        var pg=prefab as GameObject??((Component)prefab).gameObject;
        var pool=manager.GetType().Assembly.GetType("Pool");
        var spawn=pool.GetMethods(System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public).First(m=>m.Name=="SpawnGO"&&m.GetParameters().Length==6);
        for(int i=0;i<24;i++){
          var position=new Vector3(wall.transform.position.x+side*(1.5f+i*.18f),.875f,1.5f);
          var go=(GameObject)spawn.Invoke(null,new object[]{pg,position,Quaternion.identity,pl.transform.parent,true,true});
          go.name="qa-night-greed-"+i;
          var troll=go.m_Components.First(c=>c.GetType().Name=="Troll");
          if(i%2==0)troll.GetType().GetMethod("MakeToughTroll").Invoke(troll,null);
        }
      }
    }
    static void EnemyNightAfter(int f){
      if(EnemyLongProbe){EnemyLongAfter(f);return;}
      if(f<600||f%30!=0)return;
      var root=Environment.GetEnvironmentVariable("KNL_ENEMY_SHOTS");
      var foes=World.SceneObjects.Where(g=>g.name.StartsWith("qa-night-greed-")&&g.activeInHierarchy).ToArray();
      foreach(var go in foes){
        var damage=go.m_Components.First(c=>c.GetType().Name=="Damageable");
        var sprite=go.GetComponent<SpriteRenderer>();var animator=go.GetComponent<Animator>();
        var overlay=sprite.m_PropertyBlock!=null?sprite.m_PropertyBlock.GetColor("_Overlay"):Color.clear;
        var mat=sprite.sharedMaterial;
        Console.Error.WriteLine("MATERIAL "+f+" "+go.name+" name="+(mat!=null?mat.name:"NONE")+" shader="+(mat!=null&&mat.shader!=null?mat.shader.name:"NONE")+" keywords="+(mat!=null?string.Join(",",mat.m_Keywords):"NONE")+" ambient="+(sprite.m_PropertyBlock!=null?sprite.m_PropertyBlock.GetFloat("_AmbientResponse"):0)+" recolorA="+(sprite.m_PropertyBlock!=null?sprite.m_PropertyBlock.GetColor("_RecolorA"):Color.clear)+" recolorB="+(sprite.m_PropertyBlock!=null?sprite.m_PropertyBlock.GetColor("_RecolorB"):Color.clear)+" texture="+(sprite.sprite!=null&&sprite.sprite.data!=null?sprite.sprite.data.Texture:-9));
        Console.Error.WriteLine("ENEMY "+f+" "+go.name+" pos="+go.transform.position+" scale="+go.transform.lossyScale+" dead="+damage.GetType().GetProperty("isDead").GetValue(damage,null)+" hp="+damage.GetType().GetProperty("hitPoints").GetValue(damage,null)+" enabled="+sprite.enabled+" color="+sprite.color+" overlay="+overlay+" layer="+go.layer+" sprite="+(sprite.sprite!=null?sprite.sprite.name:"NULL")+" state="+animator.GetCurrentAnimatorStateInfo(0).shortNameHash);
      }
      if(f%120==0&&f<=1200){
        Console.Error.WriteLine("CAM "+f+" "+RenderSystem.MainCamera.transform.position);
        RenderSystem.CameraOverride=new Vector3(EnemyWall,1.88f,-10);
        RenderSystem.RenderFrame();
        Console.Error.WriteLine("SHADE "+f+" ambient="+RenderSettings.ambientSkyColor);
        RenderSystem.SavePng(Path.Combine(root,"f"+f+"-all.png"),1);
        foreach(var go in foes){
          var renderers=go.GetComponentsInChildren<SpriteRenderer>(true);var flags=renderers.Select(r=>r.enabled).ToArray();
          for(int i=0;i<renderers.Length;i++)renderers[i].enabled=false;
          RenderSystem.RenderFrame();RenderSystem.SavePng(Path.Combine(root,"f"+f+"-without-"+go.name+".png"),1);
          for(int i=0;i<renderers.Length;i++)renderers[i].enabled=flags[i];
        }
        var everyone=foes.SelectMany(g=>g.GetComponentsInChildren<SpriteRenderer>(true)).ToArray();var everyoneFlags=everyone.Select(r=>r.enabled).ToArray();
        for(int i=0;i<everyone.Length;i++)everyone[i].enabled=false;
        RenderSystem.RenderFrame();RenderSystem.SavePng(Path.Combine(root,"f"+f+"-no-qa-enemies.png"),1);
        foreach(var go in foes){
          var owned=go.GetComponentsInChildren<SpriteRenderer>(true);
          foreach(var r in owned){int index=Array.IndexOf(everyone,r);r.enabled=index>=0&&everyoneFlags[index];}
          RenderSystem.RenderFrame();RenderSystem.SavePng(Path.Combine(root,"f"+f+"-solo-"+go.name+".png"),1);
          foreach(var r in owned)r.enabled=false;
        }
        for(int i=0;i<everyone.Length;i++)everyone[i].enabled=everyoneFlags[i];
        RenderSystem.CameraOverride=null;
        RenderSystem.RenderFrame();
      }
    }
