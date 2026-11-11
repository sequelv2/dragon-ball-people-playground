// SSJ2/3 lightning in the style of the user's first reference GIF: bold white-hot lines with hard right-angled
// kinks and spikes, a pale blue glow, a soft blur round it, and spark dots. SaiyanLightningBolts redraws the
// paths on every flash (60 ms, like the GIF) and SSJ1AuraController passes them here as points, in world units
// from the quad's centre: _Pts[i] = (x, y, half thickness, kind) with kind 0 = start of a line, 1 = continue
// it, 2 = a spark (half thickness = its radius). Distances are exact, so the lines stay sharp at any zoom.
// Over the fighter's body (limb capsules, as for the aura) the lightning thins so it never hides the fighter.
Shader "DBPU/Saiyan Lightning"
{
    Properties
    {
        _Core ("White-hot core", Color) = (1, 1, 1, 1)
        _Electric ("Blue glow", Color) = (0.55, 0.8, 1, 1)
        _Hdr ("Game camera (0 preview, 2.8 game)", Float) = 0
        _Opacity ("Opacity", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" }
        Cull Off Lighting Off ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            #include "SaiyanCommon.cginc"

            float4 _Pts[96];
            float _Count;
            float4 _QuadHalf;
            // _Look: glow width, blur width (world units), brightness of this flash
            float4 _Look;
            uniform float4 _LimbCapsules[14];
            uniform float _LimbRadii[14];
            float4 _Core, _Electric;
            float _Hdr, _Opacity;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }

            float4 frag(v2f i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * 2.0 * _QuadHalf.xy;
                // Distance to the edge of the strokes (they taper between their corners) and to the sparks.
                float line_ = 1e3, spark = 1e3;
                float2 a = _Pts[0].xy;
                float wa = _Pts[0].z;
                [loop] for (int k = 0; k < 96; k++)
                {
                    if (k >= _Count) break;
                    float4 v = _Pts[k];
                    if (v.w > 1.5) spark = min(spark, length(p - v.xy) - v.z);
                    else if (v.w > 0.5)
                    {
                        float2 ab = v.xy - a, ap = p - a;
                        float h = saturate(dot(ap, ab) / max(dot(ab, ab), 1e-7));
                        line_ = min(line_, length(ap - ab * h) - lerp(wa, v.z, h));
                    }
                    a = v.xy; wa = v.z;
                }
                float d = min(line_, spark);
                float aa = fwidth(d) * 0.8 + 1e-5;
                // A slightly soft edge, then the blue glow and a wide blur.
                float core = smoothstep(aa * 1.5, -aa, d);
                float dd = max(d, 0.0);
                float glow = (exp(-dd / _Look.x) * 0.95 + exp(-dd / _Look.y) * 0.35) * (1.0 - core);

                // Thin over the body.
                float body = 10.0;
                [unroll] for (int l = 0; l < 14; l++)
                {
                    float2 la = _LimbCapsules[l].xy, ba = _LimbCapsules[l].zw - la, pa = p - la;
                    float hh = saturate(dot(pa, ba) / max(dot(ba, ba), 0.0001));
                    body = min(body, length(pa - ba * hh) - _LimbRadii[l]);
                }
                float keep = lerp(0.25, 1.0, smoothstep(-0.03, 0.08, body)) * _Look.z;
                core *= keep; glow *= keep;

                float alpha = saturate(core + glow * 0.7);
                // The white core runs into the bloom; the blue glow goes in as linear light, since saturated blue
                // pushed through the tonemapping compensation turns violet.
                float3 electric = _Hdr < 0.5 ? _Electric.rgb : pow(_Electric.rgb, 2.2) * (_Hdr > 1 ? 1.8 : 1);
                float3 rgb = SOut(_Core.rgb, _Hdr) * core + electric * saturate(glow);
                return float4(rgb, alpha) * _Opacity;
            }
            ENDCG
        }
    }
    Fallback Off
}
