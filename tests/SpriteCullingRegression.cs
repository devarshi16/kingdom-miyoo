using System;
using System.Reflection;
using KNL;
using UnityEngine;
class SpriteCullingRegression {
    static int passed;
    static void Check(bool yes,string name) { if(!yes)throw new Exception(name);passed++;Console.WriteLine("PASS: "+name); }
    static SpriteData Data(int width,int height,int x,int y,float pivotX,float pivotY) {
        var d=new SpriteData { imgW=width,imgH=height,offX=x,offY=y,ppu=32,rect=new Rect(0,0,width+11,height+7),pivot=new Vector2(pivotX,pivotY) };
        typeof(SpriteData).GetMethod("CacheBounds",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(d,null);return d;
    }
    static bool MatchesCorners(SpriteRenderer renderer,SpriteData d) {
        RenderSystem.PrepareSpriteBounds(renderer,renderer.transform,d);
        float minX=float.MaxValue,minY=float.MaxValue,maxX=float.MinValue,maxY=float.MinValue;
        for(int corner=0;corner<4;corner++) {
            float x=(d.offX+((corner&1)!=0?d.imgW:0)-d.pivot.x*d.rect.width)/d.ppu;
            float y=(d.offY+((corner&2)!=0?d.imgH:0)-d.pivot.y*d.rect.height)/d.ppu;
            var p=renderer.transform.TransformPoint(new Vector3(renderer.flipX?-x:x,renderer.flipY?-y:y,0));
            minX=Math.Min(minX,p.x);maxX=Math.Max(maxX,p.x);minY=Math.Min(minY,p.y);maxY=Math.Max(maxY,p.y);
        }
        return Math.Abs(renderer.m_RenderCenterX-renderer.m_RenderHalfX-minX)<.002f
            && Math.Abs(renderer.m_RenderCenterX+renderer.m_RenderHalfX-maxX)<.002f
            && Math.Abs(renderer.m_RenderCenterY-renderer.m_RenderHalfY-minY)<.002f
            && Math.Abs(renderer.m_RenderCenterY+renderer.m_RenderHalfY-maxY)<.002f;
    }
    static float R(System.Random random,float min,float max) { return min+(float)random.NextDouble()*(max-min); }
    static void Main() {
        World.ActiveScene=new SceneTag("sprite culling regression");var rng=new System.Random(714);
        var parent=new GameObject("parent");var other=new GameObject("other");other.transform.position=new Vector3(20,7,0);
        var go=new GameObject("animated sprite");go.transform.SetParent(parent.transform,false);var sr=go.AddComponent<SpriteRenderer>();
        var images=new[]{Data(8,21,0,0,.5f,.5f),Data(100,50,4,2,.2f,.8f),Data(4,6,2,4,0,1)};
        bool correct=true;
        for(int i=0;i<2500;i++) {
            var data=images[i%images.Length];sr.flipX=(i&1)!=0;sr.flipY=(i&2)!=0;
            switch(i%5) {
                case 0: go.transform.localPosition=new Vector3(R(rng,-500,500),R(rng,-10,10),0);break;
                case 1: parent.transform.rotation=Quaternion.Euler(0,0,R(rng,-360,360));break;
                case 2: go.transform.localScale=new Vector3(R(rng,-3,3),R(rng,-3,3),1);break;
                case 3: parent.transform.localScale=new Vector3(R(rng,-3,3),R(rng,-3,3),1);break;
                case 4: go.transform.SetParent(i%2==0?parent.transform:other.transform,false);break;
            }
            if(!MatchesCorners(sr,data))correct=false;
        }
        Check(correct,"2500 moving, rotating, mirrored, flipped, animated and reparented bounds match independent transformed corners");
        sr.m_Sprite=new Sprite { data=images[0] };RenderSystem.PrepareSpriteBounds(sr,go.transform,images[0]);
        var clone=UnityEngine.Object.Instantiate(go);var csr=clone.GetComponent<SpriteRenderer>();clone.transform.position+=new Vector3(80,3,0);
        Check(MatchesCorners(csr,images[0]),"cloned warm bounds refresh against the clone transform");
        csr.flipX=!csr.flipX;Check(MatchesCorners(csr,images[0]),"flip after cloning updates asymmetric image bounds");
        Check(MatchesCorners(sr,images[2]),"replacement image geometry invalidates a warm cache");
        go.transform.localScale=Vector3.zero;Check(MatchesCorners(sr,images[1]),"zero scale produces collapsed bounds without stale coverage");
        Check(Lifecycle.Exceptions==0,"culling fixtures have no lifecycle exceptions");
        Console.WriteLine("Sprite culling regressions passed: "+passed);
    }
}
