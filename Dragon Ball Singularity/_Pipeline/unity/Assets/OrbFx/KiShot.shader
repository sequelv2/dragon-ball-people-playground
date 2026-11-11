// Every small ki blast in the world drawn in one batch: an overexposed head in a soft glow of its colour and a
// comet tail fading away along its flight. uv is measured in head radii from the centre of the head, with +x
// pointing the way it flies. Vertex colour is the tint and opacity; uv2 holds its seed and tail length.
Shader "DBPU/Ki Shot"
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
   float _Hdr;
   struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;float2 uv2:TEXCOORD1;float4 color:COLOR;};
   struct v2f{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float2 data:TEXCOORD1;float4 color:COLOR;};
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.data=v.uv2;o.color=v.color;return o;}
   float4 frag(v2f i):SV_Target
   {
    float seed=i.data.x,len=max(i.data.y,.001);
    float2 p=i.uv;
    float d=length(p),d2=d*d;
    float pulse=.92+.08*sin(_Time.y*17+seed*9);
    float core=exp(-d2*3.2/pulse);
    float body=exp(-d2*.85/pulse)*saturate(1-d/1.7);
    float along=saturate(-p.x/len);
    float width=lerp(.62,.06,pow(along,.7));
    float side=p.y/width;
    float tail=exp(-side*side*1.5)*pow(1-along,1.7)*smoothstep(.4,-.4,p.x);
    float3 tint=i.color.rgb*i.color.rgb;
    float3 rgb=KiOut(KiFilm(tint*(body*2.2+tail*2.4)+float3(1,1,1)*(core*3.4+tail*tail*.9)),_Hdr);
    return float4(rgb,saturate(body*.9+tail*.75))*i.color.a;
   }
   ENDCG
  }
 }
 Fallback Off
}
