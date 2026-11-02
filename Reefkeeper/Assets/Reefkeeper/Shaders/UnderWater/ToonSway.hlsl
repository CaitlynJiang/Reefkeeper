// Cloth sway in the water current for the toon models (stall awning). Shared by the Toon and
// Outline shaders so a cloth and its outline shell move exactly together.
// UV2 (TEXCOORD1): x = sway weight (0 fixed to the frame .. 1+ free edge), y = position across
// the width (0..1), which makes a ripple travel along the cloth. _SwayAmount = 0 means static.
#ifndef REEFKEEPER_TOON_SWAY_INCLUDED
#define REEFKEEPER_TOON_SWAY_INCLUDED

float3 ToonSwayWS(float3 positionOS, float2 sway, float amount, float speed, float lift, float flutterAmt, float4 dir)
{
    float3 ws = TransformObjectToWorld(positionOS);
    float w = max(0.0, sway.x);
    if (amount <= 0.0 && lift <= 0.0 && flutterAmt <= 0.0) return ws;
    float t = _Time.y * speed;
    float3 origin = TransformObjectToWorld(float3(0, 0, 0));
    float ph = sway.y * 6.2831 * 1.2 + dot(origin.xz, float2(0.7, 1.3));      // ripple across the width
    float gust = sin(t - ph) * 0.7 + sin(t * 1.7 - ph * 1.6 + 1.3) * 0.3;     // -1..1, travelling wave
    float2 d = normalize(dir.xy + 1e-4);
    float2 side = float2(-d.y, d.x);
    float flutter = sin(t * 3.1 + ph * 4.0 + w * 5.0);
    float2 push = d * (0.55 + 0.45 * gust) * amount * w + side * flutter * flutterAmt * w;
    ws.xz += push;
    ws.y += gust * lift * w - dot(push, push) * 0.6;                          // billow; keep cloth length
    return ws;
}

#endif
