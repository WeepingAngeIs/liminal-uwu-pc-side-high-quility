#ifndef LIMINAL_FIXTURE_LIGHTING
#define LIMINAL_FIXTURE_LIGHTING
sampler3D _FixtureColor, _FixtureDirection;
float4 _FixtureMin, _FixtureSize;
half3 FixtureIncidentDirection(float3 world,float3 normal)
{
    float3 uv=saturate((world+normal*.32-_FixtureMin.xyz)/max(.1,_FixtureSize.xyz));
    return normalize(tex3D(_FixtureDirection,uv).rgb*2.-1.+half3(0,.0001,0));
}
half3 FixtureLight(float3 world,float3 normal)
{
    // The colour and dominant direction are baked from the room's actual
    // luminous surfaces with collision occlusion. Two taps on either platform.
    float3 uv=saturate((world+normal*.32-_FixtureMin.xyz)/max(.1,_FixtureSize.xyz));
    half3 energy=tex3D(_FixtureColor,uv).rgb*4.;
    half3 direction=tex3D(_FixtureDirection,uv).rgb*2.-1.;
    direction=normalize(direction+half3(0,.0001,0));
    return half3(.006,.009,.017)+energy*(.26+.74*saturate(dot(normal,direction)));
}
#endif
