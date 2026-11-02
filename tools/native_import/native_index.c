/*
 * Bounded Unity 2017 serialized-file metadata/index reader.
 * This deliberately never reads object payloads or emits game data.
 */
#define _POSIX_C_SOURCE 200809L
#include <errno.h>
#include <inttypes.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/stat.h>
#include <sys/types.h>
#include <sys/resource.h>
#include <time.h>

#define MAX_TYPES 65536u
#define MAX_OBJECTS 2000000u
#define MAX_STRING 256u

typedef struct { int32_t class_id; int8_t stripped; int16_t script_index; uint8_t script_id[16], hash[16]; } TypeRec;
typedef struct { int64_t path_id; uint64_t data_offset; uint32_t size, type_index; } ObjectRec;
typedef struct { FILE *f; uint64_t size, pos; int failed; } Reader;

static const char *files[] = {"globalgamemanagers", "globalgamemanagers.assets", "level0", "level1", "level2", "resources.assets", "sharedassets0.assets", "sharedassets1.assets", "sharedassets2.assets"};
static const char *fingerprints[] = {
 "8073fab961a42b4e67d2351131564519b63fef4e945ddf2512cb03dd013001c6",
 "361a0eaeb507fcce673622436de5189da6c9ff3ef24414268eb63ab453a27571",
 "a69b464af59f9cbaaee41a59bbb883c28ce5872ecf1a5b1b4ce91377f5f2b83a",
 "d43b2fe2ac793893cc76ec69473b7642a68fc15ed193f84e264471965619eb82",
 "f5ab8dfb310d8ec909c4be1ea65b9f492bddb8b579454ac930b499833e5aa92f",
 "d47d3873cb40081d29c4dc9448e4b4bd86ff2c93c43bd174d9fe1644dd22bd3b",
 "dcbe30d3a828aba69583bd55609aed7a8aeebce538d2f84199bf0e3ba3fa80b9",
 "babba4d01e89c634d63b01afe9d158037607a216dd4548d65e805c9400483e8e",
 "d7f0291aac639cf53a8a3bd9769882850bf4dfdb2f27d0fa027be13b3ca9c920"
};

