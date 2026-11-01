// KNL render system: collects visible renderers, sorts them Unity-style and draws them through
// the native core (knlcore) into a 320x240 framebuffer (see docs/DECISIONS.md D2).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using JObj = System.Collections.Generic.Dictionary<string, object>;

namespace KNL
{
    public static class Native
    {
        const string Lib = "knlcore";
        [DllImport(Lib)] public static extern int knl_init(int w, int h, int scale, int window);
        [DllImport(Lib)] public static extern int knl_has_window();
        [DllImport(Lib)] public static extern void knl_shutdown();
        [DllImport(Lib)] public static extern int knl_tex_load_png(string path, out int w, out int h);
        [DllImport(Lib)] public static extern void knl_clear(uint rgba);
        [DllImport(Lib)] public static extern void knl_draw(int tex, float a, float b, float c, float d, float tx, float ty, uint tint, int mode);
        [DllImport(Lib)] public static extern int knl_tex_from_pack_indexed(long off, int w, int h, int npal);
        [DllImport(Lib)] public static extern int knl_tex_from_pack_rgba(long off, int w, int h);
        [DllImport(Lib)] public static extern long knl_tex_bytes();
        [DllImport(Lib)] public static extern void knl_draw_quad(int tex, float x0, float y0, float x1, float y1, float x3, float y3, float u0, float v0, float u1, float v1, float u3, float v3, uint rgba, int flags, int cx0, int cy0, int cx1, int cy1);
        [DllImport(Lib)] public static extern void knl_draw_lit(int tex, float a, float b, float c, float d, float tx, float ty, uint tint, int[] mul12, int[] add3, uint recA, uint recB, int flags);
        [DllImport(Lib)] public static extern void knl_quad_gradient(float x0, float y0, float x1, float y1, uint c00, uint c10, uint c01, uint c11, int mode);
        [DllImport(Lib)] public static extern void knl_stats(out int sprites, out int pixels);
        [DllImport(Lib)] public static extern void knl_reflect(int y0, int y1, float scale, uint tint, float t, float amp);
        [DllImport(Lib)] public static extern int knl_save_png(string path, int scale);
        [DllImport(Lib)] public static extern int knl_present();
        [DllImport(Lib)] public static extern float knl_raster_ms();
        [DllImport(Lib)] public static extern float knl_draw_ms();
        [DllImport(Lib)] public static extern float knl_flip_ms();
        [DllImport(Lib)] public static extern uint knl_buttons();
        [DllImport(Lib)] public static extern uint knl_ticks_ms();
    }

    /// Per-sprite pixel data exported by convert.py (cut out of its atlas, mesh-masked).
    public sealed class SpriteData
    {
        public Rect rect; public Vector2 pivot; public float ppu = 32f;
        public string img; public int imgW, imgH, offX, offY;
        public Texture2D atlas;
        public string name;
        int tex = -2;
        long packOff = -1; int packFmt, packPal;        // device pack: absolute offset of palette/pixels

