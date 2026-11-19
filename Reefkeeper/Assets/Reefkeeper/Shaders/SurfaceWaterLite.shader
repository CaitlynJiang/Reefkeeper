// One opaque pass on the existing two-triangle ocean. Reuses the reef's wave math,
// with only two directional waves; no geometry displacement or scene texture reads.
Shader "Reefkeeper/Surface Water Lite"
{
    Properties
    {
        _WaterColor("Water color", Color) = (0.14,0.48,0.55,1)
        _HorizonColor("Horizon color", Color) = (0.57,0.73,0.81,1)
        _RippleColor("Ripple light", Color) = (0.55,0.83,0.82,1)
        _WaveAmplitude("Ripple slope", Range(0,0.2)) = 0.075
        _WaveLength("Wave length", Range(1,12)) = 3.8
        _WaveSpeed("Wave speed", Range(0,2)) = 0.45
        _RippleStrength("Ripple contrast", Range(0,1)) = 0.32
        _GlintStrength("Soft glint", Range(0,1)) = 0.18
        _DetailDistance("Detail fade distance", Range(10,150)) = 65
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "SurfaceWater"
            Tags { "LightMode"="UniversalForward" }
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "UnderWater/SurfaceWaves.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _WaterColor, _HorizonColor, _RippleColor;
            float _WaveAmplitude, _WaveLength, _WaveSpeed, _DetailDistance;
            half _RippleStrength, _GlintStrength;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 toEye=GetCameraPositionWS()-i.positionWS;
                float distanceToEye=max(length(toEye),0.001);
                half3 view=toEye/distanceToEye;
                half detail=saturate(1-distanceToEye/max(_DetailDistance,1));
                float3 wave=Waves(i.positionWS.xz,_WaveLength,_WaveAmplitude,_Time.y*_WaveSpeed,0,2);
                half3 normal=normalize(float3(-wave.y*detail,1,-wave.z*detail));
                half facing=saturate(dot(normal,view));
                half fresnel=1-facing;
                fresnel=fresnel*fresnel*fresnel;
                half3 color=lerp(_WaterColor.rgb,_HorizonColor.rgb,fresnel*0.78);
                half crest=saturate(wave.x/max(_WaveAmplitude*1.71,0.001)*0.5+0.5);
                half ripple=smoothstep(0.40,0.92,crest)*detail*_RippleStrength;
                color=lerp(color,_RippleColor.rgb,ripple);
                half3 light=normalize(float3(0.35,0.85,-0.4));
                half3 halfway=normalize(light+view);
                half shine=saturate(dot(normal,halfway));
                shine=shine*shine; shine=shine*shine; shine=shine*shine;
                shine=shine*shine; shine=shine*shine;
                color+=_RippleColor.rgb*shine*_GlintStrength*detail;
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
