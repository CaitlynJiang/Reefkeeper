// Simple clear glass for the cat's diving-helmet bubble: faint tint in the middle, brighter and
// more opaque toward the rim (fresnel), a sun glint, and a thin dark rim line standing in for the
// toon outline (an inverted-hull outline would show through the glass as a dark disc). Fogs with the water.
Shader "Reefkeeper/UnderWater/Glass"
{
    Properties
    {
        _Tint ("Glass tint 玻璃颜色", Color) = (0.8, 0.95, 1.0, 1)
        _Alpha ("Centre opacity 中心透明度", Range(0,1)) = 0.06
        _RimColor ("Rim colour 边缘颜色", Color) = (0.85, 0.97, 1.0, 1)
        _RimAlpha ("Rim opacity 边缘不透明度", Range(0,1)) = 0.55
        _RimPower ("Rim width 边缘宽度", Range(0.5, 8)) = 3
        _LineColor ("Outline colour 描边颜色", Color) = (0.29, 0.2, 0.157, 1)
        _LineWidth ("Outline width 描边宽度", Range(0, 0.3)) = 0.06
        _Glint ("Sun glint 高光", Range(0, 2)) = 0.9
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "UnderwaterCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint, _RimColor, _LineColor;
                float _Alpha, _RimAlpha, _RimPower, _LineWidth, _Glint;
            CBUFFER_END

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float facing = saturate(dot(n, v));
                float rim = pow(1.0 - facing, _RimPower);
                Light sun = GetMainLight();
                float3 h = normalize(sun.direction + v);
                float glint = pow(saturate(dot(n, h)), 160.0) * _Glint;
                float3 col = lerp(_Tint.rgb, _RimColor.rgb, rim) * (UW_Ambient(n) * 0.6 + sun.color * 0.6);
                float a = lerp(_Alpha, _RimAlpha, rim);
                // thin dark silhouette line at grazing angles, like the toon outlines
                float edge = smoothstep(_LineWidth, _LineWidth * 0.6, facing);
                col = lerp(col, _LineColor.rgb, edge);
                a = max(a, edge);
                col += sun.color * glint;
                a = saturate(a + glint);
                return half4(UW_ApplyWater(col, i.positionWS), a);
            }
            ENDHLSL
        }
    }
}