        static Dictionary<string, SpriteData> index;
        public static SpriteData For(string file, long pid)
        {
            if (index == null) LoadIndex();
            SpriteData s;
            return index.TryGetValue(file + ":" + pid, out s) ? s : null;
        }
        static void LoadIndex()
        {
            index = new Dictionary<string, SpriteData>();
            if (Pack.Active) { LoadPackIndex(); return; }
            var list = (List<object>)Json.ParseFile(Path.Combine(Paths.DataDir, "sprites.json"));
            foreach (JObj e in list)
            {
                if (!e.ContainsKey("img")) continue;
                var r = (JObj)e["rect"]; var sz = (List<object>)e["img_size"]; var off = (List<object>)e["img_offset"];
                var s = new SpriteData
                {
                    rect = new Rect(Json.F(r, "x"), Json.F(r, "y"), Json.F(r, "width"), Json.F(r, "height")),
                    pivot = Json.V2(e["pivot"]), ppu = Json.F(e, "ppu", 32f), img = (string)e["img"],
                    imgW = (int)(double)sz[0], imgH = (int)(double)sz[1], offX = (int)(double)off[0], offY = (int)(double)off[1],
                };
                index[(string)e["file"] + ":" + (long)(double)e["path_id"]] = s;
            }
        }
        // pack entry "sprites": records of file, pid, name, rect, pivot, ppu, size, offset, fmt, palette, pixels
        static void LoadPackIndex()
        {
            long baseOff; int len;
            if (!Pack.Locate("sprites", out baseOff, out len)) return;
            var b = Pack.Read("sprites");
            int p = 12, n = BitConverter.ToInt32(b, 8);
            Func<string> str = () => { int l = 0, sh = 0; byte x; do { x = b[p++]; l |= (x & 0x7F) << sh; sh += 7; } while ((x & 0x80) != 0); var r = System.Text.Encoding.UTF8.GetString(b, p, l); p += l; return r; };
            for (int i = 0; i < n; i++)
            {
                string file = str(); long pid = BitConverter.ToInt64(b, p); p += 8; string nm = str();
                var s = new SpriteData { name = nm };
                s.rect = new Rect(BitConverter.ToSingle(b, p), BitConverter.ToSingle(b, p + 4), BitConverter.ToSingle(b, p + 8), BitConverter.ToSingle(b, p + 12));
                s.pivot = new Vector2(BitConverter.ToSingle(b, p + 16), BitConverter.ToSingle(b, p + 20)); s.ppu = BitConverter.ToSingle(b, p + 24);
                s.imgW = BitConverter.ToInt32(b, p + 28); s.imgH = BitConverter.ToInt32(b, p + 32); s.offX = BitConverter.ToInt32(b, p + 36); s.offY = BitConverter.ToInt32(b, p + 40);
                p += 44;
                s.packFmt = b[p]; s.packPal = BitConverter.ToUInt16(b, p + 1); p += 3;
                s.packOff = baseOff + p;
                p += s.packFmt == 0 ? s.packPal * 4 + s.imgW * s.imgH : s.imgW * s.imgH * 4;
                index[file + ":" + pid] = s;
            }
        }
        public int Texture
        {
            get
            {
                if (tex == -2)
                {
                    int w, h;
                    if (packOff >= 0)
                        tex = packFmt == 0 ? Native.knl_tex_from_pack_indexed(packOff, imgW, imgH, packPal) : Native.knl_tex_from_pack_rgba(packOff, imgW, imgH);
                    else
                        tex = Native.knl_tex_load_png(Path.Combine(Paths.DataDir, img), out w, out h);
                }
                return tex;
            }
        }
    }

    public static class RenderSystem
    {
        public static int Width = 320, Height = 240;
        public static float OrthoSizeOverride = 3.75f;      // D2: 240 px / 32 ppu / 2
        public static Camera MainCamera;
        public static Vector3? CameraOverride;               // dev/debug: force camera position
        public static Color ClearColor = new Color(0.36f, 0.42f, 0.55f, 1f);
        public static int LastSprites, LastPixels, LastCandidates;

        struct Item { public SpriteRenderer sr; public MeshRenderer mr; public Matrix4x4 m; public float dist; public int order; public int id; }
        static readonly List<Item> items = new List<Item>();
        // sort keys kept apart from the 100-byte Items so the sort moves 16-byte structs (M5); the comparison
        // is a total order (id breaks ties), so the draw order is the same as sorting the Items themselves
        struct Key { public int order; public float dist; public int id, idx; }
        sealed class KeyOrder : IComparer<Key>
        {
            public int Compare(Key a, Key b)
            {
                if (a.order != b.order) return a.order.CompareTo(b.order);
                int d = b.dist.CompareTo(a.dist);
                return d != 0 ? d : a.id.CompareTo(b.id);
            }
        }
        static readonly KeyOrder keyOrder = new KeyOrder();
        static Key[] keys = new Key[256];

        public static void Init(bool window)
        {
            int rc = Native.knl_init(Width, Height, 2, window ? 1 : 0);
            if (rc != 0) throw new Exception("knl_init failed: " + rc);
        }

