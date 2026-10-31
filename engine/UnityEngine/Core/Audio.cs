// Audio: AudioSource/AudioClip/AudioMixer on top of the native mixer (native/knlaudio.c, D18).
// Clip files come from convert.py: audio/sfx/<name>.wav (RAM) and audio/music/<name>.ogg (streamed).
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace UnityEngine
{
    public partial class AudioSource
    {
        internal AudioClip m_Clip; internal bool m_PlayOnAwake = true, m_Loop, m_Mute, m_Paused; internal float m_Volume = 1f, m_Pitch = 1f;
        internal float m_MinDistance = 1f, m_MaxDistance = 500f, m_SpatialBlend, m_Pan2D; internal int m_Rolloff;
        internal AnimationCurve m_RolloffCurve; internal Audio.AudioMixerGroup m_Output; internal bool m_IgnorePause;
        internal int m_Voice = -1;
        public AudioClip clip { get { return m_Clip; } set { if (value != m_Clip) { Stop(); m_Clip = value; } } }
        public bool playOnAwake { get { return m_PlayOnAwake; } set { m_PlayOnAwake = value; } }
        public bool loop { get { return m_Loop; } set { m_Loop = value; } }
        public bool mute { get { return m_Mute; } set { m_Mute = value; } }
        public float volume { get { return m_Volume; } set { m_Volume = Mathf.Clamp01(value); } }
        public float pitch { get { return m_Pitch; } set { m_Pitch = value; } }
        public float minDistance { get { return m_MinDistance; } set { m_MinDistance = value; } }
        public float maxDistance { get { return m_MaxDistance; } set { m_MaxDistance = value; } }
        public float spatialBlend { get { return m_SpatialBlend; } set { m_SpatialBlend = Mathf.Clamp01(value); } }
        public float panStereo { get { return m_Pan2D; } set { m_Pan2D = Mathf.Clamp(value, -1f, 1f); } }
        public bool ignoreListenerPause { get { return m_IgnorePause; } set { m_IgnorePause = value; } }
        public Audio.AudioMixerGroup outputAudioMixerGroup { get { return m_Output; } set { m_Output = value; } }
        public bool isPlaying { get { return m_Voice >= 0 && !m_Paused && KNL.AudioSystem.Alive(m_Voice); } }
        public float time
        {
            get { float t; if (m_Voice >= 0 && KNL.AudioNative.knl_voice_state(m_Voice, out t) != 0) return t; return 0f; }
            set { if (m_Voice >= 0) KNL.AudioNative.knl_voice_seek(m_Voice, value); else m_PendingTime = value; }
        }
        internal float m_PendingTime;
        public void Play()
        {
            Stop();
            if (m_Clip == null) return;
            m_Paused = false;
            m_Voice = KNL.AudioSystem.Start(this, m_Clip, 1f, m_Loop, m_PendingTime);
            m_PendingTime = 0;
        }
        public void PlayDelayed(float d) { Play(); }
        public void PlayOneShot(AudioClip c) { PlayOneShot(c, 1f); }
        public void PlayOneShot(AudioClip c, float vol) { if (c != null) KNL.AudioSystem.Start(this, c, vol, false, 0f, true); }
        public void Stop() { if (m_Voice >= 0) { KNL.AudioNative.knl_voice_stop(m_Voice); KNL.AudioSystem.Forget(this); m_Voice = -1; } m_Paused = false; }
        public void Pause() { m_Paused = true; }
        public void UnPause() { m_Paused = false; }
        public static void PlayClipAtPoint(AudioClip c, Vector3 p) { KNL.AudioSystem.StartAt(c, p, 1f); }
        public static void PlayClipAtPoint(AudioClip c, Vector3 p, float vol) { KNL.AudioSystem.StartAt(c, p, vol); }
    }

    public sealed partial class AudioClip
    {
        internal float m_Length; internal int m_Sound = -2; internal bool m_Stream;
        public float length { get { return m_Length; } }
        public int samples { get { return (int)(m_Length * 22050); } }
        public int frequency { get { return 22050; } }
        public int channels { get { return m_Stream ? 2 : 1; } }
        public bool LoadAudioData() { return KNL.AudioSystem.SoundOf(this) >= 0; }
        public bool UnloadAudioData() { return true; }
    }

    public sealed partial class AudioListener
    {
        internal static readonly List<AudioListener> s_All = new List<AudioListener>();
        static bool s_Pause; static float s_Volume = 1f;
        public static bool pause { get { return s_Pause; } set { s_Pause = value; } }
        public static float volume { get { return s_Volume; } set { s_Volume = Mathf.Clamp01(value); } }
    }

    public sealed partial class RenderSettings
    {
        internal static Color s_AmbientSky = Color.white;
        public static Color ambientSkyColor { get { return s_AmbientSky; } set { s_AmbientSky = value; } }
        public static Color ambientLight { get { return s_AmbientSky; } set { s_AmbientSky = value; } }
    }
}

