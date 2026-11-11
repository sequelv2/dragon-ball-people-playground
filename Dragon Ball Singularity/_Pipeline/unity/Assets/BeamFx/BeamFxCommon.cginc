// Cheap procedural noise shared by the beam shaders. Everything is evaluated in beam space, so the
// patterns stay locked to the energy as it streams, however the beam is bent.
#ifndef BEAM_FX_COMMON
#define BEAM_FX_COMMON
// The wrap keeps the hash precise however far a long-lived beam has scrolled.
float BeamHash(float2 p){p=fmod(p,289.);p=frac(p*float2(127.1,311.7));p+=dot(p,p+34.53);return frac(p.x*p.y);}
float BeamNoise(float2 p)
{
 float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
 return lerp(lerp(BeamHash(i),BeamHash(i+float2(1,0)),f.x),lerp(BeamHash(i+float2(0,1)),BeamHash(i+float2(1,1)),f.x),f.y);
}
float BeamFbm(float2 p)
{
 float v=.5*BeamNoise(p);p=p*2.03+float2(17.1,9.2);
 v+=.25*BeamNoise(p);p=p*2.01+float2(5.3,31.7);
 v+=.125*BeamNoise(p);
 return v/.875;
}
#endif