        /// Unity's Camera.main: first enabled camera on an active GameObject tagged "MainCamera".
        static Camera s_TaggedCache; static int s_TaggedFrame = -1;
        public static Camera TaggedMainCamera()
        {
            if (s_TaggedFrame == Time.frameCount && s_TaggedCache != null && !s_TaggedCache.m_Destroyed) return s_TaggedCache;
            s_TaggedCache = null;
            var cams = Camera.s_All;
            int count = cams.Count;
            for (int i = 0; i < count; i++)
            {
                var c = cams[i];
                if (c != null && !c.m_Destroyed && c.m_Enabled && c.m_GameObject != null && !c.m_GameObject.m_Destroyed && c.m_GameObject.m_Tag == "MainCamera" && c.m_GameObject.activeInHierarchy)
                { s_TaggedCache = c; break; }
            }
            s_TaggedFrame = Time.frameCount;
            return s_TaggedCache;
        }

        public static Camera FindMainCamera()
        {
            var cams = Camera.s_All;
            int count = cams.Count;
            for (int i = 0; i < count; i++)
            {
                var c = cams[i];
                if (c != null && !c.m_Destroyed && c.m_GameObject != null && !c.m_GameObject.m_Destroyed && c.m_GameObject.activeInHierarchy && c.m_GameObject.m_Tag == "MainCamera")
                    return c;
            }
            for (int i = 0; i < count; i++)
            {
                var c = cams[i];
                if (c != null && !c.m_Destroyed && c.m_GameObject != null && !c.m_GameObject.m_Destroyed && c.m_GameObject.activeInHierarchy && c.orthographic)
                    return c;
            }
            return null;
        }

        static Camera s_InterfaceCameraCache;
        static int s_InterfaceCameraFrame = -1;
        public static Camera InterfaceCamera
        {
            get
            {
                if (s_InterfaceCameraFrame == Time.frameCount) return s_InterfaceCameraCache;
                s_InterfaceCameraCache = null;
                var cams = Camera.s_All;
                int count = cams.Count;
                for (int i = 0; i < count; i++)
                {
                    var c = cams[i];
                    if (c != null && !c.m_Destroyed && c.m_Enabled && c.m_GameObject != null && !c.m_GameObject.m_Destroyed && c.m_GameObject.activeInHierarchy &&
                        (c.m_GameObject.m_Tag == "InterfaceCamera" || c.m_GameObject.name == "Interface Camera"))
                    {
                        s_InterfaceCameraCache = c;
                        break;
                    }
                }
                s_InterfaceCameraFrame = Time.frameCount;
                return s_InterfaceCameraCache;
            }
        }

        static Vector3 CamPos()
        {
            if (CameraOverride.HasValue) return CameraOverride.Value;
            return MainCamera != null ? MainCamera.transform.position : new Vector3(0, 0, -10);
        }

        static float PixelsPerUnit { get { return Height / (2f * OrthoSizeOverride); } }

        public static Vector3 WorldToScreen(Camera c, Vector3 p)
        {
            var cp = CamPos(); float k = PixelsPerUnit;
            return new Vector3((p.x - cp.x) * k + Width / 2f, (p.y - cp.y) * k + Height / 2f, p.z - cp.z);
        }
        public static Vector3 ScreenToWorld(Camera c, Vector3 s)
        {
            var cp = CamPos(); float k = PixelsPerUnit;
            return new Vector3((s.x - Width / 2f) / k + cp.x, (s.y - Height / 2f) / k + cp.y, s.z + cp.z);
        }

        public static Bounds BoundsOf(Renderer r)
        {
            var sr = r as SpriteRenderer;
            if (sr == null || sr.m_Sprite == null) return new Bounds(r.transform.position, Vector3.zero);
            var b = sr.m_Sprite.bounds; var m = r.transform.localToWorldMatrix;
            var res = new Bounds(m.MultiplyPoint3x4(b.min), Vector3.zero);
            res.Encapsulate(m.MultiplyPoint3x4(b.max));
            res.Encapsulate(m.MultiplyPoint3x4(new Vector3(b.min.x, b.max.y, 0)));
            res.Encapsulate(m.MultiplyPoint3x4(new Vector3(b.max.x, b.min.y, 0)));
            return res;
        }

