// Marks a beam leaves in the ground, both clipped to the surface they were cut into.
//  _Mode 0: a furrow strip. uv.x is arc length, uv.y runs 0 at the surface to 1 at the bottom of
//           the gouge. Vertex colour r holds that stretch's heat.
//  _Mode 1: a scorch quad (uv -1..1). _SurfaceY clips everything above the ground line.
Shader "DBPU/Beam Ground"
{
 Properties{_Glow("Glow",Color)=(1,.55,.2,1)}
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
   #include "BeamFxCommon.cginc"
   float4 _Glow;
   float _Mode,_Heat,_Opacity,_SurfaceY,_Seed,_Hdr;
   struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct v2f{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;float worldY:TEXCOORD1;};
   v2f vert(appdata v)
   {
    v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;
    o.worldY=mul(unity_ObjectToWorld,v.vertex).y;
    return o;
   }
   float4 Furrow(v2f i)
   {
    float x=i.uv.x,y=i.uv.y;
    float jag=.62+(BeamFbm(float2(x*2.6+_Seed,_Seed))-.5)*.72;
    float shape=smoothstep(jag,jag-.12,y);
    float heat=saturate(i.color.r);
    float ridge=1-abs(2*BeamFbm(float2(x*3.1+_Seed,y*3.4))-1);
    float crack=smoothstep(.88-heat*.14,.97,ridge);
    float3 rock=lerp(float3(.03,.024,.021),float3(.008,.007,.007),y*1.2);
    float3 col=rock+_Glow.rgb*_Hdr*crack*heat*1.6;
    col+=_Glow.rgb*heat*heat*smoothstep(.2,0,y)*.9;
    return float4(col*shape,shape);
   }
   float4 Scorch(v2f i)
   {
    if(i.worldY>_SurfaceY)return 0;
    float2 p=i.uv*2-1;
    float r=length(p);if(r>=1)return 0;
    float th=atan2(p.y,p.x);
    float n=BeamFbm(float2(th*2.3+_Seed,r*3.2));
    float shape=smoothstep(1,.5+(n-.5)*.3,r);
    float ridge=1-abs(2*BeamFbm(float2(th*5.1+_Seed*2.,r*4.))-1);
    float crack=smoothstep(.9,.98,ridge)*smoothstep(1,.3,r);
    float heat=saturate(_Heat);
    float3 col=lerp(float3(.008,.007,.007),float3(.03,.024,.02),smoothstep(.2,1,r));
    col+=_Glow.rgb*_Hdr*(crack*heat*1.7+exp(-r*r*7.)*heat*1.3);
    return float4(col*shape,shape);
   }
   float4 frag(v2f i):SV_Target
   {
    float4 c=_Mode<.5?Furrow(i):Scorch(i);
    return c*saturate(_Opacity)*i.color.a;
   }
   ENDCG
  }
 }
 Fallback Off
}