typedef struct { uint32_t h[8]; uint64_t bits; uint8_t block[64]; size_t used; } Sha256;
static const uint32_t k256[64]={
0x428a2f98,0x71374491,0xb5c0fbcf,0xe9b5dba5,0x3956c25b,0x59f111f1,0x923f82a4,0xab1c5ed5,0xd807aa98,0x12835b01,0x243185be,0x550c7dc3,0x72be5d74,0x80deb1fe,0x9bdc06a7,0xc19bf174,
0xe49b69c1,0xefbe4786,0x0fc19dc6,0x240ca1cc,0x2de92c6f,0x4a7484aa,0x5cb0a9dc,0x76f988da,0x983e5152,0xa831c66d,0xb00327c8,0xbf597fc7,0xc6e00bf3,0xd5a79147,0x06ca6351,0x14292967,
0x27b70a85,0x2e1b2138,0x4d2c6dfc,0x53380d13,0x650a7354,0x766a0abb,0x81c2c92e,0x92722c85,0xa2bfe8a1,0xa81a664b,0xc24b8b70,0xc76c51a3,0xd192e819,0xd6990624,
0xf40e3585,0x106aa070,0x19a4c116,0x1e376c08,0x2748774c,0x34b0bcb5,0x391c0cb3,0x4ed8aa4a,0x5b9cca4f,0x682e6ff3,0x748f82ee,0x78a5636f,0x84c87814,0x8cc70208,0x90befffa,0xa4506ceb,0xbef9a3f7,0xc67178f2};
static uint32_t rr(uint32_t x,unsigned n){return (x>>n)|(x<<(32-n));}
static void sha_block(Sha256*s,const uint8_t*b){uint32_t w[64],a,c,d,e,f,g,h,x,t1,t2;for(int i=0;i<16;i++)w[i]=(uint32_t)b[i*4]<<24|(uint32_t)b[i*4+1]<<16|(uint32_t)b[i*4+2]<<8|b[i*4+3];for(int i=16;i<64;i++){uint32_t q=rr(w[i-15],7)^rr(w[i-15],18)^(w[i-15]>>3),z=rr(w[i-2],17)^rr(w[i-2],19)^(w[i-2]>>10);w[i]=w[i-16]+q+w[i-7]+z;}a=s->h[0];x=s->h[1];c=s->h[2];d=s->h[3];e=s->h[4];f=s->h[5];g=s->h[6];h=s->h[7];for(int i=0;i<64;i++){t1=h+(rr(e,6)^rr(e,11)^rr(e,25))+((e&f)^(~e&g))+k256[i]+w[i];t2=(rr(a,2)^rr(a,13)^rr(a,22))+((a&x)^(a&c)^(x&c));h=g;g=f;f=e;e=d+t1;d=c;c=x;x=a;a=t1+t2;}s->h[0]+=a;s->h[1]+=x;s->h[2]+=c;s->h[3]+=d;s->h[4]+=e;s->h[5]+=f;s->h[6]+=g;s->h[7]+=h;}
static void sha_init(Sha256*s){static const uint32_t iv[8]={0x6a09e667,0xbb67ae85,0x3c6ef372,0xa54ff53a,0x510e527f,0x9b05688c,0x1f83d9ab,0x5be0cd19};memcpy(s->h,iv,sizeof(iv));s->bits=0;s->used=0;}
static void sha_update(Sha256*s,const uint8_t*p,size_t n){s->bits+=(uint64_t)n*8;while(n){size_t z=64-s->used;if(z>n)z=n;memcpy(s->block+s->used,p,z);s->used+=z;p+=z;n-=z;if(s->used==64){sha_block(s,s->block);s->used=0;}}}
static void sha_final(Sha256*s,uint8_t out[32]){uint64_t bits=s->bits;s->block[s->used++]=0x80;if(s->used>56){memset(s->block+s->used,0,64-s->used);sha_block(s,s->block);s->used=0;}memset(s->block+s->used,0,56-s->used);for(int i=0;i<8;i++)s->block[63-i]=(uint8_t)(bits>>(i*8));sha_block(s,s->block);for(int i=0;i<8;i++){out[i*4]=s->h[i]>>24;out[i*4+1]=s->h[i]>>16;out[i*4+2]=s->h[i]>>8;out[i*4+3]=s->h[i];}}
static int verify_fingerprint(const char *root,const char *name,const char *expected){char path[4096];uint8_t buf[32768],digest[32];char hex[65];if(snprintf(path,sizeof(path),"%s/%s",root,name)>=(int)sizeof(path))return 0;FILE*f=fopen(path,"rb");if(!f)return 0;Sha256 s;sha_init(&s);size_t n;while((n=fread(buf,1,sizeof(buf),f))>0)sha_update(&s,buf,n);int ok=!ferror(f);fclose(f);if(!ok)return 0;sha_final(&s,digest);for(int i=0;i<32;i++)sprintf(hex+i*2,"%02x",digest[i]);hex[64]=0;return strcmp(hex,expected)==0;}

