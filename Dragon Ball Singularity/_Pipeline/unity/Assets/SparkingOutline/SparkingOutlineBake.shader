// Runs once per sprite: writes the distance from each pixel to the nearest opaque texel. Measuring to the
// texel's box, not its centre, keeps the stair steps of the pixel art, with corners rounded by the distance.
Shader "Hidden/DBPU/Sparking Outline Bake"
{
 Properties { _MainTex("Sprite",2D)="white" {} }
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
   sampler2D _MainTex;float4 _MainTex_TexelSize;
   float4 _SpriteRect;  // uv min, uv max of the sprite in its texture
   float4 _Extended;    // uv min, uv max of the padded quad
   float _Search;       // search radius in source texels
   float _RangeTexels;  // the distance stored as 1, in source texels
   float4 frag(v2f_img i):SV_Target
   {
    float2 ts=abs(_MainTex_TexelSize.xy);
    float2 uv=lerp(_Extended.xy,_Extended.zw,i.uv);
    float2 cell=floor(uv/ts);
    float best=_RangeTexels;
    int r=(int)_Search;
    [loop]for(int y=-r;y<=r;y++)
    {
     [loop]for(int x=-r;x<=r;x++)
     {
      float2 c=(cell+float2(x,y)+.5)*ts;
      if(c.x<_SpriteRect.x||c.y<_SpriteRect.y||c.x>_SpriteRect.z||c.y>_SpriteRect.w)continue;
      float gap=length(max(abs(uv-c)/ts-.5,0));
      if(gap>=best)continue;
      if(tex2Dlod(_MainTex,float4(c,0,0)).a>.4)best=gap;
     }
    }
    return saturate(best/_RangeTexels);
   }
   ENDCG
  }
 }
 Fallback Off
}
