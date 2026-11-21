// Lit 3D parts of the dive journal (PVC cards, tabs, rings, pencil) that, like the journal UI,
// ignore scene depth: the journal rides on the hand and must never be hidden by the hand or the reef.
// Parts are drawn back to front by render queue (set per material from DiveCardMock); front faces only.
Shader "Reefkeeper/JournalOverlayLit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Smoothness ("Smoothness", Range(0,1)) = 0.25
        _Metallic ("Metallic", Range(0,1)) = 0
    }

    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Opaque" "IgnoreProjector"="True" }

        Cull Back
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Color;
            half _Smoothness, _Metallic;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 view : TEXCOORD1;
                float3 viewNormal : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.view = WorldSpaceViewDir(v.vertex);
                o.viewNormal = mul((float3x3)UNITY_MATRIX_V, o.normal);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // View space: +z points back at the eyes. A key light from the upper left of the view
                // (fixed to the viewer, so the journal reads the same anywhere in the reef).
                float3 n = normalize(i.viewNormal);
                float3 l = normalize(float3(-0.45, 0.65, 0.62));
                float key = saturate(dot(n, l));
                float fill = saturate(dot(n, float3(0.3, -0.2, 0.93))) * 0.18;
                float diffuse = key * 0.7 + fill + 0.3;
                float3 h = normalize(l + float3(0, 0, 1));
                float specular = pow(saturate(dot(n, h)), lerp(8, 96, _Smoothness)) * _Smoothness * (0.45 + _Metallic);
                return fixed4(_Color.rgb * diffuse + specular, 1);
            }
            ENDCG
        }
    }
}
