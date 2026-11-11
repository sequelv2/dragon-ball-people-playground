// A column of plasma drawn on a ribbon that follows the beam's spline. uv.x is arc length in world
// units and uv.y runs -1..1 across, so the white core, the streaming flame fringe, the helical
// bands and the cracks all stay locked to the energy as it flows round a bend. Vertex colour: r heat
// (contact glow), a opacity along the length.
Shader "DBPU/Beam Ribbon"
{
 Properties{_Hot("Hot",Color)=(1,1,1,1)_Mid("Mid",Color)=(.3,.6,1,1)_Deep("Deep",Color)=(.05,.2,.9,1)}
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
   float4 _Hot,_Mid,_Deep;
   float _Clock,_BeamWidth,_Strain,_Flow,_Seed,_Helix,_Spiral,_CoreRadius,_Opacity,_Hdr;
   struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct v2f{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
   float4 frag(v2f i):SV_Target
   {
    float w=max(_BeamWidth,.04);
    float x=i.uv.x/w,y=i.uv.y,a=abs(y),t=_Clock,flow=t*_Flow;
    float strain=saturate(_Strain);
    float n1=BeamFbm(float2(x*.55-flow*.55,y*1.3+_Seed));
    float n2=BeamFbm(float2(x*1.7-flow*1.25,y*2.7-_Seed*1.3));
    float n3=BeamNoise(float2(x*5.2-flow*2.6,y*6.1));

    float radius=_CoreRadius;
    // A straight, even column: the edge only breathes a little, so the beam never looks like it trembles.
    float edge=radius*(1+(n1-.5)*.05+(n2-.5)*.03+strain*.14);
    float body=smoothstep(edge+.03,edge-.03,a);

    float coreEdge=edge*(.74+(n2-.5)*.06);
    float core=smoothstep(coreEdge,coreEdge*.55,a);
    float3 hot=_Hot.rgb*_Hdr;

    // Flame licks streaming back along the column, torn into separate tongues.
    float reach=1-radius;
    float out01=saturate((a-edge)/max(.001,reach));
    float tongue=BeamFbm(float2(x*1.05-flow*1.65,a*3.1+(y>0?3.7:-5.2)+_Seed));
    float lick=saturate(pow(1-out01,1.7)*smoothstep(.36,.7,tongue+(1-out01)*.42-strain*.1));
    float hair=smoothstep(.72,.9,BeamNoise(float2(x*2.6-flow*3.2,y*17.+_Seed)))*pow(1-out01,3)*.7;

    // Two thin bands winding round the column, as seen on the front of a cylinder.
    float s=clamp(y/max(edge,.001),-1,1);
    float th=asin(s);
    float phase=x*.72-flow*.62;
    float bandA=smoothstep(.962,.992,sin(6.2832*phase+th*1.65));
    float bandB=smoothstep(.975,.996,sin(6.2832*phase*1.0-th*1.65+2.4));
    float helix=max(bandA,bandB*.7)*body*_Helix;

    // Dark filaments cracking through the white core, racing with the flow.
    float ridge=1-abs(2*BeamFbm(float2(x*1.5-flow*.95,y*2.5+t*1.7))-1);
    float crack=smoothstep(.965,.995,ridge)*core*(.3+strain*.4);

    // The strand pair of a corkscrew beam (Special Beam Cannon).
    float strand=0;
    if(_Spiral>.001)
    {
     float sp=x*.16-flow*.55;
     float y1=sin(6.2832*sp)*.82,y2=sin(6.2832*sp+3.1416)*.82;
     float d1=(y-y1)/.075,d2=(y-y2)/.075;
     strand=max(exp(-d1*d1),exp(-d2*d2))*_Spiral;
    }

    float3 fringe=lerp(_Deep.rgb*1.3,_Mid.rgb*1.8,saturate((1-out01)*1.2));
    float halo=saturate((exp(-a*a*3.4)-.033)/.967)*(.32+strain*.26);
    float3 col=_Mid.rgb*halo*1.15;
    float alpha=halo*.28;
    col+=fringe*lick;alpha=max(alpha,lick*.82);
    col+=_Hot.rgb*hair;alpha=max(alpha,hair);

    float3 inner=lerp(_Mid.rgb*2.1,hot,core);
    inner+=_Hot.rgb*(n3-.5)*.24;
    inner=lerp(inner,_Mid.rgb*1.35,helix*.72);
    inner=lerp(inner,_Mid.rgb*1.3,crack*.75);
    col=lerp(col,inner,body);alpha=max(alpha,body);

    if(_Spiral>.001){col=lerp(col,_Mid.rgb*2.6+_Hot.rgb*.3,strand);alpha=max(alpha,strand);}

    float heat=saturate(i.color.r);
    col=lerp(col,hot*max(alpha,.2),heat*.7);

    float fade=i.color.a*saturate(_Opacity);
    return float4(col*fade,alpha*fade);
   }
   ENDCG
  }
 }
 Fallback Off
}
