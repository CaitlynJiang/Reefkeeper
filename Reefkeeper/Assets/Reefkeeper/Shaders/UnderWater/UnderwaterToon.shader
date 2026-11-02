// Cel shading for the toonkit models (lucky cat, market stall): baked base colour x a hard
// two-step light ramp (same shadow multiplier and threshold as the Blender toon material),
// lifted by the underwater ambient/sun so it sits in the scene, then caustics and water fog.
// Optional cloth sway in the current (awning): UV2 carries the sway weights, see ToonSway.hlsl.
Shader "Reefkeeper/UnderWater/Toon"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1,1,1,1)
        _BaseMap ("Baked colour", 2D) = "white" {}
        _ShadeColor ("Shadow multiplier 暗部", Color) = (0.906, 0.871, 0.863, 1)
        _Threshold ("Light threshold 明暗分界", Range(0,1)) = 0.42
        _Softness ("Edge softness", Range(0.001, 0.2)) = 0.02
        _SceneLight ("Scene lighting amount 环境光影响", Range(0,1)) = 0.6
        _CausticsAmount ("Caustics amount 焦散强度", Range(0,2)) = 0.5
        _Emission ("Self light (lanterns) 自发光", Range(0,1)) = 0
        [Header(Cloth sway in the current. Zero means static)]
        _SwayAmount ("Sway at the free edge 摇摆幅度 (m)", Range(0,0.3)) = 0
        _SwayLift ("Billow up/down 上下鼓动 (m)", Range(0,0.2)) = 0
        _SwaySpeed ("Sway speed 摇摆速度", Range(0,4)) = 1.2
        _Flutter ("Flutter 细碎抖动 (m)", Range(0,0.05)) = 0
        _SwayDir ("Current direction (xz) 水流方向", Vector) = (0.35, 0.2, 0, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor, _ShadeColor, _SwayDir;
            float4 _BaseMap_ST;
            float _Threshold, _Softness, _SceneLight, _CausticsAmount, _Emission;
            float _SwayAmount, _SwayLift, _SwaySpeed, _Flutter;
        CBUFFER_END
        #include "ToonSway.hlsl"
        float3 SwayedWS(float3 positionOS, float2 sway) { return ToonSwayWS(positionOS, sway, _SwayAmount, _SwaySpeed, _SwayLift, _Flutter, _SwayDir); }
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; float2 sway:TEXCOORD1; };
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "UnderwaterCommon.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = SwayedWS(v.positionOS.xyz, v.sway);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float ndl = saturate(dot(n, sun.direction)) * sun.shadowAttenuation;
                float lit = smoothstep(_Threshold - _Softness, _Threshold + _Softness, ndl);
                float3 albedo = _BaseColor.rgb * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;
                float3 cel = albedo * lerp(_ShadeColor.rgb, 1.0, lit);
                float3 scene = UW_Ambient(n) + sun.color * UW_SunDepthAttenuation(i.positionWS.y) * (0.35 + 0.65 * lit);
                float3 col = cel * lerp(1.0, scene, _SceneLight);
                col += albedo * sun.color * sun.shadowAttenuation * UW_Caustics(i.positionWS, n) * _CausticsAmount;
                col = lerp(col, albedo, _Emission);
                return half4(UW_ApplyWater(col, i.positionWS), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            float4 vert(Attributes v) : SV_POSITION
            {
                float3 ws = SwayedWS(v.positionOS.xyz, v.sway);
                float3 n = TransformObjectToWorldNormal(v.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 l = normalize(_LightPosition - ws);
            #else
                float3 l = _LightDirection;
            #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(ws, n, l));
            #if UNITY_REVERSED_Z
                cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
            #else
                cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return cs;
            }
            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 vert(Attributes v) : SV_POSITION { return TransformWorldToHClip(SwayedWS(v.positionOS.xyz, v.sway)); }
            half frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
