// Clean-room AnimationCurve / Gradient / Keyframe / RectOffset + Animator.StringToHash.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public partial struct Keyframe
    {
        float m_Time, m_Value, m_InTangent, m_OutTangent;
        internal int m_TangentMode;
        public Keyframe(float time, float value) { m_Time = time; m_Value = value; m_InTangent = 0; m_OutTangent = 0; m_TangentMode = 0; }
        public Keyframe(float time, float value, float inTangent, float outTangent) { m_Time = time; m_Value = value; m_InTangent = inTangent; m_OutTangent = outTangent; m_TangentMode = 0; }
        public float time { get { return m_Time; } set { m_Time = value; } }
        public float value { get { return m_Value; } set { m_Value = value; } }
        public float inTangent { get { return m_InTangent; } set { m_InTangent = value; } }
        public float outTangent { get { return m_OutTangent; } set { m_OutTangent = value; } }
        public int tangentMode { get { return m_TangentMode; } set { m_TangentMode = value; } }
    }

    public partial class AnimationCurve
    {
        Keyframe[] m_Keys = new Keyframe[0];
        internal int m_PreWrap, m_PostWrap;   // 2 = Loop, 4 = PingPong, else clamp
        public AnimationCurve() { }
        public AnimationCurve(params Keyframe[] keys) { m_Keys = keys ?? new Keyframe[0]; Sort(); }
        public Keyframe[] keys { get { return (Keyframe[])m_Keys.Clone(); } set { m_Keys = value ?? new Keyframe[0]; Sort(); } }
        public int length { get { return m_Keys.Length; } }
        public Keyframe this[int i] { get { return m_Keys[i]; } }
        void Sort() { Array.Sort(m_Keys, (a, b) => a.time.CompareTo(b.time)); }
        public int AddKey(float time, float value) { return AddKey(new Keyframe(time, value)); }
        public int AddKey(Keyframe k)
        {
            var l = new List<Keyframe>(m_Keys); l.Add(k); m_Keys = l.ToArray(); Sort();
            return Array.IndexOf(m_Keys, k);
        }
        public int MoveKey(int i, Keyframe k) { m_Keys[i] = k; Sort(); return Array.IndexOf(m_Keys, k); }
        public void RemoveKey(int i) { var l = new List<Keyframe>(m_Keys); l.RemoveAt(i); m_Keys = l.ToArray(); }
        public static AnimationCurve Linear(float t0, float v0, float t1, float v1)
        {
            float s = (v1 - v0) / (t1 - t0);
            return new AnimationCurve(new Keyframe(t0, v0, 0, s), new Keyframe(t1, v1, s, 0));
        }
        public static AnimationCurve EaseInOut(float t0, float v0, float t1, float v1)
        { return new AnimationCurve(new Keyframe(t0, v0, 0, 0), new Keyframe(t1, v1, 0, 0)); }
        public static AnimationCurve Constant(float t0, float t1, float v)
        { return new AnimationCurve(new Keyframe(t0, v, 0, 0), new Keyframe(t1, v, 0, 0)); }

        public float Evaluate(float t)
        {
            int n = m_Keys.Length;
            if (n == 0) return 0f;
            if (n == 1) return m_Keys[0].value;
            float t0 = m_Keys[0].time, t1 = m_Keys[n - 1].time, len = t1 - t0;
            if (t < t0) t = Wrap(t, t0, len, m_PreWrap);
            else if (t > t1) t = Wrap(t, t0, len, m_PostWrap);
            if (t <= t0) return m_Keys[0].value;
            if (t >= t1) return m_Keys[n - 1].value;
            int i = 1;
            while (m_Keys[i].time < t) i++;
            Keyframe a = m_Keys[i - 1], b = m_Keys[i];
            float dt = b.time - a.time;
            if (dt <= 0f) return b.value;
            if (float.IsInfinity(a.outTangent) || float.IsInfinity(b.inTangent)) return a.value;   // stepped
            float u = (t - a.time) / dt, u2 = u * u, u3 = u2 * u;
            float h00 = 2 * u3 - 3 * u2 + 1, h10 = u3 - 2 * u2 + u, h01 = -2 * u3 + 3 * u2, h11 = u3 - u2;
            return h00 * a.value + h10 * dt * a.outTangent + h01 * b.value + h11 * dt * b.inTangent;
        }
        static float Wrap(float t, float t0, float len, int mode)
        {
            if (len <= 0f) return t0;
            if (mode == 2) return t0 + Mathf.Repeat(t - t0, len);
            if (mode == 4) return t0 + Mathf.PingPong(t - t0, len);
            return t;
        }
        public WrapMode preWrapMode { get { return (WrapMode)m_PreWrap; } set { m_PreWrap = (int)value; } }
        public WrapMode postWrapMode { get { return (WrapMode)m_PostWrap; } set { m_PostWrap = (int)value; } }

        // serialized m_Pre/PostInfinity use Unity's internal enum (0 PingPong, 1 Repeat, 2 Clamp), not WrapMode
        static int FromInternal(int v) { return v == 0 ? (int)WrapMode.PingPong : v == 1 ? (int)WrapMode.Loop : (int)WrapMode.ClampForever; }
        internal void SetFromSerialized(Keyframe[] keys, int pre, int post) { m_Keys = keys; m_PreWrap = FromInternal(pre); m_PostWrap = FromInternal(post); Sort(); }
    }

    public enum WrapMode { Once = 1, Loop = 2, PingPong = 4, Default = 0, ClampForever = 8, Clamp = 1 }

    public partial struct GradientColorKey
    {
        public Color color; public float time;
        public GradientColorKey(Color col, float time) { color = col; this.time = time; }
    }
    public partial struct GradientAlphaKey
    {
        public float alpha; public float time;
        public GradientAlphaKey(float alpha, float time) { this.alpha = alpha; this.time = time; }
    }

    public partial class Gradient
    {
        GradientColorKey[] m_ColorKeys = { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) };
        GradientAlphaKey[] m_AlphaKeys = { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) };
        internal int m_Mode;   // 0 blend, 1 fixed
        public Gradient() { }
        public GradientColorKey[] colorKeys { get { return (GradientColorKey[])m_ColorKeys.Clone(); } set { m_ColorKeys = value; } }
        public GradientAlphaKey[] alphaKeys { get { return (GradientAlphaKey[])m_AlphaKeys.Clone(); } set { m_AlphaKeys = value; } }
        public void SetKeys(GradientColorKey[] c, GradientAlphaKey[] a) { m_ColorKeys = c; m_AlphaKeys = a; }
        public Color Evaluate(float t)
        {
            t = Mathf.Clamp01(t);
            Color c = m_ColorKeys[0].color;
            for (int i = 0; i < m_ColorKeys.Length; i++)
            {
                if (t <= m_ColorKeys[i].time)
                {
                    if (i == 0) { c = m_ColorKeys[0].color; break; }
                    var a = m_ColorKeys[i - 1]; var b = m_ColorKeys[i];
                    c = m_Mode == 1 ? b.color : Color.Lerp(a.color, b.color, (t - a.time) / Mathf.Max(1e-6f, b.time - a.time));
                    break;
                }
                c = m_ColorKeys[i].color;
            }
            float al = m_AlphaKeys[0].alpha;
            for (int i = 0; i < m_AlphaKeys.Length; i++)
            {
                if (t <= m_AlphaKeys[i].time)
                {
                    if (i == 0) { al = m_AlphaKeys[0].alpha; break; }
                    var a = m_AlphaKeys[i - 1]; var b = m_AlphaKeys[i];
                    al = m_Mode == 1 ? b.alpha : Mathf.Lerp(a.alpha, b.alpha, (t - a.time) / Mathf.Max(1e-6f, b.time - a.time));
                    break;
                }
                al = m_AlphaKeys[i].alpha;
            }
            c.a = al;
            return c;
        }
    }

    public partial class RectOffset
    {
        int m_Left, m_Right, m_Top, m_Bottom;   // serialized names
        public RectOffset() { }
        public RectOffset(int left, int right, int top, int bottom) { m_Left = left; m_Right = right; m_Top = top; m_Bottom = bottom; }
        public int left { get { return m_Left; } set { m_Left = value; } }
        public int right { get { return m_Right; } set { m_Right = value; } }
        public int top { get { return m_Top; } set { m_Top = value; } }
        public int bottom { get { return m_Bottom; } set { m_Bottom = value; } }
        public int horizontal { get { return left + right; } }
        public int vertical { get { return top + bottom; } }
    }

    public partial class Animator
    {
        static readonly uint[] crcTable = BuildCrc();
        static uint[] BuildCrc()
        {
            var t = new uint[256];
            for (uint i = 0; i < 256; i++) { uint c = i; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; t[i] = c; }
            return t;
        }
        /// Unity hashes state/parameter names with CRC32; keeping that makes hashes match
        /// the ones stored in the controller data.
        public static int StringToHash(string name)
        {
            uint c = 0xFFFFFFFFu;
            foreach (char ch in name) c = crcTable[(c ^ (byte)ch) & 0xFF] ^ (c >> 8);
            return (int)(c ^ 0xFFFFFFFFu);
        }
    }
}
