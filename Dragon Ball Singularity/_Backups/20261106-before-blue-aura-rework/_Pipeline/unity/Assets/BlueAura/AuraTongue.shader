// A single flame tongue carried by a stretched particle: it rises, licks past the outline of the
// aura and is eaten away by noise as it dies. Particle alpha is its life, 1 meaning fully formed.
Shader "DBPU/Blue Aura/Tongue"
{
 Properties
 {
  _Noise("Noise",2D)="gray"{}
  _Opacity("Opacity",Float)=1
  _Bands("Bands: deep>mid, mid>hot",Vector)=(.3,.62,0,0)
  _Rim("Rim",Float)=.7
  _Alpha("Alpha",Float)=.96
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
   float4 _Bands;
   float _Rim,_Alpha;
   struct appdata{float4 vertex:POSITION;float3 uv:TEXCOORD0;float4 color:COLOR;};
   struct v2f{float4 pos:SV_POSITION;float3 uv:TEXCOORD0;float4 color:COLOR;};
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
   float4 frag(v2f i):SV_Target
   {
    float x=i.uv.x*2-1;
    // A stretched particle maps v=0 to its leading end, so y=0 is the root and y=1 the tip.
    float y=1-i.uv.y;
    float seed=i.uv.z*37;
    float bend=sin(y*3.3+seed)*.26*y;
    float w=pow(saturate(1-y),.72)*smoothstep(0,.2,y);
    float d=1-abs(x-bend)/max(w,.001);
    float n=AuraSample(float2(i.uv.x*.3+seed*.1,i.uv.y*.2-frac(AuraClock()*.9)),2);
    float f=d-(1-i.color.a)*(.5+.7*n)-(n-.5)*.16;
    float aa=max(fwidth(f),.012);
    float mass=smoothstep(0,aa*1.5,f);
    float3 deep,mid,hot;AuraPalette(deep,mid,hot);
    float3 col=lerp(deep,mid,smoothstep(_Bands.x-.06,_Bands.x+.08,f));
    col=lerp(col,hot,smoothstep(_Bands.y-.06,_Bands.y+.1,f));
    col=lerp(col,lerp(mid,hot,.7),(1-smoothstep(aa*1.5,aa*1.5+.09,f))*_Rim);
    float alpha=mass*_Alpha;
    float op=saturate(_Opacity);
    return float4(col*alpha*op,alpha*op);
   }
   ENDCG
  }
 }
 Fallback Off
}
