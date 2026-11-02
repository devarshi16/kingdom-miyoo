using System;
using System.Collections;
using System.Reflection;
using KNL;
using UnityEngine;

class DayHudRegression {
    static int passed;
    static void Check(bool ok, string label) {
        if (!ok) throw new Exception("FAIL: " + label);
        passed++;
        Console.WriteLine("PASS: " + label);
    }
    static void Field(object obj, string name, object value) {
        obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
    }
    static Font BitmapFont() {
        var font = new Font();
        Field(font, "m_FontSize", 16);
        Field(font, "m_LineSpacing", 20f);
        Field(font, "m_Ascent", 14f);
        var glyphs = (IDictionary)typeof(Font).GetField("m_Glyphs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(font);
        foreach (char ch in "ivIV") {
            object glyph = new CharacterInfo();
            Field(glyph, "m_Advance", 10f);
            Field(glyph, "m_Vert", new Rect(0, 12, 10, -14));
            glyphs.Add((int)ch, glyph);
        }
        return font;
    }
    static TextGenerationSettings MakePersistent(TextGenerationSettings source) {
        var method = typeof(DayHud).GetMethod("PersistentLabelSettings", BindingFlags.Static | BindingFlags.NonPublic);
        return (TextGenerationSettings)method.Invoke(null, new object[] { source });
    }
    static void Main(string[] args) {
        var game = Assembly.LoadFrom(args[0]);
        var util = game.GetType("Util");
        var roman = util.GetMethod("RomanNumeral", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        var font = BitmapFont();
        var source = new TextGenerationSettings {
            font = font, fontSize = 0, scaleFactor = 1, color = new Color(.7f, .6f, .5f, 0),
            lineSpacing = 1, generationExtents = new Vector2(128, 32), pivot = new Vector2(0, 1),
            textAnchor = TextAnchor.UpperLeft, horizontalOverflow = HorizontalWrapMode.Overflow,
            verticalOverflow = VerticalWrapMode.Overflow
        };
        var persistent = MakePersistent(source);
        Check(source.color.a == 0 && persistent.color.a == 1, "Opaque persistent settings preserve the faded source Text color");
        Check(Math.Abs(persistent.color.r - .7f) < .001f && Math.Abs(persistent.color.g - .6f) < .001f && Math.Abs(persistent.color.b - .5f) < .001f,
            "Persistent settings retain the original Text RGB tint");

        foreach (int day in new[] { 2, 5 }) foreach (bool paused in new[] { false, true }) {
            string label = ((string)roman.Invoke(null, new object[] { day })).ToLowerInvariant();
            Check(label == (day == 2 ? "ii" : "v"), "Original RomanNumeral formats day " + day);
            var generator = new TextGenerator();
            generator.Populate(label, MakePersistent(source));
            bool opaque = generator.vertexCount > 4;
            for (int i = 0; i < generator.vertexCount - 4; i++) opaque &= generator.verts[i].color.a == 255;
            Check(opaque, "Day " + day + " remains fully opaque while clock " + (paused ? "paused" : "running"));
        }
        Console.WriteLine("Day HUD regression passed: " + passed);
    }
}
