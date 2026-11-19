// UnderwaterLit for corals, reacting to their neighbours (CoralColony sets the per-coral values
// through a MaterialPropertyBlock; with none set, it renders exactly like UnderwaterLit):
// - the coral leans away from its neighbours, more toward the top (_ColonyLean);
// - the parts that reach into a neighbour's footprint are pushed back out;
// - the side facing a neighbour slowly takes on some of its colour.
Shader "Reefkeeper/UnderWater/Lit Coral"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1,1,1,1)
        _BaseMap ("Texture", 2D) = "white" {}
        _Wrap ("Wrap lighting (soft shading)", Range(0,1)) = 0.6
        _CausticsAmount ("Caustics amount 焦散强度", Range(0,2)) = 0.7
        _NeighbourTint ("Neighbour colour bleed 邻居染色强度", Range(0,1)) = 0.7
        _TintReach ("Colour bleed reach 染色范围 (m)", Range(0.02,0.4)) = 0.22
        _PushStrength ("Push out of neighbours 挤开强度", Range(0,1)) = 0.45
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
                float _Wrap, _CausticsAmount, _NeighbourTint, _TintReach, _PushStrength, _Cutoff;
            CBUFFER_END
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            // Per coral (MaterialPropertyBlock). Unused neighbour slots have weight 0.
            float4 _ColonyBase;            // xyz: centre of the coral's base, w: its height
            float4 _ColonyLean;            // xz: lean at the top (m)
            float4 _ColonyNeighbour[4];    // xyz: neighbour's base centre, w: its footprint radius
            float4 _ColonyColour[4];       // rgb: neighbour colour (linear), a: link weight 0..1

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; float height:TEXCOORD3; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                float tall = max(_ColonyBase.w, 0.05);
                float h = saturate((ws.y - _ColonyBase.y) / tall);

                // Lean, bending more toward the top; the top dips a little so the coral keeps its length.
                float2 lean = _ColonyLean.xz * h * h;
                ws.xz += lean;
                ws.y -= dot(lean, lean) * 0.5 / tall;

                // Whatever reaches into a neighbour's footprint is pushed back toward its own side.
                [unroll] for (int i = 0; i < 4; i++)
                {
                    float w = _ColonyColour[i].a;
                    float4 n = _ColonyNeighbour[i];
                    float2 d = ws.xz - n.xz;
                    float dist = length(d) + 1e-4;
                    float inside = saturate(1.0 - dist / max(n.w, 1e-3));
                    ws.xz += d / dist * inside * n.w * _PushStrength * w * (0.4 + 0.6 * h);
                }

                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.height = h;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float wrap = saturate((dot(n, sun.direction) + _Wrap) / (1.0 + _Wrap));
                float3 albedo = _BaseColor.rgb * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;

                // Colour bleed: strongest at the base and right next to the neighbour, in soft blotches.
                float bleed = 0;
                float3 bleedColour = 0;
                [unroll] for (int k = 0; k < 4; k++)
                {
                    float w = _ColonyColour[k].a;
                    float4 nb = _ColonyNeighbour[k];
                    float gap = length(i.positionWS.xz - nb.xz) - nb.w;
                    float t = w * (1.0 - smoothstep(0.0, _TintReach, gap)) * lerp(1.0, 0.35, i.height);
                    bleedColour += _ColonyColour[k].rgb * t;
                    bleed += t;
                }
                if (bleed > 1e-4)
                {
                    float3 p = i.positionWS * 38.0;
                    float blotch = 0.65 + 0.35 * sin(p.x + sin(p.y * 1.3)) * sin(p.z * 1.1 + sin(p.x * 0.7));
                    float3 target = bleedColour / bleed * (0.6 + 0.6 * dot(albedo, float3(0.3, 0.59, 0.11)));
                    albedo = lerp(albedo, target, saturate(bleed) * blotch * _NeighbourTint);
                }

                float3 direct = sun.color * wrap * sun.shadowAttenuation * UW_SunDepthAttenuation(i.positionWS.y);
                float3 caustics = sun.color * sun.shadowAttenuation * UW_Caustics(i.positionWS, n) * _CausticsAmount;
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
