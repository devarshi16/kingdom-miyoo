/*
 * knlcore — native core for the Kingdom New Lands Miyoo Mini port.
 *
 * Software renderer into a small RGBA framebuffer (default 320x240) plus a
 * platform layer. Desktop: optional SDL2 window (compile with -DKNL_SDL),
 * exact integer upscale. Headless: render to PNG. Miyoo Mini (-DKNL_MIYOO, D21): 2x blit
 * straight into /dev/fb0 (rotated 180, the panel is mounted upside down), buttons from evdev;
 * audio still goes through SDL (knlaudio.c, Miyoo SDL2 with the MI_AO backend).
 *
 * C ABI only; called from the managed UnityEngine replacement via P/Invoke.
 */
#include <stdint.h>
#include <stdlib.h>
#include <string.h>
#include <math.h>

#define STB_IMAGE_IMPLEMENTATION
#define STBI_ONLY_PNG
#include "third_party/stb_image.h"
#define STB_IMAGE_WRITE_IMPLEMENTATION
#include "third_party/stb_image_write.h"

#ifdef KNL_SDL
#include <SDL2/SDL.h>
#endif
#ifdef KNL_MIYOO
#include <fcntl.h>
#include <unistd.h>
#include <time.h>
#include <sys/ioctl.h>
#include <sys/mman.h>
#include <linux/fb.h>
#include <linux/input.h>
#include <pthread.h>
static int g_fbfd = -1, g_evfd = -1, g_page; static uint32_t *g_fbmem; static size_t g_fblen;
static struct fb_var_screeninfo g_vinfo; static uint32_t g_keys;
#endif

#define KNL_API __attribute__((visibility("default")))

/* ------------------------------------------------------------------ state */
/* textures: straight RGBA (px, R in low byte) or 8-bit indexed with an RGBA palette (idx + pal),
   the device pack's format (D19): ~4x less memory for sprites. */
typedef struct { int w, h; uint32_t *px; uint8_t *idx; uint32_t *pal; } Tex;
#define TEXEL(T, i) ((T)->idx ? (T)->pal[(T)->idx[i]] : (T)->px[i])

static int g_w, g_h, g_scale;
static uint32_t *g_fb;            /* framebuffer, RGBA (R low byte), alpha ignored */
static Tex *g_tex; static int g_ntex, g_captex;
static int g_stats_sprites, g_stats_pixels;

#ifdef KNL_SDL
static SDL_Window *g_win; static SDL_Renderer *g_ren; static SDL_Texture *g_sdltex;
#endif

static void wait_idle(void);
#ifdef KNL_MIYOO
static void start_worker(void), stop_worker(void);
#endif

/* ------------------------------------------------------------- lifecycle */
KNL_API int knl_init(int w, int h, int scale, int window)
{
    g_w = w; g_h = h; g_scale = scale < 1 ? 1 : scale;
    g_fb = (uint32_t *)calloc((size_t)w * h, 4);
    if (!g_fb) return -1;
#ifdef KNL_SDL
    if (window) {
        if (SDL_Init(SDL_INIT_VIDEO | SDL_INIT_EVENTS) != 0) return -2;
        g_win = SDL_CreateWindow("Kingdom New Lands (KNL port)", SDL_WINDOWPOS_CENTERED,
                                 SDL_WINDOWPOS_CENTERED, w * g_scale, h * g_scale, 0);
        g_ren = SDL_CreateRenderer(g_win, -1, SDL_RENDERER_PRESENTVSYNC);
        SDL_SetHint(SDL_HINT_RENDER_SCALE_QUALITY, "0");          /* nearest */
        SDL_RenderSetLogicalSize(g_ren, w, h);
        SDL_RenderSetIntegerScale(g_ren, SDL_TRUE);
        g_sdltex = SDL_CreateTexture(g_ren, SDL_PIXELFORMAT_ABGR8888,
                                     SDL_TEXTUREACCESS_STREAMING, w, h);
    }
#elif defined(KNL_MIYOO)
    if (window) {
        g_fbfd = open("/dev/fb0", O_RDWR);
        if (g_fbfd < 0 || ioctl(g_fbfd, FBIOGET_VSCREENINFO, &g_vinfo) < 0) return -2;
        if (g_vinfo.bits_per_pixel != 32 || (int)g_vinfo.xres != w * g_scale || (int)g_vinfo.yres != h * g_scale) return -3;
        g_fblen = (size_t)g_vinfo.xres * g_vinfo.yres_virtual * 4;
        g_fbmem = (uint32_t *)mmap(NULL, g_fblen, PROT_READ | PROT_WRITE, MAP_SHARED, g_fbfd, 0);
        if (g_fbmem == MAP_FAILED) { g_fbmem = NULL; return -4; }
        g_evfd = open("/dev/input/event0", O_RDONLY | O_NONBLOCK);
        start_worker();
    }
#else
    (void)window;
#endif
    return 0;
}

KNL_API int knl_has_window(void)
{
#ifdef KNL_SDL
    return g_win != NULL;
#elif defined(KNL_MIYOO)
    return g_fbmem != NULL;
#else
    return 0;
#endif
}

KNL_API void knl_shutdown(void)
{
#ifdef KNL_SDL
    if (g_sdltex) SDL_DestroyTexture(g_sdltex);
    if (g_ren) SDL_DestroyRenderer(g_ren);
    if (g_win) SDL_DestroyWindow(g_win);
    if (g_win) SDL_Quit();
    g_win = NULL;
#endif
#ifdef KNL_MIYOO
    stop_worker();
    if (g_fbmem) { memset(g_fbmem, 0, g_fblen); munmap(g_fbmem, g_fblen); g_fbmem = NULL; }
    if (g_fbfd >= 0) close(g_fbfd);
    if (g_evfd >= 0) close(g_evfd);
    g_fbfd = g_evfd = -1;
#endif
    for (int i = 0; i < g_ntex; i++) { free(g_tex[i].px); free(g_tex[i].idx); free(g_tex[i].pal); }
    free(g_tex); g_tex = NULL; g_ntex = g_captex = 0;
    free(g_fb); g_fb = NULL;
}

