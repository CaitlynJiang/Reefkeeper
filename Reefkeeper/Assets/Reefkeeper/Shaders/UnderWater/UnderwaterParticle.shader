// Suspended particles (marine snow). Soft round dot drawn from UVs, no texture.
// Lit by the water ambient and depth-faded sun, fades into fog and near the camera.
Shader "Reefkeeper/UnderWater/Particle"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Brightness ("Brightness 亮度", Range(0,3)) = 1
        _Softness ("Edge softness 边缘柔和", Range(0.05,1)) = 0.7
        _NearFade ("Near fade distance 近处淡出", Range(0,3)) = 0.6
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "UnderwaterCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Brightness, _Softness, _NearFade;
            CBUFFER_END

            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float2 uv:TEXCOORD1; float4 color:TEXCOORD2; float eyeDepth:TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.eyeDepth = -pos.positionVS.z;
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float d = length(i.uv - 0.5) * 2.0;
                float alpha = 1.0 - smoothstep(1.0 - _Softness, 1.0, d);
                alpha *= i.color.a * _Color.a;
                alpha *= (1.0 - UW_FogAmount(i.positionWS));
                alpha *= saturate(i.eyeDepth / max(_NearFade, 1e-3));
                float3 light = UW_Ambient(float3(0, 1, 0)) + _MainLightColor.rgb * UW_SunDepthAttenuation(i.positionWS.y) * 0.5;
                float3 col = _Color.rgb * i.color.rgb * light * _Brightness;
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
