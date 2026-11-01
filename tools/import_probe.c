/* Bounded reconnaissance of Unity 2017 serialized-file headers. No asset conversion. */
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/resource.h>
#include <sys/stat.h>
static uint32_t be32(const unsigned char *p) { return ((uint32_t)p[0]<<24)|((uint32_t)p[1]<<16)|((uint32_t)p[2]<<8)|p[3]; }
int main(int argc,char **argv) {
    const char *files[]={"globalgamemanagers","globalgamemanagers.assets","level0","level1","level2","resources.assets","sharedassets0.assets","sharedassets1.assets","sharedassets2.assets"};
    if(argc!=2){fprintf(stderr,"Usage: %s <Kingdom_Data> (header inventory only; no conversion)\n",argv[0]);return 1;}
    for(unsigned i=0;i<sizeof(files)/sizeof(files[0]);i++) {
        char path[4096],unity[256];unsigned char h[20];struct stat st;
        if(snprintf(path,sizeof(path),"%s/%s",argv[1],files[i])>=(int)sizeof(path)){fprintf(stderr,"Path too long\n");return 1;}
        FILE *f=fopen(path,"rb");if(!f||stat(path,&st)||fread(h,1,sizeof(h),f)!=sizeof(h)){if(f)fclose(f);fprintf(stderr,"Missing/short serialized file: %s\n",files[i]);return 1;}
        uint32_t metadata=be32(h),total=be32(h+4),version=be32(h+8),offset=be32(h+12);
        if(version!=17 || total!=(uint64_t)st.st_size || offset<20 || offset>total || metadata>total){fclose(f);fprintf(stderr,"Unsupported/truncated header: %s\n",files[i]);return 1;}
        unsigned n=0;int ch;while(n<sizeof(unity)-1 && (ch=fgetc(f))!=EOF && ch!=0)unity[n++]=(char)ch;
        unity[n]=0;fclose(f);
        if(!n||n==sizeof(unity)-1){fprintf(stderr,"Invalid version string\n");return 1;}
        printf("%s format=%u unity=%s bytes=%u metadata=%u payload_offset=%u\n",files[i],version,unity,total,metadata,offset);
    }
    struct rusage usage;if(!getrusage(RUSAGE_SELF,&usage))printf("peak_rss_kib=%ld (header scanner only; decoding memory unknown)\n",usage.ru_maxrss);
    return 0;
}
