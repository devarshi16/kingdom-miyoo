/*
 * knlaudio.c - Kingdom New Lands (Miyoo Mini Plus port) audio mixer.  Clean-room, C99.
 *
 *   sounds : PCM s16 WAV loaded into RAM (SFX), or Ogg Vorbis files streamed (music, ambience)
 *   voices : up to KNL_VOICES concurrent players: volume, pan, pitch (linear resampling), loop, seek
 *   output : 22050 Hz stereo s16 through SDL audio (desktop) or straight to the SigmaStar MI_AO
 *            device from a mixer thread (-DKNL_MIYOO, D21); headless builds advance voice
 *            positions with knl_audio_advance(dt) so AudioSource.isPlaying/time stay correct.
 *
 * Third party: stb_vorbis v1.22 (public domain), pinned with stb_image (commit f58f558c).
 */
#define STB_VORBIS_HEADER_ONLY
#include "third_party/stb_vorbis.c"
#undef STB_VORBIS_HEADER_ONLY

#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <math.h>

#ifdef KNL_SDL
#include <SDL.h>
#endif
#ifdef KNL_MIYOO
#include <pthread.h>
#include "mi_sys.h"
#include "mi_ao.h"
#endif

#if defined(_WIN32)
#define KNL_API __declspec(dllexport)
#else
#define KNL_API __attribute__((visibility("default")))
#endif

#define KNL_SOUNDS 1024
#define KNL_VOICES 32
#define KNL_RATE 22050
#define STREAM_BUF 4096            /* interleaved shorts decoded ahead for a streaming voice */

typedef struct {
    int used;
    int16_t *pcm;                  /* RAM sounds: interleaved */
    uint8_t *adpcm; int block;     /* pack SFX: IMA ADPCM mono, blocks of `block` samples (4-byte header + block/2 bytes) */
    int frames, channels, rate;
    char *path;                    /* streamed sounds: ogg file path */
    int stream;
} Sound;

typedef struct {
    int active, paused, loop, gen;
    int snd;
    double pos;                    /* source frame position (RAM) */
    double step;                   /* source frames per output frame */
    float gl, gr;                  /* gains */
    /* streaming */
    stb_vorbis *vf;
    short buf[STREAM_BUF];
    int buf_frames, buf_pos, sch;
    double frac;
    long stream_frames_done;       /* frames consumed (for time) */
    /* adpcm */
    int blk_no; int16_t blk[1024];
} Voice;

static Sound g_snd[KNL_SOUNDS];
static Voice g_v[KNL_VOICES];
static int g_open;
static float g_master = 1.0f;
#ifdef KNL_SDL
static SDL_AudioDeviceID g_dev;
#define LOCK()   do { if (g_dev) SDL_LockAudioDevice(g_dev); } while (0)
#define UNLOCK() do { if (g_dev) SDL_UnlockAudioDevice(g_dev); } while (0)
#elif defined(KNL_MIYOO)
static pthread_mutex_t g_mx = PTHREAD_MUTEX_INITIALIZER;
static pthread_t g_thr; static volatile int g_run;
#define LOCK()   pthread_mutex_lock(&g_mx)
#define UNLOCK() pthread_mutex_unlock(&g_mx)
#else
#define LOCK()
#define UNLOCK()
#endif

/* ---------------------------------------------------------------- loading */
static int16_t *load_wav(const char *path, int *frames, int *channels, int *rate)
{
    FILE *f = fopen(path, "rb");
    if (!f) return NULL;
    unsigned char h[12];
    if (fread(h, 1, 12, f) != 12 || memcmp(h, "RIFF", 4) || memcmp(h + 8, "WAVE", 4)) { fclose(f); return NULL; }
    int ch = 1, rt = KNL_RATE, bits = 16;
    int16_t *data = NULL;
    for (;;) {
        unsigned char c[8];
        if (fread(c, 1, 8, f) != 8) break;
        uint32_t len = c[4] | (c[5] << 8) | (c[6] << 16) | ((uint32_t)c[7] << 24);
        if (!memcmp(c, "fmt ", 4)) {
            unsigned char fm[16];
            if (len < 16 || fread(fm, 1, 16, f) != 16) break;
            ch = fm[2] | (fm[3] << 8);
            rt = fm[4] | (fm[5] << 8) | (fm[6] << 16) | (fm[7] << 24);
            bits = fm[14] | (fm[15] << 8);
            if (len > 16) fseek(f, len - 16, SEEK_CUR);
        } else if (!memcmp(c, "data", 4)) {
            if (bits != 16) break;
            data = (int16_t *)malloc(len ? len : 2);
            if (!data) break;
            size_t got = fread(data, 1, len, f);
            *frames = (int)(got / (2 * ch));
            break;
        } else fseek(f, len + (len & 1), SEEK_CUR);
    }
    fclose(f);
    *channels = ch; *rate = rt;
    return data;
}

