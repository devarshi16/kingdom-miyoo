/* Local renderer benchmark. Captures contain owned game pixels: never distribute them.
 * gcc -O3 -std=gnu99 tools/render_replay.c -o /tmp/knl-replay -lm
 * /tmp/knl-replay frame.capture [iterations] [output.rgba]
 * Set KNL_RENDER_SOURCE at compile time to compare an older knlcore.c.
 */
#define _POSIX_C_SOURCE 200809L
#include <time.h>
#ifndef KNL_RENDER_SOURCE
#define KNL_RENDER_SOURCE "../engine/native/knlcore.c"
#endif
#include KNL_RENDER_SOURCE
static double ms(void) { struct timespec t; clock_gettime(CLOCK_MONOTONIC, &t); return t.tv_sec * 1000.0 + t.tv_nsec / 1e6; }
#define READ(p,s,n) do { if (fread((p),(s),(n),f)!=(size_t)(n)) { fprintf(stderr,"Truncated capture\n"); return 2; } } while(0)
int main(int argc, char **argv)
{
    if (argc < 2) return 2;
    FILE *f = fopen(argv[1], "rb"); if (!f) return 2;
    int h[6]; READ(h, sizeof(h), 1);
    if (h[0] != 0x314c4e4b || h[1] < 1 || h[1] > 4096 || h[2] < 1 || h[2] > 4096 || h[3] < 0 || h[3] > 10000 || h[4] < 0 || h[4] > 10000 || h[5] != sizeof(Cmd)) return 2;
    if (knl_init(h[1], h[2], 1, 0)) return 2;
    for (int i=0; i<h[3]; i++) {
        int th[3]; READ(th,sizeof(th),1);
        if(th[0]<1||th[1]<1||th[0]>4096||th[1]>4096) return 2;
        int id=add_tex(th[0], th[1], NULL); Tex *t=&g_tex[id];size_t n=(size_t)t->w*t->h;
        if(th[2]) { t->pal=malloc(1024);t->idx=malloc(n);if(!t->pal||!t->idx)return 2;READ(t->pal,4,256);READ(t->idx,1,n); }
        else { t->px=malloc(n*4);if(!t->px)return 2;READ(t->px,4,n); }
    }
    CmdList list={0};list.c=malloc((size_t)h[4]*sizeof(Cmd));if(!list.c)return 2;READ(list.c,sizeof(Cmd),h[4]);fclose(f);
    for(int i=0;i<h[4];i++) if(list.c[i].op<0||list.c[i].op>C_REFLECT)return 2;
    int loops=argc>2?atoi(argv[2]):100;if(loops<1||loops>10000)return 2;
    double start=ms();
    for(int i=0;i<loops;i++){list.n=h[4];run_list(&list);}
    printf("replay: %d commands, %d frames, %.3f ms/frame\n",h[4],loops,(ms()-start)/loops);
    uint64_t hash=14695981039346656037ull;
    for(size_t i=0;i<(size_t)g_w*g_h*4;i++)hash=(hash^((uint8_t*)g_fb)[i])*1099511628211ull;
    printf("pixels: %016llx\n",(unsigned long long)hash);
    if(argc>3){f=fopen(argv[3],"wb");if(!f)return 2;fwrite(g_fb,4,(size_t)g_w*g_h,f);fclose(f);}
    free(list.c);knl_shutdown();return 0;
}
