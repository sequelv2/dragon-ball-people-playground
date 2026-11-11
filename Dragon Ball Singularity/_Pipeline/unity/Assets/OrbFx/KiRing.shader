// A pressure front on one quad (uv -1..1): a soft bright line, stronger in places, with a faint wake of lit
// air behind it.
Shader "DBPU/Ki Ring"
{
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
   #include "UnityCG.cginc"
   #include "KiCommon.cginc"
   float4 _Hot,_Mid;
   float _Radius,_Thick,_Dome,_Opacity,_Seed,_Hdr;
   struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;};
   struct v2f{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv*2-1;return o;}
   float4 frag(v2f i):SV_Target
   {
    float r=length(i.uv);
    float turn=atan2(i.uv.y,i.uv.x)/6.2832+.5;
    float vary=.45+KiRound(turn,7,_Seed,_Seed);
    float d=(r-_Radius)/max(_Thick,.0005);
    float front=exp(-d*d)*vary;
    float wake=exp(min(d,0)*.14)*smoothstep(1.5,-.5,d)*_Dome;
    float3 mid=_Mid.rgb*_Mid.rgb;
    float fade=smoothstep(1,.93,r);
    float glow=exp(-abs(d)*.22)*vary*.18;
    float3 rgb=KiOut(KiFilm(_Hot.rgb*front*1.6+mid*(front*1.6+glow*1.4+wake*.8)),_Hdr)*fade;
    return float4(rgb,saturate(front*.4+glow*.2+wake*.1)*fade)*saturate(_Opacity);
   }
   ENDCG
  }
 }
 Fallback Off
}
