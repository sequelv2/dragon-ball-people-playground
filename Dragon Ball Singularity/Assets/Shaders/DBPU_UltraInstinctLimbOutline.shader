// Per-limb UI / MUI outline — wavy displacement (UV warp + mild vertex wobble), not just alpha pulse.
Shader "DBPU/UltraInstinctLimbOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline", Color) = (1, 1, 1, 1)
        _OutlinePx ("Outline thickness (texels)", Range(0.5, 8)) = 4
        _RipplePhase ("Ripple", Float) = 0
        _WaveStrength ("Wave (texels)", Range(0.5, 8)) = 3.2
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "CanUseSpriteAtlas"="True"
            "PreviewType"="Plane"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _OutlineColor;
            float _OutlinePx;
            float _RipplePhase;
            float _WaveStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            float2 WaveDisp(float2 uv, float t, float salt)
            {
                float2 ts = max(abs(_MainTex_TexelSize.xy), float2(1e-6, 1e-6));
                uv += float2(salt * 0.17, salt * 0.23);
                float k = 42.0;
                float a = _WaveStrength;
                float2 w;
                w.x = sin(t * 2.65 + uv.y * k + sin(uv.x * k * 0.38 + t * 1.35) * 2.2);
                w.y = cos(t * 2.28 + uv.x * k * 0.9 + sin(uv.y * k * 0.42 + t * 1.55) * 1.9);
                w += 0.55 * float2(
                    cos(t * 3.1 + dot(uv, float2(k * 0.55, k * 0.48)) + salt),
                    sin(t * 2.85 + dot(uv, float2(k * 0.52, k * 0.58)) + salt * 1.1)
                );
                return w * ts * a;
            }

            float EdgeAlpha(float2 uvRoot, float2 off, float phase, float salt)
            {
                return tex2D(_MainTex, uvRoot + off + WaveDisp(uvRoot + off * 0.35, phase + salt, salt)).a;
            }

            v2f vert(appdata v)
            {
                v2f o;
                float t = _RipplePhase;
                float2 wb = float2(
                    sin(t * 2.15 + v.uv.x * 22 + v.uv.y * 15),
                    cos(t * 1.98 + v.uv.y * 19 + v.uv.x * 14)
                ) * 0.0075;
                v.vertex.xy += wb;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _RipplePhase;
                float2 px = _MainTex_TexelSize.xy * _OutlinePx;
                float2 px2 = px * 1.6;

                float2 dIn = WaveDisp(i.uv, t, 0) * 0.32;
                fixed a = tex2D(_MainTex, i.uv + dIn).a * i.color.a;

                float mx = 0;
                mx = max(mx, EdgeAlpha(i.uv, float2(px.x, 0), t, 1.1));
                mx = max(mx, EdgeAlpha(i.uv, float2(-px.x, 0), t, 1.9));
                mx = max(mx, EdgeAlpha(i.uv, float2(0, px.y), t, 2.2));
                mx = max(mx, EdgeAlpha(i.uv, float2(0, -px.y), t, 2.8));
                mx = max(mx, EdgeAlpha(i.uv, float2(px.x, px.y), t, 3.3));
                mx = max(mx, EdgeAlpha(i.uv, float2(-px.x, px.y), t, 3.9));
                mx = max(mx, EdgeAlpha(i.uv, float2(px.x, -px.y), t, 4.4));
                mx = max(mx, EdgeAlpha(i.uv, float2(-px.x, -px.y), t, 5.0));
                mx = max(mx, EdgeAlpha(i.uv, float2(px2.x, 0), t, 5.6));
                mx = max(mx, EdgeAlpha(i.uv, float2(-px2.x, 0), t, 6.1));
                mx = max(mx, EdgeAlpha(i.uv, float2(0, px2.y), t, 6.7));
                mx = max(mx, EdgeAlpha(i.uv, float2(0, -px2.y), t, 7.2));
                mx = max(mx, EdgeAlpha(i.uv, float2(px2.x, px2.y), t, 7.8));
                mx = max(mx, EdgeAlpha(i.uv, float2(-px2.x, px2.y), t, 8.3));
                mx = max(mx, EdgeAlpha(i.uv, float2(px2.x, -px2.y), t, 8.9));
                mx = max(mx, EdgeAlpha(i.uv, float2(-px2.x, -px2.y), t, 9.4));

                if (mx > 0.08 && a < 0.12)
                {
                    float warp = length(WaveDisp(i.uv, t, 3.3) / max(_MainTex_TexelSize.x, 1e-6));
                    float shimmer = 0.62 + 0.38 * sin(t * 3.4 + i.uv.x * 36 + i.uv.y * 29 + warp * 0.8);
                    fixed4 o = _OutlineColor;
                    o.a *= shimmer * mx * i.color.a;
                    return o;
                }
                return fixed4(0,0,0,0);
            }
            ENDCG
        }
    }
    FallBack Off
}