/* --------------------------------------------------------------- textures */
static int add_tex(int w, int h, uint32_t *px)
{
    if (g_ntex == g_captex) {
        wait_idle();   /* the render worker may be reading g_tex */
        g_captex = g_captex ? g_captex * 2 : 256;
        g_tex = (Tex *)realloc(g_tex, sizeof(Tex) * g_captex);
    }
    g_tex[g_ntex].w = w; g_tex[g_ntex].h = h; g_tex[g_ntex].px = px; g_tex[g_ntex].idx = NULL; g_tex[g_ntex].pal = NULL;
    return g_ntex++;
}

/* ---- device pack: textures are read straight from the open pack file (no managed copies) */
static FILE *g_pack;
KNL_API int knl_pack_open(const char *path) { if (g_pack) fclose(g_pack); g_pack = fopen(path, "rb"); return g_pack != NULL; }

/* indexed sprite: npal RGBA palette entries followed by w*h indices, at absolute file offset */
KNL_API int knl_tex_from_pack_indexed(long long off, int w, int h, int npal)
{
    if (!g_pack || npal < 1 || npal > 256) return -1;
    uint32_t *pal = (uint32_t *)calloc(256, 4);
    uint8_t *idx = (uint8_t *)malloc((size_t)w * h);
    if (!pal || !idx) { free(pal); free(idx); return -1; }
    fseek(g_pack, (long)off, SEEK_SET);
    if (fread(pal, 4, npal, g_pack) != (size_t)npal || fread(idx, 1, (size_t)w * h, g_pack) != (size_t)w * h) { free(pal); free(idx); return -1; }
    int t = add_tex(w, h, NULL);
    g_tex[t].idx = idx; g_tex[t].pal = pal;
    return t;
}

/* RGBA texture at absolute file offset */
KNL_API int knl_tex_from_pack_rgba(long long off, int w, int h)
{
    if (!g_pack) return -1;
    uint32_t *px = (uint32_t *)malloc((size_t)w * h * 4);
    if (!px) return -1;
    fseek(g_pack, (long)off, SEEK_SET);
    if (fread(px, 4, (size_t)w * h, g_pack) != (size_t)w * h) { free(px); return -1; }
    return add_tex(w, h, px);
}

/* raw read from the pack (audio uses it) */
int knl_pack_read(long long off, void *dst, int len)
{
    if (!g_pack) return 0;
    fseek(g_pack, (long)off, SEEK_SET);
    return (int)fread(dst, 1, (size_t)len, g_pack) == len;
}

/* memory used by textures (bytes) */
KNL_API long long knl_tex_bytes(void)
{
    long long b = 0;
    for (int i = 0; i < g_ntex; i++) b += g_tex[i].idx ? (long long)g_tex[i].w * g_tex[i].h + 1024 : (long long)g_tex[i].w * g_tex[i].h * 4;
    return b;
}

/* returns handle >= 0, or -1. Pixels stored top row first (as in the PNG). */
KNL_API int knl_tex_load_png(const char *path, int *out_w, int *out_h)
{
    int w, h, n;
    unsigned char *d = stbi_load(path, &w, &h, &n, 4);
    if (!d) return -1;
    if (out_w) *out_w = w;
    if (out_h) *out_h = h;
    return add_tex(w, h, (uint32_t *)d);
}

KNL_API int knl_tex_create(int w, int h, const uint8_t *rgba)
{
    uint32_t *px = (uint32_t *)malloc((size_t)w * h * 4);
    if (!px) return -1;
    if (rgba) memcpy(px, rgba, (size_t)w * h * 4); else memset(px, 0, (size_t)w * h * 4);
    return add_tex(w, h, px);
}

/* ------------------------------------------------------------------ draw */
static void do_clear(uint32_t rgba)
{
    for (int i = 0, n = g_w * g_h; i < n; i++) g_fb[i] = rgba;
    g_stats_sprites = g_stats_pixels = 0;
}

/* x / 255 without a divide, exact for 0 <= x <= 65534 (all our products are <= 255*255; knl_selftest checks) */
#define DIV255(x) (((x) + 1 + ((x) >> 8)) >> 8)

/* Axis-aligned sprites (b == c == 0: any scale, flips; most of the game) get their texel column per
 * screen column and texel row per screen row precomputed with the same float expressions as the
 * general path, so the result is bit-identical and the inner loop has no float math. Columns/rows
 * that miss the texture are trimmed off the loop bounds. Returns 0 when the sprite is not visible. */
static float g_ucol[4096];
static int g_iucol[4096];
static int aa_columns(const Tex *T, float ia, float ic, float tx, int *x0, int *x1)
{
    int a = *x1, b = *x0;
    for (int x = *x0; x < *x1; x++) {
        float u = ia * (x + 0.5f - tx) + ic * 0.0f;
        g_ucol[x] = u;
        int ok = !(u < 0) && (int)u < T->w;
        g_iucol[x] = ok ? (int)u : -1;
        if (ok) { if (x < a) a = x; b = x + 1; }
    }
    *x0 = a; *x1 = b;
    return a < b;
}

static inline uint32_t blend(uint32_t dst, uint32_t r, uint32_t g, uint32_t b, uint32_t a, int mode)
{
    if (mode == 0 && a >= 255) return (r & 255) | ((g & 255) << 8) | ((b & 255) << 16) | 0xFF000000u;
    if (mode == 1) {                    /* additive (src * a + dst) */
        uint32_t dr = (dst & 255) + DIV255((r & 255) * a);
        uint32_t dg = ((dst >> 8) & 255) + DIV255((g & 255) * a);
        uint32_t db = ((dst >> 16) & 255) + DIV255((b & 255) * a);
        if (dr > 255) dr = 255;
        if (dg > 255) dg = 255;
        if (db > 255) db = 255;
        return dr | (dg << 8) | (db << 16) | 0xFF000000u;
    } else {                            /* alpha (paired Red+Blue fast path) */
        uint32_t ia = 255 - a;
        uint32_t src_rb = (r & 255) | ((b & 255) << 16);
        uint32_t dst_rb = dst & 0x00FF00FFu;
        uint32_t res_rb = src_rb * a + dst_rb * ia + 0x00010001u;
        res_rb = (res_rb + ((res_rb >> 8) & 0x00FF00FFu)) >> 8;
        res_rb &= 0x00FF00FFu;

        uint32_t dg = (dst >> 8) & 255;
        dg = DIV255((g & 255) * a + dg * ia);
        return res_rb | (dg << 8) | 0xFF000000u;
    }
}

