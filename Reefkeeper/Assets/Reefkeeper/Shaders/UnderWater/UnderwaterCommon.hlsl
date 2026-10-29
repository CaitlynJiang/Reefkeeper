// Shared underwater look. Values are global, set by UnderwaterEnvironment.cs,
// so every underwater shader and the skybox agree on the same water colour.
#ifndef REEFKEEPER_UNDERWATER_COMMON_INCLUDED
#define REEFKEEPER_UNDERWATER_COMMON_INCLUDED

float4 _UW_ShallowColor;   // water colour near the surface / looking up
float4 _UW_DeepColor;      // water colour in the depths / looking down
float4 _UW_Absorption;     // rgb: light lost per metre (red goes first)
float4 _UW_SunGlow;        // rgb: glow colour, a: tightness of the glow
float  _UW_SunGlowStrength;
float  _UW_FogDensity;
float  _UW_FogStart;
float  _UW_SurfaceY;
float  _UW_FloorY;
float  _UW_DepthLightFalloff;
float  _UW_AmbientStrength;
float4 _UW_ShadowTint;     // rgb: ambient colour multiplier where the sun doesn't reach

TEXTURE2D(_UW_CausticsTex); SAMPLER(sampler_UW_CausticsTex);
float4 _UW_CausticsColor;  // rgb: tint, a: strength
float4 _UW_Caustics;       // x: tiles per metre, y: scroll speed, z: RGB split, w: blur per metre of depth

// 0 at the floor, 1 at the surface.
float UW_HeightT(float y)
{
    return saturate((y - _UW_FloorY) / max(0.001, _UW_SurfaceY - _UW_FloorY));
}

// Colour of "empty water" seen along a direction. The skybox uses this directly,
// and fog fades objects towards it, so distant objects melt into the background.
float3 UW_WaterColor(float3 viewDirWS)
{
    float t = saturate(UW_HeightT(_WorldSpaceCameraPos.y) * 0.6 + viewDirWS.y * 0.5 + 0.2);
    float3 col = lerp(_UW_DeepColor.rgb, _UW_ShallowColor.rgb, t);
    float glow = pow(saturate(dot(viewDirWS, _MainLightPosition.xyz)), _UW_SunGlow.a);
    return col + _UW_SunGlow.rgb * glow * _UW_SunGlowStrength;
}

// Sunlight gets weaker the deeper a point is below the surface.
float UW_SunDepthAttenuation(float y)
{
    return exp(-max(0.0, _UW_SurfaceY - y) * _UW_DepthLightFalloff);
}

// Soft ambient: shallow colour from above, deep colour from below.
float3 UW_Ambient(float3 normalWS)
{
    return lerp(_UW_DeepColor.rgb, _UW_ShallowColor.rgb, normalWS.y * 0.5 + 0.5) * _UW_AmbientStrength;
}

float3 UW_CausticsSample(float2 uv, float split, float bias)
{
    float r = SAMPLE_TEXTURE2D_BIAS(_UW_CausticsTex, sampler_UW_CausticsTex, uv + float2(split, split), bias).r;
    float g = SAMPLE_TEXTURE2D_BIAS(_UW_CausticsTex, sampler_UW_CausticsTex, uv, bias).r;
    float b = SAMPLE_TEXTURE2D_BIAS(_UW_CausticsTex, sampler_UW_CausticsTex, uv - float2(split, split), bias).r;
    return float3(r, g, b);
}

// Moving light web projected along the sun direction. Returns light to add
// (multiply by albedo). Fades and blurs with depth, only lights sun-facing surfaces.
float3 UW_Caustics(float3 positionWS, float3 normalWS)
{
    float3 L = _MainLightPosition.xyz;
    float depth = _UW_SurfaceY - positionWS.y;
    // Project onto a horizontal plane along the light so the pattern leans with the sun.
    float2 p = positionWS.xz - L.xz * (positionWS.y / max(L.y, 0.2));
    float2 uv = p * _UW_Caustics.x;
    float t = _Time.y * _UW_Caustics.y;
    float bias = max(0.0, depth) * _UW_Caustics.w;
    float3 a = UW_CausticsSample(uv + float2(t, t * 0.6), _UW_Caustics.z, bias);
    float3 b = UW_CausticsSample(uv * 1.27 + float2(-t * 0.8, t * 0.4) + 0.37, _UW_Caustics.z, bias);
    float3 c = min(a, b);
    float facing = saturate(dot(normalWS, L));
    float underwater = step(0.0, depth);
    return c * facing * underwater * _UW_CausticsColor.rgb * _UW_CausticsColor.a * UW_SunDepthAttenuation(positionWS.y);
}

// 0 = clear, 1 = fully lost in the water. Used directly by additive effects.
float UW_FogAmount(float3 positionWS)
{
    float d = max(0.0, distance(positionWS, _WorldSpaceCameraPos) - _UW_FogStart);
    return 1.0 - exp(-d * _UW_FogDensity);
}

// Colour absorption + exponential fog towards the water colour.
float3 UW_ApplyWater(float3 col, float3 positionWS)
{
    float3 toPos = positionWS - _WorldSpaceCameraPos;
    float dist = length(toPos);
    float3 viewDir = toPos / max(dist, 1e-4);
    float d = max(0.0, dist - _UW_FogStart);
    col *= exp(-d * _UW_Absorption.rgb);
    return lerp(col, UW_WaterColor(viewDir), UW_FogAmount(positionWS));
}

#endif
