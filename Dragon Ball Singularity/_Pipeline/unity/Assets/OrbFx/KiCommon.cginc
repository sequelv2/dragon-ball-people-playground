// Shared by the sphere shaders.
#ifndef KI_COMMON_INCLUDED
#define KI_COMMON_INCLUDED

float2 KiHash(float2 p)
{
 float3 a=frac(p.xyx*float3(.1031,.103,.0973));
 a+=dot(a,a.yzx+33.33);
 return frac((a.xx+a.yz)*a.zy)*2-1;
}

// Smooth noise, centred on .5.
float KiNoise(float2 p)
{
 float2 i=floor(p),f=p-i,u=f*f*(3-2*f);
 float a=dot(KiHash(i),f);
 float b=dot(KiHash(i+float2(1,0)),f-float2(1,0));
 float c=dot(KiHash(i+float2(0,1)),f-float2(0,1));
 float d=dot(KiHash(i+float2(1,1)),f-float2(1,1));
 return lerp(lerp(a,b,u.x),lerp(c,d,u.x),u.y)*.75+.5;
}

// The same noise around a circle: turn is in turns, and a whole number of cells closes the loop.
float KiRound(float turn,float cells,float y,float seed)
{
 float x=frac(turn)*cells,ix=floor(x),fx=x-ix,iy=floor(y),fy=y-iy;
 float jx=ix+1>=cells?0:ix+1,s=seed*17.3;
 float ux=fx*fx*(3-2*fx),uy=fy*fy*(3-2*fy);
 float a=dot(KiHash(float2(ix+s,iy)),float2(fx,fy));
 float b=dot(KiHash(float2(jx+s,iy)),float2(fx-1,fy));
 float c=dot(KiHash(float2(ix+s,iy+1)),float2(fx,fy-1));
 float d=dot(KiHash(float2(jx+s,iy+1)),float2(fx-1,fy-1));
 return lerp(lerp(a,b,ux),lerp(c,d,ux),uy)*.75+.5;
}

// Energy to colour the way film takes it: strong light runs into white instead of clipping.
float3 KiFilm(float3 e){return 1-exp(-e);}

// The game's camera is linear with an ACES tonemapper. This takes a colour as it should appear on screen and
// gives the scene value that comes out as it, so anything close to white also runs on into the real bloom.
// hdr is 0 to leave it alone (the preview), 1 for a linear camera with no tonemapper.
float3 KiOut(float3 film,float hdr)
{
 if(hdr<.5)return film;
 float3 c=pow(saturate(film),2.2);
 if(hdr<=1)return c;
 c=min(c,.93);
 float3 a=2.43*c-2.51,b=.59*c-.03,k=.14*c;
 float3 x=(-b-sqrt(b*b-4*a*k))/(2*a)/.6;
 float grey=dot(x,float3(.27,.67,.06));
 return max(0,lerp(grey,x,1.25));
}

#endif
