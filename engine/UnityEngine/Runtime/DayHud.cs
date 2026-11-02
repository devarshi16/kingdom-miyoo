// Compact always-visible form of the original menu day widget, beside the optional coin
// counter. Reads the game's own kingdom day and director clock; never advances either.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace KNL {
public static class DayHud {
    const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static Func<int> day;
    static Func<float> clock;
    static Func<int,string> roman;
    static Component sourceText;
    static GameObject sourceRoot;
    static SpriteData sun;
    static Color sunColor = Color.white;
    static Font font;
    static Texture2D fontTexture;
    static float pixelsPerUnit = 1, minX, maxY, glyphScale = 1;
    static int lastDay = int.MinValue, width;
    static TextGenerator generator = new TextGenerator();
    public static bool Visible { get; private set; }
    public static int CurrentDay { get; private set; }
    public static float CurrentTime { get; private set; }

    public static void Observe(MonoBehaviour mb) {
        string name = mb.GetType().Name;
        if (name == "Kingdom") {
            var getter = mb.GetType().GetProperty("day", Instance);
            if (getter != null) day = (Func<int>)Delegate.CreateDelegate(typeof(Func<int>), mb, getter.GetGetMethod(true));
        } else if (name == "Director") {
            var getter = mb.GetType().GetProperty("currentTime", Instance);
            if (getter != null) clock = (Func<float>)Delegate.CreateDelegate(typeof(Func<float>), mb, getter.GetGetMethod(true));
        } else if (name == "Menu") {
            var field = mb.GetType().GetField("dayCounter", Instance);
            sourceText = field == null ? null : field.GetValue(mb) as Component;
            if (sourceText == null || sourceText.transform.parent == null) return;
            sourceRoot = sourceText.transform.parent.gameObject;
            var image = sourceRoot.GetComponent("Image");
            if (image != null) {
                var spriteProperty = image.GetType().GetProperty("sprite", Instance);
                var sprite = spriteProperty == null ? null : spriteProperty.GetValue(image, null) as Sprite;
                sun = sprite == null ? null : sprite.data;
                var tint = image.GetType().GetProperty("color", Instance);
                if (tint != null) sunColor = (Color)tint.GetValue(image, null);
            }
            lastDay = int.MinValue;
        }
    }
    public static bool ReplacesStock(GameObject go) { return Visible && ReferenceEquals(go, sourceRoot); }
    public static int TrackFill(float hour, int available) {
        return Mathf.Clamp((int)Math.Floor(Mathf.Clamp01(hour/24f)*available), 0, Math.Max(0, available));
    }
    public static void Prepare() {
        Visible = false;
        int balance;
        if (!CoinCounter.Enabled || day == null || clock == null || sourceText == null || sun == null || !CoinCounter.TryRead(out balance)) return;
        CurrentDay = day(); CurrentTime = clock();
        if (font == null || lastDay != CurrentDay) {
            var type = sourceText.GetType();
            var fp = type.GetProperty("font", Instance);
            font = fp == null ? null : fp.GetValue(sourceText, null) as Font;
            if (font == null) return;
            fontTexture = font.material.mainTexture as Texture2D;
            if (fontTexture == null) return;
            var ppu = type.GetProperty("pixelsPerUnit", Instance);
            pixelsPerUnit = ppu == null ? 1f : (float)ppu.GetValue(sourceText, null);
            if (pixelsPerUnit <= 0) pixelsPerUnit = 1;
            if (roman == null) {
                var assembly = World.GameAssembly("Assembly-CSharp");
                var util = assembly == null ? null : assembly.GetType("Util");
                var method = util == null ? null : util.GetMethod("RomanNumeral", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(int) }, null);
                if (method != null) roman = (Func<int,string>)Delegate.CreateDelegate(typeof(Func<int,string>), method);
            }
            // Menu.OnEnable uses this exact original Roman formatter and lower-case glyphs.
            string text = roman == null ? CurrentDay.ToString() : roman(CurrentDay).ToLowerInvariant();
            var settings = (TextGenerationSettings)type.GetMethod("GetGenerationSettings").Invoke(sourceText, new object[] { new Vector2(1024, 64) });
            settings.textAnchor = TextAnchor.UpperLeft; settings.pivot = new Vector2(0,1);
            settings.horizontalOverflow = HorizontalWrapMode.Overflow; settings.verticalOverflow = VerticalWrapMode.Overflow;
            generator.Populate(text, settings);
            minX = float.MaxValue; maxY = float.MinValue;
            float maxX = float.MinValue, minY = float.MaxValue;
            var vertices = generator.verts;
            for (int i = 0; i < vertices.Count-4; i++) {
                var p = vertices[i].position / pixelsPerUnit;
                minX = Math.Min(minX,p.x); maxX = Math.Max(maxX,p.x); minY = Math.Min(minY,p.y); maxY = Math.Max(maxY,p.y);
            }
            if (vertices.Count <= 4) return;
            glyphScale = maxY-minY > 14 ? 14/(maxY-minY) : 1;
            width = 26+(int)Math.Ceiling((maxX-minX)*glyphScale);
            lastDay = CurrentDay;
        }
        Visible = fontTexture != null;
    }
    static void Rect(int x, int y, int w, int h, uint c) { if (w>0 && h>0) Native.knl_quad_gradient(x,y,x+w,y+h,c,c,c,c,0); }
    public static void Draw(int coinLeft, int top) {
        if (!Visible) return;
        int left = coinLeft-3-width;
        Rect(left,top,width,19,0xB8201B16u);
        Native.knl_draw_quad(sun.Texture,left+1,top,left+19,top,left+1,top+18,
            0,0,sun.imgW,0,0,sun.imgH,sunColor.ToRGBA32(),0,0,0,RenderSystem.Width,RenderSystem.Height);
        int handle = UIRender.Handle(fontTexture), flags = fontTexture.alphaOnly ? 1 : 0;
        var vertices = generator.verts;
        for (int i = 0; i < vertices.Count-4; i += 4) {
            var a=vertices[i]; var b=vertices[i+1]; var d=vertices[i+3];
            float x0=left+22+(a.position.x/pixelsPerUnit-minX)*glyphScale;
            float y0=top+2+(maxY-a.position.y/pixelsPerUnit)*glyphScale;
            float x1=left+22+(b.position.x/pixelsPerUnit-minX)*glyphScale;
            float y1=top+2+(maxY-b.position.y/pixelsPerUnit)*glyphScale;
            float x3=left+22+(d.position.x/pixelsPerUnit-minX)*glyphScale;
            float y3=top+2+(maxY-d.position.y/pixelsPerUnit)*glyphScale;
            Native.knl_draw_quad(handle,x0,y0,x1,y1,x3,y3,a.uv0.x*fontTexture.width,(1-a.uv0.y)*fontTexture.height,
                b.uv0.x*fontTexture.width,(1-b.uv0.y)*fontTexture.height,d.uv0.x*fontTexture.width,(1-d.uv0.y)*fontTexture.height,
                ((Color)a.color).ToRGBA32(),flags,0,0,RenderSystem.Width,RenderSystem.Height);
        }
        Rect(left+2,top+18,width-4,1,0xFF554536u);
        Rect(left+2,top+18,TrackFill(CurrentTime,width-4),1,0xFFB5EFFFu);
    }
}
}
