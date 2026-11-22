Shader "Reefkeeper/Photos/Caption"
{
 Properties {_MainTex("Font atlas",2D)="white"{}}
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
  Cull Back ZWrite Off ZTest LEqual
  Blend SrcAlpha OneMinusSrcAlpha
  Pass {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
   struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;half fog:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
   V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;o.color=i.color;o.fog=ComputeFogFactor(o.positionCS.z);return o;}
   half4 frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);return half4(MixFog(i.color.rgb,i.fog),i.color.a*SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).a);}
   ENDHLSL
  }
 }
}
