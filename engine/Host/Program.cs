// knl.exe — desktop host for the Kingdom New Lands port (M1: load + render).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using KNL;
using UnityEngine;

static class Program
{
    static Type s_EnemyType;
    static IEnumerable<GameObject> EnemyObjects()
    {
        if (s_EnemyType == null) { var asm = World.GameAssembly("Assembly-CSharp"); s_EnemyType = asm != null ? asm.GetType("Enemy") : typeof(void); }
        return World.SceneObjects.Where(g => g.activeInHierarchy && g.m_Components.Any(c => c != null && s_EnemyType.IsInstanceOfType(c)));
    }

    // debug: one-line field listing of a plain (non-Unity) object, for --dump ... --deep
    static string Describe(object x)
    {
        if (x is UnityEngine.Object || x.GetType().IsPrimitive || x is string || x.GetType().IsEnum) return x.ToString();
        var bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        return "{" + string.Join(" ", x.GetType().GetFields(bf).Select(fi => { object v = fi.GetValue(x); return fi.Name + "=" + (v == null ? "null" : v.ToString()); }).ToArray()) + "}";
    }

    // debug: player x, coins, director day/time (reflection; game types are not referenced at compile time)
    static string GameProbe()
    {
        try
        {
            var bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            string r = "";
            var pl = World.SceneObjects.FirstOrDefault(g => g.m_Tag == "Player" && g.activeInHierarchy);
            if (pl != null)
            {
                r += string.Format(" | king x={0:F1}", pl.transform.position.x);
                var w = pl.m_Components.FirstOrDefault(c => c.GetType().Name == "Wallet");
                if (w != null) r += " coins=" + w.GetType().GetField("_coins", bf).GetValue(w);
                var en = EnemyObjects().OrderBy(g => Math.Abs(g.transform.position.x - pl.transform.position.x)).FirstOrDefault();
                if (en != null)
                {
                    var esr = en.GetComponent<SpriteRenderer>();
                    r += string.Format(" nearest_enemy_dx={0:F1} [{1} y={2:F2} z={3:F2} sr={4}]", en.transform.position.x - (RenderSystem.MainCamera != null ? RenderSystem.MainCamera.transform.position.x : 0f),
                        en.name, en.transform.position.y, en.transform.position.z,
                        esr == null ? "none" : (esr.enabled + "/" + (esr.sprite != null ? esr.sprite.name + (esr.sprite.data != null ? "" : "(nodata)") : "null") + "/a=" + esr.color.a.ToString("F2") + "/scale=" + en.transform.lossyScale));
                }
            }
            r += " amb=" + RenderSettings.ambientSkyColor;
            Vector4[] la; if (Shader.s_VectorArrays.TryGetValue("_Light_PositionArray", out la)) r += " lights=" + string.Join(";", la.Where(v => v.w != 0).Select(v => v.ToString()).ToArray());
            var d = World.SceneObjects.SelectMany(g => g.m_Components).FirstOrDefault(c => c != null && c.GetType().Name == "Director");
            if (d != null)
                r += string.Format(" day={0} t={1:F2}", d.GetType().GetProperty("currentDay", bf).GetValue(d, null), d.GetType().GetProperty("currentTime", bf).GetValue(d, null));
            return r;
        }
        catch (Exception e) { return " | probe:" + e.GetType().Name; }
    }

    static int Main(string[] argv)
    {
        try { return Run(argv); }
        catch (Exception e) { Console.Error.WriteLine("[FATAL] " + e); return 1; }
        finally
        {
            try { Lifecycle.ApplicationQuit(); } catch (Exception e) { Console.Error.WriteLine("[shutdown] " + e); }
            try { AudioNative.knl_audio_shutdown(); } catch (Exception e) { Console.Error.WriteLine("[shutdown] audio: " + e.Message); }
            try { PouchPhysics.Reset(); } catch (Exception e) { Console.Error.WriteLine("[shutdown] pouch: " + e.Message); }
            try { Native.knl_shutdown(); } catch (Exception e) { Console.Error.WriteLine("[shutdown] video: " + e.Message); }
        }
    }

