// Shared by the Super Saiyan Blue family of auras. Everything is drawn in "aura units": the prefab's
// local space, where the fighter's feet sit near y=-1.15 and the top of the head near y=.85.
#ifndef BLUE_AURA_COMMON
#define BLUE_AURA_COMMON
#include "UnityCG.cginc"
// r: soft fbm, g: ridged fbm, b: puffy billows, a: long pointed flame streaks. Tileable.
sampler2D _Noise;
// Set every frame by CinematicAuraRig.
float4 _Deep,_Mid,_Hot,_Lean;
float _VfxTime,_Charge,_Overdrive,_Seed,_Opacity;
// A layer that must keep its own colours whatever the rig sends (the cyan shell inside Kaioken).
float4 _FixDeep,_FixMid,_FixHot;
float _UseFixed;
float _AnimFps,_Boil,_BoilFps;
void AuraPalette(out float3 deep,out float3 mid,out float3 hot)
{
 deep=lerp(_Deep.rgb,_FixDeep.rgb,_UseFixed);
 mid=lerp(_Mid.rgb,_FixMid.rgb,_UseFixed);
 hot=lerp(_Hot.rgb,_FixHot.rgb,_UseFixed);
}
// Anime flames are drawn on twos and threes: holding each drawing reads as energy, sliding reads as a texture.
float AuraClock(){return floor(_VfxTime*_AnimFps)/_AnimFps;}
// The small quiver between drawings.
float AuraBoil(){return frac(floor(_VfxTime*_BoilFps)*.7548)*_Boil;}
float AuraSample(float2 uv,int channel){float4 n=tex2D(_Noise,uv);return channel==0?n.r:channel==1?n.g:channel==2?n.b:n.a;}
// An egg standing on its wide end: 0 at the centre, 1 on the surface, more than 1 outside.
float AuraEgg(float2 p,float cy,float rx,float ry,float taper)
{
 float yy=(p.y-cy)/ry;
 float xx=p.x/(rx*max(.2,1-taper*yy));
 return sqrt(xx*xx+yy*yy);
}
#endif
