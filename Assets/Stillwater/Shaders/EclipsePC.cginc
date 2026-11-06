// Disk shear, spiral waves, dust lanes and Doppler beaming adapted from
// tantaneity/urp-black-hole, EvaluateDiskEmission (MIT, copyright 2026 tantaneity).
// See ../ThirdParty/Tantaneity-LICENSE.txt. This Built-in sky implementation uses
// an offline Schwarzschild transfer and four gas layers instead of runtime ray integration.

float3 BlackHoleStars(float3 d,float mip)
{
    float2 uv=float2(atan2(d.z,d.x)/PI2+.5,asin(clamp(d.y,-1.,1.))/3.14159265+.5);
    return tex2Dlod(_Stars,float4(uv,0,mip)).rgb;
}

float4 AccretionGas(float r,float angle,float time,float layer,
                    float2 radialGradient,float2 angularGradient)
{
    float inner=smoothstep(2.02,2.40,r);
    float outer=1.-smoothstep(7.2,11.4,r);
    float mask=inner*outer;
    float omega=rsqrt(r*r*r+.001);
    float phase=angle/PI2-time*.65*omega/PI2;
    float omegaSlope=-1.5*r*r*omega*omega*omega;
    float2 phaseGradient=(angularGradient-time*.65*omegaSlope*radialGradient)/PI2;

    // Explicit derivatives include angular repeats, radial shear and layer depth.
    // The caller removes only the azimuth's integer wrap before this chain rule.
    float2 uv=float2(phase+log(r)*.13-layer*.012,(r-1.4)*.084+layer*.019);
    float2 coarseUGradient=phaseGradient+.13/r*radialGradient;
    float2 coarseVGradient=.084*radialGradient;
    float2 coarseDx=float2(coarseUGradient.x,coarseVGradient.x);
    float2 coarseDy=float2(coarseUGradient.y,coarseVGradient.y);
    float3 coarse=tex2Dgrad(_Plasma,uv,coarseDx,coarseDy).rgb;
    float warp=(coarse.g-.5)*.12;
    // Estimate the warp's change over one pixel using the same filtered footprint.
    // This remains defined inside a conditional disk crossing, unlike ddx(coarse).
    float warpDx=.12*(tex2Dgrad(_Plasma,uv+coarseDx,coarseDx,coarseDy).g-coarse.g);
    float warpDy=.12*(tex2Dgrad(_Plasma,uv+coarseDy,coarseDx,coarseDy).g-coarse.g);
    float2 fineUV=float2(phase*3.+.27,(r-1.4)*.084+warp);
    float2 fineDx=float2(3.*phaseGradient.x,coarseVGradient.x+warpDx);
    float2 fineDy=float2(3.*phaseGradient.y,coarseVGradient.y+warpDy);
    float3 fine=tex2Dgrad(_Plasma,fineUV,fineDx,fineDy).rgb;
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
    // Evaluate screen derivatives before either the outer return or disk branches.
    float2 radiusGradient=float2(ddx(radius),ddy(radius));
    float2 diskGradient=float2(ddx(transfer.b),ddy(transfer.b))*16.;
    float2 secondaryGradient=float2(ddx(transfer.a),ddy(transfer.a))*16.;
    float2 unitYGradient=float2(ddx(unit.y),ddy(unit.y));
    float2 angularGradient=float2(ddx(azimuth),ddy(azimuth))/PI2;
    angularGradient=(angularGradient-round(angularGradient))*PI2;
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
    float pixel=max(abs(radiusGradient.x)+abs(radiusGradient.y),.00005);
    // Near the critical orbit a single pixel spans many source directions.
    // Filter that unresolved light instead of letting point stars flash.
    float skyMip=clamp(log2(1.+pixel/max(radius-edge,.00001)*16.),0.,6.);
    float3 background=BlackHoleStars(source,skyMip);
    float horizon=exp2(-abs(direction.y-.015)*10.);
    background+=float3(.021,.032,.044)*horizon;
    // Finish both emission and absorption before the lookup boundary. The table's
    // outermost crossing still lies inside the material's radial fade.
    float exteriorFade=1.-smoothstep(2.0,2.72,gap);
    if(exteriorFade<=0.) return background;

    float diskRadius=transfer.b*16.;
    float3 light=0;
    float transmission=1.;
    if(diskRadius<11.6)
    {
        [unroll] for(int layer=0;layer<4;layer++)
        {
            float depthScale=(layer-1.5)*.045;
            float depth=depthScale*sqrt(max(diskRadius,1.));
            float2 depthGradient=depthScale*.5*rsqrt(max(diskRadius,1.))*diskGradient;
            depthGradient*=step(1.,diskRadius);
            float tilt=.55+.45*unit.y;
            float radial=diskRadius+depth*tilt;
            float2 layerGradient=diskGradient+depthGradient*tilt+depth*.45*unitYGradient;
            layerGradient*=step(1.6,radial);
            float r=max(1.6,radial);
            float2 layerAngularGradient=angularGradient+depthGradient*.21;
            float4 gas=AccretionGas(r,azimuth+depth*.21,time,layer/3.,layerGradient,layerAngularGradient);
            float opacity=gas.a*.45;
            light+=gas.rgb*transmission*.36;
            transmission*=1.-opacity;
        }
    }
    // The far side of the disk can appear again just outside the critical orbit.
    // Its radius comes from a second crossing of the disk plane by the bent ray.
    if(gap<.28 && transfer.a<.72)
    {
        float secondaryRadius=transfer.a*16.;
        float4 secondary=AccretionGas(secondaryRadius,azimuth+3.14159265,time,.35,secondaryGradient,angularGradient);
        light+=secondary.rgb*transmission*.65;
    }
    float limb=.72+.28*cos(azimuth-2.35);
    float ringWidth=max(pixel*.65,.00048);
    float photon=exp(-(radius-edge)/ringWidth);
    float glow=exp(-gap*11.)*.12+exp(-gap*3.6)*.045;
    light+=float3(4.,3.35,2.48)*photon*(.82+.18*limb);
    light+=float3(.92,.37,.16)*glow;
    light*=exteriorFade;
    transmission=lerp(1.,transmission,exteriorFade);
    float3 display=1.-exp(-max(light,0.)*_DiskExposure);
    #ifndef UNITY_COLORSPACE_GAMMA
        display=GammaToLinearSpace(display);
    #endif
    return background*lerp(.22,1.,transmission)+display;
}
