// Shared by the Super Saiyan aura shaders.
#ifndef SAIYAN_COMMON_INCLUDED
#define SAIYAN_COMMON_INCLUDED

// The game's camera is linear with an ACES tonemapper. This takes a colour as it should appear on screen and
// gives the scene value that comes out as it (the same curve as KiOut in OrbFx/KiCommon.cginc), so the cream
// tongues run on into the game's bloom. hdr is 0 to leave it alone (the preview), 1 for a linear camera with
// no tonemapper.
float3 SOut(float3 film,float hdr)
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

float SHash(float2 p)
{
 float3 a=frac(p.xyx*float3(.1031,.103,.0973));
 a+=dot(a,a.yzx+33.33);
 return frac((a.x+a.y)*a.z);
}

// Smooth value noise in [0,1].
float SNoise(float2 p)
{
 float2 i=floor(p),f=p-i,u=f*f*(3-2*f);
 return lerp(lerp(SHash(i),SHash(i+float2(1,0)),u.x),lerp(SHash(i+float2(0,1)),SHash(i+float2(1,1)),u.x),u.y);
}

#endif