        static readonly List<Item> s_UiItems = new List<Item>();
        static Key[] s_UiKeys = new Key[64];

        public static void RenderFrame()
        {
            long pt = Prof.Now();
            UIRender.Prepare();
            pt = Prof.Add("render: UI layout", pt);
            var cp = CamPos();
            float k = PixelsPerUnit;
            float halfW = Width / 2f / k + 4f, halfH = Height / 2f / k + 4f;   // generous cull margin (world units)
            Native.knl_clear(MainCamera != null && !CameraOverride.HasValue ? MainCamera.m_BackgroundColor.ToRGBA32() | 0xFF000000u : ClearColor.ToRGBA32());

            var ic = InterfaceCamera;
            bool hasIc = ic != null && ic.m_Enabled && ic.m_GameObject != null && ic.m_GameObject.activeInHierarchy;
            Vector3 icPos = hasIc ? ic.transform.position : Vector3.zero;
            float icHalfW = Width / 2f / k + 2f, icHalfH = Height / 2f / k + 2f;
            if (hasIc) s_UiItems.Clear();

            items.Clear();
            LastCandidates = 0;
            var all = Renderer.s_All;
            int count = all.Count;
            for (int i = 0; i < count; i++)
            {
                var r = all[i];
                if (r == null || r.m_Destroyed || !r.m_RendererEnabled) continue;
                var g = r.m_GameObject;
                if (g == null || g.m_Destroyed || !g.activeInHierarchy) continue;

                var sr = r as SpriteRenderer;
                if (sr != null)
                {
                    if (sr.m_Sprite == null || sr.m_Sprite.data == null) continue;
                    LastCandidates++;
                    var tr = sr.transform;
                    if (tr.m_Parent == null)
                    {
                        float px = tr.m_LocalPosition.x, py = tr.m_LocalPosition.y;
                        bool nearMain = Mathf.Abs(px - cp.x) <= halfW + 8f && Mathf.Abs(py - cp.y) <= halfH + 8f;
                        bool nearIc = hasIc && Mathf.Abs(px - icPos.x) <= icHalfW && Mathf.Abs(py - icPos.y) <= icHalfH;
                        if (!nearMain && !nearIc) continue;
                    }
                    var m = tr.localToWorldMatrix;
                    float wx = m.m03, wy = m.m13;
                    if (Mathf.Abs(wx - cp.x) <= halfW + 8f && Mathf.Abs(wy - cp.y) <= halfH + 8f)
                    {
                        items.Add(new Item { sr = sr, m = m, dist = m.m23 - cp.z, order = sr.m_SortingOrder, id = sr.m_InstanceID });
                    }
                    else if (hasIc && Mathf.Abs(wx - icPos.x) <= icHalfW && Mathf.Abs(wy - icPos.y) <= icHalfH)
                    {
                        s_UiItems.Add(new Item { sr = sr, m = m, dist = m.m23 - icPos.z, order = sr.m_SortingOrder, id = sr.m_InstanceID });
                    }
                    continue;
                }

                var mr = r as MeshRenderer;
                if (mr != null)
                {
                    var mm = mr.transform.localToWorldMatrix;
                    items.Add(new Item { mr = mr, m = mm, dist = mm.m23 - cp.z, order = mr.m_SortingOrder, id = mr.m_InstanceID });
                }
            }
            pt = Prof.Add("render: collect + cull", pt);
            // Unity transparent queue: sorting layer (single 'Default' here), order in layer, then
            // back-to-front by distance along the camera axis.
            int n = items.Count;
            if (keys.Length < n) keys = new Key[n * 2];
            for (int i = 0; i < n; i++) { var it = items[i]; keys[i] = new Key { order = it.order, dist = it.dist, id = it.id, idx = i }; }
            Array.Sort(keys, 0, n, keyOrder);
            PrepareLights(cp, halfW);
            pt = Prof.Add("render: sort", pt);
            for (int i = 0; i < n; i++)
            {
                var it = items[keys[i].idx];
                if (it.mr != null) { DrawMesh(it.mr, it.m, cp, k); pt = Prof.Add("render: meshes", pt); }
                else { DrawSprite(it.sr, it.m, cp, k); pt = Prof.Add("render: sprites", pt); }
            }
            if (hasIc && s_UiItems.Count > 0)
            {
                int uin = s_UiItems.Count;
                if (s_UiKeys.Length < uin) s_UiKeys = new Key[uin * 2];
                for (int i = 0; i < uin; i++) { var it = s_UiItems[i]; s_UiKeys[i] = new Key { order = it.order, dist = it.dist, id = it.id, idx = i }; }
                Array.Sort(s_UiKeys, 0, uin, keyOrder);
                for (int i = 0; i < uin; i++)
                {
                    var it = s_UiItems[s_UiKeys[i].idx];
                    DrawSprite(it.sr, it.m, icPos, k);
                }
                pt = Prof.Add("render: interface", pt);
            }
            UIRender.Draw(cp, k);
            pt = Prof.Add("render: UI draw", pt);
            Native.knl_stats(out LastSprites, out LastPixels);
            EdgeIndicators.Draw(cp, k);
        }

