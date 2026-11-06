// Disk shear, spiral waves, dust lanes and Doppler beaming adapted from
// tantaneity/urp-black-hole, EvaluateDiskEmission (MIT, copyright 2026 tantaneity).
// See ../ThirdParty/Tantaneity-LICENSE.txt. This Built-in sky implementation uses
// an offline Schwarzschild transfer and four gas layers instead of runtime ray integration.

float3 BlackHoleStars(float3 d,float mip)
{
    float2 uv=float2(atan2(d.z,d.x)/PI2+.5,asin(clamp(d.y,-1.,1.))/3.14159265+.5);
    return tex2Dlod(_Stars,float4(uv,0,mip)).rgb;
}

float4 AccretionGas(float r,float angle,float time,float layer,float lod)
{
    float inner=smoothstep(2.02,2.40,r);
    float outer=1.-smoothstep(7.2,11.4,r);
    float mask=inner*outer;
    float omega=rsqrt(r*r*r+.001);
    float phase=angle/PI2-time*.65*omega/PI2;

    // Integer angular repeats keep the azimuth seam continuous.
    float2 uv=float2(phase+log(r)*.13-layer*.012,(r-1.4)*.084+layer*.019);
    float3 coarse=tex2Dlod(_Plasma,float4(uv,0,lod)).rgb;
    float warp=(coarse.g-.5)*.12;
    float3 fine=tex2Dlod(_Plasma,float4(phase*3.+.27,(r-1.4)*.084+warp,0,lod)).rgb;
    float filament=pow(saturate(coarse.r*.72+fine.r*.55),2.4);
    float clump=saturate((fine.g*.6+coarse.b*.4-.60)*4.);
    clump*=clump;
    float arm=.5+.5*sin(angle*3.+7.4*log(r+1.)-time*.035);
    float spirals=lerp(.60,1.2,arm*arm);
    float dust=1.-.76*smoothstep(.43,.79,coarse.b*.60+fine.b*.40);
    float density=(.15+2.65*filament)*spirals*dust;
    float falloff=pow(saturate(2.3/r),1.38);

    // A small apparent tilt makes the approaching limb brighter while the shadow
    // remains circular. The transfer itself is the axisymmetric, face-on solution.
    float approach=cos(angle-2.35);
    float beam=pow(max(.3,1.+.30*approach),3.);
    float heat=saturate(pow(2.35/r,.65)+.13*approach+.24*clump);
    float3 temperature=lerp(float3(.95,.115,.018),float3(1.65,.70,.23),smoothstep(.30,.78,heat));
    temperature=lerp(temperature,float3(2.0,1.84,1.49),smoothstep(.76,1.08,heat));
    float alpha=1.-exp(-mask*(density*.66+clump*.68));
    float3 emission=temperature*(density+.95*clump)*falloff*beam*mask;
    return float4(emission,alpha);
}

float3 BlackHolePC(float3 direction,float radius,float2 unit,float azimuth,float time)
{
    const float edge=.183;
    const float maxGap=2.8;
    float gap=(radius-edge)/edge;
    float lookup=log(1.+max(gap,0.)*64.)/log(180.2);
    float4 transfer=tex2Dlod(_GeodesicTransfer,float4(saturate(lookup),.5,0,0));
    float2 exitDirection=normalize(transfer.rg+float2(0,1e-7));
    float3 tangent=float3(unit.x,0,0)+unit.y*north;
    float3 source=pole*exitDirection.y+tangent*exitDirection.x;
    if(gap>maxGap)
    {
        // Continue the deflection smoothly outside the tabulated field and fade it
        // toward zero behind the observer. There is no texture-boundary ring.
        const float boundary=edge*(1.+maxGap);
        float boundaryAngle=atan2(exitDirection.x,exitDirection.y);
        float bend=(boundary-boundaryAngle)*boundary/max(radius,boundary);
        bend*=1.-smoothstep(1.3,2.8,radius);
        float sourceAngle=radius-bend;
        source=pole*cos(sourceAngle)+tangent*sin(sourceAngle);
    }
    float pixel=max(fwidth(radius),.00005);
    // Near the critical orbit a single pixel spans many source directions.
    // Filter that unresolved light instead of letting point stars flash.
    float skyMip=clamp(log2(1.+pixel/max(radius-edge,.00001)*16.),0.,6.);
    float3 background=BlackHoleStars(source,skyMip);
    float horizon=exp2(-abs(direction.y-.015)*10.);
    background+=float3(.021,.032,.044)*horizon;
    if(gap>maxGap) return background;

    float diskRadius=transfer.b*16.;
    float lod=clamp(log2(max(1.,fwidth(diskRadius)*48.)),0.,5.);
    float3 light=0;
    float transmission=1.;
    [unroll] for(int layer=0;layer<4;layer++)
    {
        float depth=(layer-1.5)*.045*sqrt(max(diskRadius,1.));
        float r=max(1.6,diskRadius+depth*(.55+.45*unit.y));
        float4 gas=AccretionGas(r,azimuth+depth*.21,time,layer/3.,lod);
        float opacity=gas.a*.45;
        light+=gas.rgb*transmission*.36;
        transmission*=1.-opacity;
    }
    // The far side of the disk can appear again just outside the critical orbit.
    // Its radius comes from a second crossing of the disk plane by the bent ray.
    if(gap<.28 && transfer.a<.72)
    {
        float secondaryRadius=transfer.a*16.;
        float secondaryLod=clamp(log2(max(1.,fwidth(secondaryRadius)*48.)),0.,5.);
        float4 secondary=AccretionGas(secondaryRadius,azimuth+3.14159265,time,.35,secondaryLod);
        light+=secondary.rgb*transmission*.65;
    }
    float limb=.72+.28*cos(azimuth-2.35);
    float ringWidth=max(pixel*.65,.00048);
    float photon=exp(-(radius-edge)/ringWidth);
    float glow=exp(-gap*11.)*.12+exp(-gap*3.6)*.045;
    light+=float3(4.,3.35,2.48)*photon*(.82+.18*limb);
    light+=float3(.92,.37,.16)*glow;
    float3 display=1.-exp(-max(light,0.)*_DiskExposure);
    #ifndef UNITY_COLORSPACE_GAMMA
        display=GammaToLinearSpace(display);
    #endif
    return background*lerp(.22,1.,transmission)+display;
}