/* returns sound id, or -1. .wav -> RAM, anything else -> Vorbis stream */
KNL_API int knl_snd_load(const char *path)
{
    int id = -1;
    for (int i = 0; i < KNL_SOUNDS; i++) if (!g_snd[i].used) { id = i; break; }
    if (id < 0) return -1;
    Sound *s = &g_snd[id];
    memset(s, 0, sizeof *s);
    size_t n = strlen(path);
    if (n > 4 && !strcmp(path + n - 4, ".wav")) {
        s->pcm = load_wav(path, &s->frames, &s->channels, &s->rate);
        if (!s->pcm) return -1;
    } else {
        int err = 0;
        stb_vorbis *v = stb_vorbis_open_filename(path, &err, NULL);
        if (!v) return -1;
        stb_vorbis_info inf = stb_vorbis_get_info(v);
        s->channels = inf.channels; s->rate = (int)inf.sample_rate;
        s->frames = (int)stb_vorbis_stream_length_in_samples(v);
        stb_vorbis_close(v);
        s->path = (char *)malloc(n + 1);
        memcpy(s->path, path, n + 1);
        s->stream = 1;
    }
    s->used = 1;
    return id;
}

KNL_API float knl_snd_length(int id)
{
    if (id < 0 || id >= KNL_SOUNDS || !g_snd[id].used || g_snd[id].rate <= 0) return 0;
    return (float)g_snd[id].frames / g_snd[id].rate;
}

/* ---------------------------------------------------------------- IMA ADPCM (pack SFX) */
static const int16_t ima_step[89] = {7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31, 34, 37, 41, 45, 50, 55, 60, 66,
    73, 80, 88, 97, 107, 118, 130, 143, 157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449, 494, 544, 598, 658, 724, 796,
    876, 963, 1060, 1166, 1282, 1411, 1552, 1707, 1878, 2066, 2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428, 4871, 5358, 5894,
    6484, 7132, 7845, 8630, 9493, 10442, 11487, 12635, 13899, 15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767};
static const int8_t ima_idx[16] = {-1, -1, -1, -1, 2, 4, 6, 8, -1, -1, -1, -1, 2, 4, 6, 8};

static void adpcm_block(const Sound *s, int b, int16_t *dst)
{
    const uint8_t *p = s->adpcm + (size_t)b * (4 + s->block / 2);
    int pred = (int16_t)(p[0] | (p[1] << 8)), index = p[2];
    p += 4;
    for (int i = 0; i < s->block; i++) {
        int code = (i & 1) ? (p[i >> 1] >> 4) : (p[i >> 1] & 15);
        int step = ima_step[index], delta = step >> 3;
        if (code & 4) delta += step;
        if (code & 2) delta += step >> 1;
        if (code & 1) delta += step >> 2;
        pred += (code & 8) ? -delta : delta;
        if (pred > 32767) pred = 32767;
        if (pred < -32768) pred = -32768;
        index += ima_idx[code];
        if (index < 0) index = 0;
        if (index > 88) index = 88;
        dst[i] = (int16_t)pred;
    }
}

extern int knl_pack_read(long long off, void *dst, int len);

/* SFX record from the device pack: u32 rate, u32 samples, u32 block, then ADPCM blocks */
KNL_API int knl_snd_from_pack(long long off, int len)
{
    int id = -1;
    for (int i = 0; i < KNL_SOUNDS; i++) if (!g_snd[i].used) { id = i; break; }
    if (id < 0 || len < 12) return -1;
    uint32_t hdr[3];
    if (!knl_pack_read(off, hdr, 12) || hdr[2] == 0 || hdr[2] > 1024) return -1;
    uint8_t *d = (uint8_t *)malloc((size_t)len - 12);
    if (!d || !knl_pack_read(off + 12, d, len - 12)) { free(d); return -1; }
    Sound *s = &g_snd[id];
    memset(s, 0, sizeof *s);
    s->adpcm = d; s->block = (int)hdr[2]; s->rate = (int)hdr[0]; s->frames = (int)hdr[1]; s->channels = 1; s->used = 1;
    return id;
}