/*
 * Draw texture `t` with an affine map from texel space to framebuffer pixels:
 *   screen.x = a*u + c*v + tx ,  screen.y = b*u + d*v + ty
 * u in [0,w) left->right, v in [0,h) top->bottom (PNG row order). Framebuffer y grows
 * downward. Nearest sampling at pixel centres. tint = RGBA multiplier (R low byte).
 * mode 0 = alpha blend, 1 = additive.
 */
static void do_draw(int t, float a, float b, float c, float d, float tx, float ty,
                    uint32_t tint, int mode)
{
    if (t < 0 || t >= g_ntex) return;
    const Tex *T = &g_tex[t];
    float det = a * d - b * c;
    if (fabsf(det) < 1e-9f) return;
    float ia = d / det, ib = -b / det, ic = -c / det, id = a / det;

    /* screen-space bounds of the transformed quad */
    float xs[4] = { tx, a * T->w + tx, c * T->h + tx, a * T->w + c * T->h + tx };
    float ys[4] = { ty, b * T->w + ty, d * T->h + ty, b * T->w + d * T->h + ty };
    float minx = xs[0], maxx = xs[0], miny = ys[0], maxy = ys[0];
    for (int i = 1; i < 4; i++) {
        if (xs[i] < minx) minx = xs[i];
        if (xs[i] > maxx) maxx = xs[i];
        if (ys[i] < miny) miny = ys[i];
        if (ys[i] > maxy) maxy = ys[i];
    }
    int x0 = (int)floorf(minx), x1 = (int)ceilf(maxx), y0 = (int)floorf(miny), y1 = (int)ceilf(maxy);
    if (x0 < 0) x0 = 0;
    if (y0 < 0) y0 = 0;
    if (x1 > g_w) x1 = g_w;
    if (y1 > g_h) y1 = g_h;
    if (x0 >= x1 || y0 >= y1) return;

    uint32_t tr = tint & 255, tg = (tint >> 8) & 255, tb = (tint >> 16) & 255, ta = tint >> 24;
    int aa = b == 0 && c == 0;
    if (aa && !aa_columns(T, ia, ic, tx, &x0, &x1)) return;
    g_stats_sprites++;
    for (int y = y0; y < y1; y++) {
        float sy = y + 0.5f - ty;
        uint32_t *row = g_fb + (size_t)y * g_w;
        if (aa) {
            float v = ib * (x0 + 0.5f - tx) + id * sy;
            if (v < 0 || (int)v >= T->h) continue;
            size_t base = (size_t)(int)v * T->w;
            for (int x = x0; x < x1; x++) {
                int iu = g_iucol[x];
                if (iu < 0) continue;
                uint32_t p = TEXEL(T, base + iu);
                uint32_t pa = DIV255((p >> 24) * ta);
                if (!pa) continue;
                uint32_t pr = DIV255((p & 255) * tr), pg = DIV255(((p >> 8) & 255) * tg), pb = DIV255(((p >> 16) & 255) * tb);
                row[x] = blend(row[x], pr, pg, pb, pa, mode);
                g_stats_pixels++;
            }
            continue;
        }
        for (int x = x0; x < x1; x++) {
            float sx = x + 0.5f - tx;
            float u = ia * sx + ic * sy, v = ib * sx + id * sy;
            if (u < 0 || v < 0) continue;
            int iu = (int)u, iv = (int)v;
            if (iu >= T->w || iv >= T->h) continue;
            uint32_t p = TEXEL(T, (size_t)iv * T->w + iu);
            uint32_t pa = DIV255((p >> 24) * ta);
            if (!pa) continue;
            uint32_t pr = DIV255((p & 255) * tr), pg = DIV255(((p >> 8) & 255) * tg), pb = DIV255(((p >> 16) & 255) * tb);
            row[x] = blend(row[x], pr, pg, pb, pa, mode);
            g_stats_pixels++;
        }
    }
}

/*
 * Lit sprite draw: Kingdom's PowerSprite2 evaluated once per sprite (see docs/DECISIONS.md D16).
 *   rgb   = texel (or recoloured texel) * tint.rgb
 *   out   = rgb * mul + add          (mul/add precomputed: ambient, fake lights, sun, overlay)
 *   alpha = recolor ? tint.a : texel.a * tint.a ; texels below the shader's alpha test are skipped.
 * Recolour (flags & 1): texel alpha encodes the key: >= 224 keep, >= 192 -> red * A, >= 128 -> red * B,
 * below 128 discarded. mul is 8.8 fixed point per channel (may exceed 1.0), add is 0..255.
 * flags & 2: additive blend.
 */
