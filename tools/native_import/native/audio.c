/* Streaming first-launch audio conversion. Requires BSD-licensed libogg/libvorbis.
 * FSB5 container fields follow vgmstream's documented parser; generic Vorbis setup
 * packets are supplied by its separately pinned, licensed codec table at build time.
 * No FMOD binary or game-derived table is linked into the importer.
 */
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <math.h>
#include <vorbis/codec.h>
#include <vorbis/vorbisenc.h>
#include <ogg/ogg.h>
#include "fsb_codebooks.h"
#define PI 3.14159265358979323846
static volatile int cancelled;
void knli_cancel(void){cancelled=1;}
void knli_reset_cancel(void){cancelled=0;}
int knli_cancelled(void){return cancelled;}
static uint32_t u32(const uint8_t*b){return (uint32_t)b[0]|(uint32_t)b[1]<<8|(uint32_t)b[2]<<16|(uint32_t)b[3]<<24;}
static uint64_t u64(const uint8_t*b){return u32(b)|(uint64_t)u32(b+4)<<32;}
static void put32(uint8_t*b,uint32_t n){for(int i=0;i<4;i++)b[i]=(uint8_t)(n>>(8*i));}
static int rd(FILE*f,void*p,size_t n){return fread(p,1,n,f)==n;}
static const int step_table[]={7,8,9,10,11,12,13,14,16,17,19,21,23,25,28,31,34,37,41,45,50,55,60,66,73,80,88,97,107,118,130,143,157,173,190,209,230,253,279,307,337,371,408,449,494,544,598,658,724,796,876,963,1060,1166,1282,1411,1552,1707,1878,2066,2272,2499,2749,3024,3327,3660,4026,4428,4871,5358,5894,6484,7132,7845,8630,9493,10442,11487,12635,13899,15289,16818,18500,20350,22385,24623,27086,29794,32767};
static const int index_table[]={-1,-1,-1,-1,2,4,6,8,-1,-1,-1,-1,2,4,6,8};
static int clamp(int n,int lo,int hi){return n<lo?lo:n>hi?hi:n;}
typedef struct {FILE*out;int music,channels,total,received,emitted,bn,encoded;float ring[64][2];double fir[33];short block[1024];vorbis_info vi;vorbis_comment vc;vorbis_dsp_state vd;vorbis_block vb;ogg_stream_state os;int enc_ready;} Output;
static int pages(Output*o,int flush){ogg_page p;while((flush?ogg_stream_flush(&o->os,&p):ogg_stream_pageout(&o->os,&p))){if(fwrite(p.header,1,p.header_len,o->out)!=(size_t)p.header_len||fwrite(p.body,1,p.body_len,o->out)!=(size_t)p.body_len)return 0;}return 1;}
static int encode_pending(Output*o){ogg_packet p;while(vorbis_analysis_blockout(&o->vd,&o->vb)==1){vorbis_analysis(&o->vb,NULL);vorbis_bitrate_addblock(&o->vb);while(vorbis_bitrate_flushpacket(&o->vd,&p)){ogg_stream_packetin(&o->os,&p);if(!pages(o,0))return 0;}}return 1;}
static int init_output(Output*o,const char*path,int music,int channels,int frames){o->out=fopen(path,"wb");if(!o->out)return 0;o->music=music;o->channels=channels;o->total=frames;o->received=0;o->emitted=0;double sum=0;for(int k=-16;k<=16;k++){double cutoff=.46;double sinc=k==0?cutoff:sin(PI*cutoff*k)/(PI*k);double window=.42+.5*cos(PI*k/16)+.08*cos(2*PI*k/16);o->fir[k+16]=sinc*window;sum+=o->fir[k+16];}for(int k=0;k<33;k++)o->fir[k]/=sum;
 if(music){vorbis_info_init(&o->vi);if(vorbis_encode_init_vbr(&o->vi,channels,22050,.2f))return 0;vorbis_comment_init(&o->vc);vorbis_comment_add_tag(&o->vc,"ENCODER","Kingdom native importer");vorbis_analysis_init(&o->vd,&o->vi);vorbis_block_init(&o->vd,&o->vb);ogg_stream_init(&o->os,0x4b4e4c49);o->enc_ready=1;ogg_packet a,b,c;vorbis_analysis_headerout(&o->vd,&o->vc,&a,&b,&c);ogg_stream_packetin(&o->os,&a);ogg_stream_packetin(&o->os,&b);ogg_stream_packetin(&o->os,&c);return pages(o,1);}
 uint8_t header[12];put32(header,22050);put32(header+4,(frames+1)/2);put32(header+8,1024);return fwrite(header,1,12,o->out)==12;
}
static int adpcm(Output*o){for(int i=o->bn;i<1024;i++)o->block[i]=o->block[o->bn-1];int pred=o->block[0],index=0,d=abs(o->block[1]-o->block[0]);while(index<88&&step_table[index]<d)index++;uint8_t out[516]={pred&255,(pred>>8)&255,index,0};for(int i=0;i<1024;i++){int step=step_table[index],diff=o->block[i]-pred,code=0,delta=step>>3;if(diff<0){code=8;diff=-diff;}if(diff>=step){code|=4;diff-=step;delta+=step;}if(diff>=(step>>1)){code|=2;diff-=step>>1;delta+=step>>1;}if(diff>=(step>>2)){code|=1;delta+=step>>2;}pred=clamp(pred+((code&8)?-delta:delta),-32768,32767);index=clamp(index+index_table[code],0,88);if(i&1)out[4+i/2]|=code<<4;else out[4+i/2]=code;}o->bn=0;return fwrite(out,1,sizeof(out),o->out)==sizeof(out);}
static int emit(Output*o,float l,float r){if(o->music){float**b=vorbis_analysis_buffer(&o->vd,1);b[0][0]=l;if(o->channels>1)b[1][0]=r;vorbis_analysis_wrote(&o->vd,1);if(!encode_pending(o))return 0;}else{o->block[o->bn++]=(short)clamp((int)lrintf((l+r)*16384.f),-32768,32767);if(o->bn==1024&&!adpcm(o))return 0;}o->emitted++;return 1;}
static int feed(Output*o,float l,float r){if(cancelled)return 0;int at=o->received++;o->ring[at&63][0]=l;o->ring[at&63][1]=r;while(o->emitted<(o->total+1)/2&&o->emitted*2+16<=at){int center=o->emitted*2;double left=0,right=0;for(int k=-16;k<=16;k++){int i=center+k;if(i<0||i>=o->total)continue;left+=o->ring[i&63][0]*o->fir[k+16];right+=o->ring[i&63][1]*o->fir[k+16];}if(!emit(o,(float)left,(float)right))return 0;}return 1;}
static int finish(Output*o){while(o->received<o->total+16)if(!feed(o,0,0))return 0;if(o->music){vorbis_analysis_wrote(&o->vd,0);if(!encode_pending(o)||!pages(o,1))return 0;}else if(o->bn&&!adpcm(o))return 0;return o->emitted==(o->total+1)/2&&!ferror(o->out);}
static void close_output(Output*o){if(o->enc_ready){ogg_stream_clear(&o->os);vorbis_block_clear(&o->vb);vorbis_dsp_clear(&o->vd);vorbis_comment_clear(&o->vc);vorbis_info_clear(&o->vi);}if(o->out)fclose(o->out);}
/* One validated clip per call; fixed 44.1 kHz source is the supported PC build.
 * Work is bounded in memory regardless of the music track's length. */
