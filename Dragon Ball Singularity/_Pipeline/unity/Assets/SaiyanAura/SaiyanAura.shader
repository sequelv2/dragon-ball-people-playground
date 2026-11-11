// The Super Saiyan aura: a column of light rising around the fighter, like the film's transformation shot.
// Smooth and continuous (runs at the game's frame rate): a near-white core behind the fighter, soft edges that
// boil and lick upward, light spreading over the ground at the feet. For SSJ2/3 the lightning of the
// reference GIF is drawn over it (_Bolts: distance to its jagged lines and their half thickness; _BoltPoly: the
// same lines as vectors, so they stay sharp up close). Positions are frame pixels of the lightning drawings
// (y down); uv runs over one frame.
//   _Mode 0: the column behind the fighter.  1: the flash over the fighter.  2: lightning.
Shader "DBPU/Saiyan Aura"
{
 Properties
 {
  _Bolts("Lightning drawings",2D)="white"{}
  _Opacity("Opacity",Float)=1
  _Hdr("Hdr",Float)=0
  _Mode("Mode",Float)=0
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
   #pragma target 3.5
   #include "UnityCG.cginc"
   #include "SaiyanCommon.cginc"
   sampler2D _Bolts;
   float4 _Bolts_TexelSize;
   float4 _BoltFrame;
   // _Shape: -, -, lightning range, lightning half thickness range (frame pixels)
   float4 _Shape;
   // _Hole.w: lightning line width scale
   float4 _Hole;
   // _FramePx: frame width and height in pixels
   float4 _FramePx;
   // _Pillar: centre x, half width, feet y, top y (frame pixels)
   float4 _Pillar;
   float4 _BoltPoly[256];
   float _BoltCount;
   float4 _Edge,_Core,_Electric;
   float _Mode,_Opacity,_Hdr,_Flash,_Clock,_Charge;
   struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;};
   struct v2f{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}

   float4 Cubic(float v)
   {
    float4 n=float4(1,2,3,4)-v,s=n*n*n;
    float x=s.x,y=s.y-4*s.x,z=s.z-4*s.y+6*s.x;
    return float4(x,y,z,6-x-y-z)/6;
   }
   float4 Bicubic(sampler2D tex,float2 uv,float4 texel)
   {
    float2 st=uv*texel.zw-.5,i=floor(st),f=st-i;
    float4 xc=Cubic(f.x),yc=Cubic(f.y);
    float4 c=i.xxyy+float2(-.5,1.5).xyxy;
    float4 s=float4(xc.xz+xc.yw,yc.xz+yc.yw);
    float4 o=(c+float4(xc.yw,yc.yw)/s)*texel.xxyy;
    float4 a=tex2D(tex,o.xz),b=tex2D(tex,o.yz),d=tex2D(tex,o.xw),e=tex2D(tex,o.yw);
    float sx=s.x/(s.x+s.y),sy=s.z/(s.z+s.w);
    return lerp(lerp(e,d,sx),lerp(b,a,sx),sy);
   }

   // Exact distance to the edge of the lightning strokes, negative inside.
   float StrokeDistance(float2 p)
   {
    int n=(int)(_BoltCount+.5);
    float best=1e9,wa=0;
    float2 a=0;
    [loop]for(int k=0;k<n;k++)
    {
     float4 v=_BoltPoly[k];
     if(v.w>.5)
     {
      float2 ab=v.xy-a,ap=p-a;
      float h=saturate(dot(ap,ab)/max(dot(ab,ab),1e-6));
      best=min(best,length(ap-ab*h)-max(.9,lerp(wa,v.z,h))*_Hole.w);
     }
     a=v.xy;wa=v.z;
    }
    return best;
   }

   // Smoky, not grainy: two broad octaves.
   float Fbm(float2 p){return SNoise(p)*.68+SNoise(p*2.13+4.7)*.32;}

   // The column: x in half widths from the centre line, h from the feet (0) to the top (1).
   // Returns body coverage in x and the boiling detail in y.
   float2 Column(float2 px)
   {
    float t=_Clock;
    float h=(_Pillar.z-px.y)/(_Pillar.z-_Pillar.w);
    float dx=(px.x-_Pillar.x)/_Pillar.y;
    float side=dx<0?-1:1;
    // The edges boil and lick upward, each side on its own, faster when charging.
    float lick=Fbm(float2(side*3.1+dx*.8,px.y*.016+t*(1.3+_Charge*.8)));
    float width=(1+.22*(lick-.5)*2+.18*_Charge)*(1+.35*smoothstep(.12,-.05,h));
    float r=abs(dx)/width;
    float body=1-smoothstep(.3,1.05,r);
    // Long streaks of light rising through it.
    float streak=Fbm(float2(dx*4.5,px.y*.011+t*(2.1+_Charge)));
    float ground=smoothstep(-.06,.02,h);
    // Full height to the fighter's head, then the light fades out just above it.
    float top=1-smoothstep(.78,1,h+.1*(lick-.5));
    return float2(body*ground*top,streak);
   }

   float4 frag(v2f i):SV_Target
   {
    float2 px=float2(i.uv.x*_FramePx.x,(1-i.uv.y)*_FramePx.y);
    if(_Mode>1.5)
    {
     float4 t=Bicubic(_Bolts,_BoltFrame.xy+i.uv*_BoltFrame.zw,_Bolts_TexelSize);
     float b=t.r*_Shape.z,w=max(.9,t.g*_Shape.w)*_Hole.w;
     float sd=b-w;
     if(b<7&&_BoltCount>1.5)sd=lerp(StrokeDistance(px),sd,smoothstep(5,7,b));
     float aa=fwidth(sd)*.75+1e-4;
     float core=smoothstep(aa,-aa,sd);
     float glow=saturate(exp(-max(sd,0)/2.4)*1.05+exp(-max(sd+w,0)/7.5)*.4)*(1-core)*smoothstep(_Shape.z,_Shape.z*.6,b);
     float alpha=core+glow*.78;
     // Saturated blue pushed through the game's ACES curve turns violet, so the glow goes in as linear light.
     float3 electric=_Hdr<.5?_Electric.rgb:pow(_Electric.rgb,2.2)*(_Hdr>1?1.8:1);
     float3 rgb=SOut(float3(1,1,1),_Hdr)*core+electric*glow*(1-core);
     return float4(rgb,alpha)*_Opacity;
    }
    float2 c=Column(px);
    float dx=(px.x-_Pillar.x)/_Pillar.y;
    if(_Mode<.5)
    {
     float core=exp(-dx*dx*2.6);
     float3 col=lerp(_Edge.rgb,_Core.rgb,smoothstep(0,1,saturate(core*1.1+(c.y-.5)*.6)));
     float alpha=saturate(c.x*(1.05+.35*(c.y-.5)));
     // A wide soft haze of the same light around the column.
     float h=(_Pillar.z-px.y)/(_Pillar.z-_Pillar.w);
     float haze=exp(-abs(dx)*1.6)*.22*smoothstep(-.06,.05,h)*(1-smoothstep(.7,1,h))*(1-c.x);
     // The game's curve pushes saturated yellow towards orange; the film's light is a pale lemon.
     if(_Hdr>.5)col=lerp(dot(col,float3(.3,.6,.1)),col,.7);
     col*=_Core.a;
     float3 rgb=SOut(col,_Hdr)*alpha+SOut(_Edge.rgb*haze,_Hdr)*(1-alpha);
     return float4(rgb,alpha)*_Opacity;
    }
    // Over the fighter, only the flash when the aura ignites or the form changes.
    float alpha=saturate(_Flash)*c.x*.8;
    return float4(SOut(_Core.rgb,_Hdr)*alpha,alpha)*_Opacity;
   }
   ENDCG
  }
 }
 Fallback Off
}