static void do_draw_lit(int t, float a, float b, float c, float d, float tx, float ty,
                        uint32_t tint, const int *mul12, const int *add3,
                        uint32_t recA, uint32_t recB, int flags)
{
    if (t < 0 || t >= g_ntex) return;
    const Tex *T = &g_tex[t];
    float det = a * d - b * c;
    if (fabsf(det) < 1e-9f) return;
    float ia = d / det, ib = -b / det, ic = -c / det, id = a / det;
    float xs[4] = { tx, a * T->w + tx, c * T->h + tx, a * T->w + c * T->h + tx };
    float ys[4] = { ty, b * T->w + ty, d * T->h + ty, b * T->w + d * T->h + ty };
    float minx = xs[0], maxx = xs[0], miny = ys[0], maxy = ys[0];
    for (int i = 1; i < 4; i++) {
        if (xs[i] < minx) minx = xs[i];
        if (xs[i] > maxx) maxx = xs[i];
        if (ys[i] < miny) miny = ys[i];
        if (ys[i] > maxy) maxy = ys[i];
    }
    int x0 = (int)floorf(minx), x1 = (int)ceilf(maxx), y0 = (int)floorf(miny), y1 = (int)ceilf(maxy);
    if (x0 < 0) x0 = 0;
    if (y0 < 0) y0 = 0;
    if (x1 > g_w) x1 = g_w;
    if (y1 > g_h) y1 = g_h;
    if (x0 >= x1 || y0 >= y1) return;

    int tr = tint & 255, tg = (tint >> 8) & 255, tb = (tint >> 16) & 255, ta = tint >> 24;
    /* mul12: 8.8 multipliers at the texture corners TL, TR, BL, BR (3 channels each), interpolated
       bilinearly in texel space like Gouraud-shaded vertex lighting; the tint is folded in. */
    int cm[12];
    for (int i = 0; i < 4; i++) { cm[i * 3] = tr * mul12[i * 3] / 255; cm[i * 3 + 1] = tg * mul12[i * 3 + 1] / 255; cm[i * 3 + 2] = tb * mul12[i * 3 + 2] / 255; }
    int flat = 1;
    for (int i = 3; i < 12; i++) if (cm[i] != cm[i % 3]) { flat = 0; break; }
    float invw = T->w > 0 ? 1.0f / T->w : 0, invh = T->h > 0 ? 1.0f / T->h : 0;
    int mr = cm[0], mg = cm[1], mb = cm[2];
    int ar = add3[0], ag = add3[1], ab = add3[2];
    int Ar = recA & 255, Ag = (recA >> 8) & 255, Ab = (recA >> 16) & 255;
    int Br = recB & 255, Bg = (recB >> 8) & 255, Bb = (recB >> 16) & 255;
    int recolor = flags & 1, mode = (flags & 2) ? 1 : 0;
    int aa = b == 0 && c == 0;
    if (aa && !aa_columns(T, ia, ic, tx, &x0, &x1)) return;
    g_stats_sprites++;
    for (int y = y0; y < y1; y++) {
        float sy = y + 0.5f - ty;
        uint32_t *row = g_fb + (size_t)y * g_w;
        float vrow = aa ? ib * (x0 + 0.5f - tx) + id * sy : 0;
        if (aa && (vrow < 0 || (int)vrow >= T->h)) continue;
        int iv = (int)vrow;
        size_t row_base = aa ? (size_t)iv * T->w : 0;

        int cur_mr = 0, step_mr = 0, cur_mg = 0, step_mg = 0, cur_mb = 0, step_mb = 0;
        if (aa && !flat) {
            float fv = vrow * invh, w0 = 1.0f - fv, w1 = fv;
            float lr0 = cm[0] * w0 + cm[6] * w1, lr1 = cm[3] * w0 + cm[9] * w1;
            float lg0 = cm[1] * w0 + cm[7] * w1, lg1 = cm[4] * w0 + cm[10] * w1;
            float lb0 = cm[2] * w0 + cm[8] * w1, lb1 = cm[5] * w0 + cm[11] * w1;
            float d_u = ia * invw;
            float u0 = g_ucol[x0] * invw;
            cur_mr = (int)((lr0 + (lr1 - lr0) * u0) * 65536.0f); step_mr = (int)(((lr1 - lr0) * d_u) * 65536.0f);
            cur_mg = (int)((lg0 + (lg1 - lg0) * u0) * 65536.0f); step_mg = (int)(((lg1 - lg0) * d_u) * 65536.0f);
            cur_mb = (int)((lb0 + (lb1 - lb0) * u0) * 65536.0f); step_mb = (int)(((lb1 - lb0) * d_u) * 65536.0f);
        }

        for (int x = x0; x < x1; x++) {
            float u, v; int iu;
            if (aa) {
                iu = g_iucol[x];
                if (iu < 0) {
                    if (!flat) { cur_mr += step_mr; cur_mg += step_mg; cur_mb += step_mb; }
                    continue;
                }
            } else {
                float sx = x + 0.5f - tx;
                u = ia * sx + ic * sy; v = ib * sx + id * sy;
                if (u < 0 || v < 0) continue;
                iu = (int)u; iv = (int)v;
                if (iu >= T->w || iv >= T->h) continue;
                row_base = (size_t)iv * T->w;
            }
            uint32_t p = TEXEL(T, row_base + iu);
            int pa = p >> 24, pr = p & 255, pg = (p >> 8) & 255, pb = (p >> 16) & 255;
            int oa;
            if (recolor) {
                if (pa < 128) {
                    if (aa && !flat) { cur_mr += step_mr; cur_mg += step_mg; cur_mb += step_mb; }
                    continue;
                }
                if (pa < 224) {
                    if (pa >= 192) { pg = DIV255(pr * Ag); pb = DIV255(pr * Ab); pr = DIV255(pr * Ar); }
                    else           { pg = DIV255(pr * Bg); pb = DIV255(pr * Bb); pr = DIV255(pr * Br); }
                }
                oa = ta;
            } else {
                oa = DIV255(pa * ta);
                if (oa < 3) {
                    if (aa && !flat) { cur_mr += step_mr; cur_mg += step_mg; cur_mb += step_mb; }
                    continue;           /* shader: texkill(a - 0.01) */
                }
            }
            if (!oa) {
                if (aa && !flat) { cur_mr += step_mr; cur_mg += step_mg; cur_mb += step_mb; }
                continue;
            }
            if (!flat) {
                if (aa) {
                    mr = cur_mr >> 16; cur_mr += step_mr;
                    mg = cur_mg >> 16; cur_mg += step_mg;
                    mb = cur_mb >> 16; cur_mb += step_mb;
                } else {
                    float fu = u * invw, fv = v * invh;
                    float w00 = (1 - fu) * (1 - fv), w10 = fu * (1 - fv), w01 = (1 - fu) * fv, w11 = fu * fv;
                    mr = (int)(cm[0] * w00 + cm[3] * w10 + cm[6] * w01 + cm[9] * w11);
                    mg = (int)(cm[1] * w00 + cm[4] * w10 + cm[7] * w01 + cm[10] * w11);
                    mb = (int)(cm[2] * w00 + cm[5] * w10 + cm[8] * w01 + cm[11] * w11);
                }
            }
            if (mr < 0) mr = 0;
            if (mg < 0) mg = 0;
            if (mb < 0) mb = 0;
            int r = ((pr * mr) >> 8) + ar, g = ((pg * mg) >> 8) + ag, bl = ((pb * mb) >> 8) + ab;
            if (r < 0) r = 0; else if (r > 255) r = 255;
            if (g < 0) g = 0; else if (g > 255) g = 255;
            if (bl < 0) bl = 0; else if (bl > 255) bl = 255;
            row[x] = blend(row[x], (uint32_t)r, (uint32_t)g, (uint32_t)bl, (uint32_t)oa, mode);
            g_stats_pixels++;
        }
    }
}

