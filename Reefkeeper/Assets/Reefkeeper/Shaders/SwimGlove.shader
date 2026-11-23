Shader "Reefkeeper/Swim Lesson/Glove"
{
    Properties
    {
        _BaseColor ("Glove", Color) = (1, .90, .73, .70)
        _CuffColor ("Cuff", Color) = (.98, .49, .38, .94)
        _FingerCurl ("Finger curl", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _CuffColor;
                float _FingerCurl;
            CBUFFER_END
            struct Input
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Output
            {
                float4 position : SV_POSITION;
                half3 normalVS : TEXCOORD0;
                float wristHeight : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Output Vert(Input v)
            {
                Output o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.wristHeight = v.vertex.y;
                // FUZE palms use metres, with fingers along +Y and the palm facing +Z.
                // Bend the finger centreline in an arc, then rotate its cross-section and normal.
                const float knuckleY = .076, centreZ = .006;
                float lengthFromKnuckle = max(0, v.vertex.y - knuckleY);
                float angle = lengthFromKnuckle * (8.0 * _FingerCurl);
                float s, c;
                sincos(angle, s, c);
                float depth = v.vertex.z - centreZ;
                // Mid-angle chord form is continuous at zero curvature.
                float halfAngle = angle * .5;
                float chord = lengthFromKnuckle * (1.0 - halfAngle * halfAngle / 6.0);
                v.vertex.y += chord * cos(halfAngle) - lengthFromKnuckle - depth * s;
                v.vertex.z += chord * sin(halfAngle) + depth * (c - 1.0);
                v.normal.yz = float2(c * v.normal.y - s * v.normal.z, s * v.normal.y + c * v.normal.z);
                o.position = TransformObjectToHClip(v.vertex.xyz);
                o.normalVS = TransformWorldToViewDir(TransformObjectToWorldNormal(v.normal));
                return o;
            }
            half4 Frag(Output i) : SV_Target
            {
                half3 normal = normalize(i.normalVS);
                half cuff = 1 - smoothstep(-.012, -.010, i.wristHeight);
                half4 color = lerp(_BaseColor, _CuffColor, cuff);
                half light = saturate(dot(normal, normalize(half3(-.48, .65, .72))));
                half rim = pow(1 - saturate(normal.z), 2);
                color.rgb *= .53 + .47 * light;
                color.rgb = lerp(color.rgb, _BaseColor.rgb, rim * .19);
                color.a = lerp(color.a, 1, rim * .6);
                return color;
            }
            ENDHLSL
        }
    }
}
