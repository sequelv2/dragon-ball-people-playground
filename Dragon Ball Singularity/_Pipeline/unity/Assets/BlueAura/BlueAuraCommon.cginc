// Shared by the Super Saiyan Blue aura shaders (SSJ Blue, Blue Evolution, Kaioken over Blue).
// Positions are in aura units: the rig's local space, where the fighter's feet sit at y=-1.4 and the
// fighter is 1.86 tall (measured in game at the scale AuraHandler gives refined auras; keep in step with
// BlueAuraRig.Feet and Height). The shaders work in "H" units: the fighter's height is 1, the feet at 0.
#ifndef BLUE_AURA_COMMON
#define BLUE_AURA_COMMON
#include "UnityCG.cginc"

#define BA_FEET (-1.4)
#define BA_HEIGHT 1.86

float _Hdr,_Opacity;
// The brightest linear value a layer may ask the game's camera for. Wide sheets of white light past it
// flood the bloom and wash every colour round them into pastel; small sparks may go higher.
float _WhiteCap;
// Where the soft knee under the cap starts, as a fraction of it; 1 is a hard cap.
float _WhiteKnee;

float2 BaHash(float2 p)
{
 float3 a=frac(p.xyx*float3(.1031,.103,.0973));
 a+=dot(a,a.yzx+33.33);
 return frac((a.xx+a.yz)*a.zy)*2-1;
}

// Smooth gradient noise, centred on .5.
float BaNoise(float2 p)
{
 float2 i=floor(p),f=p-i,u=f*f*(3-2*f);
 float a=dot(BaHash(i),f);
 float b=dot(BaHash(i+float2(1,0)),f-float2(1,0));
 float c=dot(BaHash(i+float2(0,1)),f-float2(0,1));
 float d=dot(BaHash(i+float2(1,1)),f-float2(1,1));
 return lerp(lerp(a,b,u.x),lerp(c,d,u.x),u.y)*.75+.5;
}

float BaFbm(float2 p)
{
 return BaNoise(p)*.56+BaNoise(p*2.07+float2(7.1,3.3))*.29+BaNoise(p*4.13+float2(3.7,9.2))*.15;
}

// Light added up as energy and exposed the way film takes it: strong light of any colour runs into white.
float3 BaFilm(float3 e){return 1-exp(-max(e,0));}

// The game's camera is linear with an ACES tonemapper, bloom over 1 and auto exposure. This takes a colour as
// it should appear on screen and gives the scene value that comes out as it, so near-white runs on into the
// real bloom. hdr 0 leaves it alone (the preview), 1 is a linear camera with no tonemapper.
float3 BaOut(float3 film,float hdr)
{
 if(hdr<.5)return film;
 float3 c=pow(saturate(film),2.2);
 if(hdr<=1)return c;
 // A soft knee under the cap keeps the tint in light that would otherwise flatten into one grey.
 float cap=_WhiteCap>0?_WhiteCap:.93,knee=cap*(_WhiteKnee>0?_WhiteKnee:.6),room=max(cap-knee,.0001);
 c=c<knee?c:knee+room*(1-exp(-(c-knee)/room));
 float3 a=2.43*c-2.51,b=.59*c-.03,k=.14*c;
 float3 x=(-b-sqrt(b*b-4*a*k))/(2*a)/.6;
 float grey=dot(x,float3(.27,.67,.06));
 return max(0,lerp(grey,x,1.25));
}

// Object-space position to H units.
float2 BaH(float2 local){return float2(local.x,local.y-BA_FEET)/BA_HEIGHT;}

#endif
