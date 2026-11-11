// The body of the aura: a mass of flame with a white core, painted in bands like a cel. The surface
// is an egg torn by rising noise, so the outline is made of lobes and tongues that stream upwards,
// and the colour bands follow the same noise so they interlock instead of forming clean rings.
Shader "DBPU/Blue Aura/Flame"
{
 Properties
 {
  _Noise("Noise",2D)="gray"{}
  _Opacity("Opacity",Float)=1
  _Shape("Shape (-, centre y, radius x, radius y)",Vector)=(0,.4,1,1.8)
  _Taper("Taper",Float)=.34
  _Root("Root y",Float)=-1.55
  _Reach("Reach",Float)=3.6
  _Fan("Fan",Float)=.1
  _Speed("Speed",Float)=1.25
  _Sway("Sway",Float)=.35
  _LobeScale("Lobe scale",Float)=.3
  _LobeAmp("Lobe amount",Float)=.3
  _TongueScale("Tongue scale",Float)=.42
  _TongueAmp("Tongue amount",Float)=.55
  _StreakScale("Streak scale",Float)=.85
  _StreakAmp("Streak amount",Float)=.3
  _BandNoise("Band noise",Float)=.12
  _Bands("Bands: deep>mid, mid>pale, pale>hot, hot>white",Vector)=(.06,.2,.32,.46)
  _BandSoft("Band softness",Float)=.035
  _Mottle("Mottle in the deep band",Float)=.4
  _Rim("Rim",Float)=.85
  _RimWidth("Rim width",Float)=.018
  _GlowFall("Glow falloff",Float)=7
  _GlowAmount("Glow amount",Float)=.55
  _Alpha("Alpha",Float)=.96
  _AnimFps("Drawings per second",Float)=24
  _Boil("Boil",Float)=.02
  _BoilFps("Boil per second",Float)=12
  _UseFixed("Use fixed palette",Float)=0
  _FixDeep("Fixed deep",Color)=(0,0,1,1)
  _FixMid("Fixed mid",Color)=(0,1,1,1)
  _FixHot("Fixed hot",Color)=(1,1,1,1)
 }
 SubShader
 {
  Tags{"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
  Cull Off ZWrite Off ZTest Always Lighting Off Fog{Mode Off}
  Blend One OneMinusSrcAlpha
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #include "AuraCommon.cginc"
   float4 _Shape,_Bands;
   float _Taper,_Root,_Reach,_Fan,_Speed,_Sway,_LobeScale,_LobeAmp,_TongueScale,_TongueAmp,_StreakScale,_StreakAmp,_BandNoise,_BandSoft,_Mottle,_Rim,_RimWidth,_GlowFall,_GlowAmount,_Alpha;
   struct appdata{float4 vertex:POSITION;};
   struct v2f{float4 pos:SV_POSITION;float2 p:TEXCOORD0;};
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.p=v.vertex.xy;return o;}
   float4 frag(v2f i):SV_Target
   {
    float2 p=i.p;
    float energy=saturate(_Charge*.6+_Overdrive);
    float t=AuraClock();
    float speed=_Speed*(1+energy*.55);
    float h=saturate((p.y-_Root)/_Reach);
    // The plume trails the fighter: the root stays planted and the tip swings.
    p-=_Lean.xy*pow(h,1.35);
    float spread=1+_Fan*max(p.y-_Root,0);
    float u=p.x/spread+_Seed*.37+AuraBoil(),v=p.y;
    u+=(AuraSample(float2(u*.11,v*.07-frac(t*speed*.2)),0)-.5)*_Sway*(.25+h);

    float lobeA=AuraSample(float2(u*_LobeScale,v*_LobeScale*.55-frac(t*speed*.42)),2);
    float lobeB=AuraSample(float2(u*_LobeScale*2.1+.37,v*_LobeScale*1.15-frac(t*speed*.71)),2);
    float tongueA=AuraSample(float2(u*_TongueScale+.61,v*_TongueScale*.42-frac(t*speed*.55)),3);
    float tongueB=AuraSample(float2(u*_TongueScale*1.7+.13,v*_TongueScale*.66-frac(t*speed*.83)),3);
    float tongues=max(tongueA,tongueB*.8);

    float e=AuraEgg(p,_Shape.y,_Shape.z*(1+_Charge*.1+_Overdrive*.12),_Shape.w*(1+_Charge*.06+_Overdrive*.08),_Taper);
    float high=smoothstep(.15,.85,h);
    float f=1-e;
    f+=(lobeA-.5)*_LobeAmp*(.6+.6*high)+(lobeB-.5)*_LobeAmp*.5;
    // Tongues grow out of the surface; far from it they would float loose.
    f+=tongues*_TongueAmp*(.25+.75*high)*smoothstep(-.35,.15,f);

    float streak=AuraSample(float2(u*_StreakScale+.27,v*_StreakScale*.33-frac(t*speed*.97)),3);
    float ridge=AuraSample(float2(u*.8+.5,v*.38-frac(t*speed*.6)),1);
    float b=f+(ridge-.5)*_BandNoise+(streak-.2)*_StreakAmp*(.3+.7*high)+energy*.08;

    float3 deep,mid,hot;AuraPalette(deep,mid,hot);
    float s=_BandSoft;
    float3 col=lerp(deep,deep*float3(.8,.72,.95),smoothstep(.52,.72,lobeB)*_Mottle);
    col=lerp(col,mid,smoothstep(_Bands.x-s,_Bands.x+s*1.6,b));
    col=lerp(col,lerp(mid,hot,.62),smoothstep(_Bands.y-s,_Bands.y+s*2,b));
    col=lerp(col,hot,smoothstep(_Bands.z-s,_Bands.z+s*2.2,b));
    col=lerp(col,float3(1,1,1),smoothstep(_Bands.w-s,_Bands.w+s*2.5,b));

    float aa=max(fwidth(f),.0015);
    float mass=smoothstep(-aa,aa,f);
    // A one pixel minimum keeps the bright outline readable from far away.
    float rimWidth=max(_RimWidth,aa*1.6);
    float rim=1-smoothstep(rimWidth*.4,rimWidth,f);
    col=lerp(col,lerp(mid,hot,.7),rim*_Rim);
    float glow=exp(min(f,0)*_GlowFall)*_GlowAmount*(1-mass);
    float alpha=mass*_Alpha;
    float3 rgb=col*alpha+lerp(mid,hot,.25)*glow;
    float op=saturate(_Opacity);
    return float4(rgb*op,alpha*op);
   }
   ENDCG
  }
 }
 Fallback Off
}
