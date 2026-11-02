using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using KNL;
using UnityEngine;
namespace UnityEngine.UI { class Text : MonoBehaviour { } }
class OtherLabel : MonoBehaviour { }
class UIRegression {
    static int passed;
    static void Check(bool ok,string label) { if(!ok)throw new Exception("FAIL: "+label);passed++;Console.WriteLine("PASS: "+label); }
    static void Field(object obj,string name,object value) { obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(obj,value); }
    static Font BitmapFont() {
        var font=new Font(); Field(font,"m_FontSize",16);Field(font,"m_LineSpacing",20f);Field(font,"m_Ascent",14f);
        var glyphs=(IDictionary)typeof(Font).GetField("m_Glyphs",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(font);
        for(int i=32;i<127;i++) {
            int advance=10;if(i==' ')advance=4;if(i=='+'||i=='X')advance=8;if(i=='Q'||i=='W'||i=='M')advance=14;if(i=='T'||i=='V')advance=12;
            object glyph=new CharacterInfo();Field(glyph,"m_Advance",(float)advance);Field(glyph,"m_Vert",new Rect(0,12,advance,-14));glyphs.Add(i,glyph);
        }
        return font;
    }
    static void Main() {
        var font=BitmapFont();var settings=new TextGenerationSettings {font=font,fontSize=0,scaleFactor=2,color=Color.white,lineSpacing=.75f,generationExtents=new Vector2(92,24),pivot=new Vector2(.5f,1),textAnchor=TextAnchor.MiddleCenter,horizontalOverflow=HorizontalWrapMode.Wrap,verticalOverflow=VerticalWrapMode.Truncate};
        var generator=new TextGenerator();generator.Populate("SAVE + QUIT",settings);
        Check(generator.lineCount==1,"Baked Save+Quit label fits original 92-unit button");
        Check(Math.Abs(generator.GetPreferredWidth("SAVE + QUIT",settings)-104)<.001f,"Preferred width returns intrinsic native pixels for one UI pixelsPerUnit division");
        Check(Math.Abs(generator.GetPreferredHeight("SAVE + QUIT",settings)-15)<.001f,"Preferred baked line height is not scaled twice");
        float glyphWidth=generator.verts[1].position.x-generator.verts[0].position.x;
        Check(Math.Abs(glyphWidth-10)<.001f,"Baked glyph metrics remain intrinsic with scaleFactor two");
        generator.Populate("CONTINUE",settings);Check(generator.lineCount==1,"Continue final E stays on its original line");
        var narrow=settings;narrow.generationExtents=new Vector2(30,24);generator.Populate("SAVE + QUIT",narrow);Check(generator.lineCount>1,"Words still wrap in a genuinely narrow text rectangle");
        var explicitSize=settings;explicitSize.fontSize=8;explicitSize.scaleFactor=1;
        Check(Math.Abs(generator.GetPreferredWidth("SAVE + QUIT",explicitSize)-52)<.001f,"Explicit positive font size keeps its requested sizing contract");
        var rich=settings;rich.richText=true;Check(Math.Abs(generator.GetPreferredWidth("<color=#ff0000>SAVE</color> + QUIT",rich)-104)<.001f,"Rich text tags do not change measured glyph width");
        Check(DayHud.TrackFill(0,40)==0&&DayHud.TrackFill(6,40)==10&&DayHud.TrackFill(12,40)==20&&DayHud.TrackFill(24,40)==40,"Clock progress reads actual hour fractions across a complete day");
        Check(DayHud.TrackFill(-1,40)==0&&DayHud.TrackFill(30,40)==40&&DayHud.TrackFill(12,-2)==0,"Clock progress clamps out-of-range display geometry");
        string temp=Path.Combine(Path.GetTempPath(),"knl-ui-metadata-"+Guid.NewGuid());Directory.CreateDirectory(temp);Paths.DataDir=temp;
        byte[] payload=Encoding.UTF8.GetBytes("original pack object blob");string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(payload)).Replace("-","").ToLowerInvariant();
        File.WriteAllText(Path.Combine(temp,"ui-fontdata.json"),"{\"format\":1,\"assets\":{\"level0\":{\"entry_sha256\":\""+hash+"\",\"fields\":{\"123\":{\"m_FontSize\":8,\"m_Alignment\":4,\"m_LineSpacing\":0.75}}},\"level1\":{\"entry_sha256\":\"wrong\",\"fields\":{\"123\":{\"m_FontSize\":99}}}}}");
        World.ActiveScene=new SceneTag("UI regression");var text=new GameObject("text").AddComponent<UnityEngine.UI.Text>();var other=new GameObject("other").AddComponent<OtherLabel>();
        var original=new Dictionary<string,object>{{"m_FontData",new Dictionary<string,object>{{"$stripped",12d}}}};
        UIFontMetadata.VerifyPackEntry("level0",payload);var restored=UIFontMetadata.Restore("level0",123,text,original);
        Check(!ReferenceEquals(restored,original)&&Json.L((Dictionary<string,object>)restored["m_FontData"],"m_FontSize")==8,"Exact matching pack entry recovers original UI font metadata");
        Check(((Dictionary<string,object>)original["m_FontData"]).ContainsKey("$stripped"),"Recovery does not mutate shared original object trees");
        UIFontMetadata.VerifyPackEntry("level1",payload);Check(ReferenceEquals(UIFontMetadata.Restore("level1",123,text,original),original),"Metadata for a different pack entry is refused");
        Check(ReferenceEquals(UIFontMetadata.Restore("level0",123,other,original),original),"Metadata cannot alter unrelated component types");
        Check(ReferenceEquals(UIFontMetadata.Restore("level0",999,text,original),original),"Unlisted original path IDs remain unchanged");
        var intact=new Dictionary<string,object>{{"m_FontData",new Dictionary<string,object>{{"m_FontSize",12d}}}};
        Check(ReferenceEquals(UIFontMetadata.Restore("level0",123,text,intact),intact),"Fresh inline font layout always takes precedence over compatibility metadata");
        Directory.Delete(temp,true);Console.WriteLine("UI regressions passed: "+passed);
    }
}