        static void DrawSprite(SpriteRenderer sr, Matrix4x4 m, Vector3 cp, float k)
        {
            var sd = sr.m_Sprite.data;
            int tex = sd.Texture;
            if (tex < 0) return;
            // local sprite space (units) of texel (u, v), v counted from the image top:
            //   x = (offX + u - pivot.x*rect.w) / ppu ,  y = (offY + imgH - v - pivot.y*rect.h) / ppu
            float px = sd.pivot.x * sd.rect.width, py = sd.pivot.y * sd.rect.height;
            float sx = sr.m_FlipX ? -1f : 1f, sy = sr.m_FlipY ? -1f : 1f;
            float inv = 1f / sd.ppu;
            // local positions of texel origin and unit steps
            var o = new Vector3(sx * (sd.offX - px) * inv, sy * (sd.offY + sd.imgH - py) * inv, 0);
            var du = new Vector3(sx * inv, 0, 0);
            var dv = new Vector3(0, -sy * inv, 0);
            var wo = m.MultiplyPoint3x4(o);
            var wu = m.MultiplyVector(du);
            var wv = m.MultiplyVector(dv);
            // world -> framebuffer pixels (y down)
            float tx = (wo.x - cp.x) * k + Width / 2f, ty = Height / 2f - (wo.y - cp.y) * k;
            float a = wu.x * k, b = -wu.y * k, c = wv.x * k, d = -wv.y * k;
            uint tint = sr.m_Color.ToRGBA32();
            var mat = sr.m_Materials.Length > 0 ? sr.m_Materials[0] : null;
            var si = ShadeInfo.Of(mat);
            if (!si.power)
            {
                int mode = si.additive ? 1 : 0;
                long tb = Prof.Now();
                Native.knl_draw(tex, a, b, c, d, tx, ty, tint, mode);
                Prof.Add("  sprite blit (C)", tb);
                return;
            }
            // Custom/PowerSprite2, evaluated once per sprite (D16):
            //   out = col * (1 - ov.a) * lerp(1, ambient + fakeLights, R) + ov.rgb * ov.a
            Color ov = si.overlay, ra = si.recolorA, rb = si.recolorB; float resp = si.response;
            var pb = sr.m_PropertyBlock;
            if (pb != null)
            {
                Color cc; float ff;
                if (pb.colors.TryGetValue("_Overlay", out cc)) ov = cc;
                if (pb.colors.TryGetValue("_RecolorA", out cc)) ra = cc;
                if (pb.colors.TryGetValue("_RecolorB", out cc)) rb = cc;
                if (pb.floats.TryGetValue("_AmbientResponse", out ff)) resp = ff;
            }
            // vertex lighting at the four texture corners (TL, TR, BL, BR), interpolated by the blitter
            float keep = 1f - ov.a;
            if (s_ActiveLightCount == 0)
            {
                var amb = RenderSettings.s_AmbientSky;
                int m0 = Mathf.Max(0, (int)(keep * (1f + resp * (amb.r - 1f)) * 256f));
                int m1 = Mathf.Max(0, (int)(keep * (1f + resp * (amb.g - 1f)) * 256f));
                int m2 = Mathf.Max(0, (int)(keep * (1f + resp * (amb.b - 1f)) * 256f));
                for (int corner = 0; corner < 4; corner++)
                {
                    mul12[corner * 3] = m0;
                    mul12[corner * 3 + 1] = m1;
                    mul12[corner * 3 + 2] = m2;
                }
            }
            else
            {
                for (int corner = 0; corner < 4; corner++)
                {
                    float cu = (corner & 1) != 0 ? sd.imgW : 0, cv = (corner & 2) != 0 ? sd.imgH : 0;
                    var wp = m.MultiplyPoint3x4(new Vector3(sx * (sd.offX + cu - px) * inv, sy * (sd.offY + sd.imgH - cv - py) * inv, 0));
                    Color vl = VertexLight(wp);
                    mul12[corner * 3] = Mathf.Max(0, (int)(keep * (1f + resp * (vl.r - 1f)) * 256f));
                    mul12[corner * 3 + 1] = Mathf.Max(0, (int)(keep * (1f + resp * (vl.g - 1f)) * 256f));
                    mul12[corner * 3 + 2] = Mathf.Max(0, (int)(keep * (1f + resp * (vl.b - 1f)) * 256f));
                }
            }
            add3[0] = (int)(ov.r * ov.a * 255f); add3[1] = (int)(ov.g * ov.a * 255f); add3[2] = (int)(ov.b * ov.a * 255f);
            for (int i = 0; i < 3; i++) { if (add3[i] < 0) add3[i] = 0; else if (add3[i] > 255) add3[i] = 255; }
            int flags = (si.recolor ? 1 : 0) | (si.additive ? 2 : 0);
            long tl = Prof.Now();
            Native.knl_draw_lit(tex, a, b, c, d, tx, ty, tint, mul12, add3, ra.ToRGBA32(), rb.ToRGBA32(), flags);
            Prof.Add("  sprite blit lit (C)", tl);
        }
        static readonly int[] mul12 = new int[12], add3 = new int[3];

