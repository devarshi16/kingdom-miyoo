/* Exact 2x, 180-degree RGBA conversion for the Miyoo's mapped display page.
   Build one cached row before copying: interleaved stores to two uncached
   display rows prevent the memory controller from combining writes well. */
static void knl_copy_display(uint8_t *dst, int stride, const uint32_t *src,
                             int w, int h, int red, int green, int blue,
                             int alpha_length, int alpha_offset)
{
    uint64_t row[w];
    uint32_t alpha = alpha_length == 8 ? 255u << alpha_offset : 0u;
    for (int y = 0; y < h; y++) {
        const uint32_t *in = src + (size_t)y * w;
        for (int x = 0; x < w; x++) {
            uint32_t c = in[w - 1 - x];
            c = ((c & 255u) << red) | (((c >> 8) & 255u) << green) |
                (((c >> 16) & 255u) << blue) | alpha;
            row[x] = (uint64_t)c | ((uint64_t)c << 32);
        }
        uint8_t *out = dst + (size_t)(h * 2 - 1 - 2 * y) * stride;
        memcpy(out, row, (size_t)w * 8);
        memcpy(out - stride, row, (size_t)w * 8);
    }
}
