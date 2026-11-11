// Soft light thrown on the surroundings, and the ring that flashes at the feet when charging starts.
Shader "DBPU/Blue Aura/Glow"
{
 Properties
 {
  _Noise("Noise",2D)="gray"{}
  _Opacity("Opacity",Float)=1
  _Shape("Shape (-, centre y, radius x, radius y)",Vector)=(0,.2,2.6,3.2)
  _Power("Falloff",Float)=2.2
  _Amount("Amount",Float)=.45
  _HotMix("Mix towards hot",Float)=.15
  _RingMix("Ring (0 is a soft blob)",Float)=0
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
  Blend One One
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #include "AuraCommon.cginc"
   float4 _Shape;
   float _Power,_Amount,_HotMix,_RingMix;
   struct appdata{float4 vertex:POSITION;};
   struct v2f{float4 pos:SV_POSITION;float2 p:TEXCOORD0;};
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.p=v.vertex.xy;return o;}
   float4 frag(v2f i):SV_Target
   {
    float e=AuraEgg(i.p,_Shape.y,_Shape.z,_Shape.w,0);
    float blob=pow(saturate(1-e),_Power);
    float ring=exp(-pow((e-.72)/.13,2));
    float shape=lerp(blob,ring*saturate((1-e)*7),_RingMix);
    float flicker=.86+.14*AuraSample(float2(frac(AuraClock()*.37),_Seed*.1),0)*2-.14;
    float3 deep,mid,hot;AuraPalette(deep,mid,hot);
    float3 col=lerp(mid,hot,_HotMix)*shape*_Amount*flicker*(1+_Charge*.4+_Overdrive*.6)*saturate(_Opacity);
    return float4(col,0);
   }
   ENDCG
  }
 }
 Fallback Off
}
