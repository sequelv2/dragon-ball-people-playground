// Licks of flame that leave the surface of the aura and burn out as they rise. The rig builds one strip per
// lick: uv.x runs across it, uv.y from the root (0) to the tip (1); colour carries r: seed, g: age 0..1,
// a: opacity. The palette comes from the same energies as the field.
Shader "DBPU/Saiyan Blue Aura/Tongue"
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
   #include "BlueAuraCommon.cginc"
   float4 _Rim,_Body,_Hot,_Tip;
   float _Clock,_Gain;
   struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct v2f{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
   float4 frag(v2f i):SV_Target
   {
    float across=i.uv.x*2-1,v=i.uv.y,seed=i.color.r*97,age=i.color.g;
    // A round root and a sharp tip.
    float w=pow(saturate(v*3.2),.6)*pow(saturate((1-v)/.8),1.25);
    float n=BaNoise(float2(across*1.4+seed,v*2.2-_Clock*2.6+seed*.37));
    w*=.75+.5*n;
    float inside=1-abs(across)/max(w,.001);
    // Soft all over: it grows out of the flame behind it and thins into light.
    float mass=smoothstep(0,.85,inside)*smoothstep(0,.3,v);
    // It burns out from the tip down as it ages.
    float burn=BaNoise(float2(across*2.1+seed*1.7,v*3.4-_Clock*1.9));
    mass*=smoothstep(age*1.05-.3,age*1.05+.2,(1-v)*.75+burn*.35);
    float3 e=lerp(_Body.rgb,_Hot.rgb,smoothstep(.35,.95,inside)*(1-v*.7)*.45);
    e=lerp(e,_Tip.rgb,smoothstep(.35,1,v)*.8);
    e=lerp(e,_Rim.rgb,(1-smoothstep(0,.35,inside))*.7);
    float a=mass*i.color.a;
    return float4(BaOut(BaFilm(e*_Gain),_Hdr)*a,a*.8)*saturate(_Opacity);
   }
   ENDCG
  }
 }
 Fallback Off
}
