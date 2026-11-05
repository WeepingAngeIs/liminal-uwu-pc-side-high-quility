Shader "Stillwater/Eclipse sky"
{
    Properties
    {
        _Plasma ("Accretion stream structure", 2D) = "gray" {}
        _Stars ("Distant sky", 2D) = "black" {}
        _Fragments ("Drifting buildings", 2D) = "black" {}
        _BroadcastSecond ("Observation time", Float) = 2
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
            sampler2D _Plasma, _Stars, _Fragments;
            float _BroadcastSecond;
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

            float3 starChart(float3 d)
            {
                float2 uv=float2(atan2(d.z,d.x)/PI2+.5,asin(clamp(d.y,-1.,1.))/3.14159+.5);
                return tex2Dlod(_Stars,float4(uv,0,0)).rgb;
            }

            float3 orbitingArchitecture(float3 background,float2 sightline,float gap)
            {
                if(_BroadcastSecond<4. || _BroadcastSecond>19. || gap<.065) return background;
                #if defined(SW_MOBILE)
                    #define BUILDING_COUNT 3
                #else
                    #define BUILDING_COUNT 6
                #endif
                [unroll] for(int j=0;j<BUILDING_COUNT;j++)
                {
                    float t=saturate((_BroadcastSecond-4.-j*.78)/(10.+j*.15));
                    float radius=.183+lerp(.25,.014,t);
                    float az=-1.62+.36*j+(.80+.16*j)*t+1.31*t*t;
                    float2 center=radius*float2(cos(az),sin(az));
                    float2 delta=(sightline-center)/lerp(.039,.009,t);
                    if(dot(delta,delta)<1. && t>0. && t<1.)
                    {
                        float roll=(j-2.)*.09+(1.+j*.31)*t;
                        float cs=cos(roll),sn=sin(roll);
                        float2 uv=.5+.5*float2(cs*delta.x+sn*delta.y,cs*delta.y-sn*delta.x);
                        half4 facade=tex2Dlod(_Fragments,float4((j+uv.x)/6.,uv.y,0,1.+t));
                        float opacity=facade.a*smoothstep(0.,.16,t)*(1.-smoothstep(.78,1.,t));
                        background=lerp(background,facade.rgb*.84,opacity);
                    }
                }
                return background;
            }

            half4 frag(Output i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 direction=normalize(i.dir);
                float along=dot(pole,direction);
                float radius=acos(clamp(along,-1.,1.));
                const float edge=.183;
                if(radius<edge) return half4(0,0,0,1);

                float2 plane=float2(direction.x,dot(direction,north));
                float2 unit=plane/max(length(plane),.0001);
                float azimuth=atan2(unit.y,unit.x);
                float time=_Time.y;
                float gap=(radius-edge)/edge;
                // Bend apparent source directions toward the far sky on either side of the shadow.
                float deflection=.058/max(radius,.001)*exp(-max(0.,radius-edge)*1.4);
                float sourceAngle=radius-deflection;
                float3 tangent=float3(unit.x,0,0)+unit.y*north;
                float3 source=pole*cos(sourceAngle)+tangent*sin(sourceAngle);
                float horizon=exp2(-abs(direction.y-.015)*10.);
                float3 bg=starChart(source)+float3(.021,.032,.044)*horizon;
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
                #if !defined(SW_MOBILE)
                    // The lens produces a second, compressed image of the background
                    // at the photon orbit. Its view direction rotates around the shadow.
                    float rev=1.72+log(max(gap+.006,.006))*.76;
                    float3 mirrored=pole*cos(rev)-tangent*sin(rev);
                    bg+=starChart(mirrored)*exp(-gap*7.)*.7;
                    float cold=sin(azimuth*2.+gap*6.-.7)*.5+.5;
                    light+=float3(.035,.11,.17)*cold*exp(-abs(rho-.42)*7.);
                #endif
                float fade=1.-smoothstep(1.2,1.65,gap);
                // Background sampling is in the render colour space. Emission above is
                // expressed as display colours so arc exposure also agrees across platforms.
                #ifndef UNITY_COLORSPACE_GAMMA
                    light=GammaToLinearSpace(max(light,0.));
                #endif
                return half4(orbitingArchitecture(bg+light*fade,unit*radius,gap),1);
            }
            ENDCG
        }
    }
    Fallback Off
}
