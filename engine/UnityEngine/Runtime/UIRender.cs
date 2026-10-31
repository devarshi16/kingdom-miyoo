// Canvas pass: draws the meshes the game's own UnityEngine.UI.dll hands to CanvasRenderer (D17).
// World-space canvases go through the main camera like sprites; screen-space canvases map their
// root rect onto the 320x240 framebuffer with the CanvasScaler's scaleFactor.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace KNL
{
    public static class UIRender
    {
        static readonly List<Canvas> roots = new List<Canvas>();
        static readonly Dictionary<string, int> texByKey = new Dictionary<string, int>();
        public static int LastQuads;
        public static bool Debug = Environment.GetEnvironmentVariable("KNL_UIDEBUG") == "1";

        /// before layout: size screen-space roots to the screen, then let UI.dll rebuild (Canvas.willRenderCanvases)
        public static void Prepare()
        {
            roots.Clear();
            foreach (var g in World.SceneObjects)
            {
                if (g.m_Destroyed || !g.activeInHierarchy) continue;
                foreach (var c in g.m_Components)
                {
                    var cv = c as Canvas;
                    if (cv == null || !cv.enabled || !cv.isRootCanvas) continue;
                    roots.Add(cv);
                    if (cv.m_RenderMode != RenderMode.WorldSpace)
                    {
                        var rt = cv.transform as RectTransform;
                        float s = cv.m_ScaleFactor <= 0 ? 1f : cv.m_ScaleFactor;
                        var want = new Vector2(RenderSystem.Width / s, RenderSystem.Height / s);
                        if (rt != null && (rt.m_SizeDelta - want).sqrMagnitude > 1e-6f) rt.sizeDelta = want;
                    }
                }
            }
            try { Canvas.SendWillRender(); }
            catch (Exception e) { Lifecycle.Fail(null, "willRenderCanvases", e); }
        }

        public static void Draw(Vector3 cp, float k)
        {
            LastQuads = 0;
            // world-space first (far to near), then screen-space by sorting order
            roots.Sort((a, b) =>
            {
                bool wa = a.m_RenderMode == RenderMode.WorldSpace, wb = b.m_RenderMode == RenderMode.WorldSpace;
                if (wa != wb) return wa ? -1 : 1;
                if (wa) return b.transform.position.z.CompareTo(a.transform.position.z);
                return a.m_SortingOrder.CompareTo(b.m_SortingOrder);
            });
            foreach (var cv in roots)
            {
                if (cv.m_Destroyed || !cv.gameObject.activeInHierarchy) continue;
                Matrix4x4 toPixel;
                if (cv.m_RenderMode == RenderMode.WorldSpace)
                {
                    // world -> framebuffer (y down)
                    toPixel = Matrix4x4.identity;
                    toPixel.m00 = k; toPixel.m03 = -cp.x * k + RenderSystem.Width / 2f;
                    toPixel.m11 = -k; toPixel.m13 = cp.y * k + RenderSystem.Height / 2f;
                }
                else
                {
                    var rt = cv.transform as RectTransform;
                    float s = cv.m_ScaleFactor <= 0 ? 1f : cv.m_ScaleFactor;
                    var r = rt != null ? rt.rect : new Rect(-RenderSystem.Width / 2f, -RenderSystem.Height / 2f, RenderSystem.Width, RenderSystem.Height);
                    var m = Matrix4x4.identity;
                    m.m00 = s; m.m03 = -r.xMin * s;
                    m.m11 = -s; m.m13 = RenderSystem.Height + r.yMin * s;
                    toPixel = m * cv.transform.worldToLocalMatrix;
                }
                Walk(cv.transform, 1f, toPixel, cv);
            }
        }

        static readonly Vector2[] scr = new Vector2[0];
        static Vector2[] pix = new Vector2[64];

        static void Walk(Transform t, float alpha, Matrix4x4 toPixel, Canvas root)
        {
            var go = t.gameObject;
            if (!go.m_IsActive) return;
            CanvasRenderer cr = null;
            foreach (var c in go.m_Components)
            {
                var cg = c as CanvasGroup;
                if (cg != null) alpha = cg.m_Ignore ? cg.m_Alpha : alpha * cg.m_Alpha;
                var nested = c as Canvas;
                if (nested != null && !nested.enabled && nested != root) return;
                if (cr == null) cr = c as CanvasRenderer;
            }
            if (alpha <= 0.001f) return;
            if (cr != null && cr.m_HasMesh && !cr.cull) DrawMesh(cr, t, alpha, toPixel);
            for (int i = 0; i < t.m_Children.Count; i++) Walk(t.m_Children[i], alpha, toPixel, root);
        }

        static void DrawMesh(CanvasRenderer cr, Transform t, float alpha, Matrix4x4 toPixel)
        {
            var m = toPixel * t.localToWorldMatrix;
            int n = cr.m_Pos.Length;
            if (pix.Length < n) pix = new Vector2[n * 2];
            for (int i = 0; i < n; i++) { var p = m.MultiplyPoint3x4(cr.m_Pos[i]); pix[i] = new Vector2(p.x, p.y); }
            var tex = cr.m_Texture as Texture2D;
            int th = Handle(tex), tw = 0, tht = 0;
            if (th >= 0) { tw = tex.m_Sprite != null ? tex.m_Sprite.imgW : tex.m_Width; tht = tex.m_Sprite != null ? tex.m_Sprite.imgH : tex.m_Height; }
            int flags = tex != null && tex.alphaOnly ? 1 : 0;
            // sprite cut-outs are stored top-down; raw exported textures (fonts) are bottom-up
            bool bottomUp = false;
            if (cr.m_Material != null && cr.m_Material.name.IndexOf("Additive", StringComparison.OrdinalIgnoreCase) >= 0) flags |= 2;
            var cc = cr.GetColor();
            int cx0 = 0, cy0 = 0, cx1 = RenderSystem.Width, cy1 = RenderSystem.Height;
            if (cr.m_Clip)
            {   // clip rect is in the root canvas' space; toPixel maps root space directly
                var a = toPixel.MultiplyPoint3x4(new Vector3(cr.m_ClipRect.xMin, cr.m_ClipRect.yMin, 0));
                var b = toPixel.MultiplyPoint3x4(new Vector3(cr.m_ClipRect.xMax, cr.m_ClipRect.yMax, 0));
                cx0 = (int)Mathf.Floor(Mathf.Min(a.x, b.x)); cx1 = (int)Mathf.Ceil(Mathf.Max(a.x, b.x));
                cy0 = (int)Mathf.Floor(Mathf.Min(a.y, b.y)); cy1 = (int)Mathf.Ceil(Mathf.Max(a.y, b.y));
            }
            if (Debug) Console.Error.WriteLine(string.Format("[ui] {0} tex={1}({2}) h={3} {4}x{5} mat={6} quads={7} col={8} a={9:F2} p0={10}", t.name, tex != null ? tex.name : "-", tex != null && tex.m_Sprite != null ? "cut" : "raw", th, tw, tht, cr.m_Material != null ? cr.m_Material.name : "-", cr.m_Idx.Length / 6, cc, alpha, n > 0 ? pix[0].ToString() : "-"));
            var idx = cr.m_Idx;
            for (int q = 0; q + 5 < idx.Length; q += 6)
            {
                // quads come as (a, b, c) (c, d, a)
                int ia = idx[q], ib = idx[q + 1], ic = idx[q + 2], id = idx[q + 4];
                if (ia >= n || ib >= n || ic >= n || id >= n) continue;
                var col = cr.m_Col[ia];
                float r = col.r * cc.r, g = col.g * cc.g, bl = col.b * cc.b, al = col.a * cc.a * alpha;
                uint rgba = (uint)Mathf.Clamp((int)r, 0, 255) | ((uint)Mathf.Clamp((int)g, 0, 255) << 8) | ((uint)Mathf.Clamp((int)bl, 0, 255) << 16) | ((uint)Mathf.Clamp((int)al, 0, 255) << 24);
                Vector2 ua = cr.m_UV.Length > ia ? cr.m_UV[ia] : Vector2.zero, ub = cr.m_UV.Length > ib ? cr.m_UV[ib] : Vector2.zero, ud = cr.m_UV.Length > id ? cr.m_UV[id] : Vector2.zero;
                Native.knl_draw_quad(th, pix[ia].x, pix[ia].y, pix[ib].x, pix[ib].y, pix[id].x, pix[id].y,
                    ua.x * tw, (bottomUp ? ua.y : 1f - ua.y) * tht, ub.x * tw, (bottomUp ? ub.y : 1f - ub.y) * tht, ud.x * tw, (bottomUp ? ud.y : 1f - ud.y) * tht,
                    rgba, flags, cx0, cy0, cx1, cy1);
                LastQuads++;
            }
        }

        /// knl texture handle for a Texture2D: sprite cut-outs reuse the sprite PNG, others load
        /// textures/<file>/<pathid>_*.png exported by convert.py. -1 = untextured (solid colour).
        static int Handle(Texture2D t)
        {
            if (t == null) return -1;
            if (t.m_KnlTex != -2) return t.m_KnlTex;
            t.m_KnlTex = -1;
            if (t.m_Sprite != null) { t.m_KnlTex = t.m_Sprite.Texture; return t.m_KnlTex; }
            if (t.m_File == null) return -1;
            string key = t.m_File + ":" + t.m_PathID;
            int h;
            if (texByKey.TryGetValue(key, out h)) { t.m_KnlTex = h; return h; }
            if (Pack.Active)
            {   // entry "textures/<file>/<pid>": u32 w, u32 h, RGBA
                long off; int len;
                if (Pack.Locate("textures/" + t.m_File + "/" + t.m_PathID, out off, out len))
                {
                    var hdr = Pack.Read("textures/" + t.m_File + "/" + t.m_PathID);
                    int w = BitConverter.ToInt32(hdr, 0), hh = BitConverter.ToInt32(hdr, 4);
                    h = Native.knl_tex_from_pack_rgba(off + 8, w, hh);
                    if (h >= 0) { t.m_KnlTex = h; texByKey[key] = h; t.m_Width = w; t.m_Height = hh; }
                }
                return t.m_KnlTex;
            }
            var dir = Path.Combine(Path.Combine(Paths.DataDir, "textures"), t.m_File);
            if (Directory.Exists(dir))
                foreach (var fn in Directory.GetFiles(dir, t.m_PathID + "_*.png"))
                {
                    int w, hh; h = Native.knl_tex_load_png(fn, out w, out hh);
                    if (h >= 0) { t.m_KnlTex = h; texByKey[key] = h; t.m_Width = w; t.m_Height = hh; }
                    break;
                }
            return t.m_KnlTex;
        }
    }
}
