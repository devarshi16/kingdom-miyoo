// Optional repair for old packs whose converter stripped UI.Text.m_FontData dictionaries.
// Recovery is generated locally from verified owned files and bound to exact pack object blobs.
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using JObj = System.Collections.Generic.Dictionary<string, object>;

namespace KNL {
public static class UIFontMetadata {
    static bool loaded;
    static JObj assets;
    static readonly HashSet<string> verified = new HashSet<string>();
    static void Load() {
        if (loaded) return; loaded = true;
        string path = Path.Combine(Paths.DataDir, "ui-fontdata.json");
        if (!File.Exists(path)) return;
        try {
            if (new FileInfo(path).Length > 4*1024*1024) throw new InvalidDataException("UI metadata is too large");
            var root = Json.ParseFile(path) as JObj;
            if (root == null || Json.L(root, "format") != 1) throw new InvalidDataException("Unsupported UI metadata format");
            assets = root["assets"] as JObj;
        } catch (Exception e) { assets = null; Log.Write("WARN", "UI font metadata ignored: "+e.Message); }
    }
    public static void VerifyPackEntry(string name, byte[] bytes) {
        Load();
        object item;
        if (assets == null || !assets.TryGetValue(name, out item)) return;
        var data = item as JObj;
        string wanted = data == null ? null : Json.S(data, "entry_sha256");
        string actual;
        using (var hash = SHA256.Create()) actual = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        if (wanted != null && actual == wanted) verified.Add(name);
        else Log.Write("WARN", "UI font metadata does not match pack objects/"+name+"; ignored");
    }
    public static JObj Restore(string name, long pid, Component component, JObj data) {
        Load();
        if (assets == null || !verified.Contains(name) || component.GetType().FullName != "UnityEngine.UI.Text") return data;
        object old;
        if (!data.TryGetValue("m_FontData", out old) || !(old is JObj) || !((JObj)old).ContainsKey("$stripped")) return data;
        var fields = ((JObj)assets[name])["fields"] as JObj;
        object font;
        if (fields == null || !fields.TryGetValue(pid.ToString(System.Globalization.CultureInfo.InvariantCulture), out font) || !(font is JObj)) return data;
        var result = new JObj(data); result["m_FontData"] = font; return result;
    }
}
}