    static int Run(string[] argv)
    {
        var a = Args.Parse(argv);
        Paths.DataDir = a.Get("data", "extracted");
        Paths.SaveDir = System.IO.Path.GetFullPath(a.Get("save", "save"));
        System.IO.Directory.CreateDirectory(Paths.SaveDir);
        Log.Quiet = a.Has("quiet");
        var sw = System.Diagnostics.Stopwatch.StartNew();

        RenderSystem.Init(a.Has("window"));
        if (!a.Has("no-pack")) Pack.TryOpen(Paths.DataDir);   // device pack if present, else extracted/ JSON + PNG
        World.LoadSettings();
        if (a.Has("boot")) return Boot(a);
        foreach (var scene in a.Get("scenes", "level2,level1").Split(','))
        {
            var tag = World.LoadScene(scene.Trim());
            if (World.ActiveScene == null) World.ActiveScene = tag;
        }
        foreach (var name in a.Get("activate", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var go = World.SceneObjects.FirstOrDefault(g => g.name == name);
            if (go != null) go.m_IsActive = true; else Log.Info("activate: no object named " + name);
        }
        RenderSystem.MainCamera = RenderSystem.FindMainCamera();
        double loadMs = sw.Elapsed.TotalMilliseconds;

        var report = new StringBuilder();
        report.AppendLine("load_ms=" + loadMs.ToString("F0"));
        report.AppendLine("scene_gameobjects=" + World.SceneObjects.Count + " active=" + World.SceneObjects.Count(g => g.activeInHierarchy));
        foreach (var kv in World.Stats.OrderBy(k => k.Key)) report.AppendLine("stat " + kv.Key + "=" + kv.Value);
        foreach (var kv in World.UnknownTypes.OrderByDescending(k => k.Value)) report.AppendLine("unknown " + kv.Key + "=" + kv.Value);
        foreach (var kv in World.CtorFailures.OrderByDescending(k => k.Value)) report.AppendLine("ctor_fail " + kv.Key + " x" + kv.Value);
        report.AppendLine("serializer fields_set=" + Serializer.FieldsSet + " missing=" + Serializer.FieldsMissing + " errors=" + Serializer.FieldErrors);
        foreach (var kv in Serializer.Missing.OrderByDescending(k => k.Value).Take(15)) report.AppendLine("  missing_field " + kv.Key + " x" + kv.Value);
        report.AppendLine("main_camera=" + (RenderSystem.MainCamera != null ? RenderSystem.MainCamera.name + " @ " + RenderSystem.MainCamera.transform.position : "none"));

        // ---- single shot
        if (a.Has("camera"))
        {
            var p = a.Get("camera", "0,1.88").Split(',').Select(s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            RenderSystem.CameraOverride = new Vector3(p[0], p[1], -10f);
        }
        if (a.Has("out"))
        {
            var t0 = sw.Elapsed.TotalMilliseconds;
            RenderSystem.RenderFrame();
            var t1 = sw.Elapsed.TotalMilliseconds;
            RenderSystem.SavePng(a.Get("out", "frame.png"), int.Parse(a.Get("scale", "2")));
            report.AppendLine(string.Format("shot {0}: candidates={1} drawn={2} pixels={3} render_ms={4:F1}",
                a.Get("out", ""), RenderSystem.LastCandidates, RenderSystem.LastSprites, RenderSystem.LastPixels, t1 - t0));
        }

        // ---- one shot per level block (blocks scene authored layout)
        if (a.Has("shots-blocks"))
        {
            string dir = a.Get("shots-blocks", "shots");
            Directory.CreateDirectory(dir);
            var blocks = World.SceneObjects.FirstOrDefault(g => g.name == "Blocks");
            if (blocks != null)
            {
                blocks.m_IsActive = true;
                foreach (Transform b in blocks.transform)
                {
                    var srs = b.gameObject.GetComponentsInChildren<SpriteRenderer>(true).Where(s => s.sprite != null).ToArray();
                    if (srs.Length == 0) continue;
                    var bb = srs[0].bounds;
                    foreach (var s in srs) bb.Encapsulate(s.bounds);
                    float x = bb.center.x;
                    RenderSystem.CameraOverride = new Vector3(x, b.position.y + 1.88f, -10f);
                    RenderSystem.RenderFrame();
                    RenderSystem.SavePng(Path.Combine(dir, b.name + ".png"), 2);
                    report.AppendLine(string.Format("block {0}: x={1:F1} sprites={2} drawn={3}", b.name, x, srs.Length, RenderSystem.LastSprites));
                }
            }
        }

        // ---- smoke test: run the game's own Awake/OnEnable/Start on everything active
        if (a.Has("smoke"))
        {
            var msgs = a.Get("smoke", "Awake").Split(',');
            int calls = 0;
            int errsBefore = Log.Errors;
            Log.Quiet = true;
            foreach (var msg in msgs)
                foreach (var g in World.SceneObjects.ToArray())
                {
                    if (!g.activeInHierarchy) continue;
                    foreach (var c in g.m_Components.ToArray())
                        if (c is MonoBehaviour && ((Behaviour)c).enabled && Reflect.InvokeMessage(c, msg, null)) calls++;
                }
            Log.Quiet = a.Has("quiet");
            report.AppendLine(string.Format("smoke {0}: calls={1} exceptions={2}", string.Join("+", msgs), calls, Log.Errors - errsBefore));
            foreach (var kv in Log.NotImplementedHits.OrderByDescending(k => k.Value).Take(40))
                report.AppendLine("  needs " + kv.Key + " x" + kv.Value);
        }

        // ---- interactive window (desktop): arrow keys pan the camera
        if (a.Has("window") && Native.knl_has_window() != 0)
        {
            var cam = RenderSystem.CameraOverride ?? (RenderSystem.MainCamera != null ? RenderSystem.MainCamera.transform.position : Vector3.zero);
            uint last = Native.knl_ticks_ms();
            int maxFrames = int.Parse(a.Get("frames", "0")), frames = 0;
            var bench = System.Diagnostics.Stopwatch.StartNew(); double renderMs = 0;
            while (Native.knl_present() != 0 && !KNL.Engine.QuitRequested && (maxFrames == 0 || frames < maxFrames))
            {
                frames++;
                uint now = Native.knl_ticks_ms(); float dt = (now - last) / 1000f; last = now;
                uint btn = Native.knl_buttons();
                float speed = (btn & 32) != 0 ? 24f : 6f;
                if ((btn & 1) != 0) cam.x -= speed * dt;
                if ((btn & 2) != 0) cam.x += speed * dt;
                if ((btn & 4) != 0) cam.y += speed * dt;
                if ((btn & 8) != 0) cam.y -= speed * dt;
                RenderSystem.CameraOverride = cam;
                var r0 = bench.Elapsed.TotalMilliseconds;
                RenderSystem.RenderFrame();
                renderMs += bench.Elapsed.TotalMilliseconds - r0;
            }
            if (frames > 0) report.AppendLine(string.Format("window: frames={0} avg_render_ms={1:F2} avg_frame_ms={2:F2}", frames, renderMs / frames, bench.Elapsed.TotalMilliseconds / frames));
        }

        var text = report.ToString();
        Console.Write(text);
        if (a.Has("report")) File.WriteAllText(a.Get("report", "report.txt"), text);
        return 0;
    }

    /// Real game boot: load build scene 0 (loader) and run frames like the Unity player.
    static int Boot(Args a)
    {
        if (CoinCounter.Enabled) CoinCounter.ConfigureOverflow(new[] {
            // Original Unity 2017.1.2p1 player, 20 instrumented pouch fills; see
            // docs/POUCH-COUNTER.md and docs/evidence/original-pouch-20.csv.
            46,44,41,44,41,42,43,49,45,42,41,48,45,46,46,42,44,43,41,41
        });
        Lifecycle.LoadExecutionOrder();
        Lifecycle.AnimatorStep = AnimationSystem.Step;
        Lifecycle.PhysicsStep = Physics2DSystem.Step;
        Physics2DSystem.LoadSettings();
        AudioSystem.Init(a.Has("window") && !a.Has("mute") && !a.Has("wav"));
        if (a.Has("wav")) AudioSystem.StartCapture(a.Get("wav", "out.wav"));
        SceneLoader.LoadBuildSettings();
        Lifecycle.Verbose = a.Has("verbose");
        if (a.Has("input")) InputScript.Parse(a.Get("input", ""));
        int frames = int.Parse(a.Get("frames", a.Has("window") ? "0" : "300"));   // window: run until F10/close
        int shotEvery = int.Parse(a.Get("shot-every", "0"));
        string shotDir = a.Get("shots", "shots");
        if (shotEvery > 0) System.IO.Directory.CreateDirectory(shotDir);
        bool window = a.Has("window") && Native.knl_has_window() != 0;
        Time.s_OfflineClock = !window || a.Has("fixed-dt");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        SceneLoader.LoadNow(SceneLoader.BuildScenes.Length > 0 ? SceneLoader.BuildScenes[0] : "level0", false);
        Console.Error.WriteLine("[knl] booted scene 0 in " + sw.ElapsedMilliseconds + " ms");
        double logicMs = 0, renderMs = 0, presentMs = 0, perfLogic = 0, perfRender = 0;
        double perfWall = sw.Elapsed.TotalMilliseconds;
        uint lastTicks = Native.knl_ticks_ms();
        // longest frame step: below 1/maxDt fps the game slows down instead of running extra 50 Hz physics
        // steps to catch up (each costs ~15 ms on the Miyoo, which made slow frames slower; D22)
        float maxDt = float.Parse(a.Get("max-dt", "0.1"), System.Globalization.CultureInfo.InvariantCulture);
        string watch = a.Get("watch", ""); object lastWatch = null; System.Reflection.FieldInfo watchField = null; Component watchObj = null;
        for (int f = 0; frames == 0 || f < frames; f++)
        {
            if (a.Has("frame-marks")) Console.Error.WriteLine("[frame " + f + "] timeScale=" + Time.timeScale);
            if (a.Has("trace-frames"))
            {   // run mono with --trace=disabled,<filter>: SIGUSR2 toggles tracing on/off
                var tf = a.Get("trace-frames", "0-0").Split('-');
                if (f == int.Parse(tf[0]) || f == int.Parse(tf[1]) + 1) Sys.kill(Sys.getpid(), 12);
            }
            if (watch != "")
            {
                var parts = watch.Split('.');
                if (watchObj == null || watchObj.m_Destroyed)
                {
                    watchObj = World.SceneObjects.SelectMany(g => g.m_Components).FirstOrDefault(c => c != null && c.GetType().Name == parts[0]);
                    if (watchObj != null) for (var t = watchObj.GetType(); t != null && watchField == null; t = t.BaseType)
                        watchField = t.GetField(parts[1], System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly);
                }
                if (watchObj != null && watchField != null)
                {
                    var v = watchField.GetValue(watchObj);
                    if (!Equals(v, lastWatch)) { Console.Error.WriteLine("[watch] frame " + f + ": " + watch + " = " + v + " (obj " + watchObj.GetInstanceID() + ")"); lastWatch = v; }
                }
            }
            if (a.Has("test-coins") && f == int.Parse(a.Get("coin-frame", "450")))
            {
                var plObj = World.SceneObjects.FirstOrDefault(g => g.m_Tag == "Player" && g.activeInHierarchy);
                if (plObj != null)
                {
                    var w = plObj.m_Components.FirstOrDefault(c => c.GetType().Name == "Wallet");
                    if (w != null)
                    {
                        var bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
                        var property = w.GetType().GetProperty("coins", bf);
                        int before = (int)property.GetValue(w, null);
                        property.SetValue(w, int.Parse(a.Get("test-coins", "6")), null);
                        CoinCounter.NotifyTestPickups(w, Math.Max(0, (int)property.GetValue(w, null) - before));
                    }
                }
            }
            float dt = 1f / 30f;
            float wallDt = dt;
            if (window && !a.Has("fixed-dt")) { uint now = Native.knl_ticks_ms(); wallDt = Mathf.Max((now - lastTicks) / 1000f, 0.001f); dt = Mathf.Min(wallDt, maxDt); lastTicks = now; }
            var t0 = sw.Elapsed.TotalMilliseconds;
            Lifecycle.Frame(dt, wallDt);
            AudioSystem.Update(Time.unscaledDeltaTime);
            var t1 = sw.Elapsed.TotalMilliseconds;
            // render through Camera.main (tagged MainCamera); before the main scene exists, any camera
            RenderSystem.MainCamera = RenderSystem.TaggedMainCamera() ?? RenderSystem.FindMainCamera();
            RenderSystem.RenderFrame();
            Coroutines.TickEndOfFrame();
            renderMs += sw.Elapsed.TotalMilliseconds - t1; logicMs += t1 - t0;
            if (a.Has("shot-on-edge") && (EdgeIndicators.LastLeft + EdgeIndicators.LastRight) > 0 && f % 10 == 0)
            { System.IO.Directory.CreateDirectory(a.Get("shot-on-edge", "edge")); RenderSystem.SavePng(System.IO.Path.Combine(a.Get("shot-on-edge", "edge"), string.Format("e{0:D5}.png", f)), 2); }
            if (shotEvery > 0 && f % shotEvery == 0 && f >= int.Parse(a.Get("shot-from", "0"))) RenderSystem.SavePng(System.IO.Path.Combine(shotDir, string.Format("f{0:D5}.png", f)), 2);
            var t2 = sw.Elapsed.TotalMilliseconds;
            if (window && Native.knl_present() == 0) break;
            presentMs += sw.Elapsed.TotalMilliseconds - t2;
            if (window && f % 300 == 299)
            {   // the windowed run only ends on quit, so report as it goes (device: logs/knl.log)
                double elapsed = sw.Elapsed.TotalMilliseconds - perfWall;
                Console.Error.WriteLine(string.Format("[fps] f{0} fps={1:F2} frame_ms={2:F2}", f + 1, 300000.0 / elapsed, elapsed / 300));
                Console.Error.WriteLine(string.Format("[perf] f{0} logic={1:F1} render={2:F1} present={3:F1} ms/frame (raster thread {5:F1} ms last frame) managed={4:F1} MB",
                    f + 1, (logicMs - perfLogic) / 300, (renderMs - perfRender) / 300, presentMs / 300, GC.GetTotalMemory(false) / 1048576.0, Native.knl_raster_ms()));
                perfLogic = logicMs; perfRender = renderMs; presentMs = 0;
                Console.Error.WriteLine(string.Format("[worker] last_draw_ms={0:F1} last_flip_ms={1:F1}", Native.knl_draw_ms(), Native.knl_flip_ms()));
                KNL.Prof.Report(300, 25);
                perfWall = sw.Elapsed.TotalMilliseconds;
            }
            if (KNL.Engine.QuitRequested) break;
            if (a.Has("trace-scenes") && f % 30 == 0)
                Console.Error.WriteLine(string.Format("[f{0}] enemies={6} edge L{7}/R{8} | scenes={1} objs={2} live_scripts={3} exc={4} cam={5}", f,
                    string.Join(",", SceneLoader.Loaded.Select(t => t.displayName).ToArray()), World.SceneObjects.Count, Lifecycle.LiveCount, Lifecycle.Exceptions,
                    RenderSystem.MainCamera != null ? RenderSystem.MainCamera.name + RenderSystem.MainCamera.transform.position : "-",
                    EnemyObjects().Count(), EdgeIndicators.LastLeft, EdgeIndicators.LastRight) + GameProbe());
        }
        KNL.Prof.Report(Time.frameCount, 40);
        if (a.Has("out")) RenderSystem.SavePng(a.Get("out", "frame.png"), 2);
        if (a.Has("wide-out"))
        {   // debug: one extra frame with a wider view (orthographic size x2)
            float saved = RenderSystem.OrthoSizeOverride; RenderSystem.OrthoSizeOverride *= 2f;
            RenderSystem.RenderFrame(); RenderSystem.SavePng(a.Get("wide-out", "wide.png"), 2);
            RenderSystem.OrthoSizeOverride = saved;
        }
        if (a.Has("inspect"))
            foreach (var nm in a.Get("inspect", "").Split(','))
                foreach (var g in (World.SceneObjects.Any(o => o.name == nm || o.m_Tag == nm) ? World.SceneObjects : World.FindAllObjects(typeof(GameObject)).OfType<GameObject>()).Where(o => o.name == nm || o.m_Tag == nm).Take(4))
                {
                    Console.WriteLine(string.Format("[inspect] {0} tag={1} self={2} hier={3} pos={4} scale={6} comps={5}", g.name, g.m_Tag, g.m_IsActive, g.activeInHierarchy,
                        g.transform.position, string.Join(",", g.m_Components.Select(c => c.GetType().Name + (c is Behaviour && !((Behaviour)c).enabled ? "(off)" : "")).ToArray()), g.transform.lossyScale));
                    foreach (var col in g.GetComponentsInChildren<Collider2D>(true))
                        Console.WriteLine("   col " + col.GetType().Name + " on " + col.gameObject.name + " layer=" + col.gameObject.layer + " trig=" + col.isTrigger + " en=" + col.enabled + " act=" + col.gameObject.activeInHierarchy + " bounds=" + col.bounds.min + ".." + col.bounds.max + " rb=" + (col.GetComponentInParent<Rigidbody2D>() != null));
                    foreach (var sr in g.GetComponentsInChildren<SpriteRenderer>(true))
                        Console.WriteLine("   sr " + sr.gameObject.name + " en=" + sr.enabled + " act=" + sr.gameObject.activeInHierarchy + " sprite=" + (sr.sprite != null ? sr.sprite.name : "null") + " color=" + sr.color + " order=" + sr.sortingOrder + " mat=" + (sr.sharedMaterial != null ? sr.sharedMaterial.name + "/" + (sr.sharedMaterial.shader != null ? sr.sharedMaterial.shader.name : "?") : "-"));
                    var mr = g.GetComponent<MeshRenderer>(); var mf = g.GetComponent<MeshFilter>();
                    if (mr != null) Console.WriteLine("   mesh order=" + mr.sortingOrder + " enabled=" + mr.enabled + " mat=" + (mr.material != null ? mr.material.name + "/" + (mr.material.shader != null ? mr.material.shader.name : "?") : "-")
                        + " verts=" + (mf != null && mf.m_Mesh != null ? string.Join(" ", mf.m_Mesh.m_Vertices.Select(v => v.ToString()).ToArray()) : "-")
                        + " colors=" + (mf != null && mf.m_Mesh != null ? string.Join(" ", mf.m_Mesh.m_Colors.Select(v => v.ToString()).ToArray()) : "-"));
                }
        if (a.Has("dump"))
            foreach (var tn in a.Get("dump", "").Split(','))
                foreach (var obj in World.FindAllObjects(typeof(Component)).OfType<Component>().Where(c => c.GetType().Name == tn).Take(4))
                {
                    Console.WriteLine("[dump] " + tn + " on " + obj.gameObject.name + " scene=" + (obj.gameObject.m_Scene != null ? obj.gameObject.m_Scene.displayName : "ASSET") + " active=" + obj.gameObject.activeInHierarchy);
                    for (var t = obj.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
                        foreach (var fi in t.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
                        {
                            object v; try { v = fi.GetValue(fi.IsStatic ? null : obj); } catch { v = "?"; }
                            var s = v == null ? "null" : v.ToString();
                            var en = v as System.Collections.IEnumerable;
                            if (en != null && !(v is string)) s += " [" + string.Join(", ", en.Cast<object>().Take(a.Has("deep") ? 60 : 30).Select(x => x == null ? "null" : a.Has("deep") ? Describe(x) : x.GetType().Name).ToArray()) + "]";
                            if (fi.IsStatic) s = "(static) " + s;
                            if (s.Length > (a.Has("deep") ? 8000 : 400)) s = s.Substring(0, a.Has("deep") ? 8000 : 400);
                            Console.WriteLine("    " + fi.Name + " = " + s);
                        }
                }
        int n = Math.Max(1, Time.frameCount);
        Console.WriteLine(string.Format("boot: frames={0} avg_logic_ms={1:F2} avg_render_ms={2:F2} exceptions={3} live_scripts={4} objects={5}",
            Time.frameCount, logicMs / n, renderMs / n, Lifecycle.Exceptions, Lifecycle.LiveCount, World.SceneObjects.Count));
        Console.WriteLine(string.Format("memory: managed={0:F1} MB (after GC) rss={1:F1} MB textures={2:F1} MB data={3}", GC.GetTotalMemory(true) / 1048576.0, System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 / 1048576.0, Native.knl_tex_bytes() / 1048576.0, Pack.Active ? "pack" : "extracted"));
        if (a.Has("mem-drop")) { World.DropObjectData(); Console.WriteLine(string.Format("memory without object trees: managed={0:F1} MB", GC.GetTotalMemory(true) / 1048576.0)); }
        Console.WriteLine("scenes: " + string.Join(",", SceneLoader.Loaded.Select(t => t.displayName).ToArray()));
        foreach (var kv in Lifecycle.ExceptionSites.OrderByDescending(k => k.Value).Take(int.Parse(a.Get("top", "25"))))
            Console.WriteLine(string.Format("  x{0,-5} {1}", kv.Value, kv.Key.Length > 220 ? kv.Key.Substring(0, 220) : kv.Key));
        AudioSystem.StopCapture();
        Console.WriteLine(string.Format("audio: plays={0} missing_clips={1}", AudioSystem.Plays, AudioSystem.Missing));
        Console.WriteLine("needs:");
        foreach (var kv in Log.NotImplementedHits.OrderByDescending(k => k.Value).Take(int.Parse(a.Get("top", "25"))))
            Console.WriteLine(string.Format("  x{0,-5} {1}", kv.Value, kv.Key));
        return 0;
    }

    static class Sys
    {
        [System.Runtime.InteropServices.DllImport("libc")] public static extern int kill(int pid, int sig);
        [System.Runtime.InteropServices.DllImport("libc")] public static extern int getpid();
    }

    sealed class Args
    {
        readonly Dictionary<string, string> d = new Dictionary<string, string>();
        public static Args Parse(string[] v)
        {
            var a = new Args();
            for (int i = 0; i < v.Length; i++)
            {
                if (!v[i].StartsWith("--")) continue;
                string k = v[i].Substring(2);
                string val = i + 1 < v.Length && !v[i + 1].StartsWith("--") ? v[++i] : "";
                a.d[k] = val;
            }
            return a;
        }
        public bool Has(string k) { return d.ContainsKey(k); }
        public string Get(string k, string def) { string v; return d.TryGetValue(k, out v) && v != "" ? v : def; }
    }
}
