#include "../engine/native/knlcore.c"
#include "render_reference.h"
#include <assert.h>
static uint32_t rng=87981;
static uint32_t next(void){rng^=rng<<13;rng^=rng>>17;rng^=rng<<5;return rng;}
static float val(float lo,float hi){return lo+(next()%10001)*(hi-lo)/10000;}
static uint32_t expected[64*48];
static void save(void){memcpy(expected,g_fb,sizeof(expected));}
static void same(const char *name,int i){if(memcmp(expected,g_fb,sizeof(expected))){fprintf(stderr,"FAIL %s case %d\n",name,i);exit(1);}}
int main(void){
 assert(knl_init(64,48,1,0)==0);assert(knl_selftest()==0);
 uint32_t palette[256];uint8_t alpha[]={0,1,2,127,128,191,192,223,224,254,255};
 for(int i=0;i<256;i++)palette[i]=(next()&0xffffffu)|((uint32_t)alpha[i%11]<<24);
 int indexed=add_tex(23,19,NULL);g_tex[indexed].pal=malloc(1024);memcpy(g_tex[indexed].pal,palette,1024);g_tex[indexed].idx=malloc(23*19);
 uint32_t px[23*19];for(int i=0;i<23*19;i++){int k=next()%256;g_tex[indexed].idx[i]=k;px[i]=palette[k];}
 int rgba=knl_tex_create(23,19,(uint8_t*)px);
 for(int i=0;i<600;i++){
  float a=val(-3,3),d=val(-3,3),b=i%3==0?val(-1,1):0,c=i%3==0?val(-1,1):0,tx=val(-40,80),ty=val(-30,60);uint32_t tint=next(),bg=next()|0xff000000;int flags=next()%4;
  int mul[12],add[3];for(int k=0;k<3;k++){mul[k]=(int)(next()%800)-50;add[k]=(int)(next()%150)-50;}
  for(int k=3;k<12;k++)mul[k]=i%2?mul[k%3]:(int)(next()%800)-50;
  uint32_t ra=next(),rb=next();
  do_clear(bg);do_draw_lit(rgba,a,b,c,d,tx,ty,tint,mul,add,ra,rb,flags);save();
  do_clear(bg);do_draw_lit(indexed,a,b,c,d,tx,ty,tint,mul,add,ra,rb,flags);same("lit indexed/RGBA",i);
  do_clear(bg);do_draw_lit(indexed,a,b,c,d,tx,ty,tint,mul,add,ra,rb,flags);same("cached lit palette",i);
  do_clear(bg);do_draw(rgba,a,b,c,d,tx,ty,tint,flags&1);save();
  do_clear(bg);do_draw(indexed,a,b,c,d,tx,ty,tint,flags&1);same("tint indexed/RGBA",i);
 }
 for(int i=0;i<600;i++){
  float x=val(-20,60),y=val(-20,40),x1=x+val(-50,70),y1=i%2?y:y+val(-10,10),x3=i%2?x:x+val(-10,10),y3=y+val(-30,70);
  float u=val(-5,25),v=val(-5,25),u1=u+val(-30,30),v3=v+val(-30,30);uint32_t col=next(),bg=next()|0xff000000;int flags=next()%4,t=i%3==0?-1:indexed;
  do_clear(bg);reference_do_draw_quad(t,x,y,x1,y1,x3,y3,u,v,u1,v,u,v3,col,flags,2,3,60,45);save();
  do_clear(bg);do_draw_quad(t,x,y,x1,y1,x3,y3,u,v,u1,v,u,v3,col,flags,2,3,60,45);same("UI reference",i);
  uint32_t c00=next(),c01=next(),c10=i%2?c00:next(),c11=i%2?c01:next();
  do_clear(bg);reference_do_quad_gradient(x,y,x1,y3,c00,c10,c01,c11,flags&1);save();
  do_clear(bg);do_quad_gradient(x,y,x1,y3,c00,c10,c01,c11,flags&1);same("gradient reference",i);
 }
 knl_shutdown();puts("Renderer regressions passed: 3000 pixel comparisons");return 0;
}
