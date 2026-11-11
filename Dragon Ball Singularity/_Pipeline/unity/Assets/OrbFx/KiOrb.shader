// A sphere of ki as light rather than surface: a saturated body and an overexposed core, a burning rim and a
// wide halo, added up as energy and tone-mapped at the end so brightness runs into white the way it does on
// film. Each technique turns on the layers that make it itself: strokes of light and thin arcs (Spirit Bomb),
// boiling fire with flares and tongues (the sun-like balls), silk veils and streams of energy wrapping round
// it (Gigantic Crusher). uv runs -1..1 with the limb at _Body.
Shader "DBPU/Ki Orb"
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
   float4 _Hot,_Mid,_Deep,_Rim;
   float4 _Move;    // xy travel direction, z how much the light streams back
   float4 _Contact; // xy direction of whatever is pressing on it, z how hard
   float4 _CoreAt;  // where the core sits on the sphere
   float _Clock,_Seed,_Body,_Reach,_Opacity,_Hdr,_Px,_Heat;
   float _Core;      // how blown out the core is
   float _CoreWidth; // how far the white reaches
   float _Level;     // brightness of the body
   float _Strokes;   // strokes of light running round it
   float _Lines;     // thin lines among them
   float _Rings;     // arcs around it
   float _Mottle;    // boiling fire
   float _Veil;      // silk veils, on it and trailing off it
   float _Wrap;      // streams of energy wrapping round it
   float _Flares;    // great flares leaving the limb
   float _Scale;     // how fine all that is
   float _Flow;      // how fast it moves along
   float _Twist;     // how fast it turns
   float _Corona,_Tongues;
   struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;};
   struct v2f{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv*2-1;return o;}

   // A hair-thin arc of a much larger circle, sweeping across the sphere and on past it.
   float Sweep(float2 p,float angle,float away,float radius,float width,float phase,float seed)
   {
    float2 q=p-float2(cos(angle),sin(angle))*away;
    float x=(length(q)-radius)/width;
    return exp(-x*x)*smoothstep(.42,.7,KiRound(atan2(q.y,q.x)/6.2832+.5+phase,5,seed,seed));
   }

   // Soft folds: space bent over itself a few times, then one long wave through it.
   float Silk(float3 p,float t)
   {
    p+=sin(p.zxy*1.7+t*float3(.31,.23,.37))*.5;
    p+=sin(p.yzx*2.3-t*float3(.19,.29,.17))*.35;
    p+=sin(p.zxy*3.1+t*float3(.13,.17,.11))*.2;
    return .5+.5*sin(p.x*1.9+p.y*2.3+p.z*1.6);
   }

   float4 frag(v2f i):SV_Target
   {
    float2 p=i.uv/_Body;
    float r=length(p);
    float2 dir=r>.0001?p/r:float2(1,0);
    float turn=atan2(p.y,p.x)/6.2832+.5;
    float3 mid=_Mid.rgb*_Mid.rgb,deep=_Deep.rgb*_Deep.rgb;
    float t=_Clock*_Flow,spin=_Clock*_Twist;
    float cover=saturate((1-r)/max(.012,_Px*2)+.5);
    float3 e=0;
    if(r<1.03)
    {
     float rr=min(r,.999),z=sqrt(1-rr*rr);
     float3 n=float3(dir*rr,z);
     // Evenly spaced over the surface of a sphere, so everything crowds towards the limb.
     float lat=asin(rr)*.6366;
     float3 body=lerp(deep*1.4,mid,smoothstep(0,.42,z))*_Level*(.66+.34*z);
     // Too small on screen for any of this to be more than shimmer.
     float detail=saturate(1.4-_Px*55);
     float ca=cos(spin*6.2832),sa=sin(spin*6.2832);
     float2 q=dir*rr/(1+z);
     q=float2(q.x*ca-q.y*sa,q.x*sa+q.y*ca);
     float2 s=q*_Scale+_Seed*.37;
     if(_Strokes>.001&&detail>0)
     {
      float wobble=(KiRound(turn,3,lat*2-t*.1,_Seed+2)-.5)*.14;
      float clump=smoothstep(.34,.66,KiRound(turn+spin*.4,4,lat*3+t*.12,_Seed+4));
      float big=smoothstep(.54,.78,KiRound(turn+spin+lat*.12+wobble,7,lat*_Scale*5+t*.5,_Seed));
      float mids=smoothstep(.59,.76,KiRound(turn+spin*1.3+lat*.16+wobble,13,lat*_Scale*13+t*.8,_Seed+7));
      float fine=smoothstep(.62,.74,KiRound(turn+spin*1.7+lat*.2+wobble,21,lat*_Scale*24+t*1.1,_Seed+9));
      float strokes=big*.6*(.5+.5*clump)+mids*(.25+.75*clump)+fine*.8*clump;
      float wave=1-abs(2*KiRound(turn+spin*.9+wobble,5,lat*_Scale*6+t*.3,_Seed+11)-1);
      float lines=pow(saturate(wave),90)*smoothstep(.46,.62,KiRound(turn-spin*.3,6,lat*2-t*.2,_Seed+13));
      body+=(_Hot.rgb*1.5+mid)*strokes*_Strokes*detail*(.4+.6*z);
      body+=_Hot.rgb*lines*_Lines*2.4*detail;
     }
     if(_Mottle>.001&&detail>0)
     {
      // Boiling fire: clean blotches and flecks of a cooler tone on a bright field.
      float2 warp=float2(KiNoise(s*.8+float2(t*.19,-t*.15)),KiNoise(s*.8+float2(-t*.13,t*.17)+5.2))-.5;
      float fire=KiNoise(s*5+warp*2.2+float2(t*.21,t*.07))*.45+KiNoise(s*11+warp*3+float2(-t*.33,t*.19)+9)*.55;
      float3 bright=(mid*1.3+_Hot.rgb*.18)*_Level;
      float3 dim=(mid*.55+deep*.5)*_Level;
      body=lerp(body,lerp(dim,bright,smoothstep(.41,.45,fire))*(.72+.28*z),_Mottle*detail);
     }
     if(_Veil>.001&&detail>0)
     {
      // Silk veils wrapped round it, one shell seen through another.
      float3 a=float3(n.x*ca-n.z*sa,n.y,n.x*sa+n.z*ca);
      float s1=Silk(a*_Scale+_Seed,t),s2=Silk(a.yzx*_Scale*2.1+_Seed*1.3,-t*.7);
      body*=.72+.5*s1;
      body+=(mid*1.6+_Rim.rgb*.5)*(pow(s1,3)*.8+pow(s2,5)*.6)*_Veil*detail;
     }
     if(_Wrap>.001&&detail>0)
     {
      // Energy wrapping round it: streams of light circling a tilted axis, one band brighter than the rest.
      float3 axis=normalize(float3(-.2+.06*sin(_Clock*.4),.72,.66));
      float cap=dot(n,axis);
      float3 u=normalize(cross(axis,float3(1,0,0))),v=cross(axis,u);
      float around=atan2(dot(n,v),dot(n,u))/6.2832+.5;
      float drift=t*.12+spin;
      float ribbon=cap+(KiRound(around-drift*.5,4,t*.1,_Seed+41)-.5)*.16;
      float x=(ribbon-.7)/.018;
      body+=_Rim.rgb*exp(-x*x)*(.45+.9*KiRound(around-drift,5,t*.15,_Seed+45))*_Wrap*2.4;
      float streams=KiRound(around-drift*1.6,11,ribbon*9-t*.2,_Seed+47)*.6+KiRound(around-drift*2.2,23,ribbon*21-t*.3,_Seed+49)*.4;
      float thread=1-abs(2*KiRound(around-drift*1.3,6,ribbon*7,_Seed+43)-1);
      body+=(mid*1.4+_Rim.rgb*.6)*smoothstep(.54,.72,streams)*_Wrap*.9;
      body+=_Rim.rgb*pow(saturate(thread),60)*_Wrap*1.6;
      body+=_Hot.rgb*smoothstep(.72,.98,cap)*.4*_Wrap;
     }
     float2 c=p-_CoreAt.xy;
     float cr=length(c);
     float q2=cr/(_CoreWidth*(.85+.3*KiRound(atan2(c.y,c.x)/6.2832+.5,27,t*.35,_Seed+15)));
     float press=saturate(dot(dir*rr,_Contact.xy))*_Contact.z;
     body+=_Hot.rgb*exp(-q2*q2)*_Core+mid*exp(-cr*cr*2.4)*_Level*.7;
     body+=_Hot.rgb*press*press*5;
     e+=body*cover;
    }
    // The rim burns on both sides of the limb, stronger in places and on one side.
    float sd=r-1,d=max(0,sd);
    float lean=1+dot(dir,-_Move.xy)*_Move.z*1.1,leaned=max(.4,lean);
    float o=saturate(d/max(.001,_Reach*lean)),out_=1-o;
    float side=saturate(dot(dir,float2(-.62,.78))*.5+.5);
    float vary=.45+.95*KiRound(turn+spin*.2,5,t*.12,_Seed+17);
    float w=max(.007+.01*side,_Px*1.2);
    float edgeGlow=sd<0?exp(sd*12):exp(-sd*26/leaned)*out_;
    e+=_Rim.rgb*_Rim.a*(exp(-sd*sd/(w*w))*vary*1.8+edgeGlow*.7);
    // The halo: bright colour close in, then deep colour a long way out, trailing behind it in flight.
    float outside=1-cover;
    e+=(mid*exp(-d*7/leaned)*out_*.9+deep*pow(out_,3)*.9)*_Corona*outside;
    float lift=_Clock*(.7+_Move.z*1.4);
    if(_Tongues>.001)
    {
     // Soft tongues of flame leaving the limb.
     float lick=KiRound(turn,19,o*.8-lift,_Seed)*.6+KiRound(turn,41,o*1.6-lift*1.6,_Seed+5)*.4;
     lick=smoothstep(.5,.66,lick+out_*.2-.08)*pow(out_,2)*_Tongues;
     e+=(mid*1.8+deep*.8)*lick*outside;
    }
    if(_Flares>.001)
    {
     // A few great flares, streaked along their length.
     float great=pow(saturate(KiRound(turn+spin*.2,4,_Clock*.1,_Seed+31)*1.5-.35),3);
     float hair=smoothstep(.45,.62,KiRound(turn,83,o*.3-lift*.5,_Seed+33));
     e+=(mid*2.4+_Hot.rgb*.5+deep*.2)*great*hair*pow(out_,1.3)*_Flares*2*outside;
    }
    if(_Veil>.001)
    {
     // The veils run off into a swirling tail, behind it in flight and low down at rest.
     float2 tail=_Move.z>.05?-_Move.xy:float2(-.6,-.8);
     float bias=max(0,dot(dir,tail));
     float o2=saturate(d/max(.001,_Reach*(.5+.5*bias))),out2=1-o2;
     float phi=turn-spin*.6+sd*.6;
     float wn=KiRound(phi,9,sd*3-t*.35,_Seed+51)*.6+KiRound(phi+.2,23,sd*7-t*.55,_Seed+53)*.25+KiRound(phi+.4,47,sd*13-t*.8,_Seed+55)*.15;
     float wisp=smoothstep(.46,.66,wn+out2*.16)*pow(out2,1.3);
     e+=(mid*1.5+deep*.9+_Rim.rgb*.15)*wisp*_Veil*(outside+cover*smoothstep(.62,1,r)*bias);
    }
    if(_Rings>.001)
    {
     // Hair-thin arcs close to the limb, and long ones sweeping past.
     float thin=max(_Px*1.2,.0032);
     float a1=smoothstep(.5,.7,KiRound(turn+spin*.35,3,t*.06,_Seed+21));
     float a2=smoothstep(.5,.7,KiRound(turn-spin*.5,4,t*.05,_Seed+23));
     float x1=(r-1.05)/(thin*1.4),x2=(r-1.115)/thin;
     float ring=exp(-x1*x1)*a1+exp(-x2*x2)*a2*.7;
     ring+=Sweep(p,2.2+_Clock*.05,1.1,1.5,thin*1.8,_Clock*.06,_Seed+25);
     ring+=Sweep(p,-.6-_Clock*.04,1.35,1.75,thin*1.4,-_Clock*.05,_Seed+27)*.8;
     e+=(_Hot.rgb*1.6+mid)*ring*_Rings*saturate(out_*3);
    }
    float3 rgb=KiFilm(e*(1+_Heat*3));
    float2 far_=abs(i.uv);
    float keep=cover+outside*smoothstep(1,.9,max(far_.x,far_.y));
    float alpha=(cover+outside*max(rgb.r,max(rgb.g,rgb.b))*.35)*keep;
    return float4(KiOut(rgb,_Hdr)*keep,alpha)*saturate(_Opacity);
   }
   ENDCG
  }
 }
 Fallback Off
}
