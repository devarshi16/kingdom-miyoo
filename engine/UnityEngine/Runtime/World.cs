// KNL world: loads the converter's object JSON, builds GameObjects/components (including the
// game's own MonoBehaviour types from the user's Assembly-CSharp.dll) and resolves references.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine;
using JObj = System.Collections.Generic.Dictionary<string, object>;

namespace KNL
{
    /// Placeholder for native Unity component types we do not model yet.
    public sealed class UnknownComponent : Component { public string unityType; }

    public sealed class AssetFile
    {
        public string name;
        public List<string> externals = new List<string>();
        public ObjectTable objects = new ObjectTable();
        public Dictionary<long, UnityEngine.Object> instances = new Dictionary<long, UnityEngine.Object>();
        public bool hierarchyBuilt;
        public SceneTag scene;   // set when loaded as a scene
    }

    public static partial class World
    {
        static readonly Dictionary<string, AssetFile> files = new Dictionary<string, AssetFile>();
        static readonly Dictionary<string, Assembly> assemblies = new Dictionary<string, Assembly>();
        public static readonly List<GameObject> SceneObjects = new List<GameObject>();
        static readonly List<UnityEngine.Object> pendingDestroy = new List<UnityEngine.Object>();
        public static readonly Dictionary<string, int> Stats = new Dictionary<string, int>();
        public static readonly Dictionary<string, int> UnknownTypes = new Dictionary<string, int>();
        public static readonly Dictionary<string, int> CtorFailures = new Dictionary<string, int>();
        static readonly List<KeyValuePair<UnityEngine.Object, JObj>> populateQueue = new List<KeyValuePair<UnityEngine.Object, JObj>>();

        static void Count(Dictionary<string, int> d, string k) { int n; d.TryGetValue(k, out n); d[k] = n + 1; }

        // --------------------------------------------------------------- files
        public static AssetFile File(string name)
        {
            AssetFile f;
            if (files.TryGetValue(name, out f)) return f;
            f = new AssetFile { name = name };
            files[name] = f;
            if (Pack.Active)
            {
                var b = Pack.Read("objects/" + name);
                if (b == null) { Log.Write("WARN", "no data for " + name); return f; }
                f.objects = ObjectTable.FromPack(b, f.externals);
                return f;
            }
            string p = Path.Combine(Path.Combine(Paths.DataDir, "objects"), name + ".json");
            if (!System.IO.File.Exists(p)) { Log.Write("WARN", "no data for " + name); return f; }
            var root = (JObj)Json.ParseFile(p);
            foreach (var e in (List<object>)root["externals"]) f.externals.Add((string)e);
            foreach (var kv in (JObj)root["objects"]) f.objects.Add(long.Parse(kv.Key), (JObj)kv.Value);
            return f;
        }

        /// debug: drop parsed object trees (memory experiment)
        public static void DropObjectData() { foreach (var f in files.Values) f.objects.Clear(); }

        public static Assembly GameAssembly(string dllName)
        {
            string n = dllName.EndsWith(".dll") ? dllName.Substring(0, dllName.Length - 4) : dllName;
            Assembly a;
            if (assemblies.TryGetValue(n, out a)) return a;
            try { a = Assembly.Load(n); } catch (Exception e) { Log.Write("WARN", "cannot load " + n + ": " + e.Message); a = null; }
            assemblies[n] = a;
            return a;
        }

        // ------------------------------------------------------------ settings
        public static void LoadSettings()
        {
            var g = File("globalgamemanagers");
            foreach (var o in g.objects.Values)
            {
                if ((string)o["type"] != "TagManager" || !o.ContainsKey("data")) continue;
                var d = (JObj)o["data"];
                var layers = (List<object>)d["layers"];
                for (int i = 0; i < layers.Count && i < 32; i++) EngineSettings.Layers[i] = (string)layers[i];
                EngineSettings.Tags = ((List<object>)d["tags"]).Cast<string>().ToArray();
            }
            Time.s_FixedDeltaTime = 0.02f;
        }

        // ----------------------------------------------------------- resolving
        public static AssetFile Target(AssetFile from, long fileId)
        {
            if (fileId == 0) return from;
            if (fileId < 1 || fileId > from.externals.Count) return null;
            return File(from.externals[(int)fileId - 1]);
        }

        public static UnityEngine.Object Resolve(AssetFile from, object pptr)
        {
            var d = pptr as JObj;
            if (d == null) return null;
            long pid = Json.L(d, "m_PathID");
            if (pid == 0) return null;
            var f = Target(from, Json.L(d, "m_FileID"));
            if (f == null) return null;
            return Get(f, pid);
        }

        public static UnityEngine.Object Get(AssetFile f, long pid)
        {
            UnityEngine.Object o;
            if (f.instances.TryGetValue(pid, out o)) return o;
            JObj e;
            if (!f.objects.TryGetValue(pid, out e))
            {
                if (f.name == "unity default resources" || f.name == "unity_builtin_extra")
                {
                    o = Builtin(f.name, pid);
                    if (o != null) { o.EnsureId(); f.instances[pid] = o; }
                    return o;
                }
                return null;
            }
            string type = (string)e["type"];
            if (IsHierarchy(e))
            {
                // GameObjects and their components in a non-scene file are prefab assets:
                // build the whole file's hierarchy once, then look again.
                if (!f.hierarchyBuilt) BuildHierarchy(f, null);
                f.instances.TryGetValue(pid, out o);
                return o;
            }
            o = CreateAsset(f, pid, type, e);
            if (o != null) { o.EnsureId(); f.instances[pid] = o; }
            return o;
        }

        /// Unity's built-in resource files are not part of the converted data; synthesize the
        /// handful of well-known objects the game references (quad mesh, default materials, fonts).
        static UnityEngine.Object Builtin(string file, long pid)
        {
            switch (pid)
            {
                case 10202: case 10206: case 10207: case 10208: case 10209: case 10210:
                {
                    var m = new Mesh(); m.name = pid == 10210 ? "Quad" : "Builtin Mesh";
                    m.m_Vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
                    m.m_Colors = new[] { Color.white, Color.white, Color.white, Color.white };
                    m.m_UV = new[] { new Vector2(0, 0), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 1) };
                    m.m_Triangles = new[] { 0, 1, 2, 1, 0, 3 };
                    return m;
                }
                case 10102:
                {
                    var fo = new Font(); fo.name = "Arial"; fo.m_Material = new Material(); fo.m_Material.m_MainTexture = new Texture2D(1, 1); return fo;
                }
                default:
                {
                    var mat = new Material(); mat.name = file == "unity_builtin_extra" ? "Builtin-" + pid : "Default-" + pid; return mat;
                }
            }
        }

