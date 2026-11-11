// One explosion on one quad (uv 0..1), played from a soft flipbook baked in Cycles and coloured for the
// technique: R fire, G lit smoke, B hot strands, A opacity. Premultiplied, stored as square roots. The fire is
// added up as energy, overexposed while it is young, with its light spilling past it into the air.
Shader "DBPU/Ki Burst"
{
 Properties{_Atlas("Atlas",2D)="black"{}}
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
   sampler2D _Atlas;
   float4 _Hot,_Mid,_Deep,_Smoke;
   float _Frame,_Age,_Opacity,_Hdr,_SmokeAmount;
   struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;};
   struct v2f{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
   float4 Cell(float2 uv,float frame)
   {
    float2 cell=float2(fmod(frame,8),7-floor(frame/8));
    float4 c=tex2D(_Atlas,(saturate(uv)*.992+.004+cell)/8);
    return c*c;
   }
   float4 frag(v2f i):SV_Target
   {
    float frame=clamp(_Frame,0,62.999);
    float f=floor(frame),k=frame-f;
    float4 s=lerp(Cell(i.uv,f),Cell(i.uv,f+1),k);
    float fire=s.r,smoke=s.g,strand=s.b,alpha=s.a;
    float young=1-smoothstep(0,.18,_Age);
    float3 mid=_Mid.rgb*_Mid.rgb,deep=_Deep.rgb*_Deep.rgb;
    float3 e=deep*fire*1.2+mid*pow(fire,1.6)*2.2+_Hot.rgb*(pow(fire,3.5)*(.8+young*6)+strand*.6);
    float2 c=i.uv*2-1;
    float r=length(c);
    e+=(mid*1.4+_Hot.rgb*.3)*pow(saturate(1-r),2.4)*((1-smoothstep(0,.4,_Age))*1.6+.08);
    float3 rgb=KiOut(KiFilm(e)+_Smoke.rgb*smoke*_SmokeAmount*1.5,_Hdr);
    // Anything still alive at the rim of the quad is faded so its square never shows.
    float2 q=abs(c);
    float fade=smoothstep(1,.9,max(q.x,q.y));
    float cover=lerp(saturate(fire*2.4+strand),alpha*.75,_SmokeAmount);
    return float4(rgb,max(cover,0))*fade*saturate(_Opacity);
   }
   ENDCG
  }
 }
 Fallback Off
}