/*
 * UI quad: parallelogram with screen corners P0, P1 = P0 + e1, P3 = P0 + e2 and matching texel
 * coordinates (PNG space). t < 0 draws solid colour. flags & 1: alpha-only texture (fonts: rgb from
 * colour, alpha from texel). flags & 2: additive. Clip rectangle in framebuffer pixels.
 */
static void do_draw_quad(int t, float x0, float y0, float x1, float y1, float x3, float y3,
                         float u0, float v0, float u1, float v1, float u3, float v3,
                         uint32_t rgba, int flags, int cx0, int cy0, int cx1, int cy1)
{
    const Tex *T = (t >= 0 && t < g_ntex) ? &g_tex[t] : NULL;
    float ex1 = x1 - x0, ey1 = y1 - y0, ex2 = x3 - x0, ey2 = y3 - y0;
    float det = ex1 * ey2 - ex2 * ey1;
    if (fabsf(det) < 1e-6f) return;
    float i11 = ey2 / det, i12 = -ex2 / det, i21 = -ey1 / det, i22 = ex1 / det;
    float xs[4] = { x0, x1, x3, x1 + ex2 }, ys[4] = { y0, y1, y3, y1 + ey2 };
    float minx = xs[0], maxx = xs[0], miny = ys[0], maxy = ys[0];
    for (int i = 1; i < 4; i++) {
        if (xs[i] < minx) minx = xs[i];
        if (xs[i] > maxx) maxx = xs[i];
        if (ys[i] < miny) miny = ys[i];
        if (ys[i] > maxy) maxy = ys[i];
    }
    int bx0 = (int)floorf(minx), bx1 = (int)ceilf(maxx), by0 = (int)floorf(miny), by1 = (int)ceilf(maxy);
    if (cx0 < 0) cx0 = 0;
    if (cy0 < 0) cy0 = 0;
    if (cx1 > g_w) cx1 = g_w;
    if (cy1 > g_h) cy1 = g_h;
    if (bx0 < cx0) bx0 = cx0;
    if (by0 < cy0) by0 = cy0;
    if (bx1 > cx1) bx1 = cx1;
    if (by1 > cy1) by1 = cy1;
    if (bx0 >= bx1 || by0 >= by1) return;
    uint32_t cr = rgba & 255, cg = (rgba >> 8) & 255, cb = (rgba >> 16) & 255, ca = rgba >> 24;
    if (!ca) return;
    float du1 = u1 - u0, dv1 = v1 - v0, du3 = u3 - u0, dv3 = v3 - v0;
    int mode = (flags & 2) ? 1 : 0;
    g_stats_sprites++;
    for (int y = by0; y < by1; y++) {
        float py = y + 0.5f - y0;
        uint32_t *row = g_fb + (size_t)y * g_w;
        for (int x = bx0; x < bx1; x++) {
            float px = x + 0.5f - x0;
            float a = i11 * px + i12 * py, b = i21 * px + i22 * py;
            if (a < 0 || b < 0 || a >= 1 || b >= 1) continue;
            uint32_t r = cr, g = cg, bl = cb, al = ca;
            if (T) {
                int iu = (int)(u0 + a * du1 + b * du3), iv = (int)(v0 + a * dv1 + b * dv3);
                if (iu < 0) iu = 0;
                if (iv < 0) iv = 0;
                if (iu >= T->w) iu = T->w - 1;
                if (iv >= T->h) iv = T->h - 1;
                uint32_t p = TEXEL(T, (size_t)iv * T->w + iu);
                al = (p >> 24) * ca / 255;
                if (!(flags & 1)) { r = (p & 255) * cr / 255; g = ((p >> 8) & 255) * cg / 255; bl = ((p >> 16) & 255) * cb / 255; }
            }
            if (!al) continue;
            row[x] = blend(row[x], r, g, bl, al, mode);
            g_stats_pixels++;
        }
    }
}

/*
 * Axis-aligned quad with per-corner colours (bilinear), in framebuffer pixels.
 * Corners: c00 = top-left, c10 = top-right, c01 = bottom-left, c11 = bottom-right.
 * Used for Kingdom's gradient meshes (sky, haze, fill). mode as in knl_draw.
 */
static void do_quad_gradient(float x0f, float y0f, float x1f, float y1f,
                             uint32_t c00, uint32_t c10, uint32_t c01, uint32_t c11, int mode)
{
    if (x1f < x0f) { float t = x0f; x0f = x1f; x1f = t; uint32_t s = c00; c00 = c10; c10 = s; s = c01; c01 = c11; c11 = s; }
    if (y1f < y0f) { float t = y0f; y0f = y1f; y1f = t; uint32_t s = c00; c00 = c01; c01 = s; s = c10; c10 = c11; c11 = s; }
    int x0 = (int)floorf(x0f + 0.5f), x1 = (int)floorf(x1f + 0.5f);
    int y0 = (int)floorf(y0f + 0.5f), y1 = (int)floorf(y1f + 0.5f);
    float W = x1f - x0f, H = y1f - y0f;
    if (W <= 0 || H <= 0) return;
    if (x0 < 0) x0 = 0;
    if (y0 < 0) y0 = 0;
    if (x1 > g_w) x1 = g_w;
    if (y1 > g_h) y1 = g_h;
    float C[4][4];
    uint32_t cs[4] = { c00, c10, c01, c11 };
    for (int i = 0; i < 4; i++)
        for (int k = 0; k < 4; k++) C[i][k] = (float)((cs[i] >> (8 * k)) & 255);
    /* the top/bottom edge colours depend on x only: one table per column instead of per pixel
       (same float expressions, same result). Opaque alpha-blended pixels are plain stores. */
    static float top[4096][4], bot[4096][4];
    for (int x = x0; x < x1; x++) {
        float fx = (x + 0.5f - x0f) / W;
        for (int k = 0; k < 4; k++) {
            top[x][k] = C[0][k] + (C[1][k] - C[0][k]) * fx;
            bot[x][k] = C[2][k] + (C[3][k] - C[2][k]) * fx;
        }
    }
    for (int y = y0; y < y1; y++) {
        float fy = (y + 0.5f - y0f) / H;
        uint32_t *row = g_fb + (size_t)y * g_w;
        for (int x = x0; x < x1; x++) {
            float v[4];
            for (int k = 0; k < 4; k++) v[k] = top[x][k] + (bot[x][k] - top[x][k]) * fy;
            uint32_t a = (uint32_t)v[3];
            if (a == 255 && mode == 0) row[x] = (uint32_t)v[0] | ((uint32_t)v[1] << 8) | ((uint32_t)v[2] << 16) | 0xFF000000u;
            else row[x] = blend(row[x], (uint32_t)v[0], (uint32_t)v[1], (uint32_t)v[2], a, mode);
        }
    }
}