static uint32_t be32(const uint8_t *p) { return (uint32_t)p[0]<<24 | (uint32_t)p[1]<<16 | (uint32_t)p[2]<<8 | p[3]; }
static uint16_t le16(const uint8_t *p) { return (uint16_t)p[0] | (uint16_t)p[1]<<8; }
static uint32_t le32(const uint8_t *p) { return (uint32_t)p[0] | (uint32_t)p[1]<<8 | (uint32_t)p[2]<<16 | (uint32_t)p[3]<<24; }
static uint64_t le64(const uint8_t *p) { return (uint64_t)le32(p) | (uint64_t)le32(p+4)<<32; }
static int take(Reader *r, void *out, size_t n) {
    if (n > r->size-r->pos) { r->failed=1; return 0; }
    if (fread(out,1,n,r->f)!=n) { r->failed=1; return 0; }
    r->pos += n; return 1;
}
static int r_u8(Reader *r, uint8_t *v) { return take(r,v,1); }
static int r_i16(Reader *r, int16_t *v) { uint8_t b[2]; if(!take(r,b,2))return 0; *v=(int16_t)le16(b); return 1; }
static int r_i32(Reader *r, int32_t *v) { uint8_t b[4]; if(!take(r,b,4))return 0; *v=(int32_t)le32(b); return 1; }
static int r_u32(Reader *r, uint32_t *v) { uint8_t b[4]; if(!take(r,b,4))return 0; *v=le32(b); return 1; }
static int r_u64(Reader *r, uint64_t *v) { uint8_t b[8]; if(!take(r,b,8))return 0; *v=le64(b); return 1; }
static int cstring(Reader *r, char *out, size_t cap) {
    size_t n=0; uint8_t c;
    do { if(!r_u8(r,&c))return 0; if(c && n+1<cap) out[n++]=(char)c; else if(c) { r->failed=1; return 0; } } while(c);
    out[n]=0; return n>0;
}
static int skip_type_tree(Reader *r) {
    int32_t nodes, strings; uint8_t b[24];
    if(!r_i32(r,&nodes)||!r_i32(r,&strings)||nodes<0||nodes>1000000||strings<0||strings>64*1024*1024)return 0;
    /* Unity 2017 (format 17) uses 24-byte flattened type-tree nodes. */
    for(int32_t i=0;i<nodes;i++)if(!take(r,b,24))return 0;
    if((uint64_t)strings>r->size-r->pos){r->failed=1;return 0;}
    uint8_t scratch[4096]; while(strings>0){size_t n=(size_t)strings<sizeof(scratch)?(size_t)strings:sizeof(scratch);if(!take(r,scratch,n))return 0;strings-=(int32_t)n;}
    return 1;
}
static void fail(const char *s) { fprintf(stderr,"ERROR: %s\n",s); }
static int parse_file(const char *root, const char *name, FILE *out, uint64_t *total_objects) {
    char path[4096], unity[MAX_STRING]; struct stat st; uint8_t h[20], endian; uint32_t metadata,file_size,version,data_off,n_types,n_objects; int32_t platform;
    if(snprintf(path,sizeof(path),"%s/%s",root,name)>=(int)sizeof(path)){ fail("source path too long"); return 0; }
    FILE *f=fopen(path,"rb"); if(!f){ fprintf(stderr,"ERROR: missing %s\n",name); return 0; }
    if(fstat(fileno(f),&st)||st.st_size<20||fread(h,1,20,f)!=20){ fclose(f); fprintf(stderr,"ERROR: short serialized header: %s\n",name); return 0; }
    metadata=be32(h); file_size=be32(h+4); version=be32(h+8); data_off=be32(h+12); endian=h[16];
    if(version!=17 || file_size!=(uint64_t)st.st_size || metadata>file_size-20 || data_off<20+metadata || data_off>file_size || endian!=0){ fclose(f); fprintf(stderr,"ERROR: invalid/unsupported Unity header: %s\n",name); return 0; }
    Reader r={f,20u+metadata,20,0};
    int32_t n_signed;
    uint8_t tree_enabled;
    if(!cstring(&r,unity,sizeof(unity))||strcmp(unity,"2017.1.2p1")!=0||!r_i32(&r,&platform)||!r_u8(&r,&tree_enabled)||tree_enabled>1||!r_i32(&r,&n_signed)||n_signed<0||(uint32_t)n_signed>MAX_TYPES){ fclose(f); fprintf(stderr,"ERROR: invalid metadata/type count: %s\n",name); return 0; }
    n_types=(uint32_t)n_signed;
    TypeRec *types=calloc(n_types?n_types:1,sizeof(*types)); if(!types){ fclose(f); fail("out of memory for type table"); return 0; }
    for(uint32_t i=0;i<n_types;i++){
        uint8_t b; if(!r_i32(&r,&types[i].class_id)||!r_u8(&r,&b)||!r_i16(&r,&types[i].script_index)){ r.failed=1; break; }
        if(types[i].class_id==114 && !take(&r,types[i].script_id,16)){ r.failed=1; break; }
        if(!take(&r,types[i].hash,16)){ r.failed=1; break; }
        types[i].stripped=(int8_t)b;
        if(tree_enabled && !skip_type_tree(&r)) { r.failed=1; break; }
    }
    if(!r_i32(&r,&n_signed)||n_signed<0||(uint32_t)n_signed>MAX_OBJECTS)r.failed=1;
    n_objects=r.failed?0:(uint32_t)n_signed;
    ObjectRec *objs=NULL;
    if(!r.failed){ objs=calloc(n_objects?n_objects:1,sizeof(*objs)); if(!objs)r.failed=1; }
    int64_t prev_path=0;
    for(uint32_t i=0;!r.failed&&i<n_objects;i++){
        uint8_t pad[4]; int32_t bytes; uint32_t start; int32_t tid; uint64_t raw_path;
        if(r.pos&3u){ uint32_t a=(uint32_t)(4-(r.pos&3u)); if(!take(&r,pad,a))break; }
        if(!r_u64(&r,&raw_path)||!r_u32(&r,&start)||!r_i32(&r,&bytes)||!r_i32(&r,&tid)){ r.failed=1; break; }
        memcpy(&objs[i].path_id,&raw_path,sizeof(raw_path));
        if(bytes<0||tid<0||(uint32_t)tid>=n_types||(uint64_t)start+(uint32_t)bytes>file_size-data_off){ r.failed=1; break; }
        objs[i].data_offset=(uint64_t)data_off+start; objs[i].size=(uint32_t)bytes; objs[i].type_index=(uint32_t)tid;
        if(i && objs[i].path_id==prev_path){ r.failed=1; break; } prev_path=objs[i].path_id;
    }
    if(!r.failed && r.pos>20u+metadata)r.failed=1;
    if(r.failed){ free(types); free(objs); fclose(f); fprintf(stderr,"ERROR: truncated or malformed metadata/object table: %s\n",name); return 0; }
    fprintf(out,"{\"file\":\"%s\",\"unity\":\"%s\",\"platform\":%d,\"types\":[",name,unity,platform);
    for(uint32_t i=0;i<n_types;i++){
        fprintf(out,"%s{\"class_id\":%d,\"script_type_index\":%d,\"stripped\":%d,\"script_id\":\"",i?",":"",types[i].class_id,types[i].script_index,types[i].stripped);
        for(unsigned j=0;j<16;j++)fprintf(out,"%02x",types[i].script_id[j]);
        fprintf(out,"\",\"type_hash\":\"");
        for(unsigned j=0;j<16;j++)fprintf(out,"%02x",types[i].hash[j]);
        fprintf(out,"\"}");
    }
    fprintf(out,"],\"objects\":[");
    for(uint32_t i=0;i<n_objects;i++)fprintf(out,"%s{\"path_id\":\"%" PRId64 "\",\"type_index\":%u,\"class_id\":%d,\"offset\":\"%" PRIu64 "\",\"size\":%u}",i?",":"",objs[i].path_id,objs[i].type_index,types[objs[i].type_index].class_id,objs[i].data_offset,objs[i].size);
    fprintf(out,"]}\n"); *total_objects+=n_objects;
    free(types); free(objs); fclose(f); return 1;
}

