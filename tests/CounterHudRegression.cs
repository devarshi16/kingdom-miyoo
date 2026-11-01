using System;
using System.IO;
using System.Linq;
using KNL;
using UnityEngine;
class Wallet : MonoBehaviour { public bool showCoinBag = true; public int _coins; public int coins { get { return _coins; } set { _coins = value; } } public event Action OnPickupCoin; }
class Player : MonoBehaviour { public void CoinFellFromBag() { } }
class CounterHudRegression {
    // Frozen reference of the pre-atlas HUD, independent of the new texture/quad path.
    static readonly string[] Digits = { "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001", "111100111001111", "111100111101111", "111001001001001", "111101111101111", "111101111001111" };
    static void Rect(int x, int y, int w, int h, uint c) { Native.knl_quad_gradient(x,y,x+w,y+h,c,c,c,c,0); }
    static void Clear() { Native.knl_clear(0xFF443322u); Native.knl_quad_gradient(0,0,320,240,0xFF214365u,0xFF224466u,0xFF557799u,0xFF6688AAu,0); }
    static void Reference(int count) {
        string text = count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        int width=19+text.Length*8,left=320-5-width,top=5;
        Rect(left,top,width,19,0xB8201B16u);
        Rect(left+5,top+4,5,11,0xFF25466Bu); Rect(left+3,top+6,9,7,0xFF25466Bu);
        Rect(left+5,top+5,5,9,0xFF47BCEAu); Rect(left+4,top+7,7,5,0xFF47BCEAu); Rect(left+6,top+6,1,7,0xFFB5EFFFu);
        for(int i=0;i<text.Length;i++) {
            int x=left+16+i*8; char c=text[i];
            if(c=='-') { Rect(x,top+9,6,2,0xFFE7F1FFu); continue; }
            for(int r=0;r<5;r++)for(int col=0;col<3;col++)if(Digits[c-'0'][r*3+col]=='1')Rect(x+col*2,top+4+r*2,2,2,0xFFE7F1FFu);
        }
    }
    static void Main() {
        string dir=Path.Combine(Path.GetTempPath(),"knl-counter-hud-"+Guid.NewGuid()); Directory.CreateDirectory(dir);
        World.ActiveScene=new SceneTag("HUD regression"); var go=new GameObject("Player"); go.SetActive(false); go.AddComponent<Player>(); var wallet=go.AddComponent<Wallet>(); go.SetActive(true);
        if(Native.knl_init(320,240,1,0)!=0)throw new Exception("Native framebuffer initialization failed");
        try {
            int cases=0; long initialBytes=Native.knl_tex_bytes();
            foreach(int count in new[]{0,1,40,1000,-1,int.MinValue,int.MaxValue}) {
                wallet.coins=count; Clear(); Reference(count); string before=Path.Combine(dir,"before.png"); Native.knl_save_png(before,1);
                Clear(); CoinCounter.Draw(); string after=Path.Combine(dir,"after.png"); Native.knl_save_png(after,1);
                if(!File.ReadAllBytes(before).SequenceEqual(File.ReadAllBytes(after)))throw new Exception("HUD pixels differ for "+count);
                int sprites,pixels; Native.knl_stats(out sprites,out pixels);
                if(sprites!=count.ToString(System.Globalization.CultureInfo.InvariantCulture).Length+1)throw new Exception("HUD needs more than one quad per glyph/icon");
                if(Native.knl_tex_bytes()-initialBytes!=42*11*4)throw new Exception("HUD atlas is not reused across balance changes");
                cases++; Console.WriteLine("PASS: identical HUD pixels, one quad per glyph/icon, cached 1848-byte atlas for "+count);
            }
            Console.WriteLine("Counter HUD regressions passed: "+cases);
        } finally { Native.knl_shutdown(); Directory.Delete(dir,true); }
    }
}
