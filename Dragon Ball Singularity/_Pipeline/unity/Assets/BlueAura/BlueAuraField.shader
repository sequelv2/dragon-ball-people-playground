// The Super Saiyan Blue aura as soft light: a bulb of flame standing on the fighter's feet, made of broad
// columns of light streaming upwards, white flanks round a cyan column, lavender lobes licking out of the
// sides and tall tongues with sharp tips along the crown, all added up as energy so it overexposes into
// white and blooms in the game. Kaioken over Blue draws a second, smaller white-blue shell inside a red one,
// with a magenta glow between them.
// _Mode: 1 behind the fighter, 2 the thin light over the fighter, 3 the shock ring and flash.
Shader "DBPU/Saiyan Blue Aura/Field"
{
 Properties { _Mode("Mode",Float)=1 }
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
   // Palette as energies: a display colour c is sent as -ln(1-c), so colours add up like light.
   float4 _Rim,_Fringe,_Body,_Hot,_Column,_Tip,_Glow;
   float4 _InRim,_InFringe,_InBody,_InHot,_InColumn,_InTip,_InGlow;
   float4 _Shape;  // x half width, y height, z bottom (H units, feet at 0), w taper of the crown
   float4 _Motion; // x rise speed, y tongue length, z lobe size, w column width
   float4 _Inner;  // x height, y width (fractions of the outer shell), z strength (0: none), w its speed
   float4 _Look;   // x body opacity, y front opacity, z glow, w streak contrast
   float4 _Lean;   // x sideways trail of the crown (H units), y lift, z streaming
   float _Clock,_Seed,_Charge,_Fury,_Flash,_Mode,_Burst,_Gain;
   struct appdata{float4 vertex:POSITION;};
   struct v2f{float4 pos:SV_POSITION;float2 local:TEXCOORD0;};
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.local=v.vertex.xy;return o;}

   struct Shell{float3 e;float mass;float s;float glow;float streak;float crown;};

   Shell Flame(float2 p,float halfW,float height,float bottom,float taper,float rise,float seed,float tongues,float lobes,float columnW,
               float3 rim,float3 fringe,float3 body,float3 hot,float3 column,float3 tip,float contrast)
   {
    Shell o;
    float h=(p.y-bottom)/height;
    float hl=saturate(h);
    // The crown trails the fighter's motion while the root stays planted.
    p.x-=_Lean.x*hl*hl;
    float yy=h*2-1,ry=height*.5;
    // A slow drift of the whole mass from side to side.
    p.x+=(BaNoise(float2(rise*.21,seed*1.9))-.5)*.05*hl;
    float hw=halfW*max(.3,1-taper*yy);
    float xx=p.x/hw;
    float d=sqrt(xx*xx+yy*yy);
    // Signed distance to the surface in H units (positive inside): the bulb's value over its gradient.
    float2 dir=float2(xx,yy)/max(d,.0001);
    float s=(1-d)/max(.0001,length(dir/float2(hw,ry)));
    float crown=smoothstep(.42,1.02,h);
    // Broad lobes that swell and climb.
    float lob=BaFbm(float2(xx*1.3+seed,h*1.2-rise*.6));
    // Tall tongues with sharp tips: ridges of noise stretched upwards, strongest along the crown.
    float warp=(BaNoise(float2(h*1.7-rise*.4,seed*3.1+rise*.13))-.5)*.35;
    float tn=BaNoise(float2((xx+warp*hl)*1.7+seed*1.73,h*.66-rise*.95));
    float tn2=BaNoise(float2((xx-warp*hl)*3.1+seed*.61+3.1,h*1.15-rise*1.32));
    float ridge=pow(saturate(1-abs(tn*2-1)),2.4)*.75+pow(saturate(1-abs(tn2*2-1)),3.2)*.5;
    float disp=(lob-.5)*lobes*(.5+.5*smoothstep(-.1,.7,h))+(ridge-.3)*tongues*(.12+.88*crown);
    // Small flames licking out of the flanks.
    float flank=smoothstep(.5,1,abs(xx))*smoothstep(.05,.45,h)*(1-crown);
    float lick=BaNoise(float2(h*3.1-rise*1.55,xx*.6+seed*3.3));
    disp+=pow(saturate(lick*1.7-.75),1.6)*lobes*1.7*flank;
    s+=disp;
    float soft=.022+.05*crown;
    o.mass=smoothstep(-soft,soft,s)*smoothstep(-.06,.04,h);
    // Broad columns of light streaming up, alternating bright and coloured, with finer strands inside them.
    float cx=xx+warp*h*.6;
    float broad=BaFbm(float2(cx*1.6+seed*1.3,h*.38-rise*.55));
    float fine=BaNoise(float2(cx*4.4+seed*.7,h*.9-rise*1.0));
    float streak=smoothstep(.5-.16/contrast,.5+.16/contrast,broad*.62+fine*.38);
    // The cyan column down the middle.
    float colm=exp(-cx*cx/(columnW*columnW))*(.8+.9*(BaNoise(float2(cx*2.2+seed,h*.55-rise*.65))-.5));
    colm=saturate(colm)*smoothstep(.0,.1,s);
    // Lavender lobes on the flanks, widest low down.
    float fr=smoothstep(.2,.85,abs(xx))*(1-crown*.7)*(1-smoothstep(.02,.38,s))*(.6+.8*lob);
    // The crown and its tongues burn a deeper colour; the outline deeper still.
    float tp=crown*(1-smoothstep(0,.16,s));
    float rm=1-smoothstep(0,.05,s);
    float3 c=body;
    c=lerp(c,hot,streak*(1-colm*.75));
    c=lerp(c,column,colm);
    c=lerp(c,fringe,saturate(fr));
    c=lerp(c,tip,saturate(tp)*.85);
    c=lerp(c,rim,rm*.85);
    o.e=c;o.s=s;o.streak=streak;o.crown=crown;
    o.glow=exp(-max(0,-s)*8.5)*smoothstep(-.25,.1,h);
    return o;
   }

   float4 frag(v2f i):SV_Target
   {
    float2 p=BaH(i.local);
    float energy=saturate(_Charge*.75+_Fury);
    float rise=_Clock*_Motion.x*(1+energy*.5);
    float halfW=_Shape.x*(1+_Charge*.3+_Fury*.16);
    float height=_Shape.y*(1+_Charge*.2+_Fury*.12);
    float bottom=_Shape.z;
    float gain=_Gain*(1+.05*sin(_Clock*19.+_Seed)+.035*sin(_Clock*31.7+_Seed*2.)+energy*.35+_Flash*1.3);
    float op=saturate(_Opacity);

    if(_Mode>2.5)
    {
     // A shock ring and a flash when the charge starts or the form changes.
     float age=saturate(_Burst);
     float2 c=p-float2(0,bottom+height*.42);
     float2 q=c/float2(halfW*1.1,height*.56);
     float r=length(q);
     float ang=atan2(q.y,q.x);
     float R=.72+age*1.25;
     float wid=.05+.16*age;
     float ring=exp(-(r-R)*(r-R)/(wid*wid))*pow(1-age,1.6)*(.55+.9*BaNoise(float2(ang*2.2+_Seed,age*3)));
     float flash=exp(-r*r*2.2)*pow(1-age,3);
     float3 e=_Column.rgb*ring*2.2+_Hot.rgb*(ring*.8+flash*1.6);
     float3 film=BaFilm(e);
     float a=saturate(max(film.r,max(film.g,film.b)));
     return float4(BaOut(film,_Hdr)*a,a*.35)*op;
    }

    Shell o=Flame(p,halfW,height,bottom,_Shape.w,rise,_Seed,_Motion.y*(1+energy*.45),_Motion.z,_Motion.w,
                  _Rim.rgb,_Fringe.rgb,_Body.rgb,_Hot.rgb,_Column.rgb,_Tip.rgb,_Look.w);
    bool inner=_Inner.z>.001;
    Shell n=o;
    if(inner)
     n=Flame(p,halfW*_Inner.y,height*_Inner.x,bottom,_Shape.w*.7,rise*_Inner.w,_Seed+5.3,_Motion.y*.6,_Motion.z*.8,_Motion.w*1.25,
             _InRim.rgb,_InFringe.rgb,_InBody.rgb,_InHot.rgb,_InColumn.rgb,_InTip.rgb,_Look.w);

    if(_Mode<1.5)
    {
     float a=o.mass*_Look.x;
     float3 rgb=BaOut(BaFilm(o.e*gain),_Hdr)*a;
     // Near glow round the flame, and a wide faint halo: light added over whatever is behind.
     float yy=(p.y-bottom)/height*2-1;
     float hw=halfW*max(.3,1-_Shape.w*yy);
     float far_=sqrt(p.x*p.x/(hw*hw)+yy*yy);
     float halo=exp(-max(0,far_-1)*2.6)*.35;
     float g=saturate((o.glow+halo)*(1-o.mass))*_Look.z*(1+energy*.6+_Flash);
     rgb+=BaOut(BaFilm(_Glow.rgb*(1+energy*.5)),_Hdr)*g;
     float alpha=a+g*.18;
     if(inner)
     {
      float na=n.mass*_Inner.z;
      // The glow round the inner shell lights the outer flame between them, mostly above it. It is added as
      // light in the camera's own space, which in the game turns the red flame rose-magenta: the user chose
      // that look on 2026-11-06 ("leave it as it is"), so keep this mix.
      float ng=saturate(exp(-max(0,-n.s)*7.)*(1-n.mass))*_Inner.z*(.35+.65*n.crown);
      rgb+=BaOut(BaFilm(_InGlow.rgb*(1+energy*.5)),_Hdr)*ng*(.4+.6*o.mass);
      rgb=BaOut(BaFilm(n.e*gain),_Hdr)*na+rgb*(1-na);
      alpha=na+alpha*(1-na);
     }
     return float4(rgb,saturate(alpha))*op;
    }

    // The front: a thin sheet of light over the fighter, so the fighter stands inside the aura.
    Shell f=o;
    if(inner)f=n;
    float a=f.mass*_Look.y*(.3+.7*f.streak)*(1-.5*f.crown);
    float3 film=BaFilm(f.e*gain*.9);
    return float4(BaOut(film,_Hdr)*a,a*.45)*op;
   }
   ENDCG
  }
 }
 Fallback Off
}
