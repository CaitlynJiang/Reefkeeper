// UnderwaterLit + a gentle current sway for seaweed. Double-sided (blades are flat).
// UV2 (TEXCOORD1) from the Blender generator: x = sway weight (0 at the root, 1 at the tip),
// y = per-blade phase, so neighbouring blades move out of step.
Shader "Reefkeeper/UnderWater/Lit Sway"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1,1,1,1)
        _BaseMap ("Texture", 2D) = "white" {}
        _Wrap ("Wrap lighting (soft shading)", Range(0,1)) = 0.6
        _CausticsAmount ("Caustics amount 焦散强度", Range(0,2)) = 0.8
        _SwayAmount ("Sway distance at the tip 摇摆幅度 (m)", Range(0,0.4)) = 0.08
        _SwaySpeed ("Sway speed 摇摆速度", Range(0,4)) = 1.1
        _Flutter ("Flutter 细碎抖动 (m)", Range(0,0.05)) = 0.012
        _SwayDir ("Current direction (xz) 水流方向", Vector) = (0.35, 0.2, 0, 0)
        _SwayHeight ("Typical height (m)", Float) = 0.5
        [HideInInspector] _Cutoff ("Cutoff", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _BaseMap_ST;
            float4 _SwayDir;
            float _Wrap, _CausticsAmount, _Cutoff, _SwayAmount, _SwaySpeed, _Flutter, _SwayHeight;
        CBUFFER_END

        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; float2 sway:TEXCOORD1; };

        // World position after the current has pushed it: a slow lean plus a small flutter,
        // both growing toward the tip; the tip dips a little so blades keep their length.
        float3 SwayWS(float3 positionOS, float2 sway)
        {
            float3 ws = TransformObjectToWorld(positionOS);
            float3 origin = TransformObjectToWorld(float3(0, 0, 0));
            float h = saturate(sway.x);
            float t = _Time.y * _SwaySpeed;
            float ph = sway.y * 6.2831 + dot(origin.xz, float2(0.7, 1.3));
            float2 lean = float2(sin(t + ph), cos(t * 0.83 + ph * 1.3) * 0.6) + _SwayDir.xy * 0.6;
            float flutter = sin(t * 2.7 + h * 5.0 + ph * 3.0);
            float2 off = lean * _SwayAmount * h * h + flutter * _Flutter * h * float2(0.6, 0.8);
            ws.xz += off;
            ws.y -= dot(off, off) * 0.5 / max(_SwayHeight, 0.05);
            return ws;
        }
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
            #include "../UnderWater/UnderwaterCommon.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = SwayWS(v.positionOS.xyz, v.sway);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 n = normalize(i.normalWS) * IS_FRONT_VFACE(face, 1.0, -1.0);   // light both sides of a blade
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float wrap = saturate((dot(n, sun.direction) + _Wrap) / (1.0 + _Wrap));
                float3 albedo = _BaseColor.rgb * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;
                float3 direct = sun.color * wrap * sun.shadowAttenuation * UW_SunDepthAttenuation(i.positionWS.y);
                float3 caustics = sun.color * sun.shadowAttenuation * UW_Caustics(i.positionWS, n) * _CausticsAmount;
                float3 ambient = UW_Ambient(n) * lerp(_UW_ShadowTint.rgb, 1.0, wrap * sun.shadowAttenuation);
                float3 col = albedo * (ambient + direct + caustics);
                return half4(UW_ApplyWater(col, i.positionWS), 1);
            }
            ENDHLSL
        }

        // Shadows and depth sway too, so they stay attached to the blades.
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
                float3 ws = SwayWS(v.positionOS.xyz, v.sway);
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
            float4 vert(Attributes v) : SV_POSITION { return TransformWorldToHClip(SwayWS(v.positionOS.xyz, v.sway)); }
            half frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