        struct ActiveLight { public float x, y, z, w; }
        static readonly ActiveLight[] s_ActiveLights = new ActiveLight[64];
        static int s_ActiveLightCount;

        static void PrepareLights(Vector3 cp, float halfW)
        {
            s_ActiveLightCount = 0;
            Vector4[] lights;
            if (!Shader.s_VectorArrays.TryGetValue("_Light_PositionArray", out lights) || lights == null) return;
            float minX = cp.x - halfW - 4f, maxX = cp.x + halfW + 4f;
            for (int i = 0; i < lights.Length; i++)
            {
                float w = lights[i].w;
                if (w <= 0f) continue;
                float lx = lights[i].x;
                if (lx < minX || lx > maxX) continue;
                if (s_ActiveLightCount < s_ActiveLights.Length)
                    s_ActiveLights[s_ActiveLightCount++] = new ActiveLight { x = lx, y = lights[i].y, z = lights[i].z, w = w };
            }
        }

        /// ShadeSH9 with flat ambient (= RenderSettings.ambientSkyColor in gamma space) plus the
        /// LightFaker point lights: (1 - min(d / 3, 1)) * intensity * (0.49, 0.425, 0.34).
        static Color VertexLight(Vector3 p)
        {
            var amb = RenderSettings.s_AmbientSky;
            float r = amb.r, g = amb.g, b = amb.b;
            int n = s_ActiveLightCount;
            for (int i = 0; i < n; i++)
            {
                float dx = p.x - s_ActiveLights[i].x, dy = p.y - s_ActiveLights[i].y, dz = p.z - s_ActiveLights[i].z;
                float d2 = dx * dx + dy * dy + dz * dz;
                if (d2 >= 9f) continue;
                float f = (1f - Mathf.Sqrt(d2) * (1f / 3f)) * s_ActiveLights[i].w;
                r += f * 0.49f; g += f * 0.425f; b += f * 0.34f;
            }
            if (r < 0f) r = 0f;
            if (g < 0f) g = 0f;
            if (b < 0f) b = 0f;
            return new Color(r, g, b, 1f);
        }

