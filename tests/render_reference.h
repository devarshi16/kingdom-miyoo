/* Baseline scalar UI/gradient paths, retained for exact output regression checks. */
static void reference_do_draw_quad(int t, float x0, float y0, float x1, float y1, float x3, float y3,
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

static void reference_do_quad_gradient(float x0f, float y0f, float x1f, float y1f,
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