namespace UnityEngine.Audio
{
    /// One AudioMixerController: groups (parent + volume slot), snapshots (dB per slot), exposed params.
    public partial class AudioMixer
    {
        internal float[] m_Values = new float[0], m_From, m_To;
        internal float m_TransT, m_TransDur;
        internal int[] m_GroupParent = new int[0], m_GroupVolSlot = new int[0];
        internal string[] m_GroupGuids = new string[0];
        internal uint[] m_ExposedHash = new uint[0]; internal int[] m_ExposedSlot = new int[0];
        internal readonly Dictionary<int, float> m_Override = new Dictionary<int, float>();
        internal AudioMixerGroup m_OutputGroup;
        internal AudioMixerSnapshot[] m_Snapshots = new AudioMixerSnapshot[0];
        internal Dictionary<string, float[]> m_SnapValues = new Dictionary<string, float[]>();
        internal float[] SnapshotValues(string guid) { float[] v; return m_SnapValues.TryGetValue(guid, out v) ? v : null; }
        public AudioMixerGroup outputAudioMixerGroup { get { return m_OutputGroup; } set { m_OutputGroup = value; } }
        int Slot(string n)
        {
            uint h = (uint)Animator.StringToHash(n);
            for (int i = 0; i < m_ExposedHash.Length; i++) if (m_ExposedHash[i] == h) return m_ExposedSlot[i];
            return -1;
        }
        public bool SetFloat(string n, float v)
        {
            int s = Slot(n);
            if (KNL.AudioSystem.Debug) System.Console.Error.WriteLine("[audio] " + name + ".SetFloat(" + n + ", " + v + ") slot=" + s);
            if (s < 0) return false; m_Override[s] = v; return true;
        }
        public bool GetFloat(string n, out float v) { int s = Slot(n); v = 0; if (s < 0) return false; if (!m_Override.TryGetValue(s, out v)) v = s < m_Values.Length ? m_Values[s] : 0; return true; }
        public bool ClearFloat(string n) { int s = Slot(n); return s >= 0 && m_Override.Remove(s); }
        public AudioMixerSnapshot FindSnapshot(string n) { foreach (var s in m_Snapshots) if (s.name == n) return s; return null; }
        public void TransitionToSnapshots(AudioMixerSnapshot[] s, float[] w, float t)
        {
            if (s == null || s.Length == 0 || m_Values.Length == 0) return;
            var to = new float[m_Values.Length]; float wsum = 0;
            for (int i = 0; i < s.Length; i++)
            {
                float wi = w != null && i < w.Length ? w[i] : 1f; if (s[i] == null || s[i].m_Values == null) continue;
                wsum += wi; for (int k = 0; k < to.Length && k < s[i].m_Values.Length; k++) to[k] += s[i].m_Values[k] * wi;
            }
            if (wsum <= 0) return;
            for (int k = 0; k < to.Length; k++) to[k] /= wsum;
            Begin(to, t);
        }
        internal void Begin(float[] to, float t)
        {
            m_From = (float[])m_Values.Clone(); m_To = to; m_TransT = 0; m_TransDur = Mathf.Max(0f, t);
            if (m_TransDur <= 0) { m_Values = (float[])to.Clone(); m_From = m_To = null; }
        }
        internal void Tick(float dt)
        {
            if (m_To == null) return;
            m_TransT += dt; float k = Mathf.Clamp01(m_TransT / m_TransDur);
            for (int i = 0; i < m_Values.Length; i++) m_Values[i] = Mathf.Lerp(m_From[i], m_To[i], k);
            if (k >= 1f) m_From = m_To = null;
        }
        internal float Gain(int g)
        {
            float lin = 1f;
            for (int i = g; i >= 0 && i < m_GroupVolSlot.Length; i = m_GroupParent[i])
            {
                int s = m_GroupVolSlot[i]; float db; if (!m_Override.TryGetValue(s, out db)) db = s < m_Values.Length ? m_Values[s] : 0f;
                lin *= db <= -80f ? 0f : Mathf.Pow(10f, db / 20f);
            }
            if (m_OutputGroup != null && m_OutputGroup.m_Mixer != this) lin *= m_OutputGroup.Gain();
            return lin;
        }
    }
    public partial class AudioMixerGroup : UnityEngine.Object
    {
        internal AudioMixer m_Mixer; internal string m_Guid; internal int m_Index = -1;
        public AudioMixer audioMixer { get { return m_Mixer; } }
        internal float Gain()
        {
            if (m_Mixer == null) return 1f;
            if (m_Index < 0) m_Index = Array.IndexOf(m_Mixer.m_GroupGuids, m_Guid);
            return m_Mixer.Gain(m_Index < 0 ? 0 : m_Index);
        }
    }
    public partial class AudioMixerSnapshot
    {
        internal AudioMixer m_Mixer; internal float[] m_Values;
        public AudioMixer audioMixer { get { return m_Mixer; } }
        public void TransitionTo(float t) { if (m_Mixer != null && m_Values != null) m_Mixer.Begin((float[])m_Values.Clone(), t); }
    }
}

