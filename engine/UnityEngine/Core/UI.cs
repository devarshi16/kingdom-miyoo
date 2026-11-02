// Clean-room native side of Unity UI (the managed UnityEngine.UI.dll runs unmodified on top).
// M2: state-holding no-ops so menus/HUD scripts run; drawing UI comes with M3.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public sealed partial class Canvas
    {
        internal static readonly List<Canvas> s_All = new List<Canvas>();
        internal RenderMode m_RenderMode; internal Camera m_Camera; internal float m_ScaleFactor = 1f, m_RefPPU = 100f;
        internal bool m_OverrideSorting, m_PixelPerfect; internal int m_SortingOrder, m_SortingLayerID;
        public RenderMode renderMode { get { return m_RenderMode; } set { m_RenderMode = value; } }
        public Camera worldCamera { get { return m_Camera; } set { m_Camera = value; } }
        public bool isRootCanvas { get { var p = transform.parent; return p == null || p.GetComponentInParent<Canvas>() == null; } }
        public Canvas rootCanvas { get { Canvas c = this; for (var t = transform.parent; t != null; t = t.parent) { var cc = t.GetComponent<Canvas>(); if (cc != null) c = cc; } return c; } }
        public bool overrideSorting { get { return m_OverrideSorting; } set { m_OverrideSorting = value; } }
        public bool pixelPerfect { get { return m_PixelPerfect; } set { m_PixelPerfect = value; } }
        public float referencePixelsPerUnit { get { return m_RefPPU; } set { m_RefPPU = value; } }
        public float scaleFactor { get { return m_ScaleFactor; } set { m_ScaleFactor = value; } }
        public int renderOrder { get { return 0; } }
        public int sortingLayerID { get { return m_SortingLayerID; } set { m_SortingLayerID = value; } }
        public int sortingOrder { get { return m_SortingOrder; } set { m_SortingOrder = value; } }
        public int targetDisplay { get { return 0; } }
        static Material s_Default;
        public static Material GetDefaultCanvasMaterial() { if (s_Default == null) { s_Default = new Material(); s_Default.name = "Default UI Material"; } return s_Default; }
        public static Material GetETC1SupportedCanvasMaterial() { return GetDefaultCanvasMaterial(); }
        public static void ForceUpdateCanvases() { if (s_WillRender != null) s_WillRender(); }
        static WillRenderCanvases s_WillRender;
        public static event WillRenderCanvases willRenderCanvases { add { s_WillRender += value; } remove { s_WillRender -= value; } }
        internal static void SendWillRender() { if (s_WillRender != null) s_WillRender(); }
    }

    public sealed partial class CanvasRenderer
    {
        // UI.dll fills one shared worker mesh and hands it over: copy what we draw at SetMesh time.
        Color m_Color = Color.white; internal Material m_Material; internal Texture m_Texture;
        internal Vector3[] m_Pos = new Vector3[0]; internal Color32[] m_Col = new Color32[0]; internal Vector2[] m_UV = new Vector2[0]; internal int[] m_Idx = new int[0];
        internal bool m_HasMesh, m_Clip; internal Rect m_ClipRect;
        public Color GetColor() { return m_Color; }
        public void SetColor(Color c) { m_Color = c; }
        public float GetAlpha() { return m_Color.a; }
        public void SetAlpha(float a) { m_Color.a = a; }
        public bool cull { get; set; }
        public bool hasMoved { get { return false; } }
        public bool hasPopInstruction { get; set; }
        public int absoluteDepth { get { return 0; } }
        public int materialCount { get; set; }
        public int popMaterialCount { get; set; }
        public void Clear() { m_HasMesh = false; m_Pos = new Vector3[0]; m_Idx = new int[0]; }
        public void DisableRectClipping() { m_Clip = false; }
        public void EnableRectClipping(Rect r) { m_Clip = true; m_ClipRect = r; }
        public void SetAlphaTexture(Texture t) { }
        public void SetMaterial(Material m, Texture t) { m_Material = m; m_Texture = t; }
        public void SetMaterial(Material m, int i) { if (i == 0) m_Material = m; }
        public void SetPopMaterial(Material m, int i) { }
        public void SetTexture(Texture t) { m_Texture = t; }
        public void SetMesh(Mesh m)
        {
            if (m == null) { Clear(); return; }
            m_Pos = (Vector3[])m.m_Vertices.Clone(); m_UV = (Vector2[])m.m_UV.Clone(); m_Idx = (int[])m.m_Triangles.Clone();
            m_Col = new Color32[m_Pos.Length];
            for (int i = 0; i < m_Col.Length; i++) m_Col[i] = i < m.m_Colors.Length ? (Color32)m.m_Colors[i] : new Color32(255, 255, 255, 255);
            m_HasMesh = m_Pos.Length > 0;
        }
        public static void AddUIVertexStream(List<UIVertex> verts, List<Vector3> p, List<Color32> c, List<Vector2> uv0, List<Vector2> uv1, List<Vector2> uv2, List<Vector2> uv3, List<Vector3> n, List<Vector4> t)
        {
            for (int i = 0; i < p.Count; i++)
            {
                var v = UIVertex.simpleVert; v.position = p[i]; v.color = c[i]; v.uv0 = uv0[i]; v.uv1 = i < uv1.Count ? uv1[i] : Vector2.zero;
                v.normal = i < n.Count ? n[i] : Vector3.back; v.tangent = i < t.Count ? t[i] : new Vector4(1, 0, 0, -1);
                verts.Add(v);
            }
        }
        public static void CreateUIVertexStream(List<UIVertex> verts, List<Vector3> p, List<Color32> c, List<Vector2> uv0, List<Vector2> uv1, List<Vector2> uv2, List<Vector2> uv3, List<Vector3> n, List<Vector4> t, List<int> idx)
        {
            foreach (var i in idx)
            {
                var v = UIVertex.simpleVert; v.position = p[i]; v.color = c[i]; v.uv0 = uv0[i]; v.uv1 = i < uv1.Count ? uv1[i] : Vector2.zero;
                v.normal = i < n.Count ? n[i] : Vector3.back; v.tangent = i < t.Count ? t[i] : new Vector4(1, 0, 0, -1);
                verts.Add(v);
            }
        }
        public static void SplitUIVertexStreams(List<UIVertex> verts, List<Vector3> p, List<Color32> c, List<Vector2> uv0, List<Vector2> uv1, List<Vector2> uv2, List<Vector2> uv3, List<Vector3> n, List<Vector4> t, List<int> idx)
        {
            p.Clear(); c.Clear(); uv0.Clear(); uv1.Clear(); if (uv2 != null) uv2.Clear(); if (uv3 != null) uv3.Clear(); n.Clear(); t.Clear(); idx.Clear();
            for (int i = 0; i < verts.Count; i++)
            {
                var v = verts[i]; p.Add(v.position); c.Add(v.color); uv0.Add(v.uv0); uv1.Add(v.uv1); n.Add(v.normal); t.Add(v.tangent); idx.Add(i);
            }
        }
    }

    public partial struct TextGenerationSettings
    {
        public Font font; public Color color; public int fontSize; public float lineSpacing; public bool richText; public float scaleFactor;
        public FontStyle fontStyle; public TextAnchor textAnchor; public bool alignByGeometry; public bool resizeTextForBestFit;
        public int resizeTextMinSize; public int resizeTextMaxSize; public bool updateBounds; public VerticalWrapMode verticalOverflow;
        public HorizontalWrapMode horizontalOverflow; public Vector2 generationExtents; public Vector2 pivot; public bool generateOutOfBounds;
    }
    public partial struct UICharInfo { public Vector2 cursorPos; public float charWidth; }
    public partial struct UILineInfo { public int startCharIdx; public int height; public float topY; public float leading; }

    public partial struct UIVertex
    {
        public Vector3 position; public Vector3 normal; public Vector4 tangent; public Color32 color;
        public Vector2 uv0; public Vector2 uv1; public Vector2 uv2; public Vector2 uv3;
        public static UIVertex simpleVert;
        static UIVertex()
        {
            simpleVert = new UIVertex { position = Vector3.zero, normal = new Vector3(0, 0, -1), tangent = new Vector4(1, 0, 0, -1), color = new Color32(255, 255, 255, 255) };
        }
    }

    public sealed partial class Mesh
    {
        public void SetVertices(List<Vector3> v) { m_Vertices = v.ToArray(); }
        public void SetColors(List<Color32> c) { m_Colors = new Color[c.Count]; for (int i = 0; i < c.Count; i++) m_Colors[i] = c[i]; }
        public void SetColors(List<Color> c) { m_Colors = c.ToArray(); }
        public void SetUVs(int ch, List<Vector2> uv) { if (ch == 0) m_UV = uv.ToArray(); else if (ch == 1) m_UV2 = uv.ToArray(); }
        public void SetNormals(List<Vector3> n) { }
        public void SetTangents(List<Vector4> t) { }
        public void SetTriangles(List<int> t, int sub) { m_Triangles = t.ToArray(); }
        public void SetTriangles(int[] t, int sub) { m_Triangles = t ?? new int[0]; }
        public int[] GetIndices(int sub) { return (int[])m_Triangles.Clone(); }
        public Color32[] colors32
        {
            get { var r = new Color32[m_Colors.Length]; for (int i = 0; i < r.Length; i++) r[i] = m_Colors[i]; return r; }
            set { m_Colors = new Color[value.Length]; for (int i = 0; i < value.Length; i++) m_Colors[i] = value[i]; }
        }
        public Vector3[] normals { get { var n = new Vector3[m_Vertices.Length]; for (int i = 0; i < n.Length; i++) n[i] = new Vector3(0, 0, -1); return n; } set { } }
    }

    public sealed partial class Sprite
    {
        internal Vector4 m_Border; internal Texture2D m_CutTex;
        public Vector4 border { get { return m_Border; } }
        public bool packed { get { return true; } }
        public Texture2D associatedAlphaSplitTexture { get { return null; } }
        // UI draws sprites from their own cut-out image (D17): textureRect is the cut-out within the rect
        public Rect textureRect { get { return data != null ? new Rect(data.rect.x + data.offX, data.rect.y + data.offY, data.imgW, data.imgH) : default(Rect); } }
        internal Texture2D CutTexture()
        {
            if (m_CutTex == null && data != null) { m_CutTex = new Texture2D(data.imgW, data.imgH); m_CutTex.name = name; m_CutTex.m_Sprite = data; }
            return m_CutTex;
        }
    }

    public sealed partial class CanvasGroup
    {
        internal float m_Alpha = 1f; internal bool m_Interactable = true, m_Blocks = true, m_Ignore;
        public float alpha { get { return m_Alpha; } set { m_Alpha = Mathf.Clamp01(value); } }
        public bool interactable { get { return m_Interactable; } set { m_Interactable = value; } }
        public bool blocksRaycasts { get { return m_Blocks; } set { m_Blocks = value; } }
        public bool ignoreParentGroups { get { return m_Ignore; } set { m_Ignore = value; } }
    }

    public sealed partial class Font
    {
        internal int m_FontSize = 16; internal Material m_Material;
        internal float m_LineSpacing = 16f, m_Ascent = 12f, m_Descent = -4f; internal int m_ConvertCase = -1;
        internal readonly Dictionary<int, CharacterInfo> m_Glyphs = new Dictionary<int, CharacterInfo>();
        public Material material { get { if (m_Material == null) { m_Material = new Material(); m_Material.name = "Font Material"; } return m_Material; } set { m_Material = value; } }
        public bool HasCharacter(char c) { return m_Glyphs.Count == 0 || m_Glyphs.ContainsKey(c); }
        public bool dynamic { get { return false; } }
        public int fontSize { get { return m_FontSize; } }
        public int lineHeight { get { return (int)m_LineSpacing; } }
        public int ascent { get { return (int)m_Ascent; } }
        public bool GetCharacterInfo(char ch, out CharacterInfo info, int size, FontStyle style) { return m_Glyphs.TryGetValue(ch, out info); }
        public bool GetCharacterInfo(char ch, out CharacterInfo info, int size) { return m_Glyphs.TryGetValue(ch, out info); }
        public bool GetCharacterInfo(char ch, out CharacterInfo info) { return m_Glyphs.TryGetValue(ch, out info); }
        public void RequestCharactersInTexture(string s, int size, FontStyle style) { }
        public void RequestCharactersInTexture(string s, int size) { }
        public void RequestCharactersInTexture(string s) { }
        static Action<Font> s_Rebuilt;
        public static event Action<Font> textureRebuilt { add { s_Rebuilt += value; } remove { s_Rebuilt -= value; } }
    }

    public partial struct CharacterInfo
    {
        // baked (non-dynamic) glyph: uv rect in texture space (v up), vert rect in font pixels relative to the baseline
        internal Rect m_UV, m_Vert; internal float m_Advance; internal bool m_Flipped;
    }

    /// Clean-room text layout for Kingdom's baked bitmap fonts. Output follows Unity's contract for
    /// UI.Text: 4 verts per visible glyph (TL, TR, BR, BL) plus 4 trailing verts that Text drops.
    public sealed partial class TextGenerator
    {
        readonly List<UICharInfo> m_Chars = new List<UICharInfo>(); readonly List<UILineInfo> m_Lines = new List<UILineInfo>(); readonly List<UIVertex> m_Verts = new List<UIVertex>();
        Rect m_Extents; int m_Visible;
        string m_LastText; TextGenerationSettings m_LastSettings; bool m_Valid;
        public TextGenerator() { }
        public TextGenerator(int capacity) { }
        public IList<UICharInfo> characters { get { return m_Chars; } }
        public IList<UILineInfo> lines { get { return m_Lines; } }
        public IList<UIVertex> verts { get { return m_Verts; } }
        public Rect rectExtents { get { return m_Extents; } }
        public int characterCount { get { return m_Chars.Count; } }
        public int characterCountVisible { get { return m_Visible; } }
        public int lineCount { get { return m_Lines.Count; } }
        public int vertexCount { get { return m_Verts.Count; } }
        public int fontSizeUsedForBestFit { get { return m_LastSettings.fontSize; } }
        public void Invalidate() { m_Valid = false; }
        public bool Populate(string s, TextGenerationSettings t) { return PopulateWithErrors(s, t, null); }
        public bool PopulateWithErrors(string s, TextGenerationSettings t, GameObject ctx)
        {
            if (m_Valid && s == m_LastText && Same(t, m_LastSettings)) return true;
            m_LastText = s; m_LastSettings = t; m_Valid = true;
            Layout(s ?? "", t, true);
            return true;
        }
        public float GetPreferredWidth(string s, TextGenerationSettings t)
        {
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.generationExtents = Vector2.zero;
            return Measure(s, t).x;
        }
        public float GetPreferredHeight(string s, TextGenerationSettings t)
        {
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return Measure(s, t).y;
        }
        Vector2 Measure(string s, TextGenerationSettings t)
        {
            var g = new TextGenerator(); g.Layout(s ?? "", t, false);
            return new Vector2(g.m_MeasuredW, g.m_MeasuredH);
        }
        static bool Same(TextGenerationSettings a, TextGenerationSettings b)
        {
            return a.font == b.font && a.fontSize == b.fontSize && a.color == b.color && a.lineSpacing == b.lineSpacing && a.scaleFactor == b.scaleFactor
                && a.textAnchor == b.textAnchor && a.generationExtents == b.generationExtents && a.pivot == b.pivot && a.horizontalOverflow == b.horizontalOverflow
                && a.verticalOverflow == b.verticalOverflow && a.richText == b.richText && a.fontStyle == b.fontStyle;
        }

        float m_MeasuredW, m_MeasuredH;
        struct Run { public char ch; public Color32 col; }

        void Layout(string text, TextGenerationSettings t, bool emit)
        {
            m_Chars.Clear(); m_Lines.Clear(); m_Verts.Clear(); m_Visible = 0;
            var font = t.font;
            if (font == null) { m_Extents = new Rect(0, 0, 0, 0); AddTrailing(); return; }
            float sf = t.scaleFactor <= 0 ? 1f : t.scaleFactor;
            // UI.Text uses fontSize=0 for baked fonts and divides native-pixel vertices and
            // preferred metrics by pixelsPerUnit itself. Scaling those intrinsic glyphs here
            // again doubles small-font labels and wraps words that fit in the original UI.
            float scale = t.fontSize > 0 && font.m_FontSize > 0 ? (float)t.fontSize / font.m_FontSize * sf : 1f;
            float lineH = font.m_LineSpacing * scale * (t.lineSpacing == 0 ? 1f : t.lineSpacing);
            float ascent = font.m_Ascent * scale;
            Vector2 ext = t.generationExtents * sf;
            bool wrap = t.horizontalOverflow == HorizontalWrapMode.Wrap && ext.x > 0;

            // 1. strip rich-text tags, keep per-char colours
            var runs = new List<Run>(text.Length);
            var colStack = new Stack<Color32>(); Color32 cur = t.color;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (t.richText && ch == '<')
                {
                    int close = text.IndexOf('>', i);
                    if (close > i)
                    {
                        string tag = text.Substring(i + 1, close - i - 1);
                        if (tag.StartsWith("color=")) { colStack.Push(cur); cur = ParseColor(tag.Substring(6), cur); i = close; continue; }
                        if (tag == "/color") { if (colStack.Count > 0) cur = colStack.Pop(); i = close; continue; }
                        if (tag == "b" || tag == "/b" || tag == "i" || tag == "/i" || tag.StartsWith("size=") || tag == "/size" || tag.StartsWith("material") || tag == "/material" || tag.StartsWith("quad")) { i = close; continue; }
                    }
                }
                if (font.m_ConvertCase == 0) ch = char.ToUpperInvariant(ch); else if (font.m_ConvertCase == 1) ch = char.ToLowerInvariant(ch);
                runs.Add(new Run { ch = ch, col = cur });
            }
            // 2. break into lines (explicit \n, word wrap on spaces)
            var lines = new List<List<int>>(); var line = new List<int>(); float x = 0; int lastSpace = -1;
            for (int i = 0; i < runs.Count; i++)
            {
                char ch = runs[i].ch;
                if (ch == '\n') { lines.Add(line); line = new List<int>(); x = 0; lastSpace = -1; continue; }
                float adv = Advance(font, ch) * scale;
                if (wrap && x + adv > ext.x + 0.01f && line.Count > 0)
                {
                    if (lastSpace >= 0 && lastSpace < line.Count - 0)
                    {
                        var rest = line.GetRange(lastSpace + 1, line.Count - lastSpace - 1);
                        line.RemoveRange(lastSpace, line.Count - lastSpace);
                        lines.Add(line); line = rest; x = 0; foreach (var k in line) x += Advance(font, runs[k].ch) * scale;
                    }
                    else { lines.Add(line); line = new List<int>(); x = 0; }
                    lastSpace = -1;
                    if (ch == ' ') continue;
                }
                if (ch == ' ') lastSpace = line.Count;
                line.Add(i); x += adv;
            }
            lines.Add(line);

            // 3. measure, align, emit
            int nl = lines.Count;
            float blockH = nl * lineH;
            float maxW = 0; var widths = new float[nl];
            for (int l = 0; l < nl; l++) { float w = 0; foreach (var k in lines[l]) w += Advance(font, runs[k].ch) * scale; widths[l] = w; maxW = Mathf.Max(maxW, w); }
            m_MeasuredW = maxW; m_MeasuredH = blockH;
            if (!emit) return;
            Rect r = new Rect(-t.pivot.x * ext.x, -t.pivot.y * ext.y, ext.x, ext.y);
            int anchor = (int)t.textAnchor, hz = anchor % 3, vt = anchor / 3;
            float top = vt == 0 ? r.yMax : vt == 1 ? (r.yMin + r.yMax) / 2 + blockH / 2 : r.yMin + blockH;
            m_Extents = new Rect(r.xMin, top - blockH, maxW, blockH);
            for (int l = 0; l < nl; l++)
            {
                float lx = hz == 0 ? r.xMin : hz == 1 ? (r.xMin + r.xMax) / 2 - widths[l] / 2 : r.xMax - widths[l];
                float lineTop = top - l * lineH;
                float baseline = lineTop - ascent;
                lx = Mathf.Round(lx); baseline = Mathf.Round(baseline);
                m_Lines.Add(new UILineInfo { startCharIdx = lines[l].Count > 0 ? lines[l][0] : m_Chars.Count, height = (int)lineH, topY = lineTop, leading = 0 });
                float cx = lx;
                foreach (var k in lines[l])
                {
                    var run = runs[k];
                    CharacterInfo gi; bool has = font.m_Glyphs.TryGetValue(run.ch, out gi);
                    float adv = has ? gi.m_Advance * scale : Advance(font, run.ch) * scale;
                    m_Chars.Add(new UICharInfo { cursorPos = new Vector2(cx, lineTop), charWidth = adv });
                    if (has && run.ch != ' ' && gi.m_Vert.width != 0)
                    {
                        float x0 = cx + gi.m_Vert.x * scale, x1 = x0 + gi.m_Vert.width * scale;
                        float y0 = baseline + gi.m_Vert.y * scale, y1 = y0 + gi.m_Vert.height * scale;   // y0 top, y1 bottom (height < 0)
                        Rect uv = gi.m_UV;
                        Vector2 uTL, uTR, uBR, uBL;
                        if (!gi.m_Flipped)
                        { uBL = new Vector2(uv.x, uv.y); uBR = new Vector2(uv.x + uv.width, uv.y); uTR = new Vector2(uv.x + uv.width, uv.y + uv.height); uTL = new Vector2(uv.x, uv.y + uv.height); }
                        else
                        { uBL = new Vector2(uv.x, uv.y); uBR = new Vector2(uv.x, uv.y + uv.height); uTR = new Vector2(uv.x + uv.width, uv.y + uv.height); uTL = new Vector2(uv.x + uv.width, uv.y); }
                        AddVert(x0, y0, uTL, run.col); AddVert(x1, y0, uTR, run.col); AddVert(x1, y1, uBR, run.col); AddVert(x0, y1, uBL, run.col);
                        m_Visible++;
                    }
                    cx += adv;
                }
            }
            AddTrailing();
        }
        void AddVert(float x, float y, Vector2 uv, Color32 c)
        {
            var v = UIVertex.simpleVert; v.position = new Vector3(x, y, 0); v.uv0 = uv; v.color = c; m_Verts.Add(v);
        }
        void AddTrailing() { for (int i = 0; i < 4; i++) m_Verts.Add(UIVertex.simpleVert); }
        static float Advance(Font f, char ch)
        {
            CharacterInfo gi;
            if (f.m_Glyphs.TryGetValue(ch, out gi)) return gi.m_Advance;
            if (f.m_Glyphs.TryGetValue(' ', out gi)) return gi.m_Advance;
            return f.m_FontSize * 0.5f;
        }
        static Color32 ParseColor(string v, Color32 fallback)
        {
            v = v.Trim('"', '\'', ' ');
            Color c;
            if (v.StartsWith("#"))
            {
                try
                {
                    var h = v.Substring(1); if (h.Length == 6) h += "FF";
                    uint n = System.Convert.ToUInt32(h, 16);
                    return new Color32((byte)(n >> 24), (byte)(n >> 16), (byte)(n >> 8), (byte)n);
                }
                catch { return fallback; }
            }
            switch (v.ToLowerInvariant())
            {
                case "red": c = Color.red; break; case "white": c = Color.white; break; case "black": c = Color.black; break;
                case "yellow": c = new Color(1, 0.92f, 0.016f, 1); break; case "green": c = Color.green; break; case "blue": c = Color.blue; break;
                case "orange": c = new Color(1, 0.65f, 0, 1); break; case "grey": case "gray": c = Color.gray; break;
                default: return fallback;
            }
            return c;
        }
    }

    public sealed partial class RectTransformUtility
    {
        public static Rect PixelAdjustRect(RectTransform rt, Canvas c) { return new Rect(0, 0, rt.sizeDelta.x, rt.sizeDelta.y); }
        public static Vector2 PixelAdjustPoint(Vector2 p, Transform t, Canvas c) { return p; }
        public static Vector2 WorldToScreenPoint(Camera cam, Vector3 w) { return cam != null ? (Vector2)cam.WorldToScreenPoint(w) : (Vector2)w; }
        public static bool RectangleContainsScreenPoint(RectTransform r, Vector2 p, Camera c) { return false; }
        public static bool ScreenPointToLocalPointInRectangle(RectTransform r, Vector2 p, Camera c, ref Vector2 local) { local = p; return true; }
        public static void FlipLayoutAxes(RectTransform r, bool keepPos, bool recursive) { }
        public static void FlipLayoutOnAxis(RectTransform r, int axis, bool keepPos, bool recursive) { }
    }

    public partial class UISystemProfilerApi
    {
        public static void AddMarker(string n, Object o) { }
        public static void BeginSample(SampleType t) { }
        public static void EndSample(SampleType t) { }
    }

    public sealed partial class Texture2D
    {
        static Texture2D s_White, s_Black;
        public static Texture2D whiteTexture { get { if (s_White == null) { s_White = new Texture2D(4, 4); s_White.name = "white"; } return s_White; } }
        public static Texture2D blackTexture { get { if (s_Black == null) { s_Black = new Texture2D(4, 4); s_Black.name = "black"; } return s_Black; } }
    }

    public sealed partial class Application
    {
        public static string unityVersion { get { return "2017.1.2p1"; } }
        static LogCallback s_Log;
        public static event LogCallback logMessageReceived { add { s_Log += value; } remove { s_Log -= value; } }
    }
}