/*
 * Water reflection (Kingdom's look): rows [y0, y1) receive the mirror image of the rows above y0,
 * vertically squashed by `scale`, rippled horizontally (amplitude grows with depth), then tinted
 * toward `tint` (RGBA, alpha = tint strength). Reads the framebuffer as drawn so far.
 */
static void do_reflect(int y0, int y1, float scale, uint32_t tint, float t, float amp)
{
    if (y0 < 1) y0 = 1;
    if (y1 > g_h) y1 = g_h;
    if (y0 >= y1 || scale <= 0.f) return;
    float tr = (float)(tint & 255), tg = (float)((tint >> 8) & 255), tb = (float)((tint >> 16) & 255), ta = (float)(tint >> 24) / 255.f;
    static uint32_t rowbuf[4096];
    for (int y = y0; y < y1; y++) {
        int d = y - y0;
        int sy = y0 - 1 - (int)(d / scale);
        if (sy < 0) sy = 0;
        float depth = (float)d / (float)(y1 - y0 + 1);
        int dx = (int)lroundf(sinf(d * 0.9f + t * 2.6f) * amp * (0.35f + depth));
        uint32_t *src = g_fb + (size_t)sy * g_w, *dst = g_fb + (size_t)y * g_w;
        memcpy(rowbuf, src, (size_t)g_w * 4);
        float k = ta * (0.55f + 0.45f * depth);
        for (int x = 0; x < g_w; x++) {
            int sx = x + dx; if (sx < 0) sx = 0; if (sx >= g_w) sx = g_w - 1;
            uint32_t p = rowbuf[sx];
            float r = (float)(p & 255), g = (float)((p >> 8) & 255), b = (float)((p >> 16) & 255);
            r = r + (tr - r) * k; g = g + (tg - g) * k; b = b + (tb - b) * k;
            dst[x] = (uint32_t)r | ((uint32_t)g << 8) | ((uint32_t)b << 16) | 0xFF000000u;
        }
    }
}


/* -------------------------------------------------------- deferred drawing
 * Miyoo (D22): the draw calls of a frame are recorded into a command list; knl_present hands the
 * list to a worker thread on the second core, which rasterises it and flips the framebuffer while
 * the game thread already runs the next frame's logic. Same functions, same order, same pixels.
 * Desktop/headless (and KNL_SYNC=1): calls run immediately. */
enum { C_CLEAR, C_DRAW, C_LIT, C_QUAD, C_GRAD, C_REFLECT };
typedef struct { int op, t; float f[12]; uint32_t u[4]; int i[19]; } Cmd;
typedef struct { Cmd *c; int n, cap; } CmdList;
static int g_async;
static CmdList g_rec;
static Cmd *push_cmd(int op)
{
    if (g_rec.n == g_rec.cap) { g_rec.cap = g_rec.cap ? g_rec.cap * 2 : 512; g_rec.c = (Cmd *)realloc(g_rec.c, sizeof(Cmd) * g_rec.cap); }
    Cmd *c = &g_rec.c[g_rec.n++]; c->op = op; return c;
}
static void run_list(CmdList *L)
{
    for (int k = 0; k < L->n; k++) {
        const Cmd *c = &L->c[k]; const float *f = c->f;
        switch (c->op) {
        case C_CLEAR: do_clear(c->u[0]); break;
        case C_DRAW: do_draw(c->t, f[0], f[1], f[2], f[3], f[4], f[5], c->u[0], c->i[0]); break;
        case C_LIT: do_draw_lit(c->t, f[0], f[1], f[2], f[3], f[4], f[5], c->u[0], c->i, c->i + 12, c->u[1], c->u[2], c->i[15]); break;
        case C_QUAD: do_draw_quad(c->t, f[0], f[1], f[2], f[3], f[4], f[5], f[6], f[7], f[8], f[9], f[10], f[11],
                                  c->u[0], c->i[0], c->i[1], c->i[2], c->i[3], c->i[4]); break;
        case C_GRAD: do_quad_gradient(f[0], f[1], f[2], f[3], c->u[0], c->u[1], c->u[2], c->u[3], c->i[0]); break;
        case C_REFLECT: do_reflect(c->i[0], c->i[1], f[0], c->u[0], f[1], f[2]); break;
        }
    }
    L->n = 0;
}

