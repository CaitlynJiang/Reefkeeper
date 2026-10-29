// Water surface seen from below. Waves move the mesh and bend the normal; the view
// ray is refracted into the air (Snell's window: bright sky overhead) or, past the
// critical angle, totally reflected back into the water (deep water colour).
// Use on a subdivided grid placed at the water surface height.
Shader "Reefkeeper/UnderWater/Surface"
{
    Properties
    {
        _SkyColor ("Sky overhead 正上方天空", Color) = (0.82,0.97,1,1)
        _SkyHorizonColor ("Sky at window edge 视窗边缘", Color) = (0.45,0.82,0.9,1)
        _SkyBrightness ("Sky brightness 天空亮度", Range(0,3)) = 1.3
        _ReflectionTint ("Reflection tint 全反射色调", Color) = (1,1,1,1)
        _SunDiscPower ("Sun disc size (higher = smaller) 太阳大小", Range(16,2000)) = 400
        _SunDiscIntensity ("Sun disc intensity 太阳亮度", Range(0,10)) = 4
        _WaveAmplitude ("Wave height 浪高", Range(0,1)) = 0.12
        _WaveLength ("Wave length 波长", Range(1,30)) = 7
        _WaveSpeed ("Wave speed 波速", Range(0,3)) = 0.7
        _DetailStrength ("Ripple detail 细碎波纹", Range(0,2)) = 0.6
        _IOR ("Index of refraction 折射率", Range(1,1.6)) = 1.33
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "UnderwaterCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _SkyColor, _SkyHorizonColor, _ReflectionTint;
                float _SkyBrightness, _SunDiscPower, _SunDiscIntensity;
                float _WaveAmplitude, _WaveLength, _WaveSpeed, _DetailStrength, _IOR;
            CBUFFER_END

            struct Attributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            static const float2 dirs[8] = {
                float2(0.97, 0.24), float2(-0.42, 0.91), float2(0.64, -0.77), float2(-0.88, -0.47),
                float2(0.31, 0.95), float2(-0.99, 0.12), float2(0.76, 0.65), float2(0.05, -1.0) };
            static const float scales[8] = { 1.0, 0.71, 0.53, 0.37, 0.19, 0.143, 0.11, 0.083 };

            // Sum of directional sine waves. Returns height (x) and slope d/dx, d/dz (yz).
            // Speed follows deep-water dispersion so long waves travel faster.
            float3 Waves(float2 xz, float baseLength, float amplitude, float t, int first, int last)
            {
                float3 r = 0;
                [unroll] for (int i = first; i < last; i++)
                {
                    float L = baseLength * scales[i];
                    float k = 6.2832 / L;
                    float a = amplitude * scales[i];
                    float phase = dot(dirs[i], xz) * k - sqrt(9.8 * k) * t;
                    float s, c; sincos(phase, s, c);
                    r.x += a * s;
                    r.yz += a * k * c * dirs[i];
                }
                return r;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                ws.y += Waves(ws.xz, _WaveLength, _WaveAmplitude, _Time.y * _WaveSpeed, 0, 4).x;
                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float t = _Time.y * _WaveSpeed;
                float3 big = Waves(i.positionWS.xz, _WaveLength, _WaveAmplitude, t, 0, 4);
                float3 detail = Waves(i.positionWS.xz, _WaveLength, _WaveAmplitude * _DetailStrength, t, 4, 8);
                float2 slope = big.yz + detail.yz;
                float3 nUp = normalize(float3(-slope.x, 1, -slope.y));
                float3 nWater = -nUp; // the side facing us, down into the water

                float3 ray = normalize(i.positionWS - _WorldSpaceCameraPos);
                float3 L = _MainLightPosition.xyz;

                // Reflection back into the water.
                float3 reflected = reflect(ray, nWater);
                float3 reflCol = UW_WaterColor(reflected) * _ReflectionTint.rgb;

                // Refraction out into the air; refract() returns 0 past the critical angle.
                float3 refr = refract(ray, nWater, _IOR);
                float transmit = 0;
                float3 skyCol = 0;
                if (dot(refr, refr) > 1e-4)
                {
                    refr = normalize(refr);
                    float cosAir = saturate(refr.y);
                    float fresnel = 0.02 + 0.98 * pow(1.0 - cosAir, 5.0);
                    transmit = 1.0 - fresnel;
                    skyCol = lerp(_SkyHorizonColor.rgb, _SkyColor.rgb, sqrt(cosAir)) * _SkyBrightness;
                    skyCol += _MainLightColor.rgb * pow(saturate(dot(refr, L)), _SunDiscPower) * _SunDiscIntensity;
                }

                float3 col = lerp(reflCol, skyCol, transmit);
                return half4(UW_ApplyWater(col, i.positionWS), 1);
            }
            ENDHLSL
        }
    }
}
