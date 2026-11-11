// The user's SSJ1 aura (from SSJ1_Aura_PeoplePlayground_VFX.zip, 2026-11-07), reworked for the game on the
// user's request: an egg of light round the fighter (from just under the feet to well above the head, narrowing
// to the top), flames licking upward at its edge, a blinding white core hugging the body, warm amber at the rim,
// rising sparks and a flare on the ground; colours as in the user's preview, pushed through the game's
// tonemapping compensation (SOut) so they stay saturated and the core runs into the bloom.
// Positions: SSJ1AuraController passes everything in world units relative to the quad's centre.
Shader "PeoplePlayground/VFX/SSJ1Aura"
{
    Properties
    {
        [HDR] _CoreColor ("Inner Core Color (White-Hot)", Color) = (1.0, 0.99, 0.86, 1.0)
        [HDR] _MidColor ("Middle Flame Color (SSJ Gold)", Color) = (1.0, 0.91, 0.22, 1.0)
        [HDR] _OuterColor ("Outer Corona Color (deep yellow, warmer than the gold)", Color) = (1.0, 0.8, 0.1, 1.0)
        _FlameSpeed ("Upward Flow Speed", Range(0.5, 8.0)) = 3.2
        _PulseSpeed ("Ki Pulse Frequency", Range(0.0, 15.0)) = 8.5
        _PulseAmount ("Ki Pulse Amplitude", Range(0.0, 0.35)) = 0.09
        _Turbulence ("Flame Turbulence & Detail", Range(0.5, 4.0)) = 2.1
        _Hdr ("Game camera (0 preview, 2.8 game)", Float) = 0
        _Opacity ("Opacity", Float) = 1
        _Charge ("Charge", Float) = 0
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
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "SaiyanCommon.cginc"

            struct appdata_t { float4 vertex : POSITION; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };

            float4 _CoreColor, _MidColor, _OuterColor;
            float _FlameSpeed, _PulseSpeed, _PulseAmount, _Turbulence, _Hdr, _Opacity, _Charge;

            // The ragdoll's limbs: xy start, zw end, world units from the quad centre; radii in world units.
            uniform float4 _LimbCapsules[14];
            uniform float _LimbRadii[14];
            // The egg: centre x, centre y, half width, height above the centre; _Egg2: height below the centre,
            // quad half width, quad half height, height of the soles.
            uniform float4 _Egg, _Egg2;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.uv = IN.texcoord;
                return OUT;
            }

            float2 hash22(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return -1.0 + 2.0 * frac(sin(p) * 43758.5453123);
            }

            float noise2D(float2 p)
            {
                float2 i = floor(p), f = frac(p), u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(dot(hash22(i), f), dot(hash22(i + float2(1, 0)), f - float2(1, 0)), u.x),
                            lerp(dot(hash22(i + float2(0, 1)), f - float2(0, 1)), dot(hash22(i + float2(1, 1)), f - float2(1, 1)), u.x), u.y);
            }

            // Ridged flame noise: sharp upward-licking tongues.
            float animeFlameFBM(float2 p, float t)
            {
                float value = 0.0, amp = 0.55;
                float2x2 rot = float2x2(0.80, 0.60, -0.60, 0.80);
                for (int i = 0; i < 4; i++)
                {
                    float n = noise2D(p + float2(0.0, -t * (1.0 + i * 0.45)));
                    float ridge = 1.0 - abs(n);
                    ridge *= ridge;
                    value += amp * lerp(n * 0.5 + 0.5, ridge, 0.72);
                    p = mul(rot, p) * 2.05 + 100.0;
                    amp *= 0.5;
                }
                return value;
            }

            float sdCapsule(float2 p, float2 a, float2 b, float r)
            {
                float2 pa = p - a, ba = b - a;
                float h = saturate(dot(pa, ba) / max(dot(ba, ba), 0.0001));
                return length(pa - ba * h) - r;
            }

            float smin(float a, float b, float k)
            {
                float h = max(k - abs(a - b), 0.0) / k;
                return min(a, b) - h * h * k * 0.25;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float t = _Time.y * _FlameSpeed * (1.0 + _Charge * 0.5);
                float2 p = (IN.uv - 0.5) * 2.0 * _Egg2.yz;

                // Distance to the body.
                float body = 10.0;
                [unroll] for (int i = 0; i < 14; i++)
                    body = smin(body, sdCapsule(p, _LimbCapsules[i].xy, _LimbCapsules[i].zw, _LimbRadii[i]), 0.12);

                // The egg, narrowing to the top.
                float2 q = p - _Egg.xy;
                float up = q.y > 0.0 ? q.y / _Egg.w : q.y / _Egg2.x;
                float taper = 1.0 + 0.6 * saturate(up) * saturate(up);
                float r = length(float2(q.x * taper / _Egg.z, up));

                // Flames: noise in a frame stretched upward, scrolling up, warped sideways.
                float2 flameUV = p * float2(2.6, 1.15);
                float warp = noise2D(flameUV * 1.35 + float2(0.0, -t * 0.45));
                flameUV.x += warp * 0.34 * _Turbulence * 0.5;
                flameUV.y += abs(warp) * 0.18;
                float fbm = animeFlameFBM(flameUV * (_Turbulence * 0.55), t * 0.35);
                float detail = animeFlameFBM(flameUV * (_Turbulence * 1.15) + 17.3, t * 0.5);

                float pulse = sin(_Time.y * _PulseSpeed - r * 6.0) * _PulseAmount;
                // Longer tongues towards the top, short ones at the feet.
                float lick = (fbm - 0.45) * (0.32 + 0.3 * saturate(up * 0.6 + 0.4)) + (detail - 0.5) * 0.12;
                float field = r * (1.0 - pulse * 0.5) - lick;

                float inside = smoothstep(1.03, 0.9, field);
                // Amber at the rim, gold through the body of the flame.
                float3 col = lerp(_OuterColor.rgb, _MidColor.rgb, smoothstep(1.0, 0.82, field));

                // The white-hot core: hugging the body, and a column of light behind it.
                float column = exp(-pow(q.x / (0.34 * _Egg.z * (1.0 + 0.3 * _Charge)), 2.0)) * smoothstep(-1.05, -0.55, up) * (1.0 - smoothstep(0.35, 0.95, up));
                float sheath = exp(-max(body, 0.0) / 0.2);
                float core = saturate(sheath * 0.95 + column * (0.65 + 0.35 * detail)) * inside;
                col = lerp(col, _CoreColor.rgb, core);

                // Bright streaks of ki rising through the flame.
                float streaks = smoothstep(0.62, 0.86, detail) * inside * (1.0 - core) * 0.55;
                col = lerp(col, _CoreColor.rgb, streaks);

                // Sparks rising inside the egg.
                float2 grid = p * float2(9.0, 6.0) - float2(0.0, _Time.y * 2.2);
                float2 id = floor(grid), f = frac(grid) - 0.5;
                float2 rnd = hash22(id);
                float spark = smoothstep(0.1, 0.02, length(f - rnd * 0.35)) * step(0.55, rnd.x) * smoothstep(1.05, 0.6, r);

                float alpha = inside * lerp(0.7, 1.0, smoothstep(1.0, 0.7, field));
                alpha = saturate(alpha + spark);
                col = lerp(col, _CoreColor.rgb, spark);

                // The flare where the column meets the ground, and a warm halo just outside the egg.
                float flare = exp(-abs(p.y - _Egg2.w) * 9.0) * exp(-abs(q.x) * 2.4) * (0.55 + 0.4 * _Charge);
                // A wide golden glow round the egg (amber only right at its edge), like the film's light.
                float d = max(field - 1.0, 0.0);
                float halo = (exp(-d * 8.0) * 0.32 + exp(-d * 2.6) * 0.2) * (1.0 - inside) * (1.0 + _Charge * 0.5);
                float3 haloCol = lerp(_MidColor.rgb, _OuterColor.rgb, exp(-d * 8.0) * 0.5);
                // A little under full brightness, so the bloom glows round it without swamping it.
                col *= 0.93;
                float3 rgb = SOut(col, _Hdr) * alpha + SOut(saturate(haloCol * halo + _MidColor.rgb * flare * 0.75), _Hdr) * (1.0 - alpha);

                // Fade out towards the quad's borders so nothing ends in a straight cut.
                float border = smoothstep(0.0, 0.1, IN.uv.x) * smoothstep(1.0, 0.9, IN.uv.x) * smoothstep(0.0, 0.06, IN.uv.y) * smoothstep(1.0, 0.92, IN.uv.y);
                return float4(rgb, alpha) * border * _Opacity;
            }
            ENDCG
        }
    }
}
