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
            float3 atlasLight(float3 p,float3 n)
            {
                // Pools of sodium and mercury light break long passages into readable intervals.
                float2 gap=abs(frac((p.xz+float2(7.2,9.1))/float2(18.,19.))-.5)*2.;
                float falloff=saturate(1.-dot(gap,gap)*1.32);
                float faceted=.50+.36*saturate(dot(n,normalize(float3(-.43,.7,.56))))+.16*saturate(n.y);
                float facing=saturate(dot(n,normalize(float3(.72,.55,-.33))));
                float warm=saturate(.48+.48*sin(p.x*.042+p.z*.07));
                float3 wash=lerp(float3(.26,.51,.74),float3(.91,.56,.26),warm);
                return float3(.60,.70,.74)*faceted+wash*(.21+falloff*.46)+float3(.18,.105,.055)*facing;
            }
            float3 boxReflection(float3 r,float3 p)
            {
                if(unity_SpecCube0_ProbePosition.w>0)
                {
                    float3 mind=unity_SpecCube0_BoxMin.xyz-p, maxd=unity_SpecCube0_BoxMax.xyz-p;
                    float3 v=float3(r.x>0?maxd.x:mind.x,r.y>0?maxd.y:mind.y,r.z>0?maxd.z:mind.z);
                    v/=sign(r+1e-7)*max(abs(r),.0001);
                    float hit=min(v.x,min(v.y,v.z));
                    r=(p-unity_SpecCube0_ProbePosition.xyz)+r*hit;
                }
                return r;
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
                    // Intersect reflected sightlines with a virtual canopy of strip fixtures.
                    // These distant soft highlights remain legible even without reflection probes.
                    float2 ceilPos=floorUV+r.xz*min(130.,8./max(.062,r.y));
                    float2 lightGrid=abs(frac((ceilPos+float2(3.,1.5))/float2(13.,9.))-.5);
                    float width=max(fwidth(lightGrid.x)*1.6,.012);
                    float width2=max(fwidth(lightGrid.y)*1.6,.014);
                    float bar=(1.-smoothstep(.016,.035+width,lightGrid.x))*(1.-smoothstep(.14,.23+width2,lightGrid.y));
                    float bloom=exp2(-lightGrid.x*11.)*exp2(-lightGrid.y*6.);
                    float warm=frac(floor((ceilPos.x+3.)/13.)*.5);
                    float3 practical=lerp(float3(.23,.54,.70),float3(.82,.38,.12),warm*2.);
                    #ifndef UNITY_COLORSPACE_GAMMA
                        practical=GammaToLinearSpace(practical);
                    #endif
                    color*=lerp(1.,.63,puddle);
                    color+=practical*(bloom*.18+bar*.42)*(.25+.75*fresnel)*puddle;
                    #ifndef SW_MOBILE
                        float3 dir=boxReflection(r,p);
                        half4 probe=UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0,dir,1.15+2.1*(1.-puddle));
                        half3 reflection=DecodeHDR(probe,unity_SpecCube0_HDR);
                        color+=reflection*(.18+.50*fresnel)*puddle;
                    #endif
                    color+=float3(.020,.037,.051)*puddle*(.20+.8*fresnel);
                #else
                    float gloss=pow(saturate(dot(reflect(-v,n),normalize(float3(-.34,.84,.40)))),14.);
                    color+=_Sheen*(gloss*.23+fresnel*.085)*float3(.75,.85,.89);
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

