#include <stdint.h>
#include <stdlib.h>
#include <stdio.h>
#include <string.h>
#include "../engine/native/framebuffer_copy.h"
int main(void)
{
    uint32_t rng = 1234567;
    int cases = 0;
    for (int w = 1; w <= 320; w += 11) for (int h = 1; h <= 240; h += 31)
    for (int layout = 0; layout < 6; layout++) {
        static const int shifts[6][3] = {{0,8,16},{16,8,0},{8,0,16},{8,16,0},{0,16,8},{16,0,8}};
        int stride = w * 8 + 16;
        size_t bytes = (size_t)stride * h * 2;
        uint32_t *src = malloc((size_t)w * h * 4);
        uint8_t *got = malloc(bytes), *ref = malloc(bytes);
        memset(got, 0x5a, bytes); memset(ref, 0x5a, bytes);
        for (int i = 0; i < w * h; i++) { rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5; src[i] = rng; }
        int r = shifts[layout][0], g = shifts[layout][1], b = shifts[layout][2];
        for (int alpha = 0; alpha <= 8; alpha += 8) {
            knl_copy_display(got, stride, src, w, h, r, g, b, alpha, 24);
            for (int y = 0; y < h * 2; y++) for (int x = 0; x < w * 2; x++) {
                uint32_t p = src[(h - 1 - y / 2) * w + (w - 1 - x / 2)];
                p = ((p & 255) << r) | (((p >> 8) & 255) << g) | (((p >> 16) & 255) << b) | (alpha ? 0xff000000u : 0);
                memcpy(ref + y * stride + x * 4, &p, 4);
            }
            if (memcmp(got, ref, bytes)) { fprintf(stderr, "display copy mismatch\n"); return 1; }
            cases++;
        }
        free(src); free(got); free(ref);
    }
    printf("Framebuffer regressions passed: %d layouts/dimensions/alpha comparisons\n", cases);
    return 0;
}