namespace KNL
{
    using UnityEngine;
    public static class AudioNative
    {
        const string Lib = "knlcore";
        [DllImport(Lib)] public static extern int knl_audio_init(int wantDevice);
        [DllImport(Lib)] public static extern void knl_audio_advance(float dt);
        [DllImport(Lib)] public static extern void knl_audio_shutdown();
        [DllImport(Lib)] public static extern int knl_snd_load(string path);
        [DllImport(Lib)] public static extern float knl_snd_length(int id);
        [DllImport(Lib)] public static extern int knl_snd_from_pack(long off, int len);
        [DllImport(Lib)] public static extern int knl_voice_play(int snd, float vol, float pan, float pitch, int loop, float start);
        [DllImport(Lib)] public static extern void knl_voice_set(int h, float vol, float pan, float pitch, int loop, int paused);
        [DllImport(Lib)] public static extern void knl_voice_stop(int h);
        [DllImport(Lib)] public static extern int knl_voice_state(int h, out float time);
        [DllImport(Lib)] public static extern void knl_voice_seek(int h, float t);
        [DllImport(Lib)] public static extern void knl_audio_master(float v);
        [DllImport(Lib)] public static extern void knl_audio_render(short[] outStereo, int frames);
    }

    /// Per-frame audio: effective volume = source volume x mixer group gain x 3D rolloff x listener,
    /// pan from the source's offset to the listener (main camera). One-shots get their own voices.
    public static class AudioSystem
    {
        public static int Plays, Missing;
        public static bool Device;
        public static bool Debug = Environment.GetEnvironmentVariable("KNL_AUDIODEBUG") == "1";
        static readonly List<UnityEngine.AudioSource> playing = new List<UnityEngine.AudioSource>();
        struct OneShot { public int voice; public UnityEngine.AudioSource src; public float vol; public Vector3 pos; }
        static readonly List<OneShot> oneShots = new List<OneShot>();
        static readonly List<UnityEngine.Audio.AudioMixer> mixers = new List<UnityEngine.Audio.AudioMixer>();
        static readonly HashSet<string> missingNames = new HashSet<string>();
        static bool inited;

        static BinaryWriter capture; static short[] captureBuf = new short[0]; static double captureAcc; static int captureFrames;
        public static void StartCapture(string path)
        {
            capture = new BinaryWriter(File.Create(path));
            capture.Write(new byte[44]);    // header patched in StopCapture
        }
        public static void StopCapture()
        {
            if (capture == null) return;
            int bytes = captureFrames * 4; var w = capture; w.Seek(0, SeekOrigin.Begin);
            w.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' }); w.Write(36 + bytes); w.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
            w.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' }); w.Write(16); w.Write((short)1); w.Write((short)2); w.Write(22050); w.Write(22050 * 4); w.Write((short)4); w.Write((short)16);
            w.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' }); w.Write(bytes); w.Close(); capture = null;
        }
        public static void Init(bool device)
        {
            if (inited) return; inited = true;
            try { Device = AudioNative.knl_audio_init(device ? 1 : 0) != 0; }
            catch (Exception e) { Log.Info("audio unavailable: " + e.Message); }
            Log.Info(Device ? "audio: 22050 Hz stereo" : "audio: headless (voices advance with game time)");
        }
        internal static void Register(UnityEngine.Audio.AudioMixer m) { mixers.Add(m); }