KNL_API void knl_clear(uint32_t rgba)
{
    if (!g_async) { do_clear(rgba); return; }
    push_cmd(C_CLEAR)->u[0] = rgba;
}
KNL_API void knl_draw(int t, float a, float b, float c, float d, float tx, float ty, uint32_t tint, int mode)
{
    if (!g_async) { do_draw(t, a, b, c, d, tx, ty, tint, mode); return; }
    Cmd *k = push_cmd(C_DRAW); k->t = t;
    k->f[0] = a; k->f[1] = b; k->f[2] = c; k->f[3] = d; k->f[4] = tx; k->f[5] = ty; k->u[0] = tint; k->i[0] = mode;
}
KNL_API void knl_draw_lit(int t, float a, float b, float c, float d, float tx, float ty,
                          uint32_t tint, const int *mul12, const int *add3,
                          uint32_t recA, uint32_t recB, int flags)
{
    if (!g_async) { do_draw_lit(t, a, b, c, d, tx, ty, tint, mul12, add3, recA, recB, flags); return; }
    Cmd *k = push_cmd(C_LIT); k->t = t;
    k->f[0] = a; k->f[1] = b; k->f[2] = c; k->f[3] = d; k->f[4] = tx; k->f[5] = ty;
    k->u[0] = tint; k->u[1] = recA; k->u[2] = recB;
    memcpy(k->i, mul12, 12 * sizeof(int)); memcpy(k->i + 12, add3, 3 * sizeof(int)); k->i[15] = flags;
}
KNL_API void knl_draw_quad(int t, float x0, float y0, float x1, float y1, float x3, float y3,
                           float u0, float v0, float u1, float v1, float u3, float v3,
                           uint32_t rgba, int flags, int cx0, int cy0, int cx1, int cy1)
{
    if (!g_async) { do_draw_quad(t, x0, y0, x1, y1, x3, y3, u0, v0, u1, v1, u3, v3, rgba, flags, cx0, cy0, cx1, cy1); return; }
    Cmd *k = push_cmd(C_QUAD); k->t = t;
    float f[12] = { x0, y0, x1, y1, x3, y3, u0, v0, u1, v1, u3, v3 }; memcpy(k->f, f, sizeof f);
    k->u[0] = rgba; k->i[0] = flags; k->i[1] = cx0; k->i[2] = cy0; k->i[3] = cx1; k->i[4] = cy1;
}
KNL_API void knl_quad_gradient(float x0f, float y0f, float x1f, float y1f,
                               uint32_t c00, uint32_t c10, uint32_t c01, uint32_t c11, int mode)
{
    if (!g_async) { do_quad_gradient(x0f, y0f, x1f, y1f, c00, c10, c01, c11, mode); return; }
    Cmd *k = push_cmd(C_GRAD);
    k->f[0] = x0f; k->f[1] = y0f; k->f[2] = x1f; k->f[3] = y1f;
    k->u[0] = c00; k->u[1] = c10; k->u[2] = c01; k->u[3] = c11; k->i[0] = mode;
}
KNL_API void knl_reflect(int y0, int y1, float scale, uint32_t tint, float t, float amp)
{
    if (!g_async) { do_reflect(y0, y1, scale, tint, t, amp); return; }
    Cmd *k = push_cmd(C_REFLECT);
    k->i[0] = y0; k->i[1] = y1; k->f[0] = scale; k->u[0] = tint; k->f[1] = t; k->f[2] = amp;
}

#ifdef KNL_MIYOO
static pthread_t g_worker; static pthread_mutex_t g_wm = PTHREAD_MUTEX_INITIALIZER;
static pthread_cond_t g_wc = PTHREAD_COND_INITIALIZER;
static CmdList g_run;
static int g_busy, g_stop;
static double g_raster_ms;      /* worker time of the last frame (rasterise + flip) */
static void fb_flip(void);
static void *worker_main(void *ud)
{
    (void)ud;
    pthread_mutex_lock(&g_wm);
    for (;;) {
        while (!g_busy && !g_stop) pthread_cond_wait(&g_wc, &g_wm);
        if (g_stop) break;
        pthread_mutex_unlock(&g_wm);
        struct timespec a, b; clock_gettime(CLOCK_MONOTONIC, &a);
        run_list(&g_run);
        fb_flip();
        clock_gettime(CLOCK_MONOTONIC, &b);
        pthread_mutex_lock(&g_wm);
        g_raster_ms = (b.tv_sec - a.tv_sec) * 1e3 + (b.tv_nsec - a.tv_nsec) / 1e6;
        g_busy = 0;
        pthread_cond_broadcast(&g_wc);
    }
    pthread_mutex_unlock(&g_wm);
    return NULL;
}
/* blocks until the worker has finished the frame it was given */
static void wait_idle(void)
{
    if (!g_async) return;
    pthread_mutex_lock(&g_wm);
    while (g_busy) pthread_cond_wait(&g_wc, &g_wm);
    pthread_mutex_unlock(&g_wm);
}
static void kick_worker(void)
{
    wait_idle();
    CmdList t = g_run; g_run = g_rec; g_rec = t;
    pthread_mutex_lock(&g_wm); g_busy = 1; pthread_cond_signal(&g_wc); pthread_mutex_unlock(&g_wm);
}
static void start_worker(void)
{
    const char *e = getenv("KNL_SYNC");
    if (e && *e == '1') return;
    if (pthread_create(&g_worker, NULL, worker_main, NULL) == 0) g_async = 1;
}
static void stop_worker(void)
{
    if (!g_async) return;
    wait_idle();
    pthread_mutex_lock(&g_wm); g_stop = 1; pthread_cond_broadcast(&g_wc); pthread_mutex_unlock(&g_wm);
    pthread_join(g_worker, NULL);
    g_async = 0; g_stop = 0;
}
KNL_API float knl_raster_ms(void) { return (float)g_raster_ms; }
#else
static void wait_idle(void) { }
KNL_API float knl_raster_ms(void) { return 0; }
#endif

/* runs everything recorded so far, so the framebuffer is complete (screenshots) */
static void flush_draws(void) { wait_idle(); run_list(&g_rec); }

/* returns 0 when the fast integer paths agree with plain arithmetic */
KNL_API int knl_selftest(void)
{
    for (uint32_t x = 0; x <= 65534; x++) if (DIV255(x) != x / 255) return 1;
    return 0;
}

KNL_API void knl_stats(int *sprites, int *pixels)
{
    if (sprites) *sprites = g_stats_sprites;
    if (pixels) *pixels = g_stats_pixels;
}

/* --------------------------------------------------------------- output */
KNL_API int knl_save_png(const char *path, int scale)
{
    flush_draws();
    if (scale < 1) scale = 1;
    int W = g_w * scale, H = g_h * scale;
    uint32_t *out = (uint32_t *)malloc((size_t)W * H * 4);
    if (!out) return -1;
    for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
            out[(size_t)y * W + x] = g_fb[(size_t)(y / scale) * g_w + x / scale] | 0xFF000000u;
    int ok = stbi_write_png(path, W, H, 4, out, W * 4);
    free(out);
    return ok ? 0 : -1;
}

