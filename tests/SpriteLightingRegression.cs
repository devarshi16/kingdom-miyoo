using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using KNL;
using UnityEngine;

class SpriteLightingRegression {
    const BindingFlags Private = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly MethodInfo Draw = typeof(RenderSystem).GetMethod("DrawSprite",Private);
    static readonly MethodInfo Prepare = typeof(RenderSystem).GetMethod("PrepareLights",Private);
    static int passed;
    static void Check(bool ok,string name) { if(!ok)throw new Exception("FAIL: "+name);passed++;Console.WriteLine("PASS: "+name); }
    static void Set(object target,string field,object value) { target.GetType().GetField(field,Private).SetValue(target,value); }
    static Vector3 WorldPoint(Matrix4x4 m,float x,float y) {
        return new Vector3(m.m00*x+m.m01*y+m.m03,m.m10*x+m.m11*y+m.m13,m.m20*x+m.m21*y+m.m23);
    }
    static Color PointLight(Vector3 point,Color ambient,Vector4[] lights) {
        float r=ambient.r,g=ambient.g,b=ambient.b;
        foreach(var light in lights) {
            if(light.w<=0||light.x < -24||light.x > 24)continue;
            float dx=point.x-light.x,dy=point.y-light.y,dz=point.z-light.z;
            float square=dx*dx+dy*dy+dz*dz;
            if(square>=9)continue;
            float falloff=(1f-(float)Math.Sqrt(square)*(1f/3f))*light.w;
            r+=falloff*.49f;g+=falloff*.425f;b+=falloff*.34f;
        }
        return new Color(Math.Max(0,r),Math.Max(0,g),Math.Max(0,b),1);
    }
    static uint Packed(Color color) {
        return (uint)(Mathf.Clamp01(color.r)*255+.5f)|((uint)(Mathf.Clamp01(color.g)*255+.5f)<<8)|((uint)(Mathf.Clamp01(color.b)*255+.5f)<<16)|((uint)(Mathf.Clamp01(color.a)*255+.5f)<<24);
    }
    static int[] Expected(SpriteData data,Matrix4x4 matrix,bool flipX,bool flipY,bool lite,float response,Color overlay,Color ambient,Vector4[] lights) {
        var result=new int[12];
        for(int corner=0;corner<4;corner++) {
            float u=lite?data.imgW*.5f:((corner&1)==0?0:data.imgW);
            float v=lite?data.imgH*.5f:((corner&2)==0?0:data.imgH);
            float x=(data.offX+u-data.pivot.x*data.rect.width)/data.ppu;
            float y=(data.offY+data.imgH-v-data.pivot.y*data.rect.height)/data.ppu;
            var point=WorldPoint(matrix,flipX?-x:x,flipY?-y:y);
            var light=PointLight(point,ambient,lights);
            result[corner*3]=Math.Max(0,(int)((1-overlay.a)*(1+response*(light.r-1))*256));
            result[corner*3+1]=Math.Max(0,(int)((1-overlay.a)*(1+response*(light.g-1))*256));
            result[corner*3+2]=Math.Max(0,(int)((1-overlay.a)*(1+response*(light.b-1))*256));
        }
        return result;
    }
    static void Reference(int texture,SpriteData data,Matrix4x4 matrix,SpriteRenderer renderer,int[] multiply,int[] add,Color recolorA,Color recolorB,int flags) {
        float sx=renderer.flipX?-1:1,sy=renderer.flipY?-1:1;
        var origin=WorldPoint(matrix,sx*(data.offX-data.pivot.x*data.rect.width)/data.ppu,sy*(data.offY+data.imgH-data.pivot.y*data.rect.height)/data.ppu);
        float a=matrix.m00*sx/data.ppu*12,b=-matrix.m10*sx/data.ppu*12,c=-matrix.m01*sy/data.ppu*12,d=matrix.m11*sy/data.ppu*12;
        Native.knl_draw_lit(texture,a,b,c,d,origin.x*12+160,120-origin.y*12,Packed(renderer.color),multiply,add,Packed(recolorA),Packed(recolorB),flags);
    }
    static void Case(string name,SpriteRenderer renderer,SpriteData data,int texture,Matrix4x4 matrix,bool lite,Vector4[] lights,Color ambient,float response,Color overlay,Color recolorA,Color recolorB,int flags,string directory) {
        Environment.SetEnvironmentVariable("KNL_VISUALS",lite?"lite":"full");VisualQuality.Refresh();
        Shader.SetGlobalVectorArray("_Light_PositionArray",lights);RenderSettings.ambientSkyColor=ambient;
        Prepare.Invoke(null,new object[]{Vector3.zero,20f});
        Native.knl_clear(0xFF342312u);Draw.Invoke(null,new object[]{renderer,matrix,Vector3.zero,12f});
        var actual=(int[])typeof(RenderSystem).GetField("mul12",Private).GetValue(null);
        var expected=Expected(data,matrix,renderer.flipX,renderer.flipY,lite,response,overlay,ambient,lights);
        Check(actual.SequenceEqual(expected),name+": independent point-light multipliers");
        if(lite)Check(Enumerable.Range(1,3).All(i=>actual.Skip(i*3).Take(3).SequenceEqual(actual.Take(3))),name+": identical four corner multipliers");
        var add=new[]{Mathf.Clamp((int)(overlay.r*overlay.a*255),0,255),Mathf.Clamp((int)(overlay.g*overlay.a*255),0,255),Mathf.Clamp((int)(overlay.b*overlay.a*255),0,255)};
        Check(((int[])typeof(RenderSystem).GetField("add3",Private).GetValue(null)).SequenceEqual(add),name+": overlay additive terms retained");
        string actualPng=Path.Combine(directory,"actual.png"),referencePng=Path.Combine(directory,"reference.png");
        Native.knl_save_png(actualPng,1);Native.knl_clear(0xFF342312u);Reference(texture,data,matrix,renderer,expected,add,recolorA,recolorB,flags);Native.knl_save_png(referencePng,1);
        Check(File.ReadAllBytes(actualPng).SequenceEqual(File.ReadAllBytes(referencePng)),name+": tint alpha/recolor/blending pixels match reference");
    }
    static void Main() {
        string directory=Path.Combine(Path.GetTempPath(),"knl-lighting-"+Guid.NewGuid());Directory.CreateDirectory(directory);
        World.ActiveScene=new SceneTag("lighting regression");if(Native.knl_init(320,240,1,0)!=0)throw new Exception("Native init failed");
        try {
            byte[] pixels=new byte[6*4*4];byte[] alpha={0,127,128,191,192,223,224,255};
            for(int i=0;i<24;i++){pixels[i*4]=(byte)(40+i*7);pixels[i*4+1]=(byte)(180-i*3);pixels[i*4+2]=(byte)(70+i*5);pixels[i*4+3]=alpha[i%alpha.Length];}
            int texture=Native.knl_tex_create(6,4,pixels);
            var data=new SpriteData{rect=new Rect(0,0,19,17),pivot=new Vector2(.2f,.8f),ppu=4,offX=3,offY=1,imgW=6,imgH=4};Set(data,"tex",texture);
            typeof(SpriteData).GetMethod("CacheBounds",Private).Invoke(data,null);
            var sprite=new Sprite();Set(sprite,"data",data);var renderer=new GameObject("asymmetric trimmed sprite").AddComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.color=new Color(.75f,.6f,.4f,.55f);
            var material=new Material(Shader.Find("Custom/PowerSprite2"));material.name="lighting fixture";material.EnableKeyword("RECOLOR_ON");renderer.sharedMaterial=material;
            var ambient=new Color(.2f,.35f,.5f,1);var overlay=new Color(.3f,.1f,.6f,.25f);var ra=new Color(.8f,.2f,.3f,.6f);var rb=new Color(.1f,.7f,.9f,1);
            material.SetFloat("_AmbientResponse",.8f);material.SetColor("_Overlay",overlay);material.SetColor("_RecolorA",ra);material.SetColor("_RecolorB",rb);
            var matrix=Matrix4x4.TRS(new Vector3(1,2,.4f),Quaternion.Euler(0,0,32),new Vector3(1.3f,.8f,1));
            var lights=new[]{new Vector4(2.3f,.3f,.4f,1.7f),new Vector4(-.5f,2f,.3f,.6f),new Vector4(100,0,0,2),new Vector4(0,0,0,-1)};
            Case("full transformed corners",renderer,data,texture,matrix,false,lights,ambient,.8f,overlay,ra,rb,1,directory);
            var full=(int[])typeof(RenderSystem).GetField("mul12",Private).GetValue(null);Check(!full.Take(3).SequenceEqual(full.Skip(3).Take(3)),"Full mode retains spatially varying corner lighting");
            Case("lite asymmetric trimmed center",renderer,data,texture,matrix,true,lights,ambient,.8f,overlay,ra,rb,1,directory);
            renderer.flipX=true;Case("lite horizontally flipped center",renderer,data,texture,matrix,true,lights,ambient,.8f,overlay,ra,rb,1,directory);
            renderer.flipY=true;matrix.m01+=.35f;Case("lite flipped sheared center",renderer,data,texture,matrix,true,lights,ambient,.8f,overlay,ra,rb,1,directory);
            Case("full no lights",renderer,data,texture,matrix,false,new Vector4[0],ambient,.8f,overlay,ra,rb,1,directory);
            Case("lite no lights",renderer,data,texture,matrix,true,new Vector4[0],ambient,.8f,overlay,ra,rb,1,directory);
            material.SetFloat("_AmbientResponse",0);Case("full zero response",renderer,data,texture,matrix,false,lights,ambient,0,overlay,ra,rb,1,directory);
            Case("lite zero response",renderer,data,texture,matrix,true,lights,ambient,0,overlay,ra,rb,1,directory);
            var block=new MaterialPropertyBlock();var blockOverlay=new Color(.2f,.4f,.8f,.5f);var blockA=new Color(.4f,.7f,.2f,.9f);var blockB=new Color(.7f,.1f,.5f,.8f);
            block.SetFloat("_AmbientResponse",.35f);block.SetColor("_Overlay",blockOverlay);block.SetColor("_RecolorA",blockA);block.SetColor("_RecolorB",blockB);renderer.SetPropertyBlock(block);
            material.SetFloat("DstBlend",1);Case("lite additive property overrides",renderer,data,texture,matrix,true,lights,ambient,.35f,blockOverlay,blockA,blockB,3,directory);
            Check(material.GetFloat("_AmbientResponse")==0&&material.GetColor("_Overlay")==overlay,"Renderer overrides do not mutate shared material state");
            Console.WriteLine("Sprite lighting regressions passed: "+passed);
        } finally {Native.knl_shutdown();Directory.Delete(directory,true);}
    }
}