/* ---------------------------------------------------------------- voices */
static Voice *vget(int h)
{
    int i = h & 255, gen = h >> 8;
    if (h < 0 || i >= KNL_VOICES) return NULL;
    Voice *v = &g_v[i];
    return (v->active && v->gen == gen) ? v : NULL;
}

static void gains(Voice *v, float vol, float pan)
{
    if (pan < -1) pan = -1;
    if (pan > 1) pan = 1;
    float a = (pan + 1) * 0.785398f;                    /* equal power */
    v->gl = vol * cosf(a) * 1.41421f;
    v->gr = vol * sinf(a) * 1.41421f;
    if (v->gl > vol) v->gl = vol;
    if (v->gr > vol) v->gr = vol;
}

static void stream_close(Voice *v) { if (v->vf) { stb_vorbis_close(v->vf); v->vf = NULL; } }

static int stream_open(Voice *v, float start)
{
    Sound *s = &g_snd[v->snd];
    int err = 0;
    v->vf = stb_vorbis_open_filename(s->path, &err, NULL);
    if (!v->vf) return 0;
    v->sch = s->channels;
    long at = (long)(start * s->rate);
    if (at > 0 && at < s->frames) stb_vorbis_seek(v->vf, (unsigned)at); else at = 0;
    v->stream_frames_done = at;
    v->buf_frames = v->buf_pos = 0; v->frac = 0;
    return 1;
}

/* plays sound `snd`; returns a voice handle (index | gen << 8) or -1. Steals the oldest voice when full. */
KNL_API int knl_voice_play(int snd, float vol, float pan, float pitch, int loop, float start)
{
    if (snd < 0 || snd >= KNL_SOUNDS || !g_snd[snd].used) return -1;
    LOCK();
    int idx = -1;
    for (int i = 0; i < KNL_VOICES; i++) if (!g_v[i].active) { idx = i; break; }
    if (idx < 0) {          /* steal a non-looping voice first */
        for (int i = 0; i < KNL_VOICES; i++) if (!g_v[i].loop) { idx = i; break; }
        if (idx < 0) idx = 0;
        stream_close(&g_v[idx]);
    }
    Voice *v = &g_v[idx];
    int gen = (v->gen + 1) & 0x7FFFFF;
    memset(v, 0, sizeof *v);
    v->gen = gen; v->snd = snd; v->loop = loop;
    Sound *s = &g_snd[snd];
    v->step = (double)s->rate / KNL_RATE * (pitch > 0.01f ? pitch : 0.01f);
    gains(v, vol, pan);
    if (s->stream) { if (!stream_open(v, start)) { UNLOCK(); return -1; } }
    else { v->pos = start * s->rate; if (v->pos >= s->frames) v->pos = 0; }
    v->blk_no = -1;
    v->active = 1;
    UNLOCK();
    return idx | (gen << 8);
}

KNL_API void knl_voice_set(int h, float vol, float pan, float pitch, int loop, int paused)
{
    LOCK();
    Voice *v = vget(h);
    if (v) {
        gains(v, vol, pan);
        v->step = (double)g_snd[v->snd].rate / KNL_RATE * (pitch > 0.01f ? pitch : 0.01f);
        v->loop = loop; v->paused = paused;
    }
    UNLOCK();
}

KNL_API void knl_voice_stop(int h)
{
    LOCK();
    Voice *v = vget(h);
    if (v) { stream_close(v); v->active = 0; }
    UNLOCK();
}

/* 1 while playing (or paused); *time = position in seconds */
KNL_API int knl_voice_state(int h, float *time)
{
    LOCK();
    Voice *v = vget(h);
    int r = 0;
    if (v) {
        Sound *s = &g_snd[v->snd];
        double fr = s->stream ? (double)v->stream_frames_done : v->pos;
        if (time) *time = (float)(fr / (s->rate > 0 ? s->rate : KNL_RATE));
        r = 1;
    } else if (time) *time = 0;
    UNLOCK();
    return r;
}