KNL_API uint32_t knl_buttons(void);

#ifdef KNL_MIYOO
static void fb_flip(void)
{
    /* back page when the fb is double height, then pan to it; RGBA -> BGRA, 2x, rotated 180 */
    int pages = g_vinfo.yres_virtual >= 2 * g_vinfo.yres ? 2 : 1;
    int page = pages == 2 ? g_page ^ 1 : 0;
    uint32_t *dst = g_fbmem + (size_t)page * g_vinfo.yres * g_vinfo.xres;
    for (int y = 0; y < g_h; y++) {
        const uint32_t *src = g_fb + (size_t)y * g_w;
        uint32_t *r0 = dst + (size_t)(g_vinfo.yres - 1 - 2 * y) * g_vinfo.xres;
        uint32_t *r1 = r0 - g_vinfo.xres;
        uint64_t *dst0 = (uint64_t *)r0;
        uint64_t *dst1 = (uint64_t *)r1;
        for (int x = 0; x < g_w; x++) {
            uint32_t c = src[g_w - 1 - x];
            c = 0xFF000000u | (c & 0x0000FF00u) | ((c & 0xFFu) << 16) | ((c >> 16) & 0xFFu);
            uint64_t pair = (uint64_t)c | ((uint64_t)c << 32);
            dst0[x] = pair;
            dst1[x] = pair;
        }
    }
    if (pages == 2) {
        g_vinfo.yoffset = page * g_vinfo.yres;
        ioctl(g_fbfd, FBIOPAN_DISPLAY, &g_vinfo);
        g_page = page;
    }
}
#endif

/* returns 0 when the user asked to quit, 1 otherwise. Headless: always 1. */
KNL_API int knl_present(void)
{
#ifdef KNL_SDL
    if (!g_win) return 1;
    SDL_Event e;
    while (SDL_PollEvent(&e)) {
        if (e.type == SDL_QUIT) return 0;
        if (e.type == SDL_KEYDOWN && e.key.keysym.sym == SDLK_F10) return 0;   /* F10 quits */
    }
    SDL_UpdateTexture(g_sdltex, NULL, g_fb, g_w * 4);
    SDL_RenderClear(g_ren);
    SDL_RenderCopy(g_ren, g_sdltex, NULL, NULL);
    SDL_RenderPresent(g_ren);
#elif defined(KNL_MIYOO)
    if (!g_fbmem) return 1;
    if (g_async) kick_worker(); else fb_flip();
    /* Menu + Start quits (the game has no quit-to-OS of its own on this platform) */
    if ((knl_buttons() & 0x1100u) == 0x1100u) return 0;
#endif
    return 1;
}

/* Button bits (shared with KNL.Buttons): 0 left, 1 right, 2 up, 3 down, 4 A, 5 B, 6 X, 7 Y,
 * 8 Start, 9 Select, 10 L, 11 R. Desktop keyboard layout follows the game's own Rewired
 * keyboard map: arrows/WASD move, S/Down = pay, Shift = gallop, Enter/Space = submit,
 * Esc = start/menu, C = cancel. The handheld build maps its physical buttons to the same bits. */
KNL_API uint32_t knl_buttons(void)
{
    uint32_t m = 0;
#ifdef KNL_SDL
    if (!g_win) return 0;
    const Uint8 *k = SDL_GetKeyboardState(NULL);
    if (k[SDL_SCANCODE_LEFT] || k[SDL_SCANCODE_A]) m |= 1;
    if (k[SDL_SCANCODE_RIGHT] || k[SDL_SCANCODE_D]) m |= 2;
    if (k[SDL_SCANCODE_UP] || k[SDL_SCANCODE_W]) m |= 4;
    if (k[SDL_SCANCODE_DOWN] || k[SDL_SCANCODE_S]) m |= 8;
    if (k[SDL_SCANCODE_SPACE] || k[SDL_SCANCODE_RETURN] || k[SDL_SCANCODE_Z]) m |= 16;
    if (k[SDL_SCANCODE_LSHIFT] || k[SDL_SCANCODE_RSHIFT] || k[SDL_SCANCODE_C]) m |= 32;
    if (k[SDL_SCANCODE_X]) m |= 64;
    if (k[SDL_SCANCODE_ESCAPE]) m |= 256;
    if (k[SDL_SCANCODE_TAB]) m |= 512;
#elif defined(KNL_MIYOO)
    /* Miyoo Mini gpio-keys (KEY= bitmap of /proc/bus/input/devices confirms this set) */
    struct input_event ev;
    while (g_evfd >= 0 && read(g_evfd, &ev, sizeof ev) == sizeof ev) {
        if (ev.type != EV_KEY) continue;
        uint32_t b = 0;
        switch (ev.code) {
        case KEY_LEFT: b = 1; break;          case KEY_RIGHT: b = 2; break;
        case KEY_UP: b = 4; break;            case KEY_DOWN: b = 8; break;
        case KEY_SPACE: b = 16; break;        /* A */
        case KEY_LEFTCTRL: b = 32; break;     /* B */
        case KEY_LEFTSHIFT: b = 64; break;    /* X */
        case KEY_LEFTALT: b = 128; break;     /* Y */
        case KEY_ENTER: b = 256; break;       /* Start */
        case KEY_RIGHTCTRL: b = 512; break;   /* Select */
        case KEY_E: b = 1024; break;          /* L1 */
        case KEY_T: b = 2048; break;          /* R1 */
        case KEY_ESC: b = 4096; break;        /* Menu */
        }
        if (ev.value) g_keys |= b; else g_keys &= ~b;   /* value 2 = autorepeat, stays held */
    }
    m = g_keys;
#endif
    return m;
}

KNL_API uint32_t knl_ticks_ms(void)
{
#ifdef KNL_SDL
    if (g_win) return SDL_GetTicks();
#elif defined(KNL_MIYOO)
    if (g_fbmem) { struct timespec t; clock_gettime(CLOCK_MONOTONIC, &t); return (uint32_t)(t.tv_sec * 1000u + t.tv_nsec / 1000000); }
#endif
    return 0;
}
