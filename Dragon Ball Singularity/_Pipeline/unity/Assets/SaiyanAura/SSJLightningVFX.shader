Shader "PeoplePlayground/VFX/SSJLightning"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(SSJ2 and SSJ3 Bio Electricity Colors)]
        [HDR] _BoltCoreColor ("Bolt Core Color (White-Hot)", Color) = (0.96, 0.99, 1.0, 1.0)
        [HDR] _BoltGlowColor ("Bolt Outer Glow (DBZ Cyan-Blue)", Color) = (0.18, 0.72, 1.0, 1.0)
        [HDR] _LimbFlashColor ("Body Electric Flash Color", Color) = (0.55, 0.88, 1.0, 1.0)

        [Header(Lightning Arcs (60 FPS Procedural))]
        _SSJLevel ("SSJ Level (2 = SSJ2, 3 = SSJ3)", Range(1.0, 3.0)) = 2.0
        _BoltCount ("Active Lightning Channels", Range(1.0, 8.0)) = 6.0
        _SnapFrequency ("Arc Teleport / Snap Rate (Hz)", Range(4.0, 24.0)) = 13.5
        _Jaggedness ("Zig-Zag Amplitude", Range(0.2, 2.5)) = 1.25
        _BoltThickness ("Bolt Core Thickness", Range(0.001, 0.012)) = 0.0038
        _GlowRadius ("Electric Corona Width", Range(0.008, 0.06)) = 0.026
        _BranchIntensity ("Forked Sub-Branches Intensity", Range(0.0, 1.5)) = 1.0
        _AuraShellRadius ("Aura Shell Distance", Range(0.1, 0.55)) = 0.30
        _Hdr ("Game camera (0 preview, 2.8 game)", Float) = 0
        _Opacity ("Opacity", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+1"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            #include "SaiyanCommon.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 uv       : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;

            float4 _BoltCoreColor;
            float4 _BoltGlowColor;
            float4 _LimbFlashColor;

            float _SSJLevel;
            float _BoltCount;
            float _SnapFrequency;
            float _Jaggedness;
            float _BoltThickness;
            float _GlowRadius;
            float _BranchIntensity;
            float _AuraShellRadius;
            // Added for the game: its camera value (0 preview, 2.8 game) and the aura's fade.
            float _Hdr, _Opacity;

            // Added for the game: the fighter's 14 limbs (xy start, zw end, radius), world units from the quad's
            // centre; the quad's half size; and world units per unit of the original quad (its thickness, glow
            // and zig-zag sizes were tuned in uv of a 2.6 wide quad round a 2.3 tall fighter).
            uniform float4 _LimbCapsules[14];
            uniform float _LimbRadii[14];
            float4 _QuadHalf;
            float _UnitScale;
            // The aura's egg, as SSJ1Aura.shader gets it: centre x, centre y, half width, height above the centre;
            // _Egg2.x height below the centre.
            float4 _Egg, _Egg2;

            // A point of the egg from normalised coordinates (unit circle), narrowing to the top like the aura.
            float2 EggPoint(float2 e)
            {
                float y = e.y > 0.0 ? e.y * _Egg.w : e.y * _Egg2.x;
                float up = saturate(e.y);
                return _Egg.xy + float2(e.x * _Egg.z / (1.0 + 0.6 * up * up), y);
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.uv = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            float hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float2 hash21(float p)
            {
                float3 p3 = frac(float3(p, p, p) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            // Piecewise-linear 1D noise (creates sharp anime zig-zag corners instead of smooth waves)
            float jaggedNoise1D(float x, float seed)
            {
                float i = floor(x);
                float f = frac(x);
                float a = hash11(i + seed * 19.19) * 2.0 - 1.0;
                float b = hash11(i + 1.0 + seed * 19.19) * 2.0 - 1.0;
                return lerp(a, b, f);
            }

            // Evaluates a single jagged anime lightning arc + forked sub-branch
            // Returns float2(coreMask, glowMask)
            float2 evalLightningArc(float2 p, float2 a, float2 b, float seed, float lifePhase, float thickness, float glowRad, float jagScale)
            {
                float2 ba = b - a;
                float len = max(length(ba), 0.001);
                float2 dir = ba / len;
                float2 nrm = float2(-dir.y, dir.x);

                float2 pa = p - a;
                float h = dot(pa, dir) / len;

                // Progressive 60 FPS strike propagation along the bolt
                float strikeHead = saturate(lifePhase * 3.2);
                if (h < -0.05 || h > strikeHead + 0.05)
                    return float2(0.0, 0.0);

                float hc = saturate(h);
                float envelope = sin(hc * 3.14159265);

                // Multi-frequency piecewise-linear zig-zag displacement
                float zig1 = jaggedNoise1D(hc * 6.5, seed);
                float zig2 = jaggedNoise1D(hc * 19.0, seed + 7.3) * 0.38;
                float zig3 = jaggedNoise1D(hc * 41.0, seed + 15.1) * 0.14;
                float disp = (zig1 + zig2 + zig3) * 0.042 * jagScale * envelope;

                float perpDist = abs(dot(pa, nrm) - disp);
                // Taper ends at 0 and strikeHead
                float endPenalty = max(max(0.0, -h), max(0.0, h - strikeHead)) * len;
                float dMain = sqrt(perpDist * perpDist + endPenalty * endPenalty);

                // Forked secondary branch splitting off the main arc
                float dBranch = 10.0;
                if (_BranchIntensity > 0.01)
                {
                    float branchH = 0.28 + 0.35 * hash11(seed + 3.1);
                    if (strikeHead > branchH)
                    {
                        float branchDisp = (jaggedNoise1D(branchH * 6.5, seed) +
                                            jaggedNoise1D(branchH * 19.0, seed + 7.3) * 0.38) *
                                            0.042 * jagScale * sin(branchH * 3.14159265);
                        float2 branchOrigin = a + dir * (branchH * len) + nrm * branchDisp;
                        float angleSign = (hash11(seed + 9.7) > 0.5) ? 1.0 : -1.0;
                        float2 branchDir = normalize(dir * 0.72 + nrm * (0.68 * angleSign));
                        float2 branchNrm = float2(-branchDir.y, branchDir.x);
                        float branchLen = len * 0.48;

                        float2 pb = p - branchOrigin;
                        float bh = dot(pb, branchDir) / branchLen;
                        float bhc = saturate(bh);
                        float bZig = jaggedNoise1D(bhc * 9.0, seed + 29.4) * 0.022 * jagScale * sin(bhc * 3.14159265);
                        float bPerp = abs(dot(pb, branchNrm) - bZig);
                        float bEnd = max(max(0.0, -bh), max(0.0, bh - 1.0)) * branchLen;
                        dBranch = sqrt(bPerp * bPerp + bEnd * bEnd) / max(_BranchIntensity, 0.1);
                    }
                }

                // Double-strobe anime flicker during the bolt's lifetime
                float strobe = smoothstep(1.0, 0.65, lifePhase) * (0.65 + 0.35 * cos(lifePhase * 38.0));

                // Sharp stepped core + exponential plasma corona
                // Softer edges than the original (the game version is blurred a little).
                float coreMain = smoothstep(thickness * 2.1, thickness * 0.2, dMain);
                float coreFork = smoothstep(thickness * 1.4, thickness * 0.12, dBranch) * 0.85;
                float core = saturate(max(coreMain, coreFork)) * strobe;

                float glowMain = exp(-dMain / max(glowRad, 0.002));
                float glowFork = exp(-dBranch / max(glowRad * 0.65, 0.002)) * 0.65;
                // A wide soft haze round each arc, like a camera blur of the light.
                float haze = exp(-dMain / max(glowRad * 3.2, 0.002)) * 0.32 + exp(-dBranch / max(glowRad * 2.2, 0.002)) * 0.18;
                float glow = saturate(glowMain + glowFork + haze) * strobe;

                return float2(core, glow);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // World units from the quad's centre (the original worked in uv round the quad's centre).
                float2 p = (IN.uv - 0.5) * 2.0 * _QuadHalf.xy;
                float k = max(_UnitScale, 0.01);
                float t = _Time.y;

                // Scale arc density & aggressiveness between SSJ2 and SSJ3
                float ssj3Factor = saturate(_SSJLevel - 2.0);
                int totalBolts = (int)clamp(_BoltCount + ssj3Factor * 2.0, 1.0, 8.0);
                float snapRate = _SnapFrequency * (1.0 + ssj3Factor * 0.25);

                float totalCore = 0.0;
                float totalGlow = 0.0;

                // Evaluate independent 60 FPS bio-electric arc channels around the aura shell
                for (int i = 0; i < 8; i++)
                {
                    if (i >= totalBolts) break;

                    float fi = (float)i;
                    float phaseTime = t * snapRate + fi * 1.6180339;
                    float snapSeed = floor(phaseTime) + fi * 37.0;
                    float lifePhase = frac(phaseTime);

                    // Duty cycle: each channel fires intermittently (denser in SSJ3)
                    float dutyThreshold = lerp(0.32, 0.12, ssj3Factor);
                    if (hash11(snapSeed * 1.13) < dutyThreshold)
                        continue;

                    // Spawn the arc anywhere in the aura (changed for the game; the original ran it round a fixed
                    // shell in the quad): it starts at a random point of the egg, which follows the fighter, and
                    // crackles off in any direction, leaning upward, without leaving the egg.
                    float2 rndA = hash21(snapSeed + 1.0);
                    float2 rndB = hash21(snapSeed + 2.0);

                    float startAngle = rndA.x * 6.2831853;
                    float2 eA = float2(cos(startAngle), sin(startAngle)) * sqrt(rndA.y) * 0.9;
                    float2 posA = EggPoint(eA);
                    float heading = (rndB.x - 0.5) * 3.6 + 1.5707963;
                    float2 posB = posA + float2(cos(heading), sin(heading)) * (0.12 + 0.16 * rndB.y) * k;
                    // Pull the far end back inside the egg if it would leave it.
                    float2 qB = posB - _Egg.xy;
                    float upB = qB.y > 0.0 ? qB.y / _Egg.w : qB.y / _Egg2.x;
                    float rB = length(float2(qB.x * (1.0 + 0.6 * saturate(upB) * saturate(upB)) / _Egg.z, upB));
                    if (rB > 0.95) posB = _Egg.xy + qB * (0.95 / rB);

                    // Drift upward at 60 FPS while active
                    float2 upwardDrift = float2(0.0, lifePhase * 0.045 * k);
                    posA += upwardDrift;
                    posB += upwardDrift;

                    float2 arc = evalLightningArc(
                        p, posA, posB, snapSeed, lifePhase,
                        // Bolder than the original, so the lines read over the bright aura.
                        _BoltThickness * k * 1.8, _GlowRadius * k * 1.25, _Jaggedness * k * 0.75
                    );

                    totalCore = max(totalCore, arc.x);
                    totalGlow += arc.y;
                }

                totalGlow = saturate(totalGlow);

                // Over the fighter's body the lightning thins to a quarter, so it does not hide the fighter
                // (added for the game; the limbs come in world units from the quad's centre).
                float body = 10.0;
                [unroll] for (int l = 0; l < 14; l++)
                {
                    float2 la = _LimbCapsules[l].xy, lb = _LimbCapsules[l].zw;
                    float2 pa = p - la, ba = lb - la;
                    float hh = saturate(dot(pa, ba) / max(dot(ba, ba), 0.0001));
                    body = min(body, length(pa - ba * hh) - _LimbRadii[l]);
                }
                float overBody = lerp(0.25, 1.0, smoothstep(-0.01 * k, 0.03 * k, body));
                totalCore *= overBody;
                totalGlow *= overBody;

                // Compose electric corona + blinding white-cyan core
                float3 col = _BoltGlowColor.rgb * (totalGlow * 1.35);
                col = lerp(col, _BoltCoreColor.rgb, saturate(totalCore * 1.25));

                float alpha = saturate(max(totalCore, totalGlow * 0.85));
                // In the game (linear, ACES): the white core runs into the bloom, and the cyan glow goes in as
                // linear light, since saturated blue pushed through the tonemapping compensation turns violet.
                if (_Hdr > 0.5)
                {
                    float core = saturate(totalCore * 1.25);
                    float3 glowLit = pow(saturate(_BoltGlowColor.rgb * totalGlow * 1.35), 2.2) * 1.8;
                    col = lerp(glowLit, SOut(_BoltCoreColor.rgb, _Hdr), core);
                }
                return fixed4(col * alpha, alpha) * _Opacity;
            }
            ENDCG
        }
    }
}

