// The Sparking silhouette: a white-hot line with a gold fringe and a soft halo, read from a baked distance field.
// BlendOp Max joins the quads of all limbs into one outline, so overlaps at the joints add no hot spots.
// The noise uses world coordinates for the same reason: neighbouring limbs must boil together.
Shader "DBPU/Sparking Outline"
{
 Properties
 {
  [PerRendererData] _MainTex("Baked distance",2D)="white" {}
  [PerRendererData] _Range("Distance stored as 1 (px)",Float)=7
  [PerRendererData] _Fade("Fade",Float)=1
  [PerRendererData] _Burst("Ignition burst",Float)=0
  [PerRendererData] _Clock("Clock",Float)=0
  [PerRendererData] _Seed("Seed",Float)=0
  [PerRendererData] _Line("Line",Color)=(1,.99,.93,1)
  [PerRendererData] _Fringe("Fringe",Color)=(1,.84,.3,1)
  [PerRendererData] _Halo("Halo",Color)=(.95,.62,.14,1)
  _LinePx("Line width (px)",Float)=1.15
  _LineHeat("Line brightness",Float)=1.7
  _HaloPx("Halo falloff (px)",Float)=1.5
 }
 SubShader
 {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
  Cull Off ZWrite Off Lighting Off
  BlendOp Max
  Blend One One
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #include "UnityCG.cginc"
   sampler2D _MainTex;
   float _Range,_Fade,_Burst,_Clock,_Seed,_LinePx,_LineHeat,_HaloPx;
   float4 _Line,_Fringe,_Halo;
   struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };
   v2f vert(appdata_base v)
   {
    v2f o;
    o.pos=UnityObjectToClipPos(v.vertex);
    o.uv=v.texcoord.xy;
    o.world=mul(unity_ObjectToWorld,v.vertex).xy;
    return o;
   }
   float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float Noise(float2 p)
   {
    float2 c=floor(p),f=frac(p);f=f*f*(3-2*f);
    return lerp(lerp(Hash(c),Hash(c+float2(1,0)),f.x),lerp(Hash(c+float2(0,1)),Hash(c+float2(1,1)),f.x),f.y);
   }
   float4 frag(v2f i):SV_Target
   {
    float d=tex2D(_MainTex,i.uv).r*_Range;
    float t=_Clock;
    float2 w=i.world+_Seed;
    // Energy runs up the body: a slow swell and a fast flicker on top of it.
    float swell=Noise(w*float2(2.6,1.3)+float2(0,-t*1.7));
    float flicker=Noise(w*float2(8.5,4.5)+float2(t*.8,-t*5.1));
    float boil=swell*.62+flicker*.38;

    float width=_LinePx*(.84+.42*boil+.9*_Burst)*saturate(_Fade*1.6);
    float aa=max(fwidth(d)*.75,.03);
    float lineMask=1-smoothstep(width-aa,width+aa,d);
    float beyond=max(d-width,0);
    float fringe=exp(-beyond/(.42+.3*boil));
    float halo=exp(-beyond/(_HaloPx*(.72+.62*boil)*(1+1.1*_Burst)));

    float3 col=_Halo.rgb*halo*(.5+.34*boil)+_Fringe.rgb*fringe*.82;
    col=lerp(col,_Line.rgb*_LineHeat,lineMask);
    // The bake stops at _Range; this keeps the end of the halo from showing the quad.
    col*=saturate((_Range-d)/(_Range*.3))*_Fade;
    col*=1+.45*_Burst;
    return float4(col,1);
   }
   ENDCG
  }
 }
 Fallback Off
}
