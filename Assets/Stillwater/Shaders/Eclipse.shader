Shader "Stillwater/Eclipse sky"
{
    Properties
    {
        _Plasma ("Accretion stream structure", 2D) = "gray" {}
        _Stars ("Distant sky", 2D) = "black" {}
        _Nebula ("Distant nebula", 2D) = "black" {}
        _ApproachScale ("Local aperture approach", Range(1,8)) = 1
        _GeodesicTransfer ("PC gravitational lens transfer", 2D) = "black" {}
        _DiskExposure ("PC gas exposure", Range(.3,3)) = 1.45
        _ReviewTime ("Fixed review time (-1 uses live time)", Float) = -1
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ SW_MOBILE
            #include "UnityCG.cginc"
            sampler2D _Plasma, _Stars, _Nebula, _GeodesicTransfer;
            float _ApproachScale, _DiskExposure, _ReviewTime;
            static const float PI2 = 6.28318530718;
            static const float3 pole = float3(0,.743144825,.669130606);
            static const float3 north = float3(0,.669130606,-.743144825);

            struct Input { float4 vertex:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Output { float4 pos:SV_POSITION; float3 dir:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Output vert(Input v)
            {
                Output o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos=UnityObjectToClipPos(v.vertex);
                o.dir=v.vertex.xyz;
                return o;
            }

            // A folded emitting sheet evaluated on the unit sphere. The sky direction
            // stays fixed while three slow displacements deform the gas locally.
            float3 nebulaSheet(float3 d,float drift)
            {
                float3 warp=sin(d.yzx*3.7+d.zxy*1.5+float3(.34,2.4,4.1)
                                +drift*float3(1.,-.72,.53));
                float3 p=d*3.15+warp*1.34;
                float fold=.62*p.y+sin(1.28*p.x-.68*p.z)
                           +.66*sin(1.81*p.z+.52*p.x+.68*sin(1.64*p.y))
                           +.17*sin(2.73*p.x+1.52*p.y);
                float density=clamp(.78+.34*sin(dot(p,float3(.78,.58,-.62)))+.17*warp.y,.10,1.);
                return float3(fold,density,warp.x);
            }

            float3 distantSky(float3 source,float3 direction,float radius,float time,float mip)
            {
                // Radius follows the train's approach scale. Every sky layer fades
                // out together, leaving the existing clear space outside the ring.
                if(radius<=.80) return 0.;
                float2 uv=float2(atan2(source.z,source.x)/PI2+.5,asin(clamp(source.y,-1.,1.))/3.14159265+.5);
                float3 background=tex2Dlod(_Nebula,float4(uv+float2(.17,0),0,mip)).rgb*.68;
                float drift=time*.008;
                float3 sheet=nebulaSheet(source,drift);
                float fold=sheet.x, density=sheet.y;
                float haze=exp2(-.92*fold*fold);
                float thickness=max(fold,0.)*1.35+max(-fold,0.)*5.1;
                float veil=exp2(-thickness*thickness);
                float crest=exp2(-52.*fold*fold);
                float3 gas=float3(.007,.014,.036)
                          +float3(.022,.155,.32)*haze*(.52+.48*density)
                          +float3(.017,.182,.219)*veil*density
                          +float3(.115,.371,.273)*crest*density*density;
                #if !defined(SW_MOBILE)
                    // A slower rear sheet shows through gaps in the nearer one.
                    float3 rear=nebulaSheet(float3(source.z,-source.x,source.y),drift*.61+1.024);
                    float dust=exp2(-5.*(rear.x+.41)*(rear.x+.41));
                    gas+=float3(.105,.061,.215)*exp2(-2.5*rear.x*rear.x)*rear.y*(1.-.72*veil);
                    float echo=fold-.19-.045*sheet.z;
                    gas+=float3(.028,.125,.16)*exp2(-44.*echo*echo)*density*(.5+.5*rear.z);
                    // Reuse the filtered gas texture along the sheet for wisps;
                    // no extra scene texture, transparent shell or raymarch.
                    float2 flowUV=float2(dot(source,float3(.28,0,.14))+.09*sheet.z,.14*fold+.04*sheet.z);
                    float3 stream=tex2Dlod(_Plasma,float4(flowUV,0,max(mip,2.5))).rgb;
                    gas*=1.-.24*(1.-stream.b)*haze;
                    gas+=float3(.033,.083,.105)*haze*density*(.6+.58*stream.r)*stream.r*stream.r;
                    gas*=1.-.33*dust*(1.-.8*crest);
                    background+=tex2Dlod(_Stars,float4(uv,0,mip)).rgb*.15;
                #endif
                #ifndef UNITY_COLORSPACE_GAMMA
                    gas=GammaToLinearSpace(gas);
                #endif
                background+=gas+float3(.021,.032,.044)*exp2(-abs(direction.y-.015)*10.);
                return background*smoothstep(.80,1.14,radius);
            }

            #if !defined(SW_MOBILE)
                #define SW_ANIMATED_NEBULA
                #include "EclipsePC.cginc"
            #endif

            half4 frag(Output i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 direction=normalize(i.dir);
                float along=dot(pole,direction);
                // A local approach controller enlarges the sky projection without
                // moving geometry or adding objects across the protected shadow.
                // At scale 1 this is the reviewed distant aperture exactly.
                float radius=acos(clamp(along,-1.,1.))/clamp(_ApproachScale,1.,8.);
                const float edge=.183;
                if(radius<edge) return half4(0,0,0,1);

                float2 plane=float2(direction.x,dot(direction,north));
                float2 unit=plane/max(length(plane),.0001);
                float azimuth=atan2(unit.y,unit.x);
                float time=_ReviewTime>=0 ? _ReviewTime : _Time.y;
                float gap=(radius-edge)/edge;
                #if !defined(SW_MOBILE)
                    float3 upgraded=BlackHolePC(direction,radius,unit,azimuth,time);
                    return half4(upgraded,1);
                #endif
                // Bend apparent source directions toward the far sky on either side of the shadow.
                float deflection=.058/max(radius,.001)*exp(-max(0.,radius-edge)*1.4);
                float sourceAngle=radius-deflection;
                float3 tangent=float3(unit.x,0,0)+unit.y*north;
                float3 source=pole*cos(sourceAngle)+tangent*sin(sourceAngle);
                float3 bg=distantSky(source,direction,radius,time,0.);
                if(gap>1.65)
                    return half4(bg,1);

                float wheel=azimuth/PI2+.5;
                float approaching=.5+.5*cos(azimuth-2.57);
                float doppler=lerp(.48,1.35,pow(approaching,.85));
                float heat=saturate(.26+.74*approaching);
                // A face-on inner orbit with a slightly inclined, flared outer disc.
                float shoulder=1.0+.23*unit.y+.28*unit.x*unit.x;
                float rho=gap/shoulder;
                float antialias=max(.0015,fwidth(radius)/edge*.55);
                float stroke=max(antialias,.005);
                float critical=exp(-gap*gap/(stroke*stroke));
                float photon=exp(-abs(gap-.023)*100.);
                float secondary=exp(-abs(gap-.057-.006*unit.y)*105.);
                float atmosphere=exp(-rho*4.7)*(.24+.18*approaching);
                float3 plasma=0;
                // Integrate optically thin luminous gas. Layers near the observer
                // occult farther strands, and differential rotation shears the field.
                #if defined(SW_MOBILE)
                    #define GAS_LAYERS 2
                #else
                    #define GAS_LAYERS 7
                #endif
                float transmittance=1.;
                [unroll] for(int s=0;s<GAS_LAYERS;s++)
                {
                    float z=(s+.5)/GAS_LAYERS;
                    float height=(z-.5)*(.052+.18*rho)*(unit.y+.32);
                    float r=max(.0001,rho+height);
                    float orbit=wheel + .29*log2(1.+r*3.) - time/(72.+r*175.);
                    float2 uv=float2(orbit-.017*z,r*.82+.041*z);
                    float footprint=clamp(log2(1.+antialias*170.),.20,3.8);
                    half3 stream=tex2Dlod(_Plasma,float4(uv,0,footprint)).rgb;
                    float hot=stream.r*stream.r;
                    float acceleration=smoothstep(.005,.075,r);
                    float emissivity=exp2(-r*(6.8+1.55*stream.g))*acceleration;
                    float thickness=(.24+1.18*hot)*(.64+.72*stream.g);
                    float opticalDepth=thickness*(4./GAS_LAYERS)*emissivity;
                    float absorb=1.-exp(-opticalDepth);
                    float temperature=saturate((.55+hot*.57)*(.69+.29*heat)-r*.53);
                    float3 gas=lerp(float3(.56,.093,.031),float3(1.30,.54,.142),smoothstep(.13,.70,temperature));
                    gas=lerp(gas,float3(1.44,1.29,1.09),smoothstep(.60,1.,temperature));
                    plasma+=transmittance*absorb*gas*doppler;
                    transmittance*=1.-absorb;
                }
                float3 halo=float3(.68,.255,.084)*atmosphere*doppler;
                float3 light=plasma+halo+float3(1.10,.85,.58)*(critical*1.3+photon*.73)*(.77+.28*doppler)
                           +float3(.91,.58,.32)*secondary*.60;
                float fade=1.-smoothstep(1.2,1.65,gap);
                // Background sampling is in the render colour space. Emission above is
                // expressed as display colours so arc exposure also agrees across platforms.
                #ifndef UNITY_COLORSPACE_GAMMA
                    light=GammaToLinearSpace(max(light,0.));
                #endif
                return half4(bg+light*fade,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
