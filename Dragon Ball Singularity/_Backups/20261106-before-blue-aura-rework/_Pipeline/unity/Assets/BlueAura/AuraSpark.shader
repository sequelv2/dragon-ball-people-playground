// A rising grain of ki: a white point in a coloured halo.
Shader "DBPU/Blue Aura/Spark"
{
 Properties
 {
  _Opacity("Opacity",Float)=1
  _Amount("Amount",Float)=1
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
   float _Amount;
   struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct v2f{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
   float4 frag(v2f i):SV_Target
   {
    float d=length(i.uv*2-1);
    float core=smoothstep(.42,.12,d);
    float halo=pow(saturate(1-d),2);
    float3 deep,mid,hot;AuraPalette(deep,mid,hot);
    float3 col=(lerp(mid,hot,.4)*halo*.8+float3(1,1,1)*core)*i.color.a*_Amount*saturate(_Opacity);
    return float4(col,0);
   }
   ENDCG
  }
 }
 Fallback Off
}
