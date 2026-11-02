// Small clean-room precipitation and water detail for lite rendering. Game weather remains the
// source of truth: this reads Blender.state and World.WindSpeed without advancing either system.
using System;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace KNL {
public static class LiteWeather {
    public const int MaxRainDrops = 40, MaxSnowFlakes = 20, MaxWaterHighlights = 24;
    const float EmitThreshold = 0.1f;
    const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static MonoBehaviour blender, world, player;
    static readonly bool details = !string.Equals(Environment.GetEnvironmentVariable("KNL_DETAILS"), "off", StringComparison.OrdinalIgnoreCase);
    static Func<MonoBehaviour,float> rainValue, snowValue;
    static Func<float> windSpeed;
    static readonly float[] rainX = new float[MaxRainDrops], rainY = new float[MaxRainDrops];
    static readonly float[] snowX = new float[MaxSnowFlakes], snowY = new float[MaxSnowFlakes];
    static readonly float[] waterX = new float[MaxWaterHighlights], waterY = new float[MaxWaterHighlights];

    public struct WeatherState {
        public float rain, snow, windDrift;
        public int rainDrops, snowFlakes;
        public bool raining { get { return rainDrops > 0; } }
        public bool snowing { get { return snowFlakes > 0; } }
    }

    static LiteWeather() {
        for (int i = 0; i < MaxRainDrops; i++) { rainX[i] = Seed(i, 17); rainY[i] = Seed(i, 41); }
        for (int i = 0; i < MaxSnowFlakes; i++) { snowX[i] = Seed(i, 73); snowY[i] = Seed(i, 101); }
        for (int i = 0; i < MaxWaterHighlights; i++) { waterX[i] = Seed(i, 137); waterY[i] = Seed(i, 193); }
    }

    static float Seed(int i, int salt) {
        unchecked {
            uint x = (uint)(i + 1) * 0x9E3779B9u ^ (uint)salt * 0x85EBCA6Bu;
            x ^= x >> 16; x *= 0x7FEB352Du; x ^= x >> 15; x *= 0x846CA68Bu; x ^= x >> 16;
            return (x & 0x00FFFFFFu) / 16777216f;
        }
    }

    /// Call before Awake for each loaded game behaviour. Only Blender and World are retained.
    public static void Observe(MonoBehaviour mb) {
        if (mb == null) return;
        string name = mb.GetType().Name;
        if (name == "Player") { player = mb; }
        else if (name == "Blender") {
            var type = mb.GetType();
            var state = type.GetField("state", Instance);
            if (state == null || state.FieldType.IsValueType) return;
            var rain = state.FieldType.GetField("rain", Instance);
            var snow = state.FieldType.GetField("snow", Instance);
            if (rain == null || snow == null || rain.FieldType != snow.FieldType) return;
            var value = rain.FieldType.GetField("value", Instance);
            if (value == null || value.FieldType != typeof(float)) return;
            rainValue = BindFloatField(type, state, rain, value, "rain");
            snowValue = BindFloatField(type, state, snow, value, "snow");
            blender = mb;
        } else if (name == "World") {
            var method = mb.GetType().GetMethod("WindSpeed", Instance, null, Type.EmptyTypes, null);
            if (method == null || method.ReturnType != typeof(float)) return;
            try { windSpeed = (Func<float>)Delegate.CreateDelegate(typeof(Func<float>), mb, method); world = mb; }
            catch { windSpeed = null; world = null; }
        }
    }

    static Func<MonoBehaviour,float> BindFloatField(Type owner, FieldInfo state, FieldInfo branch, FieldInfo value, string label) {
        var method = new DynamicMethod("knl_weather_" + label, typeof(float), new[] { typeof(MonoBehaviour) }, typeof(LiteWeather).Module, true);
        var il = method.GetILGenerator();
        var hasState = il.DefineLabel();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Castclass, owner); il.Emit(OpCodes.Ldfld, state);
        il.Emit(OpCodes.Dup); il.Emit(OpCodes.Brtrue_S, hasState);
        il.Emit(OpCodes.Pop); il.Emit(OpCodes.Ldc_R4, 0f); il.Emit(OpCodes.Ret);
        il.MarkLabel(hasState);
        il.Emit(OpCodes.Ldfld, branch); il.Emit(OpCodes.Ldfld, value); il.Emit(OpCodes.Ret);
        return (Func<MonoBehaviour,float>)method.CreateDelegate(typeof(Func<MonoBehaviour,float>));
    }

    static float Unit(float value) {
        if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f) return 0f;
        return value >= 1f ? 1f : value;
    }
    static int Count(float intensity, int maximum) {
        if (intensity <= EmitThreshold) return 0;
        int count = (int)Math.Floor(intensity * maximum + 0.5f);
        return Mathf.Clamp(count, 1, maximum);
    }

    /// Pure projection of the original Blender values. The game's 0.1 emission threshold and
    /// World.WindSpeed()*20 drift multiplier come from Precipitation.Update.
    public static WeatherState Sample(float rain, float snow, float wind) {
        rain = Unit(rain); snow = Unit(snow);
        if (float.IsNaN(wind) || float.IsInfinity(wind)) wind = 0f;
        return new WeatherState {
            rain = rain, snow = snow, windDrift = wind * 20f,
            rainDrops = Count(rain, MaxRainDrops), snowFlakes = Count(snow, MaxSnowFlakes)
        };
    }

    /// Read the currently observed game state without changing Blender, World, or Unity time.
    public static WeatherState Read() {
        float rain = blender != null && rainValue != null ? rainValue(blender) : 0f;
        float snow = blender != null && snowValue != null ? snowValue(blender) : 0f;
        float wind = world != null && windSpeed != null ? windSpeed() : 0f;
        return Sample(rain, snow, wind);
    }

    public static bool ShouldDraw(bool lite, bool enabled, bool hasCamera, bool hasPlayer) {
        return lite && enabled && hasCamera && hasPlayer;
    }

    static bool GameplayVisible() {
        return ShouldDraw(VisualQuality.Lite, details, RenderSystem.MainCamera != null,
            player != null && player.m_GameObject != null && player.m_GameObject.activeInHierarchy);
    }

    static float Mod(float value, float range) {
        if (range <= 0f) return 0f;
        value %= range;
        return value < 0f ? value + range : value;
    }
    static void Rect(float x, float y, int w, int h, uint color) {
        if (w > 0 && h > 0) Native.knl_quad_gradient(x, y, x + w, y + h, color, color, color, color, 0);
    }

    /// Pure deterministic placement helper. Passing the same scaled time (as Unity does while
    /// paused) yields the same point; no Unity random state or game weather state is touched.
    public static Vector2 ParticlePosition(bool snow, int index, float time, float width, float height, float windDrift) {
        width = Math.Max(1f, width); height = Math.Max(1f, height);
        if (snow) {
            index = Mathf.Clamp(index, 0, MaxSnowFlakes - 1);
            float sway = (float)Math.Sin(time * 0.8f + index * 1.7f) * 2.5f;
            return new Vector2(Mod(snowX[index] * width + time * windDrift * 32f * 0.22f + sway, width),
                Mod(snowY[index] * height + time * 18f, height));
        }
        index = Mathf.Clamp(index, 0, MaxRainDrops - 1);
        return new Vector2(Mod(rainX[index] * width + time * windDrift * 32f, width),
            Mod(rainY[index] * height + time * 92f, height));
    }

    /// Draw only while a gameplay player is active, independently of wallet display. Time.time is
    /// scaled by the engine, so paused weather freezes without consulting or changing game pause.
    public static void Draw() {
        if (!GameplayVisible()) return;
        var state = Read();
        if (!state.raining && !state.snowing) return;
        float width = Math.Max(1, RenderSystem.Width), height = Math.Max(1, RenderSystem.Height), time = Time.time;
        for (int i = 0; i < state.rainDrops; i++) {
            var p = ParticlePosition(false, i, time, width, height, state.windDrift);
            Rect(p.x, p.y, 1, 4, 0xA8B9D8F0u);
        }
        for (int i = 0; i < state.snowFlakes; i++) {
            var p = ParticlePosition(true, i, time, width, height, state.windDrift);
            Rect(p.x, p.y, 2, 2, 0xDDEAF5FFu);
        }
    }

    /// Add a few short moving surface glints inside the lite water quad. This draws no reflection
    /// and never paints an empty strip over the water gradient.
    public static void DrawWater(float x0, float y0, float x1, float y1, Color top, Color bottom) {
        if (!GameplayVisible() || x1 <= x0 || y1 - y0 <= 2f) return;
        float width = x1 - x0, height = y1 - y0, time = Time.time;
        for (int i = 0; i < MaxWaterHighlights; i++) {
            float x = x0 + Mod(waterX[i] * width + time * 3.2f, width);
            float y = y0 + 1f + waterY[i] * (height - 2f);
            int length = 2 + (int)(waterY[i] * 7f);
            if (x + length > x1) length = (int)(x1 - x);
            float t = height <= 0 ? 0f : (y - y0) / height;
            Color c = Color.Lerp(top, bottom, t);
            c.r = Mathf.Min(1f, c.r * 0.75f + 0.18f);
            c.g = Mathf.Min(1f, c.g * 0.75f + 0.2f);
            c.b = Mathf.Min(1f, c.b * 0.75f + 0.22f);
            c.a = 0.18f;
            uint rgba = c.ToRGBA32();
            Rect(x, y, length, 1, rgba);
        }
    }
}
}
