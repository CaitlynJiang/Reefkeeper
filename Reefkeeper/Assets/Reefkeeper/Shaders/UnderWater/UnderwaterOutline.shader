// Flat colour for the inverted-hull outline shells exported from Blender (solidify, flipped
// normals): ordinary back-face culling leaves only the rim showing. Fogs with the water.
// The sway settings must match the cloth's Toon material so the outline follows it.
Shader "Reefkeeper/UnderWater/Outline"
{
    Properties
    {
        _BaseColor ("Line colour 描边颜色", Color) = (0.29, 0.2, 0.157, 1)
        [Header(Cloth sway. Copied from the Toon material)]
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
            float4 _BaseColor, _SwayDir;
            float _SwayAmount, _SwayLift, _SwaySpeed, _Flutter;
        CBUFFER_END
        #include "ToonSway.hlsl"
        struct Attributes { float4 positionOS:POSITION; float2 sway:TEXCOORD1; };
        float3 SwayedWS(Attributes v) { return ToonSwayWS(v.positionOS.xyz, v.sway, _SwayAmount, _SwaySpeed, _SwayLift, _Flutter, _SwayDir); }
        ENDHLSL

        Pass
        {
            Name "Unlit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "UnderwaterCommon.hlsl"
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = SwayedWS(v);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return half4(UW_ApplyWater(_BaseColor.rgb, i.positionWS), 1);
            }
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
            float4 vert(Attributes v) : SV_POSITION { return TransformWorldToHClip(SwayedWS(v)); }
            half frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
