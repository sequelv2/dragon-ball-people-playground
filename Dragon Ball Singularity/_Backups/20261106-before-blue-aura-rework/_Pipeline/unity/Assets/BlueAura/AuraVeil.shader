// A thin wash of light drawn over the fighter, so the body sits inside the aura instead of on top of it.
Shader "DBPU/Blue Aura/Veil"
{
 Properties
 {
  _Noise("Noise",2D)="gray"{}
  _Opacity("Opacity",Float)=1
  _Shape("Shape (-, centre y, radius x, radius y)",Vector)=(0,-.1,.95,1.45)
  _Taper("Taper",Float)=.1
  _Speed("Speed",Float)=1.3
  _Wash("Wash",Float)=.3
  _Add("Added light",Float)=.1
  _White("Whiteness",Float)=.7
  _AnimFps("Drawings per second",Float)=24
  _Boil("Boil",Float)=0
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
   float4 _Shape;
   float _Taper,_Speed,_Wash,_Add,_White;
   struct appdata{float4 vertex:POSITION;};
   struct v2f{float4 pos:SV_POSITION;float2 p:TEXCOORD0;};
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.p=v.vertex.xy;return o;}
   float4 frag(v2f i):SV_Target
   {
    float2 p=i.p;
    float t=AuraClock();
    float e=AuraEgg(p,_Shape.y,_Shape.z,_Shape.w,_Taper);
    float mask=saturate(1-e);
    mask=mask*mask*(3-2*mask);
    float u=p.x+_Seed*.37;
    float streak=AuraSample(float2(u*.8+.27,p.y*.28-frac(t*_Speed*.97)),3);
    float soft=AuraSample(float2(u*.35,p.y*.2-frac(t*_Speed*.5)),0);
    float amount=mask*saturate(.5+streak*.55+(soft-.5)*.6);
    float3 deep,mid,hot;AuraPalette(deep,mid,hot);
    float3 col=lerp(lerp(mid,hot,.6),float3(1,1,1),_White*amount);
    float alpha=amount*_Wash*(1+_Charge*.35+_Overdrive*.5);
    float op=saturate(_Opacity);
    return float4((col*alpha+lerp(mid,hot,.4)*amount*_Add)*op,alpha*op);
   }
   ENDCG
  }
 }
 Fallback Off
}
