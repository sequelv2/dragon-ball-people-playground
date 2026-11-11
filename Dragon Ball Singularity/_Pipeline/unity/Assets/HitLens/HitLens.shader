// Thin gravitational lenses in one fullscreen pass: each pixel samples the scene at
// theta - thetaE^2 * direction / |theta|, so space bends around the blow and a small inverted
// image forms inside the Einstein radius. Offsets are in screen heights, so lenses stay round.
Shader "Hidden/DBPU/Hit Lens"
{
 Properties { _MainTex("Scene",2D)="white" {} }
 SubShader
 {
  Cull Off ZWrite Off ZTest Always
  Pass
  {
   CGPROGRAM
   #pragma vertex vert_img
   #pragma fragment frag
   #pragma target 4.0
   #include "UnityCG.cginc"
   #define MAX_LENS 8
   sampler2D _MainTex;float4 _MainTex_TexelSize;
   int _LensCount;
   float4 _Lens[MAX_LENS];     // xy viewport centre, z Einstein radius, w reach
   float4 _LensB[MAX_LENS];    // x dispersion, y ring brightness
   float4 _LensTint[MAX_LENS];
   // Samples may land off screen; mirroring hides the frame edge better than clamping.
   float2 Mirror(float2 uv){return 1-abs(1-abs(uv)%2);}
   float4 frag(v2f_img i):SV_Target
   {
    float aspect=abs(_MainTex_TexelSize.z/_MainTex_TexelSize.w);
    float2 ac=float2(aspect,1);
    float2 offset=0;float chroma=0;float3 add=0;
    [loop]for(int l=0;l<_LensCount;l++)
    {
     float4 L=_Lens[l],B=_LensB[l];
     float2 d=(i.uv-L.xy)*ac;float r=length(d);
     float reach=max(.001,L.w);
     if(r>=reach)continue;
     float soft=max(r,.0008);
     float taper=1-smoothstep(reach*.45,reach,r);
     float alpha=min(L.z*L.z/soft,reach)*taper;
     offset-=d/soft*alpha;
     chroma+=alpha*B.x;
     if(B.y>0)add+=_LensTint[l].rgb*B.y*exp(-pow((r-L.z)/max(.0016,L.z*.05),2));
    }
    float2 uv=i.uv+offset/ac;
    float3 col=tex2D(_MainTex,Mirror(uv)).rgb;
    chroma=saturate(chroma*6);
    if(chroma>.002)
    {
     float2 spread=offset/ac*chroma*.09;
     col.r=tex2D(_MainTex,Mirror(uv+spread)).r;
     col.b=tex2D(_MainTex,Mirror(uv-spread)).b;
    }
    return float4(col+add,1);
   }
   ENDCG
  }
 }
 Fallback Off
}
