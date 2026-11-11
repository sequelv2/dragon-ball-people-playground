// Particles of the energy inside the Super Saiyan aura (the "saiyanaura_core" prefab): soft rising streaks of ki
// (_Kind 0, drawn on stretched billboards) and small bright sparks (_Kind 1). Vertex colour carries the tint and
// the fade over each particle's life.
Shader "DBPU/Saiyan Aura Particle"
{
 Properties
 {
  _Kind("Kind",Float)=0
  _Hdr("Hdr",Float)=0
  _Opacity("Opacity",Float)=1
 }
 SubShader
 {
  Tags{"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane"}
  Cull Off ZWrite Off ZTest Always Lighting Off Fog{Mode Off}
  Blend One OneMinusSrcAlpha
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #include "UnityCG.cginc"
   #include "SaiyanCommon.cginc"
   float _Kind,_Hdr,_Opacity;
   struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct v2f{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
   float4 frag(v2f i):SV_Target
   {
    float2 p=i.uv*2-1;
    float body,hot;
    if(_Kind<.5)
    {
     // A round soft blob: the stretched billboard draws it out into a streak along the motion, whichever
     // way the quad's uv runs.
     float d=saturate(1-length(p));
     body=d*sqrt(d);
     hot=body*body*body;
    }
    else
    {
     float d=length(p);
     body=exp(-d*d*5);
     hot=exp(-d*d*28);
    }
    float a=saturate(body*.8+hot*.2)*i.color.a*_Opacity;
    float3 col=lerp(i.color.rgb,float3(1,1,.95),hot*.7);
    // Added light, with only a touch of cover so the streaks do not grey a bright background.
    return float4(SOut(col,_Hdr)*a,a*.15);
   }
   ENDCG
  }
 }
 Fallback Off
}