        /// Gradient quads (sky / fill / haze: vertex colours written by the game's HorizontalGradient)
        /// and the water reflection pass. Other mesh effects (PostFX, motion blur) are skipped for now.
        static void DrawMesh(MeshRenderer mr, Matrix4x4 m, Vector3 cp, float k)
        {
            var mf = mr.gameObject.GetComponent<MeshFilter>();
            var mesh = mf != null ? mf.m_Mesh : null;
            var mat = mr.material;
            string shader = mat != null && mat.m_Shader != null ? mat.m_Shader.name : "";
            if (mesh == null || mesh.m_Vertices.Length < 4) return;
            if (shader.IndexOf("PostFX", StringComparison.Ordinal) >= 0) return;
            // corners in screen space
            float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
            var scr = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                var w = m.MultiplyPoint3x4(mesh.m_Vertices[i]);
                scr[i] = new Vector2((w.x - cp.x) * k + Width / 2f, Height / 2f - (w.y - cp.y) * k);
                x0 = Mathf.Min(x0, scr[i].x); x1 = Mathf.Max(x1, scr[i].x); y0 = Mathf.Min(y0, scr[i].y); y1 = Mathf.Max(y1, scr[i].y);
            }
            if (shader.IndexOf("Water", StringComparison.Ordinal) >= 0)
            {
                var water = mr.gameObject.m_Components.FirstOrDefault(c => c.GetType().Name == "Water");
                float scale = 0.8f;
                if (water != null) { var f = water.GetType().GetField("reflectionScale"); if (f != null) scale = (float)f.GetValue(water); }
                // the reflection starts at the waterline (top of the quad) and fills to the bottom of the screen
                Native.knl_reflect((int)Mathf.Round(y0), Height, scale, mat.GetColor("_Color").ToRGBA32(), Time.time, 1.2f);
                return;
            }
            var tint = mat != null && mat.m_ColorProps.ContainsKey("_Color") ? mat.GetColor("_Color") : Color.white;
            Color c00 = Color.white, c10 = Color.white, c01 = Color.white, c11 = Color.white;
            var cols = mesh.m_Colors;
            for (int i = 0; i < 4; i++)
            {
                var col = (i < cols.Length ? cols[i] : Color.white) * tint;
                bool left = scr[i].x < (x0 + x1) / 2f, top = scr[i].y < (y0 + y1) / 2f;
                if (left && top) c00 = col; else if (!left && top) c10 = col; else if (left) c01 = col; else c11 = col;
            }
            Native.knl_quad_gradient(x0, y0, x1, y1, c00.ToRGBA32(), c10.ToRGBA32(), c01.ToRGBA32(), c11.ToRGBA32(), 0);
        }

        public static int SavePng(string path, int scale) { return Native.knl_save_png(path, scale); }
    }
}


