Shader "Dragon Ball Singularity/Majin Living Flesh"
{
    Properties
    {
        _FleshAtlas ("Blender flesh albedo", 2D) = "white" {}
        _NormalAtlas ("Blender camera normals", 2D) = "bump" {}
        _Tint ("Majin tissue tint", Color) = (0.93,0.48,0.68,1)
        _Frame ("Authored frame", Float) = 0
        _Variant ("Authored fragment", Float) = 0
        _Activity ("Living activity", Range(0,1)) = 0
        _Contraction ("Tissue contraction", Range(0,1)) = 0
        _Opacity ("Opacity", Range(0,1)) = 1
        _Wetness ("Wet surface", Range(0,1)) = 0.68
        _LightDir ("Soft studio light", Vector) = (-0.45,0.65,0.62,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="False" }
        Cull Off Lighting Off ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _FleshAtlas, _NormalAtlas;
            float4 _FleshAtlas_TexelSize, _Tint, _LightDir;
            float _Frame, _Variant, _Activity, _Contraction, _Opacity, _Wetness;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float4 axes : TEXCOORD1; };
            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.axes.xy = normalize(mul((float3x3)unity_ObjectToWorld, float3(1,0,0)).xy);
                o.axes.zw = normalize(mul((float3x3)unity_ObjectToWorld, float3(0,1,0)).xy);
                return o;
            }
            float2 atlasUV(float2 uv, float frame, float variant)
            {
                float2 inset = _FleshAtlas_TexelSize.xy * float2(24,8) * 0.65;
                uv = lerp(inset, 1.0-inset, uv);
                // Blender row zero is the top of the PNG; Unity UV origin is below.
                return (float2(fmod(frame,24.0), 7.0-fmod(variant,8.0)) + uv) / float2(24,8);
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float f = max(0,_Frame), frame = floor(f), blend = frac(f);
                float2 uvA = atlasUV(i.uv,frame,_Variant), uvB = atlasUV(i.uv,frame+1.0,_Variant);
                float2 uvRest = atlasUV(i.uv,0,_Variant);
                float4 a = tex2D(_FleshAtlas,uvA), b = tex2D(_FleshAtlas,uvB), rest = tex2D(_FleshAtlas,uvRest);
                float4 albedo = lerp(a,b,blend);
                float activity = smoothstep(0.015,0.27,_Activity);
                albedo = lerp(rest,albedo,activity);
                float alpha = saturate(albedo.a*_Opacity);
                clip(alpha-0.003);
                float3 na = tex2D(_NormalAtlas,uvA).xyz*2.0-1.0;
                float3 nb = tex2D(_NormalAtlas,uvB).xyz*2.0-1.0;
                float3 nr = tex2D(_NormalAtlas,uvRest).xyz*2.0-1.0;
                float3 n = normalize(lerp(nr*rest.a,lerp(na*a.a,nb*b.a,blend),activity) + float3(0,0,0.0001));
                n = normalize(float3(n.x*i.axes.xy+n.y*i.axes.zw,n.z));
                float3 light = normalize(_LightDir.xyz), eye = float3(0,0,1), halfVector = normalize(light+eye);
                float ndl = dot(n,light);
                float wrap = saturate((ndl+0.38)/1.38);
                float cavity = saturate(dot(albedo.rgb,float3(.3333,.3333,.3333)));
                float3 baseColour = _Tint.rgb * albedo.rgb;
                float3 diffuse = baseColour * (0.39+wrap*0.71);
                float thickness = saturate(1.0-cavity);
                float forwardScatter = pow(saturate(dot(-n,light)*0.5+0.5),2.0);
                float3 transmitted = _Tint.rgb * forwardScatter * (0.055+thickness*0.08);
                float wet = saturate(_Wetness+_Activity*.13+_Contraction*.12);
                float highlight = pow(saturate(dot(n,halfVector)),48.0)*0.48 + pow(saturate(dot(n,halfVector)),9.0)*0.075;
                float fresnel = pow(1.0-saturate(n.z),3.0);
                float3 reflection = lerp(float3(.65,.57,.62),float3(1,.94,.97),cavity) * (highlight+fresnel*.065)*wet;
                float3 colour = diffuse + transmitted + reflection;
                // No emission: the brightness remains a wet material highlight.
                return float4(colour*alpha,alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