        static bool IsHierarchy(JObj e)
        {
            string t = (string)e["type"];
            if (t == "GameObject") return true;
            var d = e.ContainsKey("data") ? e["data"] as JObj : null;
            if (t == "MonoBehaviour")   // component on a GameObject vs. ScriptableObject asset
                return d != null && d.ContainsKey("m_GameObject") && Json.L((JObj)d["m_GameObject"], "m_PathID") != 0;
            return IsComponentTypeName(t);
        }
        static bool IsComponentTypeName(string t)
        {
            var ty = UnityType(t);
            return ty != null && typeof(Component).IsAssignableFrom(ty);
        }

        static readonly Dictionary<string, Type> unityTypes = new Dictionary<string, Type>();
        public static Type UnityType(string name)
        {
            Type t;
            if (unityTypes.TryGetValue(name, out t)) return t;
            t = typeof(GameObject).Assembly.GetType("UnityEngine." + name);
            unityTypes[name] = t;
            return t;
        }

        // ------------------------------------------------------------- assets
        static UnityEngine.Object CreateAsset(AssetFile f, long pid, string type, JObj e)
        {
            var data = e.ContainsKey("data") ? (JObj)e["data"] : null;
            string nm = data != null ? (Json.S(data, "m_Name") ?? "") : "";
            Count(Stats, "asset:" + type);
            switch (type)
            {
                case "Sprite":
                {
                    var s = new Sprite(); s.name = nm; s.data = SpriteData.For(f.name, pid);
                    if (data != null && data.ContainsKey("m_Border")) { var b = (JObj)data["m_Border"]; s.m_Border = new Vector4(Json.F(b, "x"), Json.F(b, "y"), Json.F(b, "z"), Json.F(b, "w")); }
                    return s;
                }
                case "Texture2D":
                {
                    var t = new Texture2D(); t.name = nm; t.m_File = f.name; t.m_PathID = pid;
                    if (data != null) { t.m_Width = (int)Json.L(data, "m_Width"); t.m_Height = (int)Json.L(data, "m_Height"); t.m_Format = (int)Json.L(data, "m_TextureFormat"); }
                    return t;
                }
                case "Material":
                {
                    var m = new Material(); m.name = nm;
                    if (data != null)
                    {
                        f.instances[pid] = m;  // allow self references
                        m.m_Shader = Resolve(f, data["m_Shader"]) as Shader;
                        var sp = data["m_SavedProperties"] as JObj;
                        if (sp != null)
                        {
                            foreach (List<object> kv in (List<object>)sp["m_Colors"]) m.m_ColorProps[(string)kv[0]] = Json.C(kv[1]);
                            foreach (List<object> kv in (List<object>)sp["m_Floats"]) m.m_FloatProps[(string)kv[0]] = (float)(double)kv[1];
                            var kw = data.ContainsKey("m_ShaderKeywords") ? data["m_ShaderKeywords"] as string : null;
                            if (!string.IsNullOrEmpty(kw)) foreach (var k in kw.Split(' ')) if (k.Length > 0) m.m_Keywords.Add(k);
                            foreach (List<object> kv in (List<object>)sp["m_TexEnvs"])
                            {
                                var te = (JObj)kv[1]; string tn = (string)kv[0];
                                m.m_TexOffsets[tn] = Json.V2(te["m_Offset"]); m.m_TexScales[tn] = Json.V2(te["m_Scale"]);
                                if (tn == "_MainTex") m.m_MainTexture = Resolve(f, te["m_Texture"]) as Texture;
                            }
                        }
                    }
                    return m;
                }
                case "AnimationClip":
                {
                    var c = new AnimationClip(); c.name = nm; c.m_File = f; c.m_Raw = data; return c;
                }
                case "AnimatorController":
                {
                    var ac = new AnimControllerAsset { file = f, raw = data }; ac.name = nm; return ac;
                }
                case "AnimatorOverrideController":
                {
                    var oc = new AnimatorOverrideController(); oc.name = nm;
                    oc.EnsureId(); f.instances[pid] = oc;
                    if (data != null)
                    {
                        oc.m_Base = Resolve(f, data["m_Controller"]) as RuntimeAnimatorController;
                        foreach (JObj kv in (List<object>)data["m_Clips"])
                        {
                            var o = Resolve(f, kv["m_OriginalClip"]) as AnimationClip; var n = Resolve(f, kv["m_OverrideClip"]) as AnimationClip;
                            if (o != null) oc.m_Overrides[o] = n;
                        }
                    }
                    return oc;
                }
                case "AudioClip":
                {
                    var ac = new AudioClip(); ac.name = nm; if (data != null) ac.m_Length = Json.F(data, "m_Length"); return ac;
                }
                case "Font":
                {
                    var fo = new Font(); fo.name = nm; fo.EnsureId(); f.instances[pid] = fo;
                    if (data != null)
                    {
                        fo.m_FontSize = (int)Json.L(data, "m_FontSize");
                        fo.m_LineSpacing = Json.F(data, "m_LineSpacing", 16f); fo.m_Ascent = Json.F(data, "m_Ascent", 12f);
                        fo.m_Descent = Json.F(data, "m_Descent", -4f); fo.m_ConvertCase = (int)Json.L(data, "m_ConvertCase");
                        var rects = data.ContainsKey("m_CharacterRects") ? data["m_CharacterRects"] as List<object> : null;
                        if (rects != null)
                            foreach (JObj g in rects)
                            {
                                var ci = new CharacterInfo(); var uv = (JObj)g["uv"]; var vr = (JObj)g["vert"];
                                ci.m_UV = new Rect(Json.F(uv, "x"), Json.F(uv, "y"), Json.F(uv, "width"), Json.F(uv, "height"));
                                ci.m_Vert = new Rect(Json.F(vr, "x"), Json.F(vr, "y"), Json.F(vr, "width"), Json.F(vr, "height"));
                                ci.m_Advance = Json.F(g, "advance"); ci.m_Flipped = Json.B(g, "flipped");
                                fo.m_Glyphs[(int)Json.L(g, "index")] = ci;
                            }
                        fo.m_Material = Resolve(f, data["m_DefaultMaterial"]) as Material;
                        var tex = Resolve(f, data["m_Texture"]) as Texture;
                        if (fo.m_Material == null) { fo.m_Material = new Material(); fo.m_Material.name = nm + " Material"; }
                        if (fo.m_Material.m_MainTexture == null) fo.m_Material.m_MainTexture = tex ?? new Texture2D(1, 1);
                    }
                    return fo;
                }
                case "AudioMixerController":
                {
                    var mx = new UnityEngine.Audio.AudioMixer(); mx.name = nm; f.instances[pid] = mx;
                    if (data != null) LoadMixer(f, mx, data);
                    AudioSystem.Register(mx);
                    return mx;
                }
                case "AudioMixerSnapshotController":
                {
                    var sn = new UnityEngine.Audio.AudioMixerSnapshot(); sn.name = nm; f.instances[pid] = sn;
                    if (data != null) { sn.m_Mixer = Resolve(f, data["m_AudioMixer"]) as UnityEngine.Audio.AudioMixer; if (sn.m_Mixer != null) sn.m_Values = sn.m_Mixer.SnapshotValues(GuidOf(data["m_SnapshotID"])); }
                    return sn;
                }
                case "AudioMixerGroupController":
                {
                    var gr = new UnityEngine.Audio.AudioMixerGroup(); gr.name = nm; f.instances[pid] = gr;
                    if (data != null) { gr.m_Mixer = Resolve(f, data["m_AudioMixer"]) as UnityEngine.Audio.AudioMixer; gr.m_Guid = GuidOf(data["m_GroupID"]); }
                    return gr;
                }
                case "PhysicsMaterial2D":
                {
                    var pm = new PhysicsMaterial2D(); pm.name = nm;
                    if (data != null) { pm.m_Friction = Json.F(data, "friction", 0.4f); pm.m_Bounciness = Json.F(data, "bounciness"); }
                    return pm;
                }
                case "Shader":
                {
                    var s = new Shader();
                    // 2017 shaders keep their name in m_ParsedForm
                    var pf = data != null && data.ContainsKey("m_ParsedForm") ? data["m_ParsedForm"] as JObj : null;
                    s.name = pf != null ? (Json.S(pf, "m_Name") ?? nm) : nm;
                    return s;
                }
                case "Mesh":
                {
                    var m = new Mesh(); m.name = nm;
                    // Kingdom's meshes are quads (sky/haze/water). Raw vertex bytes are not kept by
                    // convert.py, so rebuild the quad from vertex count + local bounds.
                    int vc = 4; Vector3 ctr = Vector3.zero, ext = new Vector3(0.5f, 0.5f, 0);
                    if (data != null)
                    {
                        vc = (int)Json.L((JObj)data["m_VertexData"], "m_VertexCount");
                        var aabb = (JObj)data["m_LocalAABB"]; ctr = Json.V3(aabb["m_Center"]); ext = Json.V3(aabb["m_Extent"]);
                    }
                    m.m_Vertices = new Vector3[vc]; m.m_Colors = new Color[vc]; m.m_UV = new Vector2[vc];
                    for (int i = 0; i < vc; i++) m.m_Colors[i] = Color.white;
                    if (vc == 4)
                    {
                        m.m_Vertices = new[] { ctr + new Vector3(-ext.x, -ext.y, 0), ctr + new Vector3(ext.x, ext.y, 0), ctr + new Vector3(ext.x, -ext.y, 0), ctr + new Vector3(-ext.x, ext.y, 0) };
                        m.m_UV = new[] { new Vector2(0, 0), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 1) };
                        m.m_Triangles = new[] { 0, 1, 2, 1, 0, 3 };
                    }
                    return m;
                }
                case "MonoBehaviour":
                {
                    // ScriptableObject asset (or a MonoBehaviour living in an asset file)
                    var script = e.ContainsKey("script") ? (string)e["script"] : null;
                    var t = ScriptType(script);
                    if (t == null || !typeof(ScriptableObject).IsAssignableFrom(t)) { Count(UnknownTypes, "asset-script:" + script); return null; }
                    var so = (UnityEngine.Object)CreateScriptInstance(t);
                    so.name = nm;
                    f.instances[pid] = so;
                    if (data != null) Serializer.Populate(so, data, f);
                    return so;
                }
                default:
                {
                    var t = UnityType(type);
                    UnityEngine.Object o = null;
                    if (t != null && typeof(UnityEngine.Object).IsAssignableFrom(t) && !t.IsAbstract)
                    {
                        try { o = (UnityEngine.Object)Activator.CreateInstance(t, true); }
                        catch { o = (UnityEngine.Object)FormatterServices.GetUninitializedObject(t); o.EnsureId(); }
                    }
                    if (o == null) { Count(UnknownTypes, "asset:" + type); o = new UnityEngine.Object(); }
                    o.name = nm;
                    return o;
                }
            }
        }

        public static void EnsureClip(AnimationClip c)
        {
            if (c.m_Data != null || c.m_Raw == null) return;
            try { c.m_Data = ClipData.Decode(c.m_File, c.m_Raw); }
            catch (Exception e) { Log.Write("WARN", "clip " + c.name + ": " + e.Message); c.m_Raw = null; }
        }

        static readonly Dictionary<string, int> classIds = new Dictionary<string, int>
        {
            {"GameObject",1},{"Transform",4},{"Camera",20},{"MeshRenderer",23},{"MeshFilter",33},{"Rigidbody2D",50},
            {"CircleCollider2D",58},{"PolygonCollider2D",60},{"BoxCollider2D",61},{"EdgeCollider2D",68},{"AudioSource",82},
            {"Animator",95},{"TrailRenderer",96},{"MonoBehaviour",114},{"ParticleSystem",198},{"ParticleSystemRenderer",199},
            {"SpriteRenderer",212},{"Canvas",223},{"CanvasGroup",225},{"CanvasRenderer",222},{"RectTransform",224},
        };
        public static int ClassIdOf(Component c)
        {
            if (c is MonoBehaviour) return 114;
            int id; return classIds.TryGetValue(c.GetType().Name, out id) ? id : -1;
        }

        public static Type ScriptType(string script)
        {
            if (string.IsNullOrEmpty(script) || script.StartsWith("?")) return null;
            int c = script.IndexOf(':');
            var asm = GameAssembly(script.Substring(0, c));
            if (asm == null) return null;
            string tn = script.Substring(c + 1).TrimStart('.');
            return asm.GetType(tn);
        }

        public static object CreateScriptInstance(Type t)
        {
            try { return Activator.CreateInstance(t, true); }
            catch (Exception ex)
            {
                var inner = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
                Count(CtorFailures, t.FullName + ": " + inner.GetType().Name + " " + inner.Message);
                var o = FormatterServices.GetUninitializedObject(t);
                var uo = o as UnityEngine.Object;
                if (uo != null) uo.EnsureId();
                return o;
            }
        }

        // ----------------------------------------------------------- hierarchy
        /// Build every GameObject + component in file f. scene == null -> prefab assets.
        public static void BuildHierarchy(AssetFile f, SceneTag scene)
        {
            if (f.hierarchyBuilt) return;
            f.hierarchyBuilt = true;
            f.scene = scene;
            var gos = new List<KeyValuePair<long, JObj>>();
            // phase 1a: GameObjects
            foreach (var kv in f.objects)
            {
                if ((string)kv.Value["type"] != "GameObject" || !kv.Value.ContainsKey("data")) continue;
                var d = (JObj)kv.Value["data"];
                var go = new GameObject(scene, Json.S(d, "m_Name"));
                go.m_IsActive = Json.B(d, "m_IsActive");
                go.m_Layer = (int)Json.L(d, "m_Layer");
                int tagIdx = (int)Json.L(d, "m_Tag");
                go.m_Tag = TagName(tagIdx);
                f.instances[kv.Key] = go;
                gos.Add(new KeyValuePair<long, JObj>(kv.Key, d));
                if (scene != null) SceneObjects.Add(go);
                Count(Stats, scene != null ? "scene:GameObject" : "prefab:GameObject");
            }
            // phase 1b: components (created, attached; data applied in phase 2)
            foreach (var g in gos)
            {
                var go = (GameObject)f.instances[g.Key];
                foreach (var c in (List<object>)g.Value["m_Component"])
                {
                    var cref = ((JObj)c)["component"] as JObj;
                    if (cref == null) continue;
                    long cpid = Json.L(cref, "m_PathID");
                    JObj ce;
                    if (Json.L(cref, "m_FileID") != 0 || !f.objects.TryGetValue(cpid, out ce)) continue;
                    var comp = CreateComponent(f, cpid, ce);
                    if (comp == null) continue;
                    comp.m_GameObject = go;
                    comp.name = go.name;
                    go.m_Components.Add(comp);
                    if (scene != null) RegisterSceneComponent(comp);
                    var tr = comp as Transform;
                    if (tr != null) go.m_Transform = tr;
                    f.instances[cpid] = comp;
                    if (ce.ContainsKey("data")) populateQueue.Add(new KeyValuePair<UnityEngine.Object, JObj>(comp, (JObj)ce["data"]));
                }
            }
            // phase 2: apply data (references can now resolve inside this file)
            var queue = populateQueue.ToArray(); populateQueue.Clear();
            foreach (var kv in queue) ApplyComponentData(f, (Component)kv.Key, kv.Value);
        }

        static string TagName(int i)
        {
            if (i == 0) return "Untagged";
            if (i < 20000) return new[] { "Untagged", "Respawn", "Finish", "EditorOnly", "", "MainCamera", "Player", "GameController" }.ElementAtOrDefault(i) ?? "Untagged";
            int k = i - 20000;
            return k >= 0 && k < EngineSettings.Tags.Length ? EngineSettings.Tags[k] : "Untagged";
        }

        static Component CreateComponent(AssetFile f, long pid, JObj e)
        {
            string type = (string)e["type"];
            Component c;
            if (type == "MonoBehaviour")
            {
                var script = e.ContainsKey("script") ? (string)e["script"] : null;
                var t = ScriptType(script);
                if (t == null || !typeof(Component).IsAssignableFrom(t))
                {
                    Count(UnknownTypes, "script:" + (script ?? "?"));
                    c = new UnknownComponent { unityType = "MonoBehaviour:" + script };
                }
                else
                {
                    c = (Component)CreateScriptInstance(t);
                    Count(Stats, "script-instances");
                }
            }
            else
            {
                var t = UnityType(type);
                if (t == null || t.IsAbstract || !typeof(Component).IsAssignableFrom(t))
                {
                    Count(UnknownTypes, "native:" + type);
                    c = new UnknownComponent { unityType = type };
                }
                else
                {
                    c = (Component)CreateScriptInstance(t);
                    Count(Stats, "native:" + type);
                }
            }
            c.EnsureId();
            return c;
        }

        static AnimationCurve CurveOf(object v) { return Serializer.Convert(v, typeof(AnimationCurve), null) as AnimationCurve; }
        // GUIDs come as {data[0..3]} uint32s
        static string GuidOf(object o)
        {
            var g = o as JObj; if (g == null) return "";
            return string.Format("{0:x8}{1:x8}{2:x8}{3:x8}", (uint)Json.L(g, "data[0]"), (uint)Json.L(g, "data[1]"), (uint)Json.L(g, "data[2]"), (uint)Json.L(g, "data[3]"));
        }
        static void LoadMixer(AssetFile f, UnityEngine.Audio.AudioMixer mx, JObj data)
        {
            var mc = data["m_MixerConstant"] as JObj; if (mc == null) return;
            var groups = (List<object>)mc["groups"];
            mx.m_GroupParent = new int[groups.Count]; mx.m_GroupVolSlot = new int[groups.Count];
            for (int i = 0; i < groups.Count; i++) { var g = (JObj)groups[i]; mx.m_GroupParent[i] = (int)Json.L(g, "parentConstantIndex"); mx.m_GroupVolSlot[i] = (int)Json.L(g, "volumeIndex"); }
            mx.m_GroupGuids = ((List<object>)mc["groupGUIDs"]).Select(GuidOf).ToArray();
            mx.m_ExposedHash = ((List<object>)mc["exposedParameterNames"]).Select(x => (uint)(double)x).ToArray();
            mx.m_ExposedSlot = ((List<object>)mc["exposedParameterIndices"]).Select(x => (int)(double)x).ToArray();
            var snaps = (List<object>)mc["snapshots"]; var sguids = ((List<object>)mc["snapshotGUIDs"]).Select(GuidOf).ToArray();
            mx.m_SnapValues = new Dictionary<string, float[]>();
            for (int i = 0; i < snaps.Count && i < sguids.Length; i++)
                mx.m_SnapValues[sguids[i]] = ((List<object>)((JObj)snaps[i])["values"]).Select(x => (float)(double)x).ToArray();
            var start = data.ContainsKey("m_StartSnapshot") ? Resolve(f, data["m_StartSnapshot"]) : null;
            mx.m_Values = snaps.Count > 0 ? ((List<object>)((JObj)snaps[0])["values"]).Select(x => (float)(double)x).ToArray() : new float[0];
            mx.m_OutputGroup = Resolve(f, data["m_OutputGroup"]) as UnityEngine.Audio.AudioMixerGroup;
            var sl = data["m_Snapshots"] as List<object>;
            if (sl != null) mx.m_Snapshots = sl.Select(x => Resolve(f, x) as UnityEngine.Audio.AudioMixerSnapshot).Where(x => x != null).ToArray();
            var st = start as UnityEngine.Audio.AudioMixerSnapshot;
            if (st != null && st.m_Values != null) mx.m_Values = (float[])st.m_Values.Clone();
        }

        static void ApplyComponentData(AssetFile f, Component c, JObj d)
        {
            var b = c as Behaviour;
            if (b != null && d.ContainsKey("m_Enabled")) b.m_Enabled = Json.B(d, "m_Enabled");

            var tr = c as Transform;
            if (tr != null)
            {
                tr.m_LocalPosition = Json.V3(d["m_LocalPosition"]);
                tr.m_LocalRotation = Json.Q(d["m_LocalRotation"]);
                tr.m_LocalScale = Json.V3(d["m_LocalScale"]);
                foreach (var ch in (List<object>)d["m_Children"])
                {
                    var ct = Resolve(f, ch) as Transform;
                    if (ct == null) continue;
                    ct.m_Parent = tr;
                    tr.m_Children.Add(ct);
                }
                var rt = c as RectTransform;
                if (rt != null)
                {
                    // raw fields: localPosition (already loaded) stays the source of truth
                    rt.m_AnchorMin = Json.V2(d["m_AnchorMin"]); rt.m_AnchorMax = Json.V2(d["m_AnchorMax"]);
                    rt.m_AnchoredPosition = Json.V2(d["m_AnchoredPosition"]); rt.m_SizeDelta = Json.V2(d["m_SizeDelta"]);
                    rt.m_Pivot = Json.V2(d["m_Pivot"]);
                }
                return;
            }
            var r = c as Renderer;
            if (r != null)
            {
                r.m_RendererEnabled = Json.B(d, "m_Enabled");
                r.m_SortingOrder = (int)Json.L(d, "m_SortingOrder");
                r.m_SortingLayerID = (int)Json.L(d, "m_SortingLayerID");
                var mats = d["m_Materials"] as List<object>;
                if (mats != null) r.m_Materials = mats.Select(m => Resolve(f, m) as Material).ToArray();
                var sr = c as SpriteRenderer;
                if (sr != null)
                {
                    sr.m_Sprite = Resolve(f, d["m_Sprite"]) as Sprite;
                    sr.m_Color = Json.C(d["m_Color"]);
                    sr.m_FlipX = Json.B(d, "m_FlipX");
                    sr.m_FlipY = Json.B(d, "m_FlipY");
                }
                return;
            }
            var rb = c as Rigidbody2D;
            if (rb != null)
            {
                rb.m_BodyType = (int)Json.L(d, "m_BodyType"); rb.m_Simulated = !d.ContainsKey("m_Simulated") || Json.B(d, "m_Simulated");
                rb.m_Mass = Json.F(d, "m_Mass", 1f); rb.m_Drag = Json.F(d, "m_LinearDrag"); rb.m_AngularDrag = Json.F(d, "m_AngularDrag", 0.05f);
                rb.m_GravityScale = Json.F(d, "m_GravityScale", 1f); rb.m_Constraints = (RigidbodyConstraints2D)(int)Json.L(d, "m_Constraints");
                return;
            }
            var col = c as Collider2D;
            if (col != null)
            {
                col.m_IsTrigger = Json.B(d, "m_IsTrigger"); col.m_Offset = Json.V2(d["m_Offset"]);
                col.m_Material = Resolve(f, d["m_Material"]) as PhysicsMaterial2D;
                var bx = c as BoxCollider2D; if (bx != null) bx.m_Size = Json.V2(d["m_Size"]);
                var ci = c as CircleCollider2D; if (ci != null) ci.m_Radius = Json.F(d, "m_Radius", 0.5f);
                var pc = c as PolygonCollider2D;
                if (pc != null)
                {
                    pc.m_Paths.Clear();
                    foreach (List<object> path in (List<object>)((JObj)d["m_Points"])["m_Paths"]) pc.m_Paths.Add(path.Select(Json.V2).ToArray());
                }
                var ec = c as EdgeCollider2D; if (ec != null) ec.m_Points = ((List<object>)d["m_Points"]).Select(Json.V2).ToArray();
                return;
            }
            var tj = c as TargetJoint2D;
            if (tj != null)
            {
                tj.m_Anchor = Json.V2(d["m_Anchor"]); tj.m_Target = Json.V2(d["m_Target"]);
                tj.m_MaxForce = Json.F(d, "m_MaxForce", 1000f); tj.m_Frequency = Json.F(d, "m_Frequency", 5f); tj.m_Damping = Json.F(d, "m_DampingRatio", 1f);
                return;
            }
            var au = c as AudioSource;
            if (au != null)
            {
                au.m_Clip = Resolve(f, d["m_audioClip"]) as AudioClip; au.m_PlayOnAwake = Json.B(d, "m_PlayOnAwake");
                au.m_Volume = Json.F(d, "m_Volume", 1f); au.m_Pitch = Json.F(d, "m_Pitch", 1f); au.m_Loop = Json.B(d, "Loop"); au.m_Mute = Json.B(d, "Mute");
                au.m_MaxDistance = Json.F(d, "MaxDistance", 500f); au.m_MinDistance = Json.F(d, "MinDistance", 1f);
                au.m_Pan2D = Json.F(d, "Pan2D", 0f); au.m_Rolloff = (int)Json.L(d, "rolloffMode");
                au.m_Output = Resolve(f, d["OutputAudioMixerGroup"]) as UnityEngine.Audio.AudioMixerGroup;
                var pl = d.ContainsKey("panLevelCustomCurve") ? CurveOf(d["panLevelCustomCurve"]) : null;
                au.m_SpatialBlend = pl != null ? Mathf.Clamp01(pl.Evaluate(0f)) : 0f;
                au.m_RolloffCurve = d.ContainsKey("rolloffCustomCurve") ? CurveOf(d["rolloffCustomCurve"]) : null;
                return;
            }
            var an = c as Animator;
            if (an != null)
            {
                an.m_Controller = Resolve(f, d["m_Controller"]) as RuntimeAnimatorController;
                an.m_CullingMode = (AnimatorCullingMode)(int)Json.L(d, "m_CullingMode");
                an.m_UpdateMode = (AnimatorUpdateMode)(int)Json.L(d, "m_UpdateMode");
                return;
            }
            var mf = c as MeshFilter;
            if (mf != null) { mf.m_Mesh = Resolve(f, d["m_Mesh"]) as Mesh; return; }
            var cv = c as Canvas;
            if (cv != null)
            {
                cv.m_RenderMode = (RenderMode)(int)Json.L(d, "m_RenderMode"); cv.m_Camera = Resolve(f, d["m_Camera"]) as Camera;
                cv.m_SortingOrder = (int)Json.L(d, "m_SortingOrder"); cv.m_OverrideSorting = Json.B(d, "m_OverrideSorting");
                cv.m_PixelPerfect = Json.B(d, "m_PixelPerfect");
                return;
            }
            var cg = c as CanvasGroup;
            if (cg != null)
            {
                cg.m_Alpha = Json.F(d, "m_Alpha", 1f); cg.m_Interactable = Json.B(d, "m_Interactable");
                cg.m_Blocks = Json.B(d, "m_BlocksRaycasts"); cg.m_Ignore = Json.B(d, "m_IgnoreParentGroups");
                return;
            }
            var li = c as Light;
            if (li != null)
            {
                li.m_Type = (LightType)(int)Json.L(d, "m_Type"); li.m_Color = Json.C(d["m_Color"]);
                li.m_Intensity = Json.F(d, "m_Intensity", 1f); li.m_Range = Json.F(d, "m_Range", 10f);
                return;
            }
            var cam = c as Camera;
            if (cam != null)
            {
                cam.m_Orthographic = Json.B(d, "orthographic");
                cam.m_OrthographicSize = Json.F(d, "orthographic size", 5f);
                cam.m_BackgroundColor = Json.C(d["m_BackGroundColor"]);
                cam.m_Depth = Json.F(d, "m_Depth");
                cam.nearClipPlane = Json.F(d, "near clip plane");
                cam.farClipPlane = Json.F(d, "far clip plane");
                cam.m_TargetTexture = Resolve(f, d["m_TargetTexture"]) as RenderTexture;
                return;
            }
            if (c is MonoBehaviour) { Serializer.Populate(c, d, f); return; }
        }

        // ------------------------------------------------------------- scenes
        public static SceneTag LoadScene(string fileName) { return LoadScene(fileName, fileName); }
        public static SceneTag LoadScene(string fileName, string displayName)
        {
            var f = File(fileName);
            var tag = new SceneTag(fileName) { displayName = displayName };
            if (f.hierarchyBuilt)
            {   // reloading a scene: rebuild from data with fresh objects
                f.instances.Clear(); f.hierarchyBuilt = false;
            }
            BuildHierarchy(f, tag);
            return tag;
        }

        public static IEnumerable<GameObject> Roots()
        {
            foreach (var g in SceneObjects)
                if (!g.m_Destroyed && g.m_Transform != null && g.m_Transform.m_Parent == null) yield return g;
        }

        // ------------------------------------------------------- object API
        public static GameObject FindGameObject(string name)
        {
            if (name.Contains("/"))
            {
                var parts = name.TrimStart('/').Split(new[] { '/' }, 2);
                foreach (var r in Roots()) if (r.name == parts[0] && r.activeInHierarchy)
                {
                    if (parts.Length == 1 || parts[1].Trim('/').Length == 0) return r;
                    var t = r.transform.Find(parts[1]); return t != null ? t.gameObject : null;
                }
                return null;
            }
            foreach (var g in SceneObjects) if (!g.m_Destroyed && g.name == name && g.activeInHierarchy) return g;
            return null;
        }
        public static GameObject FindGameObjectWithTag(string tag)
        {
            foreach (var g in SceneObjects) if (!g.m_Destroyed && g.m_Tag == tag && g.activeInHierarchy) return g;
            return null;
        }
        public static GameObject[] FindGameObjectsWithTag(string tag)
        {
            var r = SceneObjects.Where(g => !g.m_Destroyed && g.m_Tag == tag && g.activeInHierarchy).ToArray();
            if (r.Length == 0 && Environment.GetEnvironmentVariable("KNL_DEBUG_TAGS") != null)
                foreach (var g in SceneObjects.Where(g => g.m_Tag == tag))
                    Console.Error.WriteLine("[tag " + tag + "] " + g.name + " destroyed=" + g.m_Destroyed + " self=" + g.m_IsActive + " hier=" + g.activeInHierarchy + " scene=" + (g.m_Scene != null ? g.m_Scene.displayName : "null") + " path=" + PathOf(g));
            return r;
        }
        static string PathOf(GameObject g) { var s = g.name; for (var t = g.m_Transform.m_Parent; t != null; t = t.m_Parent) s = t.name + (t.m_GameObject.m_IsActive ? "" : "[off]") + "/" + s; return s; }

        public static List<UnityEngine.Object> FindObjectsOfType(Type t)
        {
            var l = new List<UnityEngine.Object>();
            foreach (var g in SceneObjects)
            {
                if (g.m_Destroyed || !g.activeInHierarchy) continue;
                if (t.IsInstanceOfType(g)) l.Add(g);
                foreach (var c in g.m_Components) if (t.IsInstanceOfType(c)) l.Add(c);
            }
            return l;
        }

        /// Everything loaded: scene objects (active or not) first, then already-created assets.
        public static List<UnityEngine.Object> FindAllObjects(Type t)
        {
            var l = new List<UnityEngine.Object>();
            var seen = new HashSet<UnityEngine.Object>(ReferenceEqualityComparer.Instance);
            foreach (var g in SceneObjects)
            {
                if (g.m_Destroyed) continue;
                if (t.IsInstanceOfType(g) && seen.Add(g)) l.Add(g);
                foreach (var c in g.m_Components) if (t.IsInstanceOfType(c) && seen.Add(c)) l.Add(c);
            }
            foreach (var f in files.Values)
                foreach (var o in f.instances.Values)
                {
                    if (o == null || o.m_Destroyed || !t.IsInstanceOfType(o) || !seen.Add(o)) continue;
                    var go = o as GameObject ?? (o is Component ? ((Component)o).gameObject : null);
                    if (go != null && go.m_Scene != null) continue;   // scene object already handled
                    l.Add(o);
                }
            return l;
        }

        public static void AttachNewGameObject(GameObject go)
        {
            go.m_Scene = ActiveScene;
            var tr = new Transform(); tr.m_GameObject = go; tr.name = go.name;
            go.m_Transform = tr; go.m_Components.Add(tr);
            SceneObjects.Add(go);
        }
        public static SceneTag ActiveScene;

        public static Component AddComponent(GameObject go, Type t)
        {
            var c = (Component)CreateScriptInstance(t);
            c.m_GameObject = go; c.name = go.name;
            go.m_Components.Add(c);
            go.m_PhysicsMaskValid = false;
            if (go.m_Scene != null) RegisterSceneComponent(c);
            Lifecycle.OnComponentAdded(c);
            return c;
        }

        public static void RegisterSceneComponent(Component c)
        {
            if (c == null) return;
            var r = c as Renderer; if (r != null) { Renderer.s_All.Add(r); return; }
            var a = c as Animator; if (a != null) { Animator.s_All.Add(a); return; }
            var col = c as Collider2D; if (col != null) { Collider2D.s_All.Add(col); return; }
            var rb = c as Rigidbody2D; if (rb != null) { Rigidbody2D.s_All.Add(rb); return; }
            var cam = c as Camera; if (cam != null) { Camera.s_All.Add(cam); return; }
            var cv = c as Canvas; if (cv != null) { Canvas.s_All.Add(cv); return; }
            var al = c as AudioListener; if (al != null) { AudioListener.s_All.Add(al); return; }
            if (EdgeIndicators.IsEnemy(c)) EdgeIndicators.s_Enemies.Add(c);
        }

        public static void ScheduleDestroy(UnityEngine.Object o, float t)
        {
            if (o == null) return;
            if (t <= 0f) pendingDestroy.Add(o);
            else Timed.Add(Time.time + t, () => pendingDestroy.Add(o));
        }
        public static void DestroyNow(UnityEngine.Object o)
        {
            if (ReferenceEquals(o, null) || o.m_Destroyed) return;
            var go = o as GameObject;
            if (go != null)
            {
                foreach (var ch in go.m_Transform.m_Children.ToArray()) DestroyNow(ch.m_GameObject);
                foreach (var c in go.m_Components.ToArray())
                {
                    var mb = c as MonoBehaviour;
                    if (mb != null) Lifecycle.OnDestroyed(mb);
                    c.m_Destroyed = true;
                }
                if (go.m_Transform.m_Parent != null) go.m_Transform.m_Parent.m_Children.Remove(go.m_Transform);
                Physics2DSystem.OnDestroyed(go);
            }
            var comp = o as Component;
            if (comp != null && comp.m_GameObject != null)
            {
                var mb = comp as MonoBehaviour;
                if (mb != null) Lifecycle.OnDestroyed(mb);
                comp.m_GameObject.m_Components.Remove(comp);
            }
            o.m_Destroyed = true;
        }
        public static void FlushDestroyed()
        {
            Timed.Tick();
            foreach (var o in pendingDestroy.ToArray()) DestroyNow(o);
            pendingDestroy.Clear();
            SceneObjects.RemoveAll(g => g.m_Destroyed);
            Renderer.s_All.RemoveAll(c => c == null || c.m_Destroyed || c.m_GameObject == null || c.m_GameObject.m_Destroyed);
            Animator.s_All.RemoveAll(c => c == null || c.m_Destroyed || c.m_GameObject == null || c.m_GameObject.m_Destroyed);
            Collider2D.s_All.RemoveAll(c => c == null || c.m_Destroyed || c.m_GameObject == null || c.m_GameObject.m_Destroyed);
            Rigidbody2D.s_All.RemoveAll(c => c == null || c.m_Destroyed || c.m_GameObject == null || c.m_GameObject.m_Destroyed);
            Camera.s_All.RemoveAll(c => c == null || c.m_Destroyed || c.m_GameObject == null || c.m_GameObject.m_Destroyed);
            Canvas.s_All.RemoveAll(c => c == null || c.m_Destroyed || c.m_GameObject == null || c.m_GameObject.m_Destroyed);
            AudioListener.s_All.RemoveAll(c => c == null || c.m_Destroyed || c.m_GameObject == null || c.m_GameObject.m_Destroyed);
            EdgeIndicators.s_Enemies.RemoveAll(c => c == null || c.m_Destroyed || c.m_GameObject == null || c.m_GameObject.m_Destroyed);
        }
        public static SceneTag PersistentScene = new SceneTag("DontDestroyOnLoad") { displayName = "DontDestroyOnLoad", persistent = true, buildIndex = -1 };
        public static void MarkPersistent(UnityEngine.Object o)
        {
            var go = o as GameObject ?? (o is Component ? ((Component)o).gameObject : null);
            if (go == null) return;
            var root = go.transform.root.gameObject;
            foreach (var g in SceneObjects) if (g.transform.root == root.transform) g.m_Scene = PersistentScene;
        }
        public static bool IsPersistent(GameObject g) { return g.m_Scene != null && g.m_Scene.persistent; }
        public static void OnActiveChanged(GameObject go) { Lifecycle.OnActiveChanged(go); }
        public static void OnBehaviourEnabledChanged(Behaviour b) { Lifecycle.OnBehaviourEnabledChanged(b); }
        public static void OnParentChanged(Transform t) { }
    }

    /// Simple timed callbacks (Destroy(obj, t)).
    public static class Timed
    {
        static readonly List<KeyValuePair<float, Action>> list = new List<KeyValuePair<float, Action>>();
        public static void Add(float at, Action a) { list.Add(new KeyValuePair<float, Action>(at, a)); }
        public static void Tick()
        {
            if (list.Count == 0) return;
            var due = list.Where(kv => kv.Key <= Time.time).ToList();
            list.RemoveAll(kv => kv.Key <= Time.time);
            foreach (var kv in due) kv.Value();
        }
    }

    // ------------------------------------------------------------- serializer
    /// Applies a type-tree dict (from convert.py) to a script instance's fields, following
    /// Unity's serialization rules: keys are field names; PPtrs resolve to engine objects.
    public static class Serializer
    {
        const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        static readonly HashSet<string> Header = new HashSet<string> { "m_GameObject", "m_Enabled", "m_Script", "m_Name", "m_EditorHideFlags", "m_EditorClassIdentifier", "m_ObjectHideFlags" };
        public static int FieldsSet, FieldsMissing, FieldErrors;
        public static readonly Dictionary<string, int> Missing = new Dictionary<string, int>();

        static readonly Dictionary<Type, Dictionary<string, FieldInfo>> fieldCache = new Dictionary<Type, Dictionary<string, FieldInfo>>();
        static Dictionary<string, FieldInfo> Fields(Type t)
        {
            Dictionary<string, FieldInfo> d;
            if (fieldCache.TryGetValue(t, out d)) return d;
            d = new Dictionary<string, FieldInfo>();
            for (var tt = t; tt != null && tt != typeof(object); tt = tt.BaseType)
                foreach (var fi in tt.GetFields(F))
                    if (!d.ContainsKey(fi.Name) && !fi.IsStatic && !fi.IsLiteral) d[fi.Name] = fi;
            fieldCache[t] = d;
            return d;
        }

        public static void Populate(object target, JObj data, AssetFile ctx)
        {
            var fields = Fields(target.GetType());
            foreach (var kv in data)
            {
                if (Header.Contains(kv.Key)) continue;
                FieldInfo fi;
                if (!fields.TryGetValue(kv.Key, out fi))
                {
                    FieldsMissing++;
                    int n; Missing.TryGetValue(target.GetType().Name + "." + kv.Key, out n); Missing[target.GetType().Name + "." + kv.Key] = n + 1;
                    continue;
                }
                try
                {
                    var v = Convert(kv.Value, fi.FieldType, ctx);
                    if (v != Skip) { fi.SetValue(target, v); FieldsSet++; }
                }
                catch (Exception e)
                {
                    FieldErrors++;
                    if (FieldErrors < 20) Log.Write("WARN", "field " + target.GetType().Name + "." + fi.Name + ": " + e.Message);
                }
            }
            var so = target as UnityEngine.Object;
            if (so != null && target is ScriptableObject && data.ContainsKey("m_Name")) so.name = (string)data["m_Name"];
        }

        static readonly object Skip = new object();

        public static object Convert(object v, Type t, AssetFile ctx)
        {
            if (typeof(UnityEngine.Object).IsAssignableFrom(t))
            {
                var o = World.Resolve(ctx, v);
                if (o == null) return null;
                if (t.IsInstanceOfType(o)) return o;
                // a GameObject reference stored where a component type is expected, or vice versa
                var go = o as GameObject;
                if (go != null && typeof(Component).IsAssignableFrom(t)) return go.GetComponent(t);
                return null;
            }
            if (v == null) return t.IsValueType ? Activator.CreateInstance(t) : null;
            if (t == typeof(string)) return v as string ?? v.ToString();
            if (t.IsEnum) return Enum.ToObject(t, System.Convert.ToInt64(v is bool ? ((bool)v ? 1 : 0) : v));
            if (t.IsPrimitive)
            {
                if (v is bool) v = (bool)v ? 1.0 : 0.0;
                if (t == typeof(bool)) return (double)v != 0;
                if (t == typeof(char)) return (char)(int)(double)v;
                return System.Convert.ChangeType(v, t, System.Globalization.CultureInfo.InvariantCulture);
            }
            if (t.IsArray)
            {
                var l = v as List<object>;
                if (l == null) return Skip;
                var et = t.GetElementType();
                var arr = Array.CreateInstance(et, l.Count);
                for (int i = 0; i < l.Count; i++) { var x = Convert(l[i], et, ctx); if (x != Skip) arr.SetValue(x, i); }
                return arr;
            }
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
            {
                var l = v as List<object>;
                if (l == null) return Skip;
                var et = t.GetGenericArguments()[0];
                var list = (IList)Activator.CreateInstance(t);
                foreach (var x in l) { var c = Convert(x, et, ctx); list.Add(c == Skip ? null : c); }
                return list;
            }
            var d = v as JObj;
            if (d == null) return Skip;
            // Unity built-in value types whose serialized names differ from our fields
            if (t == typeof(Color32))
            {
                uint rgba = (uint)(double)d["rgba"];
                return new Color32((byte)(rgba & 255), (byte)((rgba >> 8) & 255), (byte)((rgba >> 16) & 255), (byte)(rgba >> 24));
            }
            if (t == typeof(LayerMask)) return (LayerMask)(int)(uint)(double)d["m_Bits"];
            if (t == typeof(Rect)) return new Rect(Json.F(d, "x"), Json.F(d, "y"), Json.F(d, "width"), Json.F(d, "height"));
            if (t == typeof(Bounds)) return new Bounds(Json.V3(d["m_Center"]), Json.V3(d["m_Extent"]) * 2f);
            if (t == typeof(AnimationCurve))
            {
                var keys = new List<Keyframe>();
                foreach (JObj k in (List<object>)d["m_Curve"])
                {
                    var kf = new Keyframe(Json.F(k, "time"), Json.F(k, "value"), Json.F(k, "inSlope"), Json.F(k, "outSlope"));
                    kf.m_TangentMode = (int)Json.L(k, "tangentMode");
                    keys.Add(kf);
                }
                var ac = new AnimationCurve();
                ac.SetFromSerialized(keys.ToArray(), (int)Json.L(d, "m_PreInfinity"), (int)Json.L(d, "m_PostInfinity"));
                return ac;
            }
            if (t == typeof(Gradient))
            {
                int nc = (int)Json.L(d, "m_NumColorKeys"), na = (int)Json.L(d, "m_NumAlphaKeys");
                var ck = new GradientColorKey[Math.Max(nc, 1)]; var ak = new GradientAlphaKey[Math.Max(na, 1)];
                for (int i = 0; i < nc; i++) { var c = Json.C(d["key" + i]); ck[i] = new GradientColorKey(c, Json.L(d, "ctime" + i) / 65535f); }
                for (int i = 0; i < na; i++) { var c = Json.C(d["key" + i]); ak[i] = new GradientAlphaKey(c.a, Json.L(d, "atime" + i) / 65535f); }
                if (nc == 0) ck[0] = new GradientColorKey(Color.white, 0); if (na == 0) ak[0] = new GradientAlphaKey(1, 0);
                var gr = new Gradient(); gr.SetKeys(ck, ak); gr.m_Mode = (int)Json.L(d, "m_Mode");
                return gr;
            }
            object inst;
            if (t.IsValueType) inst = Activator.CreateInstance(t);
            else
            {
                try { inst = Activator.CreateInstance(t, true); }
                catch { inst = FormatterServices.GetUninitializedObject(t); }
            }
            Populate(inst, d, ctx);
            return inst;
        }
    }
}