KNL_API void knl_voice_seek(int h, float t)
{
    LOCK();
    Voice *v = vget(h);
    if (v) {
        Sound *s = &g_snd[v->snd];
        if (s->stream) { stream_close(v); stream_open(v, t); }
        else { v->pos = t * s->rate; if (v->pos < 0 || v->pos >= s->frames) v->pos = 0; }
    }
    UNLOCK();
}

KNL_API void knl_audio_master(float vol) { g_master = vol; }

/* ---------------------------------------------------------------- mixing */
/* fetch one source frame (L, R) as floats from a streaming voice; returns 0 at end */
static int stream_frame(Voice *v, float *l, float *r)
{
    while (v->buf_pos >= v->buf_frames) {
        if (!v->vf) return 0;
        int got = stb_vorbis_get_samples_short_interleaved(v->vf, v->sch, v->buf, STREAM_BUF);
        if (got <= 0) {
            if (!v->loop) return 0;
            stb_vorbis_seek_start(v->vf);
            v->stream_frames_done = 0;
            got = stb_vorbis_get_samples_short_interleaved(v->vf, v->sch, v->buf, STREAM_BUF);
            if (got <= 0) return 0;
        }
        v->buf_frames = got; v->buf_pos = 0;
    }
    const short *p = v->buf + v->buf_pos * v->sch;
    *l = p[0]; *r = v->sch > 1 ? p[1] : p[0];
    return 1;
}

static void mix(int16_t *out, int frames, int render)
{
    static float acc[2 * 2048];
    while (frames > 0) {
        int n = frames > 2048 ? 2048 : frames;
        if (render) memset(acc, 0, sizeof(float) * 2 * n);
        for (int vi = 0; vi < KNL_VOICES; vi++) {
            Voice *v = &g_v[vi];
            if (!v->active || v->paused) continue;
            Sound *s = &g_snd[v->snd];
            if (s->stream) {
                for (int i = 0; i < n; i++) {
                    float l, r;
                    if (!stream_frame(v, &l, &r)) { stream_close(v); v->active = 0; break; }
                    if (render) { acc[2 * i] += l * v->gl; acc[2 * i + 1] += r * v->gr; }
                    v->frac += v->step;
                    while (v->frac >= 1.0) { v->frac -= 1.0; v->buf_pos++; v->stream_frames_done++; if (v->buf_pos >= v->buf_frames && !stream_frame(v, &l, &r)) break; }
                }
            } else {
                int ch = s->channels;
                for (int i = 0; i < n; i++) {
                    int p = (int)v->pos;
                    if (p >= s->frames) {
                        if (v->loop && s->frames > 0) { v->pos -= s->frames; p = (int)v->pos; }
                        else { v->active = 0; break; }
                    }
                    if (render && (v->gl != 0 || v->gr != 0)) {     /* silent voices only advance */
                        float fr = (float)(v->pos - p);
                        float l, r;
                        if (s->adpcm) {
                            int b = p / s->block, o = p - b * s->block;
                            if (b != v->blk_no) { adpcm_block(s, b, v->blk); v->blk_no = b; }
                            int o2 = o + 1 < s->block && p + 1 < s->frames ? o + 1 : o;
                            l = r = v->blk[o] + (v->blk[o2] - v->blk[o]) * fr;
                        } else {
                            int q = p + 1 < s->frames ? p + 1 : (v->loop ? 0 : p);
                            l = s->pcm[p * ch] + (s->pcm[q * ch] - s->pcm[p * ch]) * fr;
                            r = ch > 1 ? s->pcm[p * ch + 1] + (s->pcm[q * ch + 1] - s->pcm[p * ch + 1]) * fr : l;
                        }
                        acc[2 * i] += l * v->gl; acc[2 * i + 1] += r * v->gr;
                    }
                    v->pos += v->step;
                }
            }
        }
        if (render)
            for (int i = 0; i < 2 * n; i++) {
                float x = acc[i] * g_master;
                /* soft knee above 24576 (-2.5 dBFS): only samples that would clip are bent */
                float ax = x < 0 ? -x : x;
                if (ax > 24576.f) { float o = ax - 24576.f; ax = 24576.f + 8191.f * o / (o + 8191.f); x = x < 0 ? -ax : ax; }
                if (x > 32767) x = 32767;
                if (x < -32768) x = -32768;
                out[i] = (int16_t)x;
            }
        out += 2 * n; frames -= n;
    }
}

