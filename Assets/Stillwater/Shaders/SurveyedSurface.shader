Shader "Liminal uwu/Surveyed surface"
{
    Properties{
        _FixtureColor("Fixture irradiance",3D)="black"{}
        _FixtureDirection("Fixture direction",3D)="white"{}
        _FixtureMin("Fixture origin",Vector)=(0,0,0,0)
        _FixtureSize("Fixture size",Vector)=(1,1,1,0)
        _MainTex("Albedo",2D)="white"{}
        _Detail("Normal XY, roughness, occlusion",2D)="gray"{}
        _Tint("Finish pigment",Color)=(1,1,1,1)
        _Metres("Repeat size (m)",Float)=3
        _Wet("Clear coat",Range(0,1))=.3
        _Neutral("Desaturate scan",Range(0,1))=0
        _GroundField("Baked fixture light and contact",2D)="white"{}
        _RoomMin("Room minimum",Vector)=(-30,0,-30,0)
        _RoomMax("Room maximum",Vector)=(30,18,30,0)
        _RoomProbe("Room reflection",Cube)="black"{}
        _ProbeOrigin("Capture origin",Vector)=(0,0,0,0)
        _RailHaze("Rail hall atmosphere",Range(0,2))=1
    }
    SubShader{
        Tags{ "RenderType"="Opaque" }
        Pass{
            Tags {"LightMode"="ForwardBase"}
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma shader_feature_local _ SURVEY_LITE
            #pragma shader_feature_local _ SW_ROOM_FIELD
            #include "UnityCG.cginc"
            #include "FixtureLighting.cginc"
            #pragma shader_feature_local _ SW_FIXTURE_VOLUME
            #ifndef SURVEY_LITE
                #include "RailAtmosphere.cginc"
            #endif
            struct vin{float4 pos:POSITION;float3 n:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct vout{float4 pos:SV_POSITION;float3 p:TEXCOORD0;half3 n:TEXCOORD1;UNITY_FOG_COORDS(2) UNITY_VERTEX_OUTPUT_STEREO};
            sampler2D _MainTex,_Detail;half4 _Tint;float _Metres,_Wet,_Neutral;
            sampler2D _GroundField;samplerCUBE _RoomProbe;
            float4 _RoomMin,_RoomMax,_ProbeOrigin;
            vout vert(vin i){vout o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos=UnityObjectToClipPos(i.pos);o.p=mul(unity_ObjectToWorld,i.pos).xyz;
                o.n=UnityObjectToWorldNormal(i.n);UNITY_TRANSFER_FOG(o,o.pos);return o;}
            float3 practical(float3 displacement,float3 n,float3 radiance,float range)
            {
                float d=dot(displacement,displacement);
                float l=saturate(dot(n,displacement*rsqrt(max(.05,d))));
                return radiance*(.18+.82*l)/(1.+d*range);
            }
            half4 frag(vout i):SV_Target{
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 p=i.p,n=normalize(i.n),a=abs(n);
                float3 t=a.y>max(a.x,a.z)?float3(1,0,0):(a.x>a.z?float3(0,0,1):float3(1,0,0));
                float3 b=a.y>max(a.x,a.z)?float3(0,0,1):float3(0,1,0);
                float2 uv=float2(dot(p,t),dot(p,b))/_Metres;
                half3 base=tex2D(_MainTex,uv).rgb;
                base=lerp(base,dot(base,half3(.2126,.7152,.0722)).xxx,_Neutral)*_Tint.rgb;
                float rough=.55,ao=.95;
                #ifndef SURVEY_LITE
                    half4 data=tex2D(_Detail,uv);float2 tilt=(data.rg-.5)*1.35;
                    n=normalize(n+t*tilt.x+b*tilt.y);rough=data.b;ao=.5+.5*data.a;
                #endif
                float3 eye=normalize(_WorldSpaceCameraPos-p), L=normalize(float3(-.53,.72,-.42));
                float3 light=float3(.115,.175,.26)*(.46+.52*saturate(n.y)+.46*saturate(dot(n,L)));
                float3 specDir=L;
                if(p.z < -106.5 && abs(p.x)<25){
                    float z=-109.-round((-109.-p.z)/5.)*5.;
                    float height=max(1.2,7.9-max(0.,-109.-p.z)*.135);
                    float3 d=float3((p.x<0?-1.:1.)*min(5.8,height*.66),height,z)-p;
                    specDir=normalize(d);
                    light+=float3(.30,.52,.50)+practical(d,n,float3(.99,1.38,1.14),.045);
                }else if(p.x < -103.4 && abs(p.z+80.)<13){
                    float3 d=float3(-104.-round((-104.-p.x)/4.)*4.,max(.95,3.95-(-104.-p.x)*.061),-80.)-p;
                    specDir=normalize(d);light+=float3(.32,.23,.12)+practical(d,n,float3(1.25,.78,.36),.12);
                }else if(p.x>49 && p.z>48){
                    float3 d=float3(90.,4.+round(max(0.,p.y-4.)/6.3)*6.3,94.)-p;
                    light+=float3(.19,.14,.12)+.36*saturate(dot(n,L))+practical(d,n,float3(1.65,.79,.29),.07);
                }else{
                    float3 d=float3(round(p.x/9.)*9.,4.7,round(p.z/13.)*13.)-p;
                    specDir=normalize(d);light+=practical(d,n,float3(.75,.53,.31),.11);
                }
                #ifdef SW_FIXTURE_VOLUME
                    light=FixtureLight(p,n);
                #endif
                float f=pow(1.-saturate(dot(n,eye)),4.);
                #ifdef SW_ROOM_FIELD
                    float2 tile=saturate((p.xz-_RoomMin.xz)/max(_RoomMax.xz-_RoomMin.xz,.02));
                    half4 indirect=tex2D(_GroundField,tile);
                    float nearFloor=1.-smoothstep(.65,4.6,p.y-_RoomMin.y);
                    #ifdef SW_FIXTURE_VOLUME
                        light*=lerp(.70,1.,indirect.a);
                    #else
                        light=lerp(light,light*lerp(.38,1.,indirect.a)+indirect.rgb*.56,nearFloor);
                    #endif
                #endif
                float3 specularTint=float3(.71,.66,.53);
                #ifdef SW_FIXTURE_VOLUME
                    specDir=FixtureIncidentDirection(p,n);specularTint=light;
                #endif
                float highlight=pow(saturate(dot(n,normalize(specDir+eye))),lerp(89.,18.,rough));
                half3 result=base*light*ao+specularTint*highlight*_Wet*(1.-.7*rough);
                #ifndef SURVEY_LITE
                    float3 reflectionRay=reflect(-eye,n);
                    #ifdef SW_ROOM_FIELD
                        float3 divisor=lerp(-1.,1.,step(0.,reflectionRay))*max(.0001,abs(reflectionRay));
                        float3 planes=max((_RoomMax.xyz-p)/divisor,(_RoomMin.xyz-p)/divisor);
                        float tHit=max(.05,min(planes.x,min(planes.y,planes.z)));
                        float3 ray=p+reflectionRay*tHit-_ProbeOrigin.xyz;
                        half3 reflected=texCUBElod(_RoomProbe,float4(ray,.45+rough*2.5)).rgb;
                    #else
                        half4 probe=UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0,reflectionRay,1.+rough*3.);
                        half3 reflected=DecodeHDR(probe,unity_SpecCube0_HDR);
                    #endif
                    result+=reflected*_Wet*(.075+.40*f);
                #endif
                #ifndef SURVEY_LITE
                    result=ApplyRailAtmosphere(result,p);
                #endif
                UNITY_APPLY_FOG(i.fogCoord,result);return half4(result,1);
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}
