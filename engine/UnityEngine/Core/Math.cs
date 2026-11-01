// Clean-room implementations of Unity's math value types (public API semantics).
using System;
using System.Runtime.CompilerServices;

namespace UnityEngine
{
    public partial struct Mathf
    {
        public const float PI = 3.14159274f;
        public const float Infinity = float.PositiveInfinity;
        public const float NegativeInfinity = float.NegativeInfinity;
        public const float Deg2Rad = PI / 180f;
        public const float Rad2Deg = 180f / PI;
        public static readonly float Epsilon = float.Epsilon;

        public static float Sin(float f) { return (float)Math.Sin(f); }
        public static float Cos(float f) { return (float)Math.Cos(f); }
        public static float Tan(float f) { return (float)Math.Tan(f); }
        public static float Asin(float f) { return (float)Math.Asin(f); }
        public static float Acos(float f) { return (float)Math.Acos(f); }
        public static float Atan(float f) { return (float)Math.Atan(f); }
        public static float Atan2(float y, float x) { return (float)Math.Atan2(y, x); }
        public static float Sqrt(float f) { return (float)Math.Sqrt(f); }
        public static float Abs(float f) { return Math.Abs(f); }
        public static int Abs(int v) { return Math.Abs(v); }
        public static float Min(float a, float b) { return a < b ? a : b; }
        public static int Min(int a, int b) { return a < b ? a : b; }
        public static float Min(params float[] v) { float m = v[0]; for (int i = 1; i < v.Length; i++) if (v[i] < m) m = v[i]; return m; }
        public static float Max(float a, float b) { return a > b ? a : b; }
        public static int Max(int a, int b) { return a > b ? a : b; }
        public static float Max(params float[] v) { float m = v[0]; for (int i = 1; i < v.Length; i++) if (v[i] > m) m = v[i]; return m; }
        public static float Pow(float f, float p) { return (float)Math.Pow(f, p); }
        public static float Exp(float p) { return (float)Math.Exp(p); }
        public static float Log(float f) { return (float)Math.Log(f); }
        public static float Log(float f, float p) { return (float)Math.Log(f, p); }
        public static float Log10(float f) { return (float)Math.Log10(f); }
        public static float Ceil(float f) { return (float)Math.Ceiling(f); }
        public static float Floor(float f) { return (float)Math.Floor(f); }
        public static float Round(float f) { return (float)Math.Round(f); }
        public static int CeilToInt(float f) { return (int)Math.Ceiling(f); }
        public static int FloorToInt(float f) { return (int)Math.Floor(f); }
        public static int RoundToInt(float f) { return (int)Math.Round(f); }
        public static float Sign(float f) { return f >= 0f ? 1f : -1f; }
        public static float Clamp(float v, float min, float max) { return v < min ? min : (v > max ? max : v); }
        public static int Clamp(int v, int min, int max) { return v < min ? min : (v > max ? max : v); }
        public static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }
        public static float Lerp(float a, float b, float t) { return a + (b - a) * Clamp01(t); }
        public static float LerpUnclamped(float a, float b, float t) { return a + (b - a) * t; }
        public static float InverseLerp(float a, float b, float v) { return a != b ? Clamp01((v - a) / (b - a)) : 0f; }
        public static float Repeat(float t, float length) { return Clamp(t - Floor(t / length) * length, 0f, length); }
        public static float PingPong(float t, float length) { t = Repeat(t, length * 2f); return length - Abs(t - length); }
        public static float DeltaAngle(float current, float target) { float d = Repeat(target - current, 360f); if (d > 180f) d -= 360f; return d; }
        public static float LerpAngle(float a, float b, float t) { float d = Repeat(b - a, 360f); if (d > 180f) d -= 360f; return a + d * Clamp01(t); }
        public static float MoveTowards(float c, float t, float maxDelta) { return Abs(t - c) <= maxDelta ? t : c + Sign(t - c) * maxDelta; }
        public static float SmoothStep(float from, float to, float t) { t = Clamp01(t); t = -2f * t * t * t + 3f * t * t; return to * t + from * (1f - t); }
        public static bool Approximately(float a, float b) { return Abs(b - a) < Max(1E-06f * Max(Abs(a), Abs(b)), Epsilon * 8f); }
        public static bool IsPowerOfTwo(int v) { return v > 0 && (v & (v - 1)) == 0; }
        public static int NextPowerOfTwo(int v) { int p = 1; while (p < v) p <<= 1; return p; }
        public static float PerlinNoise(float x, float y) { return KNL.Noise.Perlin(x, y); }
        public static float SmoothDamp(float current, float target, ref float currentVelocity, float smoothTime, float maxSpeed, float deltaTime)
        {
            smoothTime = Max(0.0001f, smoothTime);
            float num = 2f / smoothTime, num2 = num * deltaTime;
            float num3 = 1f / (1f + num2 + 0.48f * num2 * num2 + 0.235f * num2 * num2 * num2);
            float num4 = current - target, num5 = target, num6 = maxSpeed * smoothTime;
            num4 = Clamp(num4, -num6, num6);
            target = current - num4;
            float num7 = (currentVelocity + num * num4) * deltaTime;
            currentVelocity = (currentVelocity - num * num7) * num3;
            float num8 = target + (num4 + num7) * num3;
            if (num5 - current > 0f == num8 > num5) { num8 = num5; currentVelocity = (num8 - num5) / deltaTime; }
            return num8;
        }
        public static float SmoothDamp(float current, float target, ref float currentVelocity, float smoothTime, float maxSpeed)
        { return SmoothDamp(current, target, ref currentVelocity, smoothTime, maxSpeed, Time.deltaTime); }
        public static float SmoothDamp(float current, float target, ref float currentVelocity, float smoothTime)
        { return SmoothDamp(current, target, ref currentVelocity, smoothTime, Infinity, Time.deltaTime); }
    }

    public partial struct Vector2
    {
        public float x, y;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public float this[int i] { get { return i == 0 ? x : y; } set { if (i == 0) x = value; else y = value; } }
        public static Vector2 zero { get { return new Vector2(0, 0); } }
        public static Vector2 one { get { return new Vector2(1, 1); } }
        public static Vector2 up { get { return new Vector2(0, 1); } }
        public static Vector2 down { get { return new Vector2(0, -1); } }
        public static Vector2 left { get { return new Vector2(-1, 0); } }
        public static Vector2 right { get { return new Vector2(1, 0); } }
        public float magnitude { get { return Mathf.Sqrt(x * x + y * y); } }
        public float sqrMagnitude { get { return x * x + y * y; } }
        public static float SqrMagnitude(Vector2 v) { return v.x * v.x + v.y * v.y; }
        public Vector2 normalized { get { float m = magnitude; return m > 1E-05f ? this / m : zero; } }
        public void Normalize() { this = normalized; }
        public void Set(float nx, float ny) { x = nx; y = ny; }
        public static float Distance(Vector2 a, Vector2 b) { return (a - b).magnitude; }
        public static float Dot(Vector2 a, Vector2 b) { return a.x * b.x + a.y * b.y; }
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) { t = Mathf.Clamp01(t); return new Vector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t); }
        public static Vector2 LerpUnclamped(Vector2 a, Vector2 b, float t) { return new Vector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t); }
        public static Vector2 MoveTowards(Vector2 c, Vector2 t, float d) { Vector2 v = t - c; float m = v.magnitude; return (m <= d || m == 0f) ? t : c + v / m * d; }
        public static Vector2 Scale(Vector2 a, Vector2 b) { return new Vector2(a.x * b.x, a.y * b.y); }
        public static Vector2 Min(Vector2 a, Vector2 b) { return new Vector2(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y)); }
        public static Vector2 Max(Vector2 a, Vector2 b) { return new Vector2(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y)); }
        public static float Angle(Vector2 a, Vector2 b) { return Mathf.Acos(Mathf.Clamp(Dot(a.normalized, b.normalized), -1f, 1f)) * Mathf.Rad2Deg; }
        public static Vector2 ClampMagnitude(Vector2 v, float max) { return v.sqrMagnitude > max * max ? v.normalized * max : v; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 operator +(Vector2 a, Vector2 b) { return new Vector2(a.x + b.x, a.y + b.y); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 operator -(Vector2 a, Vector2 b) { return new Vector2(a.x - b.x, a.y - b.y); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 operator -(Vector2 a) { return new Vector2(-a.x, -a.y); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 operator *(Vector2 a, float d) { return new Vector2(a.x * d, a.y * d); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 operator *(float d, Vector2 a) { return new Vector2(a.x * d, a.y * d); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 operator /(Vector2 a, float d) { return new Vector2(a.x / d, a.y / d); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Vector2 a, Vector2 b) { return (a - b).sqrMagnitude < 9.99999944E-11f; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Vector2 a, Vector2 b) { return !(a == b); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Vector2(Vector3 v) { return new Vector2(v.x, v.y); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Vector3(Vector2 v) { return new Vector3(v.x, v.y, 0f); }
        public override bool Equals(object o) { return o is Vector2 && x.Equals(((Vector2)o).x) && y.Equals(((Vector2)o).y); }
        public override int GetHashCode() { return x.GetHashCode() ^ (y.GetHashCode() << 2); }
        public override string ToString() { return string.Format("({0:F1}, {1:F1})", x, y); }
        public string ToString(string f) { return string.Format("({0}, {1})", x.ToString(f), y.ToString(f)); }
    }

    public partial struct Vector3
    {
        public float x, y, z;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3(float x, float y) { this.x = x; this.y = y; z = 0f; }
        public float this[int i] { get { return i == 0 ? x : (i == 1 ? y : z); } set { if (i == 0) x = value; else if (i == 1) y = value; else z = value; } }
        public static Vector3 zero { get { return new Vector3(0, 0, 0); } }
        public static Vector3 one { get { return new Vector3(1, 1, 1); } }
        public static Vector3 up { get { return new Vector3(0, 1, 0); } }
        public static Vector3 down { get { return new Vector3(0, -1, 0); } }
        public static Vector3 left { get { return new Vector3(-1, 0, 0); } }
        public static Vector3 right { get { return new Vector3(1, 0, 0); } }
        public static Vector3 forward { get { return new Vector3(0, 0, 1); } }
        public static Vector3 back { get { return new Vector3(0, 0, -1); } }
        public float magnitude { get { return Mathf.Sqrt(x * x + y * y + z * z); } }
        public float sqrMagnitude { get { return x * x + y * y + z * z; } }
        public static float SqrMagnitude(Vector3 v) { return v.x * v.x + v.y * v.y + v.z * v.z; }
        public Vector3 normalized { get { return Normalize(this); } }
        public void Normalize() { this = Normalize(this); }
        public void Set(float nx, float ny, float nz) { x = nx; y = ny; z = nz; }
        public static Vector3 Normalize(Vector3 v) { float m = v.magnitude; return m > 1E-05f ? v / m : zero; }
        public static float Magnitude(Vector3 v) { return v.magnitude; }
        public static float Distance(Vector3 a, Vector3 b) { return (a - b).magnitude; }
        public static float Dot(Vector3 a, Vector3 b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
        public static Vector3 Cross(Vector3 a, Vector3 b) { return new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x); }
        public static Vector3 Scale(Vector3 a, Vector3 b) { return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z); }
        public void Scale(Vector3 s) { x *= s.x; y *= s.y; z *= s.z; }
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { t = Mathf.Clamp01(t); return LerpUnclamped(a, b, t); }
        public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) { return new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t); }
        public static Vector3 MoveTowards(Vector3 c, Vector3 t, float d) { Vector3 v = t - c; float m = v.magnitude; return (m <= d || m < float.Epsilon) ? t : c + v / m * d; }
        public static Vector3 Min(Vector3 a, Vector3 b) { return new Vector3(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Min(a.z, b.z)); }
        public static Vector3 Max(Vector3 a, Vector3 b) { return new Vector3(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y), Mathf.Max(a.z, b.z)); }
        public static Vector3 Project(Vector3 v, Vector3 n) { float d = Dot(n, n); return d < Mathf.Epsilon ? zero : n * Dot(v, n) / d; }
        public static float Angle(Vector3 a, Vector3 b) { return Mathf.Acos(Mathf.Clamp(Dot(a.normalized, b.normalized), -1f, 1f)) * Mathf.Rad2Deg; }
        public static Vector3 ClampMagnitude(Vector3 v, float max) { return v.sqrMagnitude > max * max ? v.normalized * max : v; }
        public static Vector3 SmoothDamp(Vector3 current, Vector3 target, ref Vector3 vel, float smoothTime, float maxSpeed, float dt)
        {
            float vx = vel.x, vy = vel.y, vz = vel.z;
            var r = new Vector3(Mathf.SmoothDamp(current.x, target.x, ref vx, smoothTime, maxSpeed, dt),
                                Mathf.SmoothDamp(current.y, target.y, ref vy, smoothTime, maxSpeed, dt),
                                Mathf.SmoothDamp(current.z, target.z, ref vz, smoothTime, maxSpeed, dt));
            vel = new Vector3(vx, vy, vz);
            return r;
        }
        public static Vector3 SmoothDamp(Vector3 c, Vector3 t, ref Vector3 v, float s) { return SmoothDamp(c, t, ref v, s, Mathf.Infinity, Time.deltaTime); }
        public static Vector3 SmoothDamp(Vector3 c, Vector3 t, ref Vector3 v, float s, float m) { return SmoothDamp(c, t, ref v, s, m, Time.deltaTime); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 operator -(Vector3 a, Vector3 b) { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 operator -(Vector3 a) { return new Vector3(-a.x, -a.y, -a.z); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 operator *(Vector3 a, float d) { return new Vector3(a.x * d, a.y * d, a.z * d); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 operator *(float d, Vector3 a) { return new Vector3(a.x * d, a.y * d, a.z * d); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 operator /(Vector3 a, float d) { return new Vector3(a.x / d, a.y / d, a.z / d); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Vector3 a, Vector3 b) { return (a - b).sqrMagnitude < 9.99999944E-11f; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Vector3 a, Vector3 b) { return !(a == b); }
        public override bool Equals(object o) { if (!(o is Vector3)) return false; var v = (Vector3)o; return x.Equals(v.x) && y.Equals(v.y) && z.Equals(v.z); }
        public override int GetHashCode() { return x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2); }
        public override string ToString() { return string.Format("({0:F1}, {1:F1}, {2:F1})", x, y, z); }
        public string ToString(string f) { return string.Format("({0}, {1}, {2})", x.ToString(f), y.ToString(f), z.ToString(f)); }
    }

    public partial struct Vector4
    {
        public float sqrMagnitude { get { return x * x + y * y + z * z + w * w; } }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 operator /(Vector4 a, float d) { return new Vector4(a.x / d, a.y / d, a.z / d, a.w / d); }
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public Vector4(float x, float y, float z) { this.x = x; this.y = y; this.z = z; w = 0f; }
        public float this[int i] { get { switch (i) { case 0: return x; case 1: return y; case 2: return z; default: return w; } } set { switch (i) { case 0: x = value; break; case 1: y = value; break; case 2: z = value; break; default: w = value; break; } } }
        public static Vector4 zero { get { return new Vector4(0, 0, 0, 0); } }
        public static Vector4 one { get { return new Vector4(1, 1, 1, 1); } }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 operator +(Vector4 a, Vector4 b) { return new Vector4(a.x + b.x, a.y + b.y, a.z + b.z, a.w + b.w); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 operator -(Vector4 a, Vector4 b) { return new Vector4(a.x - b.x, a.y - b.y, a.z - b.z, a.w - b.w); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 operator *(Vector4 a, float d) { return new Vector4(a.x * d, a.y * d, a.z * d, a.w * d); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Vector4(Vector3 v) { return new Vector4(v.x, v.y, v.z, 0f); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Vector3(Vector4 v) { return new Vector3(v.x, v.y, v.z); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Vector4(Vector2 v) { return new Vector4(v.x, v.y, 0f, 0f); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Vector4 a, Vector4 b) { var d = a - b; return d.x * d.x + d.y * d.y + d.z * d.z + d.w * d.w < 9.99999944E-11f; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Vector4 a, Vector4 b) { return !(a == b); }
        public override bool Equals(object o) { return o is Vector4 && this == (Vector4)o; }
        public override int GetHashCode() { return x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2) ^ (w.GetHashCode() >> 1); }
        public override string ToString() { return string.Format("({0:F1}, {1:F1}, {2:F1}, {3:F1})", x, y, z, w); }
    }

    public partial struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity { get { return new Quaternion(0, 0, 0, 1); } }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion operator *(Quaternion l, Quaternion r)
        {
            return new Quaternion(l.w * r.x + l.x * r.w + l.y * r.z - l.z * r.y,
                                  l.w * r.y + l.y * r.w + l.z * r.x - l.x * r.z,
                                  l.w * r.z + l.z * r.w + l.x * r.y - l.y * r.x,
                                  l.w * r.w - l.x * r.x - l.y * r.y - l.z * r.z);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 operator *(Quaternion q, Vector3 p)
        {
            float x2 = q.x * 2f, y2 = q.y * 2f, z2 = q.z * 2f;
            float xx = q.x * x2, yy = q.y * y2, zz = q.z * z2, xy = q.x * y2, xz = q.x * z2, yz = q.y * z2;
            float wx = q.w * x2, wy = q.w * y2, wz = q.w * z2;
            return new Vector3((1f - (yy + zz)) * p.x + (xy - wz) * p.y + (xz + wy) * p.z,
                               (xy + wz) * p.x + (1f - (xx + zz)) * p.y + (yz - wx) * p.z,
                               (xz - wy) * p.x + (yz + wx) * p.y + (1f - (xx + yy)) * p.z);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Quaternion a, Quaternion b) { return Dot(a, b) > 0.999999f; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Quaternion a, Quaternion b) { return !(a == b); }
        public static float Dot(Quaternion a, Quaternion b) { return a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w; }
        public static Quaternion Inverse(Quaternion q) { float n = Dot(q, q); return n > 0 ? new Quaternion(-q.x / n, -q.y / n, -q.z / n, q.w / n) : q; }
        public static Quaternion AngleAxis(float angle, Vector3 axis)
        {
            axis = axis.normalized; float h = angle * Mathf.Deg2Rad * 0.5f, s = Mathf.Sin(h);
            return new Quaternion(axis.x * s, axis.y * s, axis.z * s, Mathf.Cos(h));
        }
        // Unity convention: rotation applied Z, then X, then Y (q = qy * qx * qz)
        public static Quaternion Euler(float ex, float ey, float ez)
        {
            return AngleAxis(ey, Vector3.up) * AngleAxis(ex, Vector3.right) * AngleAxis(ez, Vector3.forward);
        }
        public static Quaternion Euler(Vector3 e) { return Euler(e.x, e.y, e.z); }
        public Vector3 eulerAngles
        {
            get
            {
                // inverse of Euler(): Y-X-Z intrinsic
                float sinx = 2f * (w * x - y * z);
                float ex = Mathf.Abs(sinx) >= 1f ? Mathf.Sign(sinx) * 90f : Mathf.Asin(sinx) * Mathf.Rad2Deg;
                float ey = Mathf.Atan2(2f * (w * y + x * z), 1f - 2f * (x * x + y * y)) * Mathf.Rad2Deg;
                float ez = Mathf.Atan2(2f * (w * z + x * y), 1f - 2f * (x * x + z * z)) * Mathf.Rad2Deg;
                return new Vector3(Wrap(ex), Wrap(ey), Wrap(ez));
            }
            set { this = Euler(value); }
        }
        static float Wrap(float a) { a %= 360f; return a < 0 ? a + 360f : a; }
        public static float Angle(Quaternion a, Quaternion b) { float d = Mathf.Min(Mathf.Abs(Dot(a, b)), 1f); return d > 0.999999f ? 0f : Mathf.Acos(d) * 2f * Mathf.Rad2Deg; }
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) { return SlerpUnclamped(a, b, Mathf.Clamp01(t)); }
        public static Quaternion SlerpUnclamped(Quaternion a, Quaternion b, float t)
        {
            float d = Dot(a, b);
            if (d < 0f) { d = -d; b = new Quaternion(-b.x, -b.y, -b.z, -b.w); }
            float s0, s1;
            if (d > 0.9995f) { s0 = 1f - t; s1 = t; }
            else { float th = Mathf.Acos(d), st = Mathf.Sin(th); s0 = Mathf.Sin((1f - t) * th) / st; s1 = Mathf.Sin(t * th) / st; }
            return Normalize(new Quaternion(a.x * s0 + b.x * s1, a.y * s0 + b.y * s1, a.z * s0 + b.z * s1, a.w * s0 + b.w * s1));
        }
        public static Quaternion Lerp(Quaternion a, Quaternion b, float t)
        {
            t = Mathf.Clamp01(t); if (Dot(a, b) < 0f) b = new Quaternion(-b.x, -b.y, -b.z, -b.w);
            return Normalize(new Quaternion(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t));
        }
        public static Quaternion RotateTowards(Quaternion from, Quaternion to, float maxDeg) { float a = Angle(from, to); return a == 0f ? to : SlerpUnclamped(from, to, Mathf.Min(1f, maxDeg / a)); }
        static Quaternion Normalize(Quaternion q) { float m = Mathf.Sqrt(Dot(q, q)); return m < Mathf.Epsilon ? identity : new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m); }
        public static Quaternion LookRotation(Vector3 forward, Vector3 up)
        {
            forward = forward.normalized; Vector3 right = Vector3.Cross(up, forward).normalized; up = Vector3.Cross(forward, right);
            float m00 = right.x, m01 = up.x, m02 = forward.x, m10 = right.y, m11 = up.y, m12 = forward.y, m20 = right.z, m21 = up.z, m22 = forward.z;
            float tr = m00 + m11 + m22; Quaternion q;
            if (tr > 0f) { float s = Mathf.Sqrt(tr + 1f) * 2f; q = new Quaternion((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25f * s); }
            else if (m00 > m11 && m00 > m22) { float s = Mathf.Sqrt(1f + m00 - m11 - m22) * 2f; q = new Quaternion(0.25f * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s); }
            else if (m11 > m22) { float s = Mathf.Sqrt(1f + m11 - m00 - m22) * 2f; q = new Quaternion((m01 + m10) / s, 0.25f * s, (m12 + m21) / s, (m02 - m20) / s); }
            else { float s = Mathf.Sqrt(1f + m22 - m00 - m11) * 2f; q = new Quaternion((m02 + m20) / s, (m12 + m21) / s, 0.25f * s, (m10 - m01) / s); }
            return Normalize(q);
        }
        public static Quaternion LookRotation(Vector3 forward) { return LookRotation(forward, Vector3.up); }
        public static Quaternion FromToRotation(Vector3 a, Vector3 b)
        {
            a = a.normalized; b = b.normalized; float d = Vector3.Dot(a, b);
            if (d < -0.999999f) { Vector3 ax = Vector3.Cross(Vector3.right, a); if (ax.sqrMagnitude < 1e-6f) ax = Vector3.Cross(Vector3.up, a); return AngleAxis(180f, ax); }
            Vector3 c = Vector3.Cross(a, b); return Normalize(new Quaternion(c.x, c.y, c.z, 1f + d));
        }
        public override bool Equals(object o) { if (!(o is Quaternion)) return false; var q = (Quaternion)o; return x.Equals(q.x) && y.Equals(q.y) && z.Equals(q.z) && w.Equals(q.w); }
        public override int GetHashCode() { return x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2) ^ (w.GetHashCode() >> 1); }
        public override string ToString() { return string.Format("({0:F1}, {1:F1}, {2:F1}, {3:F1})", x, y, z, w); }
    }

    public partial struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; a = 1f; }
        public float this[int i] { get { switch (i) { case 0: return r; case 1: return g; case 2: return b; default: return a; } } set { switch (i) { case 0: r = value; break; case 1: g = value; break; case 2: b = value; break; default: a = value; break; } } }
        public static Color white { get { return new Color(1, 1, 1, 1); } }
        public static Color black { get { return new Color(0, 0, 0, 1); } }
        public static Color clear { get { return new Color(0, 0, 0, 0); } }
        public static Color red { get { return new Color(1, 0, 0, 1); } }
        public static Color green { get { return new Color(0, 1, 0, 1); } }
        public static Color blue { get { return new Color(0, 0, 1, 1); } }
        public static Color yellow { get { return new Color(1, 0.921568632f, 0.0156862754f, 1); } }
        public static Color cyan { get { return new Color(0, 1, 1, 1); } }
        public static Color magenta { get { return new Color(1, 0, 1, 1); } }
        public static Color gray { get { return new Color(0.5f, 0.5f, 0.5f, 1); } }
        public static Color grey { get { return gray; } }
        public float grayscale { get { return 0.299f * r + 0.587f * g + 0.114f * b; } }
        public static Color Lerp(Color a, Color b, float t) { t = Mathf.Clamp01(t); return LerpUnclamped(a, b, t); }
        public static Color LerpUnclamped(Color a, Color b, float t) { return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color operator +(Color a, Color b) { return new Color(a.r + b.r, a.g + b.g, a.b + b.b, a.a + b.a); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color operator -(Color a, Color b) { return new Color(a.r - b.r, a.g - b.g, a.b - b.b, a.a - b.a); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color operator *(Color a, Color b) { return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color operator *(Color a, float d) { return new Color(a.r * d, a.g * d, a.b * d, a.a * d); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color operator *(float d, Color a) { return new Color(a.r * d, a.g * d, a.b * d, a.a * d); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color operator /(Color a, float d) { return new Color(a.r / d, a.g / d, a.b / d, a.a / d); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Color a, Color b) { return (Vector4)a == (Vector4)b; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Color a, Color b) { return !(a == b); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Vector4(Color c) { return new Vector4(c.r, c.g, c.b, c.a); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Color(Vector4 v) { return new Color(v.x, v.y, v.z, v.w); }
        public static void RGBToHSV(Color c, out float H, out float S, out float V)
        {
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), min = Mathf.Min(c.r, Mathf.Min(c.g, c.b)), d = max - min;
            V = max; S = max > 0 ? d / max : 0f;
            if (d <= 0f) { H = 0f; return; }
            if (max == c.r) H = (c.g - c.b) / d; else if (max == c.g) H = 2f + (c.b - c.r) / d; else H = 4f + (c.r - c.g) / d;
            H /= 6f; if (H < 0f) H += 1f;
        }
        public static Color HSVToRGB(float H, float S, float V)
        {
            if (S <= 0f) return new Color(V, V, V, 1);
            float h = (H - Mathf.Floor(H)) * 6f; int i = (int)h; float f = h - i;
            float p = V * (1 - S), q = V * (1 - S * f), t = V * (1 - S * (1 - f));
            switch (i) { case 0: return new Color(V, t, p); case 1: return new Color(q, V, p); case 2: return new Color(p, V, t); case 3: return new Color(p, q, V); case 4: return new Color(t, p, V); default: return new Color(V, p, q); }
        }
        public override bool Equals(object o) { return o is Color && this == (Color)o; }
        public override int GetHashCode() { return ((Vector4)this).GetHashCode(); }
        public override string ToString() { return string.Format("RGBA({0:F3}, {1:F3}, {2:F3}, {3:F3})", r, g, b, a); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal uint ToRGBA32()
        {
            uint R = (uint)(Mathf.Clamp01(r) * 255f + 0.5f), G = (uint)(Mathf.Clamp01(g) * 255f + 0.5f);
            uint B = (uint)(Mathf.Clamp01(b) * 255f + 0.5f), A = (uint)(Mathf.Clamp01(a) * 255f + 0.5f);
            return R | (G << 8) | (B << 16) | (A << 24);
        }
    }

    public partial struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Color32(Color c) { return new Color32((byte)(Mathf.Clamp01(c.r) * 255f), (byte)(Mathf.Clamp01(c.g) * 255f), (byte)(Mathf.Clamp01(c.b) * 255f), (byte)(Mathf.Clamp01(c.a) * 255f)); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Color(Color32 c) { return new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f); }
        public static Color32 Lerp(Color32 a, Color32 b, float t) { t = Mathf.Clamp01(t); return new Color32((byte)(a.r + (b.r - a.r) * t), (byte)(a.g + (b.g - a.g) * t), (byte)(a.b + (b.b - a.b) * t), (byte)(a.a + (b.a - a.a) * t)); }
        public override string ToString() { return string.Format("RGBA({0}, {1}, {2}, {3})", r, g, b, a); }
    }

    public partial struct Rect
    {
        float m_XMin, m_YMin, m_Width, m_Height;
        public Rect(float x, float y, float width, float height) { m_XMin = x; m_YMin = y; m_Width = width; m_Height = height; }
        public Rect(Vector2 pos, Vector2 size) { m_XMin = pos.x; m_YMin = pos.y; m_Width = size.x; m_Height = size.y; }
        public float x { get { return m_XMin; } set { m_XMin = value; } }
        public float y { get { return m_YMin; } set { m_YMin = value; } }
        public float width { get { return m_Width; } set { m_Width = value; } }
        public float height { get { return m_Height; } set { m_Height = value; } }
        public float xMin { get { return m_XMin; } set { float xm = xMax; m_XMin = value; m_Width = xm - m_XMin; } }
        public float yMin { get { return m_YMin; } set { float ym = yMax; m_YMin = value; m_Height = ym - m_YMin; } }
        public float xMax { get { return m_Width + m_XMin; } set { m_Width = value - m_XMin; } }
        public float yMax { get { return m_Height + m_YMin; } set { m_Height = value - m_YMin; } }
        public Vector2 position { get { return new Vector2(m_XMin, m_YMin); } set { m_XMin = value.x; m_YMin = value.y; } }
        public Vector2 size { get { return new Vector2(m_Width, m_Height); } set { m_Width = value.x; m_Height = value.y; } }
        public Vector2 center { get { return new Vector2(x + m_Width / 2f, y + m_Height / 2f); } set { m_XMin = value.x - m_Width / 2f; m_YMin = value.y - m_Height / 2f; } }
        public Vector2 min { get { return new Vector2(xMin, yMin); } set { xMin = value.x; yMin = value.y; } }
        public Vector2 max { get { return new Vector2(xMax, yMax); } set { xMax = value.x; yMax = value.y; } }
        public static Rect zero { get { return new Rect(0, 0, 0, 0); } }
        public static Rect MinMaxRect(float xmin, float ymin, float xmax, float ymax) { return new Rect(xmin, ymin, xmax - xmin, ymax - ymin); }
        public bool Contains(Vector2 p) { return p.x >= xMin && p.x < xMax && p.y >= yMin && p.y < yMax; }
        public bool Contains(Vector3 p) { return Contains((Vector2)p); }
        public bool Overlaps(Rect o) { return o.xMax > xMin && o.xMin < xMax && o.yMax > yMin && o.yMin < yMax; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Rect a, Rect b) { return a.x == b.x && a.y == b.y && a.width == b.width && a.height == b.height; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Rect a, Rect b) { return !(a == b); }
        public override bool Equals(object o) { return o is Rect && this == (Rect)o; }
        public override int GetHashCode() { return x.GetHashCode() ^ (width.GetHashCode() << 2) ^ (y.GetHashCode() >> 2) ^ (height.GetHashCode() >> 1); }
        public override string ToString() { return string.Format("(x:{0:F2}, y:{1:F2}, width:{2:F2}, height:{3:F2})", x, y, width, height); }
    }

    public partial struct Bounds
    {
        Vector3 m_Center, m_Extents;
        public Bounds(Vector3 center, Vector3 size) { m_Center = center; m_Extents = size * 0.5f; }
        public Vector3 center { get { return m_Center; } set { m_Center = value; } }
        public Vector3 extents { get { return m_Extents; } set { m_Extents = value; } }
        public Vector3 size { get { return m_Extents * 2f; } set { m_Extents = value * 0.5f; } }
        public Vector3 min { get { return m_Center - m_Extents; } set { SetMinMax(value, max); } }
        public Vector3 max { get { return m_Center + m_Extents; } set { SetMinMax(min, value); } }
        public void SetMinMax(Vector3 mn, Vector3 mx) { m_Extents = (mx - mn) * 0.5f; m_Center = mn + m_Extents; }
        public void Encapsulate(Vector3 p) { SetMinMax(Vector3.Min(min, p), Vector3.Max(max, p)); }
        public void Encapsulate(Bounds b) { Encapsulate(b.center - b.extents); Encapsulate(b.center + b.extents); }
        public void Expand(float a) { a *= 0.5f; m_Extents += new Vector3(a, a, a); }
        public bool Contains(Vector3 p) { var mn = min; var mx = max; return p.x >= mn.x && p.x <= mx.x && p.y >= mn.y && p.y <= mx.y && p.z >= mn.z && p.z <= mx.z; }
        public bool Intersects(Bounds b) { var a0 = min; var a1 = max; var b0 = b.min; var b1 = b.max; return a0.x <= b1.x && a1.x >= b0.x && a0.y <= b1.y && a1.y >= b0.y && a0.z <= b1.z && a1.z >= b0.z; }
        public Vector3 ClosestPoint(Vector3 p) { return Vector3.Max(min, Vector3.Min(max, p)); }
        public override string ToString() { return string.Format("Center: {0}, Extents: {1}", m_Center, m_Extents); }
    }

    public partial struct Matrix4x4
    {
        public float m00, m10, m20, m30, m01, m11, m21, m31, m02, m12, m22, m32, m03, m13, m23, m33;
        public static Matrix4x4 identity { get { var m = new Matrix4x4(); m.m00 = m.m11 = m.m22 = m.m33 = 1f; return m; } }
        public static Matrix4x4 zero { get { return new Matrix4x4(); } }
        public float this[int row, int col]
        {
            get { return this[row + col * 4]; }
            set { this[row + col * 4] = value; }
        }
        public float this[int i]
        {
            get { switch (i) { case 0: return m00; case 1: return m10; case 2: return m20; case 3: return m30; case 4: return m01; case 5: return m11; case 6: return m21; case 7: return m31; case 8: return m02; case 9: return m12; case 10: return m22; case 11: return m32; case 12: return m03; case 13: return m13; case 14: return m23; default: return m33; } }
            set { switch (i) { case 0: m00 = value; break; case 1: m10 = value; break; case 2: m20 = value; break; case 3: m30 = value; break; case 4: m01 = value; break; case 5: m11 = value; break; case 6: m21 = value; break; case 7: m31 = value; break; case 8: m02 = value; break; case 9: m12 = value; break; case 10: m22 = value; break; case 11: m32 = value; break; case 12: m03 = value; break; case 13: m13 = value; break; case 14: m23 = value; break; default: m33 = value; break; } }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix4x4 operator *(Matrix4x4 a, Matrix4x4 b)
        {
            return new Matrix4x4 {
                m00 = a.m00 * b.m00 + a.m01 * b.m10 + a.m02 * b.m20 + a.m03 * b.m30,
                m10 = a.m10 * b.m00 + a.m11 * b.m10 + a.m12 * b.m20 + a.m13 * b.m30,
                m20 = a.m20 * b.m00 + a.m21 * b.m10 + a.m22 * b.m20 + a.m23 * b.m30,
                m30 = a.m30 * b.m00 + a.m31 * b.m10 + a.m32 * b.m20 + a.m33 * b.m30,
                m01 = a.m00 * b.m01 + a.m01 * b.m11 + a.m02 * b.m21 + a.m03 * b.m31,
                m11 = a.m10 * b.m01 + a.m11 * b.m11 + a.m12 * b.m21 + a.m13 * b.m31,
                m21 = a.m20 * b.m01 + a.m21 * b.m11 + a.m22 * b.m21 + a.m23 * b.m31,
                m31 = a.m30 * b.m01 + a.m31 * b.m11 + a.m32 * b.m21 + a.m33 * b.m31,
                m02 = a.m00 * b.m02 + a.m01 * b.m12 + a.m02 * b.m22 + a.m03 * b.m32,
                m12 = a.m10 * b.m02 + a.m11 * b.m12 + a.m12 * b.m22 + a.m13 * b.m32,
                m22 = a.m20 * b.m02 + a.m21 * b.m12 + a.m22 * b.m22 + a.m23 * b.m32,
                m32 = a.m30 * b.m02 + a.m31 * b.m12 + a.m32 * b.m22 + a.m33 * b.m32,
                m03 = a.m00 * b.m03 + a.m01 * b.m13 + a.m02 * b.m23 + a.m03 * b.m33,
                m13 = a.m10 * b.m03 + a.m11 * b.m13 + a.m12 * b.m23 + a.m13 * b.m33,
                m23 = a.m20 * b.m03 + a.m21 * b.m13 + a.m22 * b.m23 + a.m23 * b.m33,
                m33 = a.m30 * b.m03 + a.m31 * b.m13 + a.m32 * b.m23 + a.m33 * b.m33,
            };
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 MultiplyPoint3x4(Vector3 p) { return new Vector3(m00 * p.x + m01 * p.y + m02 * p.z + m03, m10 * p.x + m11 * p.y + m12 * p.z + m13, m20 * p.x + m21 * p.y + m22 * p.z + m23); }
        public Vector3 MultiplyPoint(Vector3 p)
        {
            var r = MultiplyPoint3x4(p); float w = m30 * p.x + m31 * p.y + m32 * p.z + m33;
            return w != 0f && w != 1f ? r / w : r;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 MultiplyVector(Vector3 v) { return new Vector3(m00 * v.x + m01 * v.y + m02 * v.z, m10 * v.x + m11 * v.y + m12 * v.z, m20 * v.x + m21 * v.y + m22 * v.z); }
        public Vector4 GetColumn(int i) { return new Vector4(this[0, i], this[1, i], this[2, i], this[3, i]); }
        public void SetColumn(int i, Vector4 c) { this[0, i] = c.x; this[1, i] = c.y; this[2, i] = c.z; this[3, i] = c.w; }
        public void SetTRS(Vector3 t, Quaternion q, Vector3 s) { this = TRS(t, q, s); }
        public static Matrix4x4 TRS(Vector3 t, Quaternion q, Vector3 s)
        {
            Vector3 cx = q * new Vector3(s.x, 0, 0), cy = q * new Vector3(0, s.y, 0), cz = q * new Vector3(0, 0, s.z);
            var m = new Matrix4x4();
            m.m00 = cx.x; m.m10 = cx.y; m.m20 = cx.z;
            m.m01 = cy.x; m.m11 = cy.y; m.m21 = cy.z;
            m.m02 = cz.x; m.m12 = cz.y; m.m22 = cz.z;
            m.m03 = t.x; m.m13 = t.y; m.m23 = t.z; m.m33 = 1f;
            return m;
        }
        public static Matrix4x4 Translate(Vector3 v) { return TRS(v, Quaternion.identity, Vector3.one); }
        public static Matrix4x4 Scale(Vector3 v) { return TRS(Vector3.zero, Quaternion.identity, v); }
        public static Matrix4x4 Ortho(float l, float r, float b, float t, float n, float f)
        {
            var m = identity; m.m00 = 2f / (r - l); m.m11 = 2f / (t - b); m.m22 = -2f / (f - n);
            m.m03 = -(r + l) / (r - l); m.m13 = -(t + b) / (t - b); m.m23 = -(f + n) / (f - n); return m;
        }
        public Matrix4x4 inverse { get { return Inverse(this); } }
        public static Matrix4x4 Inverse(Matrix4x4 m)
        {
            // general 4x4 inverse (cofactor expansion)
            float[] a = new float[16]; for (int i = 0; i < 16; i++) a[i] = m[i];
            float[] inv = new float[16];
            inv[0] = a[5] * a[10] * a[15] - a[5] * a[11] * a[14] - a[9] * a[6] * a[15] + a[9] * a[7] * a[14] + a[13] * a[6] * a[11] - a[13] * a[7] * a[10];
            inv[4] = -a[4] * a[10] * a[15] + a[4] * a[11] * a[14] + a[8] * a[6] * a[15] - a[8] * a[7] * a[14] - a[12] * a[6] * a[11] + a[12] * a[7] * a[10];
            inv[8] = a[4] * a[9] * a[15] - a[4] * a[11] * a[13] - a[8] * a[5] * a[15] + a[8] * a[7] * a[13] + a[12] * a[5] * a[11] - a[12] * a[7] * a[9];
            inv[12] = -a[4] * a[9] * a[14] + a[4] * a[10] * a[13] + a[8] * a[5] * a[14] - a[8] * a[6] * a[13] - a[12] * a[5] * a[10] + a[12] * a[6] * a[9];
            inv[1] = -a[1] * a[10] * a[15] + a[1] * a[11] * a[14] + a[9] * a[2] * a[15] - a[9] * a[3] * a[14] - a[13] * a[2] * a[11] + a[13] * a[3] * a[10];
            inv[5] = a[0] * a[10] * a[15] - a[0] * a[11] * a[14] - a[8] * a[2] * a[15] + a[8] * a[3] * a[14] + a[12] * a[2] * a[11] - a[12] * a[3] * a[10];
            inv[9] = -a[0] * a[9] * a[15] + a[0] * a[11] * a[13] + a[8] * a[1] * a[15] - a[8] * a[3] * a[13] - a[12] * a[1] * a[11] + a[12] * a[3] * a[9];
            inv[13] = a[0] * a[9] * a[14] - a[0] * a[10] * a[13] - a[8] * a[1] * a[14] + a[8] * a[2] * a[13] + a[12] * a[1] * a[10] - a[12] * a[2] * a[9];
            inv[2] = a[1] * a[6] * a[15] - a[1] * a[7] * a[14] - a[5] * a[2] * a[15] + a[5] * a[3] * a[14] + a[13] * a[2] * a[7] - a[13] * a[3] * a[6];
            inv[6] = -a[0] * a[6] * a[15] + a[0] * a[7] * a[14] + a[4] * a[2] * a[15] - a[4] * a[3] * a[14] - a[12] * a[2] * a[7] + a[12] * a[3] * a[6];
            inv[10] = a[0] * a[5] * a[15] - a[0] * a[7] * a[13] - a[4] * a[1] * a[15] + a[4] * a[3] * a[13] + a[12] * a[1] * a[7] - a[12] * a[3] * a[5];
            inv[14] = -a[0] * a[5] * a[14] + a[0] * a[6] * a[13] + a[4] * a[1] * a[14] - a[4] * a[2] * a[13] - a[12] * a[1] * a[6] + a[12] * a[2] * a[5];
            inv[3] = -a[1] * a[6] * a[11] + a[1] * a[7] * a[10] + a[5] * a[2] * a[11] - a[5] * a[3] * a[10] - a[9] * a[2] * a[7] + a[9] * a[3] * a[6];
            inv[7] = a[0] * a[6] * a[11] - a[0] * a[7] * a[10] - a[4] * a[2] * a[11] + a[4] * a[3] * a[10] + a[8] * a[2] * a[7] - a[8] * a[3] * a[6];
            inv[11] = -a[0] * a[5] * a[11] + a[0] * a[7] * a[9] + a[4] * a[1] * a[11] - a[4] * a[3] * a[9] - a[8] * a[1] * a[7] + a[8] * a[3] * a[5];
            inv[15] = a[0] * a[5] * a[10] - a[0] * a[6] * a[9] - a[4] * a[1] * a[10] + a[4] * a[2] * a[9] + a[8] * a[1] * a[6] - a[8] * a[2] * a[5];
            float det = a[0] * inv[0] + a[1] * inv[4] + a[2] * inv[8] + a[3] * inv[12];
            var r = new Matrix4x4();
            if (det == 0f) return zero;
            for (int i = 0; i < 16; i++) r[i] = inv[i] / det;
            return r;
        }
    }

    public partial struct LayerMask
    {
        int m_Mask;
        public int value { get { return m_Mask; } set { m_Mask = value; } }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator int(LayerMask m) { return m.m_Mask; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator LayerMask(int i) { LayerMask m; m.m_Mask = i; return m; }
        public static int NameToLayer(string n) { return KNL.EngineSettings.LayerIndex(n); }
        public static string LayerToName(int i) { return KNL.EngineSettings.LayerName(i); }
        public static int GetMask(params string[] names) { int m = 0; foreach (var n in names) { int l = NameToLayer(n); if (l >= 0) m |= 1 << l; } return m; }
    }
}

namespace KNL
{
    /// Classic improved-Perlin noise, scaled to Unity's [0,1] output convention.
    public static class Noise
    {
        static readonly int[] P = Build();
        static int[] Build()
        {
            var r = new System.Random(1234); int[] p = new int[512]; int[] b = new int[256];
            for (int i = 0; i < 256; i++) b[i] = i;
            for (int i = 255; i > 0; i--) { int j = r.Next(i + 1); int t = b[i]; b[i] = b[j]; b[j] = t; }
            for (int i = 0; i < 512; i++) p[i] = b[i & 255];
            return p;
        }
        static float Fade(float t) { return t * t * t * (t * (t * 6 - 15) + 10); }
        static float Grad(int h, float x, float y) { switch (h & 3) { case 0: return x + y; case 1: return -x + y; case 2: return x - y; default: return -x - y; } }
        public static float Perlin(float x, float y)
        {
            int X = (int)System.Math.Floor(x) & 255, Y = (int)System.Math.Floor(y) & 255;
            x -= (float)System.Math.Floor(x); y -= (float)System.Math.Floor(y);
            float u = Fade(x), v = Fade(y);
            int a = P[X] + Y, b = P[X + 1] + Y;
            float n = Lerp(v, Lerp(u, Grad(P[a], x, y), Grad(P[b], x - 1, y)), Lerp(u, Grad(P[a + 1], x, y - 1), Grad(P[b + 1], x - 1, y - 1)));
            return UnityEngine.Mathf.Clamp01(n * 0.5f + 0.5f);
        }
        static float Lerp(float t, float a, float b) { return a + t * (b - a); }
    }
}
