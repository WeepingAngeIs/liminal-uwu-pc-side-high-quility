#ifndef LIMINAL_RAIL_ATMOSPHERE_INCLUDED
#define LIMINAL_RAIL_ATMOSPHERE_INCLUDED

float _RailHaze;

float3 ApplyRailAtmosphere(float3 color, float3 worldPosition)
{
    if (_RailHaze <= 0.) return color;

    // Integrate only the visible camera-to-surface segment through the hall.
    // The opaque fragment supplies the endpoint, including in stereo views.
    const float3 lower = float3(-32., 0., 47.);
    const float3 upper = float3(32., 48., 113.);
    float3 eye = _WorldSpaceCameraPos.xyz;
    float3 segment = worldPosition - eye;
    const float epsilon = .00001;
    float3 parallel = 1. - step(epsilon, abs(segment));
    float3 outside = step(eye, lower - epsilon) + step(upper + epsilon, eye);
    if (dot(parallel, outside) > 0.) return color;

    float3 inverse = (step(0., segment) * 2. - 1.) / max(abs(segment), epsilon);
    float3 first = (lower - eye) * inverse;
    float3 last = (upper - eye) * inverse;
    float3 nearPlane = lerp(min(first, last), -1.e10, parallel);
    float3 farPlane = lerp(max(first, last), 1.e10, parallel);
    float entry = max(0., max(nearPlane.x, max(nearPlane.y, nearPlane.z)));
    float exit = min(1., min(farPlane.x, min(farPlane.y, farPlane.z)));
    if (exit <= entry) return color;

    // Linear height density has an exact integral at the segment midpoint.
    // Upper ribs receive more haze while the boarding platform stays clear.
    float distanceInHall = (exit - entry) * length(segment);
    float middleHeight = clamp(eye.y + segment.y * (entry + exit) * .5, 0., 48.);
    float opticalDepth = distanceInHall * (.0025 + .00017 * middleHeight) * _RailHaze;
    float transmittance = exp2(-1.442695 * opticalDepth);
    return lerp(float3(.03, .065, .12), color, transmittance);
}

#endif
