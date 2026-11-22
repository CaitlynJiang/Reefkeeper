Shader "Reefkeeper/Photos/Develop"
{
 Properties {
  _BaseMap("Photo",2D)="white"{}
  _PlaceholderMap("Uncollected",2D)="white"{}
  _BaseColor("Tint",Color)=(1,1,1,1)
  _Reveal("Develop",Range(0,1))=1
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Cull Back ZWrite On ZTest LEqual
  Pass {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
   TEXTURE2D(_PlaceholderMap); SAMPLER(sampler_PlaceholderMap);
   CBUFFER_START(UnityPerMaterial)
   half4 _BaseColor; float _Reveal;
   CBUFFER_END
   struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half fog:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
   V vert(A i) {V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;o.fog=ComputeFogFactor(o.positionCS.z);return o;}
   half4 frag(V i):SV_Target {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    half3 photo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
    half3 blank=SAMPLE_TEXTURE2D(_PlaceholderMap,sampler_PlaceholderMap,i.uv).rgb;
    float sweep=smoothstep(i.uv.y-.12,i.uv.y+.12,saturate(_Reveal)*1.24-.12);
    half3 developing=lerp(half3(.92,.91,.84),photo,saturate(_Reveal*1.5));
    return half4(MixFog(lerp(blank,developing,sweep)*_BaseColor.rgb,i.fog),1);
   }
   ENDHLSL
  }
 }
}
