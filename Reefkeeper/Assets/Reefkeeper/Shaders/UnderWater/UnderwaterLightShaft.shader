// Additive light shaft on a cone mesh (built by UnderwaterLightShafts.cs).
// Looks volumetric by fading: silhouette edges, both ends, where it meets
// geometry, near the camera and into the fog. Vertex colour R = random phase,
// G = random brightness.
Shader "Reefkeeper/UnderWater/Light Shaft"
{
    Properties
    {
        _Color ("Color", Color) = (0.75,1,0.95,1)
        _Intensity ("Intensity 亮度", Range(0,2)) = 0.35
        _EdgePower ("Edge softness 边缘柔和", Range(0.5,8)) = 2.5
        _TopFade ("Top fade 顶部淡出", Range(0.01,1)) = 0.15
        _BottomFade ("Bottom fade 底部淡出", Range(0.01,1)) = 0.6
        [Toggle(_DEPTH_FADE)] _DepthFade ("Soft contact (needs depth texture) 接触柔化", Float) = 1
        _SoftDepth ("Soft contact distance", Range(0.05,5)) = 1.5
        _CameraFadeStart ("Camera fade start 近处淡出起点", Range(0,10)) = 1
        _CameraFadeLength ("Camera fade length", Range(0.1,20)) = 4
        _StreakScale ("Streak count 条纹数量", Range(1,8)) = 3
        _StreakSpeed ("Streak speed 条纹流速", Range(0,0.5)) = 0.04
        _FlickerSpeed ("Flicker speed 闪烁速度", Range(0,3)) = 0.6
        _FlickerAmount ("Flicker amount 闪烁幅度", Range(0,1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "LightShaft"
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _DEPTH_FADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "UnderwaterCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensity, _EdgePower, _TopFade, _BottomFade, _SoftDepth;
                float _CameraFadeStart, _CameraFadeLength, _StreakScale, _StreakSpeed, _FlickerSpeed, _FlickerAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; float4 color:COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1;
                float2 uv:TEXCOORD2; float4 color:TEXCOORD3; float4 screenPos:TEXCOORD4; float eyeDepth:TEXCOORD5;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.color = v.color;
                o.screenPos = ComputeScreenPos(pos.positionCS);
                o.eyeDepth = -pos.positionVS.z;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n = normalize(i.normalWS);
                float3 v = normalize(_WorldSpaceCameraPos - i.positionWS);
                float edge = pow(abs(dot(n, v)), _EdgePower);

                float along = i.uv.y; // 0 at the top, 1 at the bottom
                float ends = smoothstep(0.0, _TopFade, along) * (1.0 - smoothstep(1.0 - _BottomFade, 1.0, along));

                float soft = 1.0;
                #if defined(_DEPTH_FADE)
                    float2 screenUV = i.screenPos.xy / i.screenPos.w;
                    float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                    soft = saturate((sceneEye - i.eyeDepth) / _SoftDepth);
                #endif

                float camFade = saturate((i.eyeDepth - _CameraFadeStart) / _CameraFadeLength);

                float phase = i.color.r;
                float t = _Time.y;
                // Blurry lookup into the caustics texture gives soft drifting streaks.
                float2 streakUV = float2(i.uv.x * round(_StreakScale) + phase * 7.0, along * 0.35 - t * _StreakSpeed);
                float streak = SAMPLE_TEXTURE2D_BIAS(_UW_CausticsTex, sampler_UW_CausticsTex, streakUV, 3.0).r;
                streak = lerp(0.55, 1.6, saturate(streak * 2.5));
                float flicker = 1.0 - _FlickerAmount * (0.5 + 0.5 * sin(t * _FlickerSpeed + phase * 6.2832));

                float fade = edge * ends * soft * camFade * flicker * streak * i.color.g;
                fade *= (1.0 - UW_FogAmount(i.positionWS)) * UW_SunDepthAttenuation(i.positionWS.y);
                float3 col = _Color.rgb * _MainLightColor.rgb * _Intensity * fade;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