        public static int SoundOf(AudioClip c)
        {
            if (c.m_Sound != -2) return c.m_Sound;
            c.m_Sound = -1;
            if (Pack.Active)
            {   // SFX from the pack (ADPCM), music/long loops streamed from music/<name>.ogg
                long off; int len;
                if (Pack.Locate("sfx/" + c.name, out off, out len)) c.m_Sound = AudioNative.knl_snd_from_pack(off, len);
                else
                {
                    var m = Path.Combine(Path.Combine(Paths.DataDir, "music"), c.name + ".ogg");
                    if (File.Exists(m)) { c.m_Sound = AudioNative.knl_snd_load(m); c.m_Stream = true; }
                }
                if (c.m_Sound < 0 && missingNames.Add(c.name)) Missing++;
                return c.m_Sound;
            }
            var sfx = Path.Combine(Paths.DataDir, "audio/sfx/" + c.name + ".wav");
            var mus = Path.Combine(Paths.DataDir, "audio/music/" + c.name + ".ogg");
            string p = File.Exists(sfx) ? sfx : File.Exists(mus) ? mus : null;
            if (p != null) { try { c.m_Sound = AudioNative.knl_snd_load(p); c.m_Stream = p == mus; } catch (Exception) { c.m_Sound = -1; } }
            if (c.m_Sound < 0 && missingNames.Add(c.name)) { Missing++; if (Lifecycle.Verbose) Log.Write("WARN", "audio clip missing: " + c.name); }
            return c.m_Sound;
        }

        internal static int Start(UnityEngine.AudioSource src, AudioClip c, float volScale, bool loop, float start, bool oneShot = false)
        {
            int snd = SoundOf(c);
            Plays++;
            if (snd < 0) return -1;
            float vol, pan; Mix(src, src.transform.position, volScale, out vol, out pan);
            int v = AudioNative.knl_voice_play(snd, vol, pan, src.m_Pitch, loop ? 1 : 0, start);
            if (Debug) Console.Error.WriteLine(string.Format("[audio] t={0:F1} {1} on {2} vol={3:F3} (src {4:F2} group {5:F3} blend {6:F2} dist {7:F1}) pan={8:F2} loop={9} {10}", Time.unscaledTime, c.name, src.gameObject.name, vol, src.m_Volume, src.m_Output != null ? src.m_Output.Gain() : -1f, src.m_SpatialBlend, Vector3.Distance(src.transform.position, ListenerPos()), pan, loop, src.m_Output != null ? src.m_Output.name + "@" + (src.m_Output.m_Mixer != null ? src.m_Output.m_Mixer.name : "?") : "-"));
            if (v < 0) return -1;
            if (oneShot) oneShots.Add(new OneShot { voice = v, src = src, vol = volScale });
            else if (!playing.Contains(src)) playing.Add(src);
            return v;
        }
        internal static void StartAt(AudioClip c, Vector3 p, float vol)
        {
            int snd = c != null ? SoundOf(c) : -1; Plays++;
            if (snd < 0) return;
            float g, pan; Spatial(p, 1f, 1f, 500f, 0, null, out g, out pan);
            int v = AudioNative.knl_voice_play(snd, vol * g * UnityEngine.AudioListener.volume, pan, 1f, 0, 0f);
            if (v >= 0) oneShots.Add(new OneShot { voice = v, src = null, vol = vol, pos = p });
        }
        internal static void Forget(UnityEngine.AudioSource s) { playing.Remove(s); }
        internal static bool Alive(int voice) { float t; return AudioNative.knl_voice_state(voice, out t) != 0; }