namespace UnityEngine
{
    public partial struct DrivenRectTransformTracker
    {
        public void Add(Object driver, RectTransform rt, DrivenTransformProperties p) { }
        public void Clear() { }
    }
    public sealed partial class RectTransform
    {
        static ReapplyDrivenProperties s_Reapply;
        public static event ReapplyDrivenProperties reapplyDrivenProperties { add { s_Reapply += value; } remove { s_Reapply -= value; } }
    }
}

namespace UnityEngine.Sprites
{
    public sealed partial class DataUtility
    {
        // UVs/padding relative to the sprite's own cut-out texture (see Sprite.texture)
        public static Vector4 GetOuterUV(Sprite s) { return new Vector4(0, 0, 1, 1); }
        public static Vector4 GetPadding(Sprite s)
        {
            var d = s != null ? s.data : null; if (d == null) return Vector4.zero;
            return new Vector4(d.offX, d.offY, d.rect.width - d.offX - d.imgW, d.rect.height - d.offY - d.imgH);
        }
        public static Vector4 GetInnerUV(Sprite s)
        {
            var d = s != null ? s.data : null; if (d == null || d.imgW == 0 || d.imgH == 0) return new Vector4(0, 0, 1, 1);
            var b = s.m_Border; var pad = GetPadding(s);
            return new Vector4(Mathf.Clamp01((b.x - pad.x) / d.imgW), Mathf.Clamp01((b.y - pad.y) / d.imgH),
                               Mathf.Clamp01(1f - (b.z - pad.z) / d.imgW), Mathf.Clamp01(1f - (b.w - pad.w) / d.imgH));
        }
        public static Vector2 GetMinSize(Sprite s) { return s != null ? new Vector2(s.m_Border.x + s.m_Border.z, s.m_Border.y + s.m_Border.w) : Vector2.zero; }
    }
}
