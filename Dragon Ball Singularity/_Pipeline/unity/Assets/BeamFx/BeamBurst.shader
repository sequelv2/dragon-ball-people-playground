// Every radial effect around a beam, one quad each (uv -1..1):
//  0 crown  - feathered spikes spraying round a white-hot core, biased along _Dir
//  1 rays   - long shafts of light, split between two colours across _Dir
//  2 flame  - a rising aura of tongues
//  3 orb    - a churning sphere of plasma
//  4 wash   - a soft sky tint split across _Dir (no noise, drawn huge)
//  5 arc    - a thin crescent shock of radius _Power, centred on _Dir
//  6 slash  - a tapered crescent stroke along an arc, for swats
Shader "DBPU/Beam Burst"
{
 Properties{_Hot("Hot",Color)=(1,1,1,1)_Mid("Mid",Color)=(.3,.6,1,1)_Deep("Deep",Color)=(.05,.2,.9,1)_Alt("Alt",Color)=(1,.8,.2,1)}
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
   float4 _Hot,_Mid,_Deep,_Alt,_Dir;
   float _Clock,_Seed,_Power,_Opacity,_Mode,_Spread,_Hdr;
   struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;};
   struct v2f{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv*2-1;return o;}

   // Value noise over the angle and time, with a whole number of cells so it closes around the circle.
   float Round(float th,float freq,float seed,float t)
   {
    float cells=max(ceil(freq*6.2832),1);
    float x=frac(th/6.2832+.5)*cells,i=floor(x),f=x-i;f=f*f*(3-2*f);
    float j=fmod(i+1,cells),s=seed*37.1;
    float ti=floor(t),tf=t-ti;tf=tf*tf*(3-2*tf);
    return lerp(lerp(BeamHash(float2(i+s,ti)),BeamHash(float2(j+s,ti)),f),lerp(BeamHash(float2(i+s,ti+1)),BeamHash(float2(j+s,ti+1)),f),tf);
   }

   float4 Crown(float2 p)
   {
    float r=length(p),t=_Clock*3.4,th=atan2(p.y,p.x);
    if(r>=1)return 0;
    float2 dirn=r>.0001?p/r:float2(1,0);
    float bias=dot(dirn,_Dir.xy);
    float spread=_Spread>0?smoothstep(0,1,saturate(.5+.5*(bias-(1-2*_Spread))/max(.001,_Spread))):1;
    float wob=th+(BeamNoise(float2(r*3.-t,_Seed))-.5)*.16;
    // Three sizes of blade: broad feathers, narrow ones and hair-thin streaks.
    float s1=pow(Round(wob,4.1,_Seed,t*.75),2.6);
    float s2=pow(Round(wob,10.3,_Seed+7.,t*1.15),3.4);
    float s3=pow(Round(wob,31.,_Seed+13.,t*1.9),5.);
    float gate=lerp(.3,1,spread)*saturate(_Power);
    float broad=(.2+s1*1.05)*gate,narrow=(.14+s2*1.15)*gate,hair=(.1+s3*1.25)*gate;
    float v1=smoothstep(broad,broad*.3,r)*smoothstep(.05,.3,s1+.1);
    float v2=smoothstep(narrow,narrow*.2,r)*smoothstep(.08,.34,s2);
    float v3=smoothstep(hair,hair*.12,r)*smoothstep(.1,.3,s3);
    float blade=saturate(max(v1,max(v2,v3)));
    float reach=max(broad,max(narrow,hair));
    float mid01=saturate(r/max(.03,reach));
    float3 tint=lerp(_Mid.rgb*2.,_Deep.rgb*1.5,smoothstep(.45,1,mid01));
    float3 col=lerp(_Hot.rgb*_Hdr,tint,smoothstep(.06,.46,mid01));
    float core=exp(-r*r/max(.0008,.012+.03*_Power));
    float halo=exp(-r*r*4.4)*.3*saturate(_Power);
    float3 c=col*blade+_Hot.rgb*_Hdr*core+_Mid.rgb*halo;
    float a=max(blade,max(saturate(core),halo*.45));
    return float4(c,a);
   }

