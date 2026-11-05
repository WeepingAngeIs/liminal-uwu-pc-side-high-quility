Shader "Stillwater/Station finish"
{
    Properties
    {
        _MainTex("Finish",2D)="white"{}
        _Field("Weather field",2D)="gray"{}
        _Color("Pigment",Color)=(1,1,1,1)
        _Scale("Metres per repeat",Float)=4
        _Sheen("Glaze",Range(0,1))=.12
        _Flood("Water coverage",Range(0,1))=0
        _RoomProbe("Captured room",Cube)="black"{}
        _ProbeOrigin("Capture position",Vector)=(0,0,0,0)
        _RoomMin("Room minimum",Vector)=(-30,0,-30,0)
        _RoomMax("Room maximum",Vector)=(30,18,30,0)
        _Emission("Luminance",Range(0,2))=0
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma shader_feature_local _ SW_WET
            #pragma shader_feature_local _ SW_MOBILE
            #pragma shader_feature_local _ SW_PRINT
            #pragma shader_feature_local _ SW_GLASS
            #pragma shader_feature_local _ SW_PROBE
            #pragma multi_compile _ VERTEXLIGHT_ON
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            struct Input { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Output
            {
                float4 pos:SV_POSITION;
                float3 world:TEXCOORD0;
                half3 normal:TEXCOORD1;
                float2 uv:TEXCOORD2;
                half3 light:TEXCOORD3;
                UNITY_FOG_COORDS(4)
                UNITY_VERTEX_OUTPUT_STEREO
            };
            sampler2D _MainTex, _Field;
            samplerCUBE _RoomProbe;
            float4 _ProbeOrigin, _RoomMin, _RoomMax;
            half4 _Color;
            float _Scale, _Sheen, _Flood, _Emission;
            Output vert(Input v)
            {
                Output o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos=UnityObjectToClipPos(v.vertex);
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
                o.normal=UnityObjectToWorldNormal(v.normal);
                o.uv=v.uv;
                #ifdef VERTEXLIGHT_ON
                    o.light=Shade4PointLights(unity_4LightPosX0,unity_4LightPosY0,unity_4LightPosZ0,
                        unity_LightColor[0].rgb,unity_LightColor[1].rgb,unity_LightColor[2].rgb,unity_LightColor[3].rgb,
                        unity_4LightAtten0,o.world,o.normal);
                #else
                    o.light=0;
                #endif
                UNITY_TRANSFER_FOG(o,o.pos);
                return o;
            }
            float3 sceneRGB(float3 rgb)
            {
                #ifndef UNITY_COLORSPACE_GAMMA
                    return GammaToLinearSpace(rgb);
                #else
                    return rgb;
                #endif
            }
            float localLamp(float3 d,float3 n,float range)
            {
                float distance2=dot(d,d);
                float lambert=.12+.88*saturate(dot(n,d*rsqrt(max(.16,distance2))));
                float attenuation=1./(1.+range*distance2);
                return lambert*attenuation*attenuation;
            }
            float3 atlasLight(float3 p,float3 n)
            {
                float2 block=floor(p.xz/80.+.5);
                float2 local=p.xz-block*80.;
                float up=saturate(n.y), ciel=saturate(-n.y);
                float facing=saturate(dot(n,float3(-.64,.53,.55)));
                // Moon bounce at night is the minimum exposure; overhangs and footings stay dark.
                float3 ambient=float3(.026,.051,.101)*(.48+.63*facing+.28*up);
                float roomSide=max(abs(local.x),abs(local.y));
                float isHub=(abs(block.x)+abs(block.y)<.1)?1.:0.;
                float isRail=(abs(block.x)+abs(block.y-1.)<.1)?1.:0.;
                float isService=(abs(block.x+1.)+abs(block.y+1.)<.1)?1.:0.;
                float3 diffuse=ambient;
                // Amber, blue and rose lamps in the exchange match the actual six street posts.
                float z=clamp(floor((local.y+16.)/15.+.5),0.,2.)*15.-16.;
                float sx=local.x>=0?1.:-1.;
                float3 sodium=float3(7.*sx-local.x,6.17-p.y,z-local.y);
                float3 neon=float3(7.7*sx-local.x,4.65-p.y,z-local.y);
                diffuse+=isHub*(float3(2.28,.88,.30)*localLamp(sodium,n,.020)
                              +float3(.94,.028,.24)*localLamp(neon,n,.037));
                // Bays receive isolated pools with blue intervening shadows. The practicals
                // illuminate the surrounding material without per-pixel real-time lights.
                float2 nearest=round((local-float2(2.,1.))/float2(16.,17.))*float2(16.,17.)+float2(2.,1.);
                float height=lerp(8.2,3.75,isService);
                float3 bay=float3(nearest.x-local.x,height-p.y,nearest.y-local.y);
                float zone=frac(dot(floor((nearest+2.)/float2(16.,17.)),float2(.3183,.4721))+.37);
                float3 tones=lerp(float3(1.55,.68,.20),float3(.27,.67,1.04),smoothstep(.36,.67,zone));
                tones=lerp(tones,float3(1.20,.90,.32),isService*.85);
                diffuse+=(1.-isHub*.91)*tones*localLamp(bay,n,.028);
                // A cool grazing wash defines the rail spiral far above the terminal floor.
                float heightBand=.5+.5*cos((p.y-7.)*.34);
                float railRim=exp2(-abs(length(local)-17.8)*.17);
                diffuse+=isRail*(float3(.055,.103,.225)*(.50+.50*facing)
                        +railRim*float3(.15,.32,.61)*(.15+.85*heightBand));
                // Reflected light from upper windows models the blue edge of the night sky.
                float wallSky=smoothstep(18.,32.,roomSide)*smoothstep(2.,15.,p.y);
                diffuse+=float3(.047,.098,.165)*wallSky*(.34+.66*facing)*(1.-ciel*.83);
                float foot=smoothstep(-.05,1.1,p.y);
                diffuse*=.78+.22*max(foot,up);
                return diffuse;
            }
            float3 roomDirection(float3 r,float3 p)
            {
                float3 safeR=sign(r+1e-6)*max(abs(r),.0001);
                float3 a=(_RoomMin.xyz-p)/safeR,b=(_RoomMax.xyz-p)/safeR;
                float3 farPlane=max(a,b);
                float hit=max(.05,min(farPlane.x,min(farPlane.y,farPlane.z)));
                return p+r*hit-_ProbeOrigin.xyz;
            }
            float3 windowColor(float3 p,float3 n,float2 uv,float3 v)
            {
                float3 cell=floor(p/float3(4.6,3.22,4.6)+.49);
                float seed=frac(sin(dot(cell,float3(12.41,19.83,8.12)))*7531.83);
                float horizon=saturate(.5+.5*reflect(-v,n).y);
                float3 shade=lerp(float3(.040,.076,.121),float3(.105,.216,.326),horizon);
                float3 inside=lerp(float3(.135,.22,.29),float3(.45,.30,.185),smoothstep(.72,.96,seed));
                float frame=smoothstep(.0,.063,min(min(uv.x,1.-uv.x),min(uv.y,1.-uv.y)));
                float blade=frac(uv.y*(10.+floor(seed*5.))+seed*.13);
                float blinds=lerp(.65,1.,smoothstep(.16,.53,blade));
                float curtain=smoothstep(.22+seed*.3,.37+seed*.3,uv.x);
                float cavity=lerp(.34,.92,curtain)*(.34+.66*uv.y)*blinds;
                float lit=smoothstep(.41,.90,seed)*cavity;
                return sceneRGB(lerp(float3(.035,.053,.068),shade+inside*lit,frame));
            }
            half4 frag(Output i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 p=i.world,n=normalize(i.normal),an=abs(n);
                float2 uv=(an.y>=max(an.x,an.z)?p.xz:(an.x>=an.z?p.zy:p.xy))/_Scale;
                #ifdef SW_PRINT
                    uv=i.uv;
                #endif
                half3 pigment=tex2D(_MainTex,uv).rgb*_Color.rgb;
                float3 lit=atlasLight(p,n)+i.light*.50;
                half3 color=pigment*lit;
                float3 v=normalize(_WorldSpaceCameraPos.xyz-p);
                float fresnel=pow(1.-saturate(dot(n,v)),3.);
                #ifdef SW_GLASS
                    {
                        half3 glass=windowColor(p,n,i.uv,v);
                        UNITY_APPLY_FOG(i.fogCoord,glass);
                        return half4(glass,1);
                    }
                #endif
                #ifdef SW_WET
                    float2 floorUV=p.xz;
                    half3 field=tex2D(_Field,floorUV*.083).rgb;
                    float puddle=lerp(.27+.73*smoothstep(.36,.64,field.r),1.,_Flood);
                    float2 drift=float2(.0016,.0024)*_Time.y;
                    half2 distortion=tex2D(_Field,floorUV*.224+drift).gb-.5;
                    #ifndef SW_MOBILE
                        half2 small=tex2D(_Field,floorUV*.64-drift*1.6).gb-.5;
                        distortion+=small*.40;
                        // A point impact propagates as a fading pair of shallow round waves.
                        float2 cell=floor(floorUV*.70);
                        float2 seed=frac(sin(float2(dot(cell,float2(17.13,39.71)),dot(cell,float2(91.73,12.26))))*4375.71);
                        float2 delta=frac(floorUV*.70)-(.30+.4*seed);
                        float radius=length(delta);
                        float age=frac(_Time.y*.52+seed.x);
                        float edge=radius-age*.29;
                        float ripple=exp2(-abs(edge)*105.)-.45*exp2(-abs(edge+.021)*100.);
                        distortion+=delta/max(.03,radius)*ripple*(1.-age)*.52*puddle;
                    #endif
                    float3 wetNormal=normalize(n+float3(distortion.x,0,distortion.y)*(.053+.065*(1.-puddle)));
                    float3 r=reflect(-v,wetNormal);
                    float2 block=floor(p.xz/80.+.5);
                    color*=lerp(.92,.55,puddle);
                    // Project the actual warm luminaires and hanging rose lettering in the hub.
                    if(abs(block.x)+abs(block.y)<.1)
                    {
                        float2 lightPos=floorUV+r.xz*(6.17/max(.06,r.y));
                        float2 signPos=floorUV+r.xz*(4.65/max(.06,r.y));
                        float lz=clamp(round((lightPos.y+16.)/15.),0.,2.)*15.-16.;
                        float sz=clamp(round((signPos.y+16.)/15.),0.,2.)*15.-16.;
                        float2 delta=abs(float2(abs(lightPos.x)-7.,lightPos.y-lz));
                        float2 lettering=abs(float2(abs(signPos.x)-7.65,signPos.y-sz));
                        float warm=exp2(-delta.x*2.1-delta.y*3.1);
                        float rose=exp2(-lettering.x*.98-lettering.y*3.4);
                        color+=puddle*(.25+fresnel)*(.90*float3(1.,.52,.15)*warm+.72*float3(.96,.075,.43)*rose);
                    }
                    #ifdef SW_PROBE
                        float3 direction=roomDirection(r,p);
                        float roughness=.25+1.45*(1.-puddle)+.12*abs(field.g-.5);
                        half3 reflection=texCUBElod(_RoomProbe,float4(direction,roughness)).rgb;
                        color+=reflection*(.28+.69*fresnel)*puddle;
                    #else
                        float2 support=floorUV+r.xz*min(65.,7./max(.08,r.y));
                        float2 lane=abs(frac((support+float2(2.,1.))/float2(16.,17.))-.5);
                        float wash=exp2(-lane.x*22.-lane.y*13.);
                        color+=float3(.040,.09,.15)*wash*puddle*(.28+.72*fresnel);
                    #endif
                    color+=float3(.0025,.006,.012)*puddle*(.12+.88*fresnel);
                #else
                    float gloss=pow(saturate(dot(reflect(-v,n),normalize(float3(-.34,.84,.40)))),14.);
                    color+=_Sheen*(gloss*.067+fresnel*.025)*float3(.75,.85,.89);
                #endif
                color=lerp(color,pigment,saturate(_Emission))+pigment*max(0.,_Emission-1.);
                UNITY_APPLY_FOG(i.fogCoord,color);
                return half4(color,1);
            }
            ENDCG
        }
        UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
    }
    Fallback "Diffuse"
}