int main(int argc,char **argv){
    struct timespec started, finished;
    clock_gettime(CLOCK_MONOTONIC, &started);
    int fixture_mode=argc==3&&!strcmp(argv[1],"fixture-index");
    if(argc!=3 || (strcmp(argv[1],"index")&&!fixture_mode)){ fprintf(stderr,"usage: native_index index <Kingdom_Data>\n       native_index fixture-index <fixture-dir>  (tests only)\n"); return 2; }
    FILE *out=stdout; uint64_t total=0;
    fprintf(stderr,"STATUS: Preparing game files — this is a one-time process\nSTATUS: Checking supported source files\n");
    if(!fixture_mode){
        for(size_t i=0;i<sizeof(files)/sizeof(files[0]);i++){
            fprintf(stderr,"STATUS: Verifying %s\n",files[i]);
            if(!verify_fingerprint(argv[2],files[i],fingerprints[i])){fprintf(stderr,"ERROR: unsupported or changed source fingerprint: %s\n",files[i]);return 1;}
        }
        fprintf(stderr,"STATUS: Supported source fingerprints verified\n");
    }
    for(size_t i=0;i<sizeof(files)/sizeof(files[0]);i++){
        if(!parse_file(argv[2],files[i],out,&total))return 1;
        fprintf(stderr,"STATUS: Indexed %s\n",files[i]);
    }
    fprintf(stderr,"STATUS: Metadata index complete (%" PRIu64 " objects). Payload decoding and KNL pack output are not implemented.\n",total);
    struct rusage usage;
    if (!getrusage(RUSAGE_SELF, &usage) && !clock_gettime(CLOCK_MONOTONIC, &finished)) {
        double seconds = (double)(finished.tv_sec-started.tv_sec)+(finished.tv_nsec-started.tv_nsec)/1e9;
        fprintf(stderr,"MEASURE: index_elapsed_seconds=%.6f peak_rss_kib=%ld (index only)\n",seconds,usage.ru_maxrss);
    }
    return 0;
}