        static void Mix(UnityEngine.AudioSource s, Vector3 pos, float scale, out float vol, out float pan)
        {
            float g = 1f; pan = s.m_Pan2D;
            if (s.m_SpatialBlend > 0f)
            {
                float sg, sp; Spatial(pos, s.m_SpatialBlend, s.m_MinDistance, s.m_MaxDistance, s.m_Rolloff, s.m_RolloffCurve, out sg, out sp);
                g = sg; pan = Mathf.Lerp(s.m_Pan2D, sp, s.m_SpatialBlend);
            }
            float group = s.m_Output != null ? s.m_Output.Gain() : 1f;
            bool paused = UnityEngine.AudioListener.pause && !s.m_IgnorePause;
            vol = s.m_Mute || paused ? 0f : s.m_Volume * scale * g * group * UnityEngine.AudioListener.volume;
        }

        static void Spatial(Vector3 p, float blend, float minD, float maxD, int rolloff, AnimationCurve curve, out float gain, out float pan)
        {
            Vector3 l = ListenerPos();
            float dx = p.x - l.x, dy = p.y - l.y, dz = p.z - l.z;
            float d = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
            if (rolloff == 2 && curve != null) gain = Mathf.Clamp01(curve.Evaluate(maxD > 0 ? d / maxD : 0));
            else if (rolloff == 1) gain = Mathf.Clamp01(1f - (d - minD) / Mathf.Max(0.01f, maxD - minD));
            else gain = d <= minD ? 1f : d >= maxD ? 0f : minD / d;
            gain = Mathf.Lerp(1f, gain, blend);
            pan = d > 0.001f ? Mathf.Clamp(dx / d, -1f, 1f) : 0f;
        }

        static UnityEngine.AudioListener listener; static int listenerFrame = -1;
        static Vector3 ListenerPos()
        {
            if (listenerFrame != Time.frameCount)
            {
                listenerFrame = Time.frameCount;
                if (listener == null || listener.m_Destroyed || !listener.isActiveAndEnabled)
                {
                    listener = null;
                    var all = AudioListener.s_All;
                    for (int i = 0; i < all.Count; i++)
                    {
                        var al = all[i];
                        if (al != null && !al.m_Destroyed && al.isActiveAndEnabled) { listener = al; break; }
                    }
                }
            }
            if (listener != null) return listener.transform.position;
            var cam = RenderSystem.MainCamera;
            return cam != null ? cam.transform.position : Vector3.zero;
        }

        /// once per frame from the engine loop
        public static void Update(float unscaledDt)
        {
            if (!inited) return;
            foreach (var m in mixers) m.Tick(unscaledDt);
            if (capture != null)
            {   // offline capture: render the mix for this frame's duration
                captureAcc += unscaledDt * 22050; int n = (int)captureAcc; captureAcc -= n;
                if (captureBuf.Length < n * 2) captureBuf = new short[n * 2];
                AudioNative.knl_audio_render(captureBuf, n);
                for (int i = 0; i < n * 2; i++) capture.Write(captureBuf[i]);
                captureFrames += n;
            }
            else if (!Device) AudioNative.knl_audio_advance(unscaledDt);
            for (int i = playing.Count - 1; i >= 0; i--)
            {
                var s = playing[i];
                if (s == null || s.m_Destroyed || s.m_Voice < 0 || !Alive(s.m_Voice)) { if (s != null) s.m_Voice = -1; playing.RemoveAt(i); continue; }
                bool active = s.gameObject.activeInHierarchy && s.enabled;
                if (!active) { AudioNative.knl_voice_stop(s.m_Voice); s.m_Voice = -1; playing.RemoveAt(i); continue; }
                float vol, pan; Mix(s, s.transform.position, 1f, out vol, out pan);
                AudioNative.knl_voice_set(s.m_Voice, vol, pan, s.m_Pitch, s.m_Loop ? 1 : 0, s.m_Paused ? 1 : 0);
            }
            for (int i = oneShots.Count - 1; i >= 0; i--)
            {
                var o = oneShots[i];
                if (!Alive(o.voice)) { oneShots.RemoveAt(i); continue; }
                if (o.src != null && !o.src.m_Destroyed)
                {
                    float vol, pan; Mix(o.src, o.src.transform.position, o.vol, out vol, out pan);
                    AudioNative.knl_voice_set(o.voice, vol, pan, o.src.m_Pitch, 0, 0);
                }
            }
        }

        /// AudioSource.playOnAwake: Unity starts these when the source is enabled
        public static void OnEnable(UnityEngine.AudioSource s) { if (s.m_PlayOnAwake && s.m_Clip != null && s.m_Voice < 0) s.Play(); }
    }
}
