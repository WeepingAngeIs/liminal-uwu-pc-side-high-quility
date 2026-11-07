Shader "Liminal uwu/Embankment transmission"
{
    Properties
    {
        _Plasma ("Accretion stream structure", 2D) = "gray" {}
        _Stars ("Distant sky", 2D) = "black" {}
        _Nebula ("Distant nebula", 2D) = "black" {}
        _GeodesicTransfer ("PC gravitational lens transfer", 2D) = "black" {}
        _DiskExposure ("Film exposure", Range(.3,3)) = 1.25
        _BroadcastSecond ("Synchronized observatory time", Float) = 2
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
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
            float _BroadcastSecond, _DiskExposure;
            static const float PI2=6.28318530718;
            static const float3 pole=float3(0,.743144825,.669130606);
            static const float3 north=float3(0,.669130606,-.743144825);
            #ifndef SW_MOBILE
                #include "EclipsePC.cginc"
            #endif

            struct Input {float4 vertex:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Output {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
            Output vert(Input v)
            {
                Output o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;
                return o;
            }

            #ifdef SW_MOBILE
            float3 MobileObservation(float3 direction,float radius,float2 unit,float azimuth,float time)
            {
                float2 stars=float2(atan2(direction.z,direction.x)/PI2+.5,
                                    asin(clamp(direction.y,-1.,1.))/3.14159265+.5);
                float3 background=tex2Dlod(_Nebula,float4(stars+float2(.17,0),0,1)).rgb*.68*smoothstep(.80,1.14,radius);
                float gap=(radius-.183)/.183;
                float fade=1.-smoothstep(1.2,1.65,gap);
                if(fade<=0.)return background;
                float rho=gap/(1.+.23*unit.y+.28*unit.x*unit.x);
                float wheel=azimuth/PI2+.5;
                float3 gas=0;
                float limb=.55+.45*(.5+.5*cos(azimuth-2.35));
                [unroll] for(int layer=0;layer<2;layer++)
                {
                    float radial=max(.001,rho+(layer-.5)*.035);
                    float2 uv=float2(wheel+.29*log2(1.+radial*3.)-time/(72.+radial*175.),
                                     radial*.82+layer*.041);
                    half3 stream=tex2Dlod(_Plasma,float4(uv,0,1.5)).rgb;
                    float hot=stream.r*stream.r;
                    float density=(.24+hot*1.35)*exp2(-radial*(5.7+stream.g*1.55));
                    density*=smoothstep(.002,.065,radial);
                    float3 temperature=lerp(float3(.65,.13,.035),float3(1.45,.77,.31),hot);
                    temperature=lerp(temperature,float3(1.6,1.44,1.2),hot*hot);
                    gas+=temperature*density*.74*limb;
                }
                float ring=exp(-gap/max(.003,fwidth(radius)/.183));
                float3 display=1.-exp(-(gas+float3(3.8,3.0,2.2)*ring)*_DiskExposure);
                #ifndef UNITY_COLORSPACE_GAMMA
                    display=GammaToLinearSpace(display);
                #endif
                return background+display*fade;
            }
            #endif

            half4 frag(Output i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float sec=fmod(max(0.,_BroadcastSecond),24.);
                if(sec>=22.)return half4(0,0,0,1);

                // Three deliberately slow observatory shots. The blackout hides
                // each cut and the loop reset; the aperture never contains debris.
                float localTime,tangent,fade;
                float2 aim;
                float window=0.;
                if(sec<7.8)
                {
                    localTime=saturate(sec/7.8);
                    tangent=lerp(.60,.56,localTime);
                    aim=float2(lerp(-.10,.035,localTime),.10);
                    fade=smoothstep(0.,.75,sec)*(1.-smoothstep(7.05,7.8,sec));
                    window=1.;
                }
                else if(sec<15.1)
                {
                    localTime=saturate((sec-7.8)/7.3);
                    tangent=lerp(.40,.325,localTime);
                    aim=float2(.035-.045*localTime,.018);
                    fade=smoothstep(7.8,8.5,sec)*(1.-smoothstep(14.4,15.1,sec));
                }
                else
                {
                    localTime=saturate((sec-15.1)/6.9);
                    tangent=lerp(.255,.28,localTime);
                    aim=float2(-.018+.027*localTime,-.025);
                    fade=smoothstep(15.1,15.8,sec)*(1.-smoothstep(21.15,22.,sec));
                }
                float2 film=(i.uv-.5)*float2(1.77777778,1.);
                float2 ray=film*(2.*tangent)+aim;
                float3 direction=normalize(pole+float3(ray.x,0,0)+north*ray.y);
                float radius=acos(clamp(dot(direction,pole),-1.,1.));
                float2 plane=float2(direction.x,dot(direction,north));
                float2 unit=plane/max(length(plane),.0001);
                float azimuth=atan2(unit.y,unit.x);
                float3 color=0;
                if(radius>=.183)
                {
                    #ifdef SW_MOBILE
                        color=MobileObservation(direction,radius,unit,azimuth,sec+48.);
                    #else
                        color=BlackHolePC(direction,radius,unit,azimuth,sec+48.);
                    #endif
                }

                // The establishing shot is framed by the observatory's dark
                // mullions and a low sill. They stay at the edge of the picture.
                float2 pixel=max(fwidth(i.uv),.0001);
                float jamb=min(abs(i.uv.x-.075),abs(i.uv.x-.925));
                float frame=window*(1.-smoothstep(.011,.011+pixel.x*1.5,jamb));
                frame=max(frame,window*(1.-smoothstep(.105,.105+pixel.y*1.5,i.uv.y)));
                color=lerp(color,float3(.009,.016,.021),frame);
                float2 corner=(i.uv-.5)*2.;
                color*=1.-.12*saturate(dot(corner,corner)*.65);
                // Quiet cinema framing avoids scanline shimmer on distant screens.
                float letterbox=smoothstep(.045,.045+pixel.y,i.uv.y)*
                                (1.-smoothstep(.955-pixel.y,.955,i.uv.y));
                return half4(color*fade*letterbox,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