#ifdef KNL_SDL
static void sdl_cb(void *ud, Uint8 *stream, int len) { (void)ud; mix((int16_t *)stream, len / 4, 1); }
#endif
#ifdef KNL_MIYOO
#define AO_FRAMES 1024
/* the mixer thread: mix one period, hand it to MI_AO (SendFrame blocks while its queue is full) */
static void *ao_thread(void *ud)
{
    (void)ud;
    static int16_t buf[2 * AO_FRAMES];
    while (g_run) {
        LOCK(); mix(buf, AO_FRAMES, 1); UNLOCK();
        MI_AUDIO_Frame_t fr; memset(&fr, 0, sizeof fr);
        fr.eBitwidth = E_MI_AUDIO_BIT_WIDTH_16; fr.eSoundmode = E_MI_AUDIO_SOUND_MODE_STEREO;
        fr.u32Len = sizeof buf; fr.apVirAddr[0] = buf;
        MI_AO_SendFrame(0, 0, &fr, -1);
    }
    return NULL;
}
static int ao_open(void)
{
    MI_AUDIO_Attr_t a; memset(&a, 0, sizeof a);
    a.eBitwidth = E_MI_AUDIO_BIT_WIDTH_16; a.eWorkmode = E_MI_AUDIO_MODE_I2S_MASTER;
    a.u32FrmNum = 6; a.u32PtNumPerFrm = AO_FRAMES; a.u32ChnCnt = 2;
    a.eSoundmode = E_MI_AUDIO_SOUND_MODE_STEREO; a.eSamplerate = (MI_AUDIO_SampleRate_e)KNL_RATE;
    if (MI_AO_SetPubAttr(0, &a) || MI_AO_Enable(0) || MI_AO_EnableChn(0, 0)) return 0;
    MI_SYS_ChnPort_t port = { E_MI_MODULE_ID_AO, 0, 0, 0 };
    MI_SYS_SetChnOutputPortDepth(&port, 12, 13);
    g_run = 1;
    if (pthread_create(&g_thr, NULL, ao_thread, NULL)) { g_run = 0; MI_AO_DisableChn(0, 0); MI_AO_Disable(0); return 0; }
    return 1;
}
#endif

/* opens the output device; returns 1 with sound, 0 headless (voices then advance via knl_audio_advance) */
KNL_API int knl_audio_init(int want_device)
{
    memset(g_v, 0, sizeof g_v);
    g_open = 0;
#ifdef KNL_SDL
    if (want_device) {
        if (!SDL_WasInit(SDL_INIT_AUDIO) && SDL_InitSubSystem(SDL_INIT_AUDIO) != 0) return 0;
        SDL_AudioSpec want, have;
        memset(&want, 0, sizeof want);
        want.freq = KNL_RATE; want.format = AUDIO_S16SYS; want.channels = 2; want.samples = 1024; want.callback = sdl_cb;
        g_dev = SDL_OpenAudioDevice(NULL, 0, &want, &have, 0);
        if (g_dev) { SDL_PauseAudioDevice(g_dev, 0); g_open = 1; }
    }
#elif defined(KNL_MIYOO)
    if (want_device) g_open = ao_open();
#else
    (void)want_device;
#endif
    return g_open;
}

/* headless: advance all voices by dt seconds without rendering */
KNL_API void knl_audio_advance(float dt)
{
    if (g_open) return;
    static int16_t scratch[2 * 2048];
    int frames = (int)(dt * KNL_RATE + 0.5f);
    while (frames > 0) { int n = frames > 2048 ? 2048 : frames; mix(scratch, n, 0); frames -= n; }
}

/* mixes `frames` stereo frames into out (tests / offline capture) */
KNL_API void knl_audio_render(int16_t *out, int frames) { mix(out, frames, 1); }

KNL_API void knl_audio_shutdown(void)
{
#ifdef KNL_SDL
    if (g_dev) { SDL_CloseAudioDevice(g_dev); g_dev = 0; }
#elif defined(KNL_MIYOO)
    if (g_run) { g_run = 0; pthread_join(g_thr, NULL); MI_AO_DisableChn(0, 0); MI_AO_Disable(0); }
#endif
    for (int i = 0; i < KNL_VOICES; i++) stream_close(&g_v[i]);
}

/* stb_vorbis implementation (kept in this translation unit) */
#undef STB_VORBIS_HEADER_ONLY
#include "third_party/stb_vorbis.c"