int knli_audio(const char*input,const char*output,int music,char*error,int error_size){FILE*f=NULL;Output out={0};vorbis_info vi;vorbis_comment vc;vorbis_dsp_state vd;vorbis_block vb;int dec_headers=0,dec_ready=0,ok=0;const char*why="Invalid or truncated FSB5 clip";uint8_t h[64],packet[65536];
 f=fopen(input,"rb");if(!f)goto done;if(!rd(f,h,60)||memcmp(h,"FSB5",4)||u32(h+4)!=1||u32(h+8)!=1)goto done;uint32_t header=u32(h+12),names=u32(h+16),bytes=u32(h+20),codec=u32(h+24);long end=60L+header+names+bytes;fseek(f,0,SEEK_END);if(ftell(f)!=end||header<8||header>1024*1024)goto done;fseek(f,60,SEEK_SET);if(!rd(f,h,8))goto done;uint64_t mode=u64(h);int channels=((mode>>5)&3)==0?1:((mode>>5)&3)==1?2:0;int rate_index=(mode>>1)&15;int frames=(int)(mode>>34);uint32_t offset=(uint32_t)((mode>>7)&0x7ffffff)*32,setup=0;
 if(!channels||rate_index!=8||frames<1||frames>44100*3600||offset>=bytes){why="Unsupported FSB5 channels/rate/length";goto done;}int extra=mode&1;while(extra){if(ftell(f)+4>60L+header||!rd(f,h,4))goto done;uint32_t flag=u32(h),n=(flag>>1)&0xffffff;extra=flag&1;if(ftell(f)+n>60L+header)goto done;if((flag>>25)==11){if(n<4||!rd(f,h,4))goto done;setup=u32(h);fseek(f,n-4,SEEK_CUR);}else fseek(f,n,SEEK_CUR);}
 if(!init_output(&out,output,music,channels,frames)){why="Cannot create encoded audio output";goto done;}fseek(f,60L+header+names+offset,SEEK_SET);
 if(codec==2){if((long)frames*channels*2>end-ftell(f))goto done;for(int i=0;i<frames;i++){if(!rd(f,h,channels*2))goto done;float l=(int16_t)(h[0]|h[1]<<8)/32768.f,r=channels==1?l:(int16_t)(h[2]|h[3]<<8)/32768.f;if(!feed(&out,l,r))goto done;}}
 else if(codec==15){const vcb_info_t*book=NULL;for(int i=0;i<vcb_list_count;i++)if(vcb_list[i].id==setup){book=&vcb_list[i];break;}if(!book){why="Unknown generic Vorbis setup ID";goto done;}vorbis_info_init(&vi);vorbis_comment_init(&vc);dec_headers=1;uint8_t id[30]={1,'v','o','r','b','i','s'};id[11]=channels;put32(id+12,44100);id[28]=0xb8;id[29]=1;ogg_packet p={0};p.packet=id;p.bytes=30;p.b_o_s=1;if(vorbis_synthesis_headerin(&vi,&vc,&p))goto done;uint8_t comment[]={3,'v','o','r','b','i','s',0,0,0,0,0,0,0,0,1};p.packet=comment;p.bytes=sizeof(comment);p.b_o_s=0;if(vorbis_synthesis_headerin(&vi,&vc,&p))goto done;p.packet=(unsigned char*)book->codebooks;p.bytes=book->size;if(vorbis_synthesis_headerin(&vi,&vc,&p)||vi.channels!=channels||vi.rate!=44100)goto done;if(vorbis_synthesis_init(&vd,&vi)||vorbis_block_init(&vd,&vb))goto done;dec_ready=1;
  long number=3;while(out.received<frames&&ftell(f)+2<=end){if(!rd(f,h,2))goto done;int n=h[0]|h[1]<<8;if(n==0||n==65535)break;if(ftell(f)+n>end||!rd(f,packet,n))goto done;p.packet=packet;p.bytes=n;p.packetno=number++;p.granulepos=-1;if(vorbis_synthesis(&vb,&p))goto done;vorbis_synthesis_blockin(&vd,&vb);float**pcm;int samples;while((samples=vorbis_synthesis_pcmout(&vd,&pcm))>0){int use=samples;if(use>frames-out.received)use=frames-out.received;for(int i=0;i<use;i++)if(!feed(&out,pcm[0][i],channels==1?pcm[0][i]:pcm[1][i]))goto done;vorbis_synthesis_read(&vd,samples);if(out.received>=frames)break;}}
  /* A final Vorbis overlap can be padded by FMOD. Reject substantial truncation. */
  if(frames-out.received>2048){why="Vorbis stream ended before declared audio duration";goto done;}
 }else{why="Unsupported FSB5 codec";goto done;}
 if(!finish(&out)){why="Audio output incomplete";goto done;}ok=1;
 done:if(dec_ready){vorbis_block_clear(&vb);vorbis_dsp_clear(&vd);}if(dec_headers){vorbis_comment_clear(&vc);vorbis_info_clear(&vi);}close_output(&out);if(f)fclose(f);if(!ok){remove(output);if(error&&error_size>0)snprintf(error,error_size,"%s",why);}return ok;
}
