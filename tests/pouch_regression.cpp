#include <cassert>
#include <Box2D/Box2D.h>
#include <cstdio>
extern "C" {
void *knl_pouch_create(float,float);void knl_pouch_destroy(void*);void *knl_pouch_filter(void*);void knl_pouch_filter_destroy(void*);
void *knl_pouch_body(void*,int,float,float,float,float,float,float);
int knl_pouch_polygon(void*,const float*,int,int,int,float,float);void knl_pouch_mass(void*,float);
void knl_pouch_layer(void*,void*,int,int,unsigned);void knl_pouch_step(void*,float,float,float);void knl_pouch_get(void*,float*);
int knl_pouch_contacts(void*,int*,float*,int);
void knl_pouch_set(void*,int,int,float,float,float,float,float,float);void knl_pouch_remove(void*,void*,void*);
}
int main(){
 void *w=knl_pouch_create(0,-15),*f=knl_pouch_filter(w),*wall=knl_pouch_body(w,0,0,0,0,0,0,0);
 // A synthetic concave U, deliberately not a convex bowl/hull. No game data is embedded.
 float outline[]={-1,1,-.8f,1,-.8f,-.5f,.8f,-.5f,.8f,1,1,1,1,-.7f,-1,-.7f};
 assert(knl_pouch_polygon(wall,outline,8,1,0,.4f,0));knl_pouch_layer(f,wall,1,31,1u<<16);
 float sensor[]={-.5f,-.7f,.5f,-.7f,.5f,-.3f,-.5f,-.3f};assert(knl_pouch_polygon(wall,sensor,4,3,1,0,0));knl_pouch_layer(f,wall,3,31,1u<<16);
 float coin[]={-.1f,.04f,-.13f,0,-.1f,-.04f,.1f,-.04f,.13f,0,.1f,.04f};
 void *b=knl_pouch_body(w,1,0,.8f,.4f,.7f,.05f,.6f);assert(knl_pouch_polygon(b,coin,6,2,0,.2f,.1f));knl_pouch_mass(b,1);knl_pouch_layer(f,b,2,16,1u<<31);
 float state[6];for(int i=0;i<150;i++)knl_pouch_step(w,.02f,100,2*b2_pi);knl_pouch_get(b,state);
 assert(state[1]>-.5f&&state[1]<-.35f);assert(state[0]>-.7f&&state[0]<.7f);puts("PASS: thin polygon enters concave opening and rests on its floor");
 int ids[128];float points[256];int n=knl_pouch_contacts(w,ids,points,64);bool sensed=false;for(int i=0;i<n;i++)if((ids[i*2]==3&&ids[i*2+1]==2)||(ids[i*2]==2&&ids[i*2+1]==3))sensed=true;assert(sensed);puts("PASS: sensor reports polygon overlap without changing floor response");
 knl_pouch_layer(f,b,2,16,0);knl_pouch_set(b,1,0,0,.8f,0,0,0,0);for(int i=0;i<150;i++)knl_pouch_step(w,.02f,100,2*b2_pi);knl_pouch_get(b,state);assert(state[1]<-1);puts("PASS: full 32-bit collision masks control contacts");
 knl_pouch_set(b,0,0,0,.8f,0,0,0,0);for(int i=0;i<10;i++)knl_pouch_step(w,.02f,100,2*b2_pi);knl_pouch_get(b,state);assert(state[1]==.8f);puts("PASS: disabled/pool body stops simulation");
 knl_pouch_set(b,1,0,0,.8f,0,0,0,0);knl_pouch_mass(b,1);assert(((b2Body*)b)->GetMass()==1);puts("PASS: pooled reactivation restores configured mass");
 knl_pouch_set(b,1,0,0,10,0,0,0,1000);knl_pouch_step(w,.02f,100,2*b2_pi);knl_pouch_get(b,state);assert(state[2]<=.02f*2*b2_pi+1e-6f);assert(state[5]<=2*b2_pi+1e-5f);puts("PASS: configured angular speed limit applies during integration");
 knl_pouch_remove(w,f,b);knl_pouch_destroy(w);knl_pouch_filter_destroy(f);puts("Pouch regressions passed: 6");
}
