// Stylised ground foliage ("Ghibli grass" approach, after Kristof Dedene / the Zvair Shader Graph
// tutorial), adapted to the underwater look:
//  - leaf cards cut out by an alpha texture (_BaseMap.a, _Cutoff)
//  - normals forced up (blended a little toward a dome) so a whole patch lights as one soft mass
//  - colour from a large world-space noise pattern (like sampling the ground under each leaf),
//    times a dark-root -> bright-tip gradient up each leaf
//  - wind as a gust wave travelling across the field in world space, plus a small per-leaf flutter
// UV2 (TEXCOORD1): x = height along the leaf (0 root .. 1 tip), y = per-leaf phase.
Shader "Reefkeeper/UnderWater/Lit Foliage"
{
    Properties
    {
        _BaseMap ("Leaf mask (alpha) or colour texture", 2D) = "white" {}
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.4
        [Header(Colour)]
        _ColorA ("Field colour A 颜色A", Color) = (0.36, 0.62, 0.24, 1)
        _ColorB ("Field colour B 颜色B", Color) = (0.55, 0.78, 0.30, 1)
        _ColorNoiseScale ("Colour patch size (1/m)", Float) = 0.35
        _RootColor ("Root tint 根部", Color) = (0.35, 0.45, 0.35, 1)
        _TipColor ("Tip tint 叶尖", Color) = (1.15, 1.12, 0.95, 1)
        [Header(Lighting)]
        _NormalOverride ("Normals up (0 mesh .. 1 ground) 法线朝上", Range(0,1)) = 1
        _Roundness ("Dome roundness 圆润度", Range(0,1)) = 0.3
        _Wrap ("Wrap lighting (soft shading)", Range(0,1)) = 0.7
        _CausticsAmount ("Caustics amount 焦散强度", Range(0,2)) = 0.7
        _Fuzz ("Fuzz rim 绒毛边缘光", Range(0,1)) = 0.2
        [Header(Wind)]
        _WindAmount ("Gust strength at tip (m) 摆幅", Range(0,0.3)) = 0.05
        _WindSpeed ("Gust speed 速度", Range(0,6)) = 1.5
        _WindDir ("Current direction (xz) 水流方向", Vector) = (1, 0.4, 0, 0)
        _GustFreq ("Gust wave frequency (1/m)", Float) = 0.6
        _Flutter ("Flutter 细碎抖动 (m)", Range(0,0.05)) = 0.008
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _ColorA, _ColorB, _RootColor, _TipColor, _WindDir;
            float _Cutoff, _ColorNoiseScale, _NormalOverride, _Roundness, _Wrap, _CausticsAmount, _Fuzz;
            float _WindAmount, _WindSpeed, _GustFreq, _Flutter;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

        struct Attributes
        {
            float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float2 sway : TEXCOORD1;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        float Hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
        float ValueNoise(float2 p)
        {
            float2 i = floor(p), f = frac(p), u = f * f * (3 - 2 * f);
            return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), u.x), lerp(Hash21(i + float2(0, 1)), Hash21(i + 1), u.x), u.y);
        }
        float Fbm(float2 p) { return ValueNoise(p) * 0.6 + ValueNoise(p * 2.13 + 7.3) * 0.3 + ValueNoise(p * 4.37 + 3.1) * 0.1; }

        // A gust wave travels along the current; each leaf adds a small flutter of its own.
        float3 WindWS(float3 positionOS, float2 sway)
        {
            float3 ws = TransformObjectToWorld(positionOS);
            float h = saturate(sway.x);
            float2 dir = normalize(_WindDir.xy + 1e-4);
            float t = _Time.y * _WindSpeed;
            float gust = sin(dot(ws.xz, dir) * _GustFreq - t) * 0.5 + 0.5;
            gust = gust * gust * (0.6 + 0.4 * ValueNoise(ws.xz * 0.15 - dir * t * 0.1));
            float flutter = sin(t * 3.1 + sway.y * 6.2831 + h * 4.0);
            float2 off = dir * _WindAmount * h * h * (0.25 + gust) + float2(-dir.y, dir.x) * flutter * _Flutter * h;
            ws.xz += off;
            ws.y -= dot(off, off) * 2.0;     // keep leaf length roughly constant
            return ws;
        }

        half Mask(float2 uv) { return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a; }
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
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "../UnderWater/UnderwaterCommon.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2; float height : TEXCOORD3; float3 originWS : TEXCOORD4;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionWS = WindWS(v.positionOS.xyz, v.sway);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.height = saturate(v.sway.x);
                o.originWS = TransformObjectToWorld(float3(0, 0, 0));
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                clip(tex.a - _Cutoff);

                // lighting normal: the ground's (up), nudged toward a dome around the clump
                float3 dome = normalize(i.positionWS - (i.originWS - float3(0, 0.25, 0)));
                float3 groundN = normalize(lerp(float3(0, 1, 0), dome, _Roundness));
                float3 n = normalize(lerp(normalize(i.normalWS), groundN, _NormalOverride));

                // colour: big soft patches across the seabed, darker roots, brighter tips
                float field = smoothstep(0.25, 0.75, Fbm(i.positionWS.xz * _ColorNoiseScale));
                float3 albedo = lerp(_ColorA.rgb, _ColorB.rgb, field);
                albedo *= lerp(_RootColor.rgb, _TipColor.rgb, smoothstep(0.0, 1.0, i.height));
                albedo *= tex.rgb;

                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float wrap = saturate((dot(n, sun.direction) + _Wrap) / (1.0 + _Wrap));
                float3 direct = sun.color * wrap * sun.shadowAttenuation * UW_SunDepthAttenuation(i.positionWS.y);
                float3 caustics = sun.color * sun.shadowAttenuation * UW_Caustics(i.positionWS, n) * _CausticsAmount;
                float3 ambient = UW_Ambient(n) * lerp(_UW_ShadowTint.rgb, 1.0, wrap * sun.shadowAttenuation);
                float3 col = albedo * (ambient + direct + caustics);
                float rim = pow(1.0 - saturate(abs(dot(normalize(i.normalWS), GetWorldSpaceNormalizeViewDir(i.positionWS)))), 2.5) * _Fuzz;
                col += albedo * rim * (UW_Ambient(n) * 1.5 + sun.color * 0.5 * sun.shadowAttenuation) * i.height;
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
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            V vert(Attributes v)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 ws = WindWS(v.positionOS.xyz, v.sway);
                float3 n = TransformObjectToWorldNormal(v.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 l = normalize(_LightPosition - ws);
            #else
                float3 l = _LightDirection;
            #endif
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(ws, n, l));
            #if UNITY_REVERSED_Z
                o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }
            half4 frag(V i) : SV_Target { clip(Mask(i.uv) - _Cutoff); return 0; }
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
            #pragma multi_compile_instancing
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(Attributes v)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformWorldToHClip(WindWS(v.positionOS.xyz, v.sway));
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }
            half frag(V i) : SV_Target { clip(Mask(i.uv) - _Cutoff); return 0; }
            ENDHLSL
        }
    }
}
