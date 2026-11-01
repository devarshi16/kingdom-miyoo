/* Pouch physics bridge. Box2D 2.3.1 (vendored, motion-limit hook documented in README.knl.md) solves the original
 * serialized coin polygons and concave bag wall; no coin capacity or spawn rules live here.
 * This isolated world does not replace the rest of the port's physics engine. */
#include <Box2D/Box2D.h>
#include <stdint.h>
#include <vector>
#include <algorithm>
#include <cmath>
float32 knl_pouch_linear_limit=100.0f;
float32 knl_pouch_angular_limit=2*b2_pi;
#define API extern "C" __attribute__((visibility("default")))
API void *knl_pouch_create(float gx,float gy) { return new b2World(b2Vec2(gx,gy)); }
API void knl_pouch_destroy(void *w) { delete (b2World*)w; }
API void *knl_pouch_body(void *w,int dynamic,float x,float y,float angle,float drag,float adrag,float gravity)
{
    b2BodyDef d;d.type=dynamic?b2_dynamicBody:b2_staticBody;d.position.Set(x,y);d.angle=angle;
    d.linearDamping=drag;d.angularDamping=adrag;d.gravityScale=gravity;
    return ((b2World*)w)->CreateBody(&d);
}
static float cross(b2Vec2 a,b2Vec2 b,b2Vec2 c) { return b2Cross(b-a,c-a); }
static void fixture(b2Body *b,const b2Vec2 *v,int n,int id,int sensor,float friction,float bounce)
{
    b2PolygonShape shape;shape.Set(v,n);
    b2FixtureDef d;d.shape=&shape;d.density=1;d.friction=friction;d.restitution=bounce;
    // Unity has 32 layers, whereas Box2D's built-in filter has 16. A custom filter below uses all 32.
    d.userData=(void*)(intptr_t)id;d.isSensor=sensor!=0;
    b->CreateFixture(&d);
}
struct PouchFilter : b2ContactFilter {
    struct Layer { int id,layer;unsigned mask; };std::vector<Layer> layers;
    bool ShouldCollide(b2Fixture *a,b2Fixture *b) {
        int ai=(int)(intptr_t)a->GetUserData(),bi=(int)(intptr_t)b->GetUserData();
        const Layer *la=0,*lb=0;
        for(size_t i=0;i<layers.size();i++){if(layers[i].id==ai)la=&layers[i];if(layers[i].id==bi)lb=&layers[i];}
        return la&&lb&&(la->mask&(1u<<lb->layer))&&(lb->mask&(1u<<la->layer));
    }
};
API void *knl_pouch_filter(void *w) { PouchFilter *f=new PouchFilter;((b2World*)w)->SetContactFilter(f);return f; }
API void knl_pouch_filter_destroy(void *f) { delete (PouchFilter*)f; }
API void knl_pouch_layer(void *f,void *body,int id,int layer,unsigned mask)
{
    PouchFilter *filter=(PouchFilter*)f;
    for(size_t i=0;i<filter->layers.size();i++)if(filter->layers[i].id==id){
        if(filter->layers[i].layer==layer&&filter->layers[i].mask==mask)return;
        filter->layers[i].layer=layer;filter->layers[i].mask=mask;
        for(b2Fixture *fx=((b2Body*)body)->GetFixtureList();fx;fx=fx->GetNext())fx->Refilter();return;
    }
    PouchFilter::Layer l={id,layer,mask};filter->layers.push_back(l);
}
API int knl_pouch_polygon(void *body,const float *xy,int n,int id,int sensor,float friction,float bounce)
{
    if(n<3||n>1024)return 0;
    std::vector<b2Vec2> p;float area=0;
    for(int i=0;i<n;i++){p.push_back(b2Vec2(xy[i*2],xy[i*2+1]));area+=xy[i*2]*xy[((i+1)%n)*2+1]-xy[((i+1)%n)*2]*xy[i*2+1];}
    if(area<0)std::reverse(p.begin(),p.end());
    bool convex=n<=b2_maxPolygonVertices;
    for(int i=0;i<n&&convex;i++)if(cross(p[i],p[(i+1)%n],p[(i+2)%n])<-1e-7f)convex=false;
    if(convex){fixture((b2Body*)body,&p[0],n,id,sensor,friction,bounce);return 1;}
    // Ear clipping preserves the filled concave outline; taking its convex hull would close the bag.
    std::vector<b2Vec2> triangles;
    while(p.size()>3){
        bool found=false;
        for(size_t i=0;i<p.size();i++){
            size_t a=(i+p.size()-1)%p.size(),c=(i+1)%p.size();
            if(cross(p[a],p[i],p[c])<=1e-7f)continue;
            bool inside=false;
            for(size_t j=0;j<p.size();j++)if(j!=a&&j!=i&&j!=c&&cross(p[a],p[i],p[j])>=0&&cross(p[i],p[c],p[j])>=0&&cross(p[c],p[a],p[j])>=0){inside=true;break;}
            if(inside)continue;
            triangles.push_back(p[a]);triangles.push_back(p[i]);triangles.push_back(p[c]);p.erase(p.begin()+i);found=true;break;
        }
        if(!found)return 0;
    }
    triangles.insert(triangles.end(),p.begin(),p.end());
    for(size_t i=0;i<triangles.size();i+=3)fixture((b2Body*)body,&triangles[i],3,id,sensor,friction,bounce);
    return 1;
}
API void knl_pouch_remove(void *world,void *filter,void *body) {
    PouchFilter *f=(PouchFilter*)filter;
    for(b2Fixture *fx=((b2Body*)body)->GetFixtureList();fx;fx=fx->GetNext()) {
        int id=(int)(intptr_t)fx->GetUserData();
        for(size_t i=0;i<f->layers.size();)if(f->layers[i].id==id)f->layers.erase(f->layers.begin()+i);else i++;
    }
    ((b2World*)world)->DestroyBody((b2Body*)body);
}
API void knl_pouch_mass(void *body,float mass)
{
    b2Body *b=(b2Body*)body;b2MassData d;b->GetMassData(&d);if(d.mass<=0||d.mass==mass)return;
    d.I*=mass/d.mass;d.mass=mass;b->SetMassData(&d);
}
API void knl_pouch_set(void *body,int active,int type,float x,float y,float angle,float vx,float vy,float av)
{
    b2Body *b=(b2Body*)body;b->SetActive(active!=0);b->SetType(type==0?b2_dynamicBody:type==1?b2_kinematicBody:b2_staticBody);
    b2Vec2 p=b->GetPosition();
    // Unity's Euler angles wrap at 360; equivalent angles are not teleports. The small
    // tolerance also absorbs the world/local float round-trip through a translated UI root.
    if(fabsf(p.x-x)>1e-5f||fabsf(p.y-y)>1e-5f||fabsf(remainderf(b->GetAngle()-angle,2*b2_pi))>1e-5f)b->SetTransform(b2Vec2(x,y),angle);
    b2Vec2 v=b->GetLinearVelocity();
    if(v.x!=vx||v.y!=vy)b->SetLinearVelocity(b2Vec2(vx,vy));
    if(b->GetAngularVelocity()!=av)b->SetAngularVelocity(av);
}
API void knl_pouch_get(void *body,float *v)
{
    b2Body *b=(b2Body*)body;b2Vec2 p=b->GetPosition(),vel=b->GetLinearVelocity();
    v[0]=p.x;v[1]=p.y;v[2]=b->GetAngle();v[3]=vel.x;v[4]=vel.y;v[5]=b->GetAngularVelocity();
}
API void knl_pouch_step(void *w,float dt,float linearLimit,float angularLimit) { knl_pouch_linear_limit=linearLimit;knl_pouch_angular_limit=angularLimit;((b2World*)w)->Step(dt,8,3); }
API int knl_pouch_contacts(void *world,int *ids,float *points,int cap)
{
    int n=0;
    for(b2Contact *c=((b2World*)world)->GetContactList();c&&n<cap;c=c->GetNext())if(c->IsTouching()){
        b2WorldManifold m;m.normal.SetZero();m.points[0].SetZero();
        if(!c->GetFixtureA()->IsSensor()&&!c->GetFixtureB()->IsSensor())c->GetWorldManifold(&m);ids[n*2]=(int)(intptr_t)c->GetFixtureA()->GetUserData();ids[n*2+1]=(int)(intptr_t)c->GetFixtureB()->GetUserData();
        points[n*4]=m.points[0].x;points[n*4+1]=m.points[0].y;points[n*4+2]=m.normal.x;points[n*4+3]=m.normal.y;n++;
    }return n;
}
