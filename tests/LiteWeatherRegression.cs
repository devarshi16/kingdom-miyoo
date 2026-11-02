using System;
using KNL;
using UnityEngine;

public struct BlenderFloat { public float value; }
public class BlenderState { public BlenderFloat rain, snow; }
public class Blender : MonoBehaviour { public BlenderState state; }
public class World : MonoBehaviour {
    public float wind;
    public float WindSpeed() { return wind; }
}

class LiteWeatherRegression {
    static int passed;
    static void Check(bool ok, string label) {
        if (!ok) throw new Exception("FAIL: " + label);
        passed++;
        Console.WriteLine("PASS: " + label);
    }
    static void Main() {
        var quiet = LiteWeather.Sample(.1f, .1f, .25f);
        Check(quiet.rainDrops == 0 && quiet.snowFlakes == 0, "Original 0.1 precipitation threshold stays inactive");
        var onset = LiteWeather.Sample(.101f, .101f, .25f);
        Check(onset.raining && onset.snowing && onset.rainDrops > 0 && onset.snowFlakes > 0, "Rain and snow start above the original threshold");
        Check(Math.Abs(onset.windDrift - 5f) < .001f, "Wind drift retains original WindSpeed times 20 scaling");

        var full = LiteWeather.Sample(1f, 1f, -1f);
        Check(full.rainDrops == LiteWeather.MaxRainDrops && full.snowFlakes == LiteWeather.MaxSnowFlakes,
            "Maximum normalized weather stays within the fixed particle limits");
        Check(Math.Abs(full.windDrift + 20f) < .001f, "Negative game wind direction is preserved");
        var clamped = LiteWeather.Sample(4f, -2f, float.NaN);
        Check(clamped.rain == 1f && clamped.rainDrops == LiteWeather.MaxRainDrops && clamped.snow == 0f && clamped.snowFlakes == 0 && clamped.windDrift == 0f,
            "Invalid and out-of-range source values are safely normalized");

        var blender = new Blender { state = new BlenderState { rain = new BlenderFloat { value = .6f }, snow = new BlenderFloat { value = .25f } } };
        var world = new World { wind = -.4f };
        LiteWeather.Observe(blender); LiteWeather.Observe(world);
        var observed = LiteWeather.Read();
        Check(Math.Abs(observed.rain - .6f) < .001f && Math.Abs(observed.snow - .25f) < .001f && Math.Abs(observed.windDrift + 8f) < .001f,
            "Cached source readers follow Blender.state rain/snow and World.WindSpeed");
        Check(blender.state.rain.value == .6f && blender.state.snow.value == .25f && world.wind == -.4f,
            "Sampling leaves original weather and wind values unchanged");
        blender.state.rain = new BlenderFloat { value = .8f }; world.wind = .1f;
        observed = LiteWeather.Read();
        Check(Math.Abs(observed.rain - .8f) < .001f && Math.Abs(observed.windDrift - 2f) < .001f,
            "Cached readers see later game-owned state changes");

        Check(LiteWeather.ShouldDraw(true, true, true, true), "Weather can draw in lite gameplay with an active player independently of wallet display");
        Check(!LiteWeather.ShouldDraw(false, true, true, true), "Weather is disabled in full visual mode");
        Check(!LiteWeather.ShouldDraw(true, false, true, true), "Developer details switch disables presentation");
        Check(!LiteWeather.ShouldDraw(true, true, false, true) && !LiteWeather.ShouldDraw(true, true, true, false),
            "Weather requires a gameplay camera and active player");

        var rainAtPause = LiteWeather.ParticlePosition(false, 7, 12f, 320, 240, 3f);
        var rainWhilePaused = LiteWeather.ParticlePosition(false, 7, 12f, 320, 240, 3f);
        var rainAfterTime = LiteWeather.ParticlePosition(false, 7, 12.1f, 320, 240, 3f);
        Check(rainAtPause == rainWhilePaused, "Scaled paused time freezes deterministic rain positions");
        Check(rainAtPause != rainAfterTime, "Weather positions advance when scaled game time advances");
        var snow = LiteWeather.ParticlePosition(true, 5, 2f, 320, 240, -4f);
        Check(snow.x >= 0 && snow.x < 320 && snow.y >= 0 && snow.y < 240, "Snow positions stay inside the screen bounds");
        Check(LiteWeather.MaxWaterHighlights == 24, "Water detail uses a fixed bounded highlight count");
        Console.WriteLine("Lite weather regressions passed: " + passed);
    }
}
