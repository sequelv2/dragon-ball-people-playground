// Motes, rising streaks and twinkles around the aura. uv1.x picks the kind: 1 a soft mote with a halo,
// 2 a thin rising streak, 3 a four-point twinkle. The vertex colour is the display colour and opacity.
Shader "DBPU/Saiyan Blue Aura/Spark"
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
   #include "BlueAuraCommon.cginc"
   struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;float2 kind:TEXCOORD1;float4 color:COLOR;};
   struct v2f{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float kind:TEXCOORD1;float4 color:COLOR;};
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv*2-1;o.kind=v.kind.x;o.color=v.color;return o;}
   float4 frag(v2f i):SV_Target
   {
    float2 c=i.uv;
    float r2=dot(c,c),v;
    if(i.kind<1.5)v=exp(-r2*10)*1.6+exp(-r2*3)*.3;
    else if(i.kind<2.5)v=exp(-c.x*c.x*10)*pow(saturate(1-abs(c.y)),1.6)*1.4;
    else v=exp(-r2*16)*1.5+(exp(-abs(c.x)*20-c.y*c.y*2.5)+exp(-abs(c.y)*20-c.x*c.x*2.5))*.9;
    float3 e=-log(1-min(i.color.rgb,.985));
    float3 film=BaFilm(e*v);
    float cover=saturate(v)*i.color.a;
    return float4(BaOut(film,_Hdr)*cover,cover*.35)*saturate(_Opacity);
   }
   ENDCG
  }
 }
 Fallback Off
}
