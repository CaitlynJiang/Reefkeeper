// Stylised lit surface for underwater props: wrap lighting, gradient ambient,
// sunlight fading with depth, then water absorption and fog.
Shader "Reefkeeper/UnderWater/Lit"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.8,0.8,0.8,1)
        _BaseMap ("Texture", 2D) = "white" {}
        _Wrap ("Wrap lighting (soft shading)", Range(0,1)) = 0.5
        _CausticsAmount ("Caustics amount 焦散强度", Range(0,2)) = 1
        [HideInInspector] _Cutoff ("Cutoff", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "UnderwaterCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float _Wrap, _CausticsAmount, _Cutoff;
            CBUFFER_END
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float wrap = saturate((dot(n, sun.direction) + _Wrap) / (1.0 + _Wrap));
                float3 albedo = _BaseColor.rgb * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;
                float3 direct = sun.color * wrap * sun.shadowAttenuation * UW_SunDepthAttenuation(i.positionWS.y);
                float3 caustics = sun.color * sun.shadowAttenuation * UW_Caustics(i.positionWS, n) * _CausticsAmount;
                // Shadowed and back-facing areas pick up a coloured tint instead of going grey.
                float3 ambient = UW_Ambient(n) * lerp(_UW_ShadowTint.rgb, 1.0, wrap * sun.shadowAttenuation);
                float3 col = albedo * (ambient + direct + caustics);
                return half4(UW_ApplyWater(col, i.positionWS), 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