namespace KNL
{
    /// D3: off-screen enemy indicators. The original game shows ~14.2 world units at 16:9; we show
    /// 10 (D2 crop). Enemies in the cropped-away strips get a small purple chevron on that edge,
    /// at their (clamped) height. Overlay only: no game code is patched.
    /// Material-level shader parameters, cached on the Material until a property changes.
    public sealed class ShadeInfo
    {
        public bool power, recolor, additive;
        public float response = 1f;
        public Color overlay = new Color(0, 0, 0, 0), recolorA = Color.white, recolorB = Color.white;
        static readonly ShadeInfo Plain = new ShadeInfo();
        public static ShadeInfo Of(Material m)
        {
            if (m == null) return Plain;
            if (m.m_Shading != null) return m.m_Shading;
            var si = new ShadeInfo();
            string sh = m.m_Shader != null ? m.m_Shader.name : "";
            si.power = sh == "Custom/PowerSprite2" || sh == "Custom/AutoAnimate" || sh == "Custom/Lit Particle Cutout";
            float dst; bool hasDst = m.m_FloatProps.TryGetValue("DstBlend", out dst);
            si.additive = (hasDst && dst == 1f) || m.name.IndexOf("Additive", StringComparison.OrdinalIgnoreCase) >= 0
                || sh == "Custom/Reflected Light" || sh == "Custom/Directional" || sh == "Mobile/Particles/Additive";
            si.recolor = m.m_Keywords.Contains("RECOLOR_ON");
            Color c; float f;
            if (m.m_FloatProps.TryGetValue("_AmbientResponse", out f)) si.response = f;
            if (m.m_ColorProps.TryGetValue("_Overlay", out c)) si.overlay = c;
            if (m.m_ColorProps.TryGetValue("_RecolorA", out c)) si.recolorA = c;
            if (m.m_ColorProps.TryGetValue("_RecolorB", out c)) si.recolorB = c;
            m.m_Shading = si;
            return si;
        }
    }

    public static class EdgeIndicators
    {
        public static bool Enabled = true;
        public static float OriginalHalfWidth = 7.1f;      // 8 units tall at 16:9 -> 14.2 wide
        public static Color Purple = new Color(0.56f, 0.25f, 0.66f, 1f);
        internal static readonly List<Component> s_Enemies = new List<Component>();
        static Type s_Enemy; static bool s_Looked;
        public static int LastLeft, LastRight;

        public static bool IsEnemy(Component c)
        {
            if (c == null) return false;
            if (!s_Looked) { s_Looked = true; var asm = World.GameAssembly("Assembly-CSharp"); s_Enemy = asm != null ? asm.GetType("Enemy") : null; }
            return s_Enemy != null && s_Enemy.IsInstanceOfType(c);
        }

        public static void Draw(Vector3 cp, float k)
        {
            LastLeft = LastRight = 0;
            if (!Enabled) return;
            int count = s_Enemies.Count;
            if (count == 0) return;
            float halfView = RenderSystem.Width / 2f / k;
            float leftY = 0, rightY = 0;
            for (int i = 0; i < count; i++)
            {
                var c = s_Enemies[i];
                if (c == null || c.m_Destroyed) continue;
                var g = c.m_GameObject;
                if (g == null || g.m_Destroyed || !g.activeInHierarchy) continue;
                var p = g.m_Transform.position; float dx = p.x - cp.x;
                if (Mathf.Abs(dx) <= halfView || Mathf.Abs(dx) > OriginalHalfWidth) continue;
                if (dx < 0) { LastLeft++; leftY = p.y; } else { LastRight++; rightY = p.y; }
            }
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f);
            if (LastLeft > 0) Chevron(true, ScreenY(leftY, cp, k), LastLeft > 1 ? pulse : 0.85f);
            if (LastRight > 0) Chevron(false, ScreenY(rightY, cp, k), LastRight > 1 ? pulse : 0.85f);
        }
        static float ScreenY(float wy, Vector3 cp, float k)
        {
            float y = RenderSystem.Height / 2f - (wy + 0.5f - cp.y) * k;   // aim at the enemy's body, not its feet
            return Mathf.Clamp(y, 8f, RenderSystem.Height - 8f);
        }
        /// 4x7 px chevron pointing outward, 2 px from the edge.
        static void Chevron(bool left, float cy, float alpha)
        {
            var c = Purple; c.a = alpha; uint col = c.ToRGBA32();
            int[] widths = { 1, 2, 3, 4, 3, 2, 1 };
            for (int i = 0; i < widths.Length; i++)
            {
                float y = Mathf.Round(cy) - 3 + i, w = widths[i];
                float x0 = left ? 2f + (4 - w) : RenderSystem.Width - 6f;   // tip faces outward: '<' left, '>' right
                Native.knl_quad_gradient(x0, y, x0 + w, y + 1, col, col, col, col, 0);
            }
        }
    }
}
