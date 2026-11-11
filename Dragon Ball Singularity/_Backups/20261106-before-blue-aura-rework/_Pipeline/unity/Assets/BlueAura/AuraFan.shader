// The Kaioken mantle over Super Saiyan Blue: a huge field of long pointed streaks fanning out from the
// body. It goes crimson far out, hot magenta in the middle and pink-white against the shell inside it.
Shader "DBPU/Blue Aura/Fan"
{
 Properties
 {
  _Noise("Noise",2D)="gray"{}
  _Opacity("Opacity",Float)=1
  _Shape("Shape (-, centre y, radius x, radius y)",Vector)=(0,.6,1.9,2.8)
  _Taper("Taper",Float)=.1
  _Root("Root y",Float)=-1.7
  _Reach("Reach",Float)=5
  _Fan("Fan",Float)=.55
  _Speed("Speed",Float)=1.6
  _SpikeScale("Spike scale",Float)=.3
  _SpikeAmp("Spike amount",Float)=.85
  _LobeAmp("Lobe amount",Float)=.16
  _Inner("Inner shell, in surface units",Float)=.52
  _Bloom("Bloom against the shell",Float)=.8
  _Dark("Dark streaks",Float)=.75
  _GlowFall("Glow falloff",Float)=3.5
  _GlowAmount("Glow amount",Float)=.32
  _Alpha("Alpha",Float)=.97
  _AnimFps("Drawings per second",Float)=24
  _Boil("Boil",Float)=.02
  _BoilFps("Boil per second",Float)=12
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
   float4 _Shape;
   float _Taper,_Root,_Reach,_Fan,_Speed,_SpikeScale,_SpikeAmp,_LobeAmp,_Inner,_Bloom,_Dark,_GlowFall,_GlowAmount,_Alpha;
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
    p-=_Lean.xy*pow(h,1.35);
    float spread=1+_Fan*max(p.y-_Root,0);
    float u=p.x/spread+_Seed*.37+AuraBoil(),v=p.y/(1+_Fan*.35);
    float s=_SpikeScale;
    float sA=AuraSample(float2(u*s+.11,v*s*.42-frac(t*speed*.31)),3);
    float sB=AuraSample(float2(u*s*1.65+.63,v*s*.62-frac(t*speed*.47)),3);
    float sC=AuraSample(float2(u*s*2.6+.29,v*s*.85-frac(t*speed*.66)),3);
    float sD=AuraSample(float2(u*s*1.25+.83,v*s*.5-frac(t*speed*.38)),3);
    float lobe=AuraSample(float2(u*.34,v*.2-frac(t*speed*.27)),2);

    float e=AuraEgg(p,_Shape.y,_Shape.z*(1+_Charge*.12+_Overdrive*.14),_Shape.w*(1+_Charge*.08+_Overdrive*.1),_Taper);
    float radial=1-e;
    float spikes=max(sA,sB*.9);
    float f=radial+(spikes-.3)*_SpikeAmp+(lobe-.5)*_LobeAmp;

    float3 deep,mid,hot;AuraPalette(deep,mid,hot);
    float3 crimson=deep*.62;
    float3 col=lerp(crimson,deep,smoothstep(.0,.16,f));
    col=lerp(col,mid,smoothstep(.1,.34,f+(sB-.35)*.34));
    // Pale streaks burning through the magenta, strongest near the fighter.
    float bright=smoothstep(.4,.72,max(sC,sA*.9))*smoothstep(.02,.42,radial);
    col=lerp(col,lerp(mid,hot,.72),bright*.82);
    // Dark blades cutting the outer field.
    float dark=smoothstep(.42,.74,sD)*(1-smoothstep(.08,.4,radial));
    col=lerp(col,crimson*.72,dark*_Dark);
    // The field goes pink, then lilac, then near white where it meets the shell inside it.
    float near=saturate((radial-_Inner*.42)/max(.001,_Inner*.58));
    near=near*near*(3-2*near);
    col=lerp(col,hot,near*_Bloom);
    col=lerp(col,float3(.98,.82,.98),pow(near,2.6)*_Bloom);

    float aa=max(fwidth(f),.0015);
    float mass=smoothstep(-aa,aa,f);
    float glow=exp(min(f,0)*_GlowFall)*_GlowAmount*(1-mass);
    float alpha=mass*_Alpha;
    float3 rgb=col*alpha+deep*glow;
    float op=saturate(_Opacity);
    return float4(rgb*op,alpha*op);
   }
   ENDCG
  }
 }
 Fallback Off
}