float4 Rays(float2 p)
   {
    float r=length(p);if(r>=1)return 0;
    float th=atan2(p.y,p.x),t=_Clock;
    float2 dirn=r>.0001?p/r:float2(1,0);
    float r1=pow(Round(th,9.,_Seed,t*.55),5.);
    float r2=pow(Round(th,23.,_Seed+3.,t*.9+4.),7.);
    float r3=pow(Round(th,53.,_Seed+5.,t*1.4+9.),9.);
    float fall=pow(1-r,1.7);
    float inner=smoothstep(0,.05,r);
    float v=(r1*1.5+r2*1.1+r3*.8)*fall*inner;
    float side=smoothstep(-.14,.14,dot(dirn,_Dir.xy));
    float3 tint=lerp(_Mid.rgb,_Alt.rgb,side);
    float3 col=(tint*1.4+_Hot.rgb*.5*saturate(v*2-.5))*v*saturate(_Power);
    return float4(col,0);
   }

   float4 Flame(float2 p)
   {
    float t=_Clock*2.3;
    float2 q=p;q.x+=(BeamFbm(float2(p.y*2.2-t*1.1,_Seed))-.5)*.5*(p.y+1);
    float h=saturate((q.y+1)*.5);
    float wide=lerp(.72,.05,pow(h,.72));
    float tongues=BeamFbm(float2(q.x*5.+_Seed,q.y*1.3-t*2.1));
    float tall=BeamNoise(float2(q.x*7.+_Seed*1.7,t*.8));
    float body=smoothstep(wide*(.82+tongues*.55),wide*.28,abs(q.x));
    float top=smoothstep(.62+tall*.4,.3,h);
    // Fades in from the root: the fighter may be in mid-air, and nothing says the quad ends on the ground.
    float root=smoothstep(0,.24,h);
    float v=saturate(body*top*root*(.55+tongues*.95));
    float core=saturate(smoothstep(.52,.96,v))*smoothstep(1,.25,h);
    float3 col=lerp(_Deep.rgb*1.35,_Mid.rgb*1.9,saturate(v*1.5));
    col=lerp(col,_Hot.rgb*_Hdr,core);
    float a=smoothstep(.1,.55,v);
    return float4(col*a,a*.88)*saturate(_Power);
   }

   float4 Orb(float2 p)
   {
    float r=length(p);if(r>=1)return 0;
    float t=_Clock*2.6;
    float z=sqrt(saturate(1-r*r));
    float2 q=p/(1+z)*1.6;
    float n=BeamFbm(q*3.2+float2(t*.7,-t*.5)+_Seed);
    float limb=pow(1-z,2.2);
    float disc=smoothstep(1,.72+(n-.5)*.28,r);
    float3 col=lerp(_Hot.rgb*_Hdr*1.2,_Mid.rgb*2.2,limb*.8+(1-n)*.2);
    float glow=exp(-r*r*2.4)*.4;
    return float4(col*disc+_Mid.rgb*glow*(1-disc),max(disc,glow*.3))*saturate(_Power);
   }

   float4 Wash(float2 p)
   {
    float r=length(p);
    float fall=1-smoothstep(.15,1,r);
    float2 dirn=r>.0001?p/r:float2(1,0);
    float side=smoothstep(-.1,.1,dot(p,_Dir.xy)*3);
    float3 tint=lerp(_Mid.rgb,_Alt.rgb,side);
    float a=fall*saturate(_Power);
    return float4(tint*a,a);
   }

   float4 Arc(float2 p)
   {
    float r=length(p);if(r>=1)return 0;
    float2 dirn=r>.0001?p/r:float2(1,0);
    float R=saturate(_Power);
    float thick=.006+(1-R)*.012;
    float d=(r-R)/thick;
    float ring=exp(-d*d);
    float bias=saturate((dot(dirn,_Dir.xy)-(1-2*_Spread))/max(.001,_Spread*.65));
    float th=atan2(p.y,p.x);
    float gaps=smoothstep(.3,.5,BeamNoise(float2(th*4.+_Seed,_Seed)));
    float v=ring*smoothstep(0,1,bias)*gaps;
    return float4((_Hot.rgb*_Hdr*.6+_Mid.rgb)*v,v*.7);
   }

   float4 Slash(float2 p)
   {
    float r=length(p);if(r>=1)return 0;
    float2 dirn=r>.0001?p/r:float2(1,0);
    float bias=dot(dirn,_Dir.xy);
    float along=saturate((bias-(1-2*_Spread))/max(.001,2*_Spread));
    float thick=lerp(.02,.2,along*along);
    float d=(r-.82)/thick;
    float stroke=exp(-d*d)*smoothstep(0,.12,along);
    float grain=.72+.28*BeamNoise(float2(r*24.,atan2(p.y,p.x)*5.+_Seed));
    float v=saturate(stroke*grain*1.5)*saturate(_Power);
    return float4(lerp(_Mid.rgb*1.6,_Hot.rgb*_Hdr,saturate(v*1.4))*v,v);
   }

   float4 frag(v2f i):SV_Target
   {
    float4 c;
    int m=(int)(_Mode+.5);
    if(m==0)c=Crown(i.uv);
    else if(m==1)c=Rays(i.uv);
    else if(m==2)c=Flame(i.uv);
    else if(m==3)c=Orb(i.uv);
    else if(m==4)c=Wash(i.uv);
    else if(m==5)c=Arc(i.uv);
    else c=Slash(i.uv);
    return c*saturate(_Opacity);
   }
   ENDCG
  }
 }
 Fallback Off
}
