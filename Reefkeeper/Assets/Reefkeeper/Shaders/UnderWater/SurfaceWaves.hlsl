// Shared directional waves used by underwater and lightweight above-water surfaces.
#ifndef REEFKEEPER_SURFACE_WAVES_INCLUDED
#define REEFKEEPER_SURFACE_WAVES_INCLUDED

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

#endif
