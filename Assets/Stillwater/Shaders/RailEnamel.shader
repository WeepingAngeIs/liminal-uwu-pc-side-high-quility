Shader "Stillwater/Carriage enamel"
{
    Properties
    {
        _Color ("Lacquer", Color) = (.2,.55,.51,1)
        _Gloss ("Polish", Range(0,1)) = .65
        _Finish ("Paint, moquette, rubber, brass", Float) = 0
        _Moquette ("Woven upholstery", 2D) = "white" {}
        _Wear ("Finish grain", 2D) = "gray" {}
        _RailHaze ("Rail hall atmosphere", Range(0,2)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ RAIL_MOBILE
            #include "UnityCG.cginc"
            #ifndef RAIL_MOBILE
                #include "RailAtmosphere.cginc"
            #endif

            half4 _Color;
            half _Gloss, _Finish;
            sampler2D _Moquette, _Wear;
            struct app {
                float4 vertex:POSITION; float3 normal:NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f {
                float4 pos:SV_POSITION; half3 normal:TEXCOORD0; float3 world:TEXCOORD1;
                float3 local:TEXCOORD2; half3 localN:TEXCOORD3; float3 eyeOS:TEXCOORD5;
                UNITY_FOG_COORDS(4) UNITY_VERTEX_OUTPUT_STEREO
            };
            v2f vert(app v) {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos=UnityObjectToClipPos(v.vertex);
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
                o.normal=UnityObjectToWorldNormal(v.normal); o.local=v.vertex.xyz; o.localN=v.normal;
                o.eyeOS=ObjSpaceViewDir(v.vertex);
                UNITY_TRANSFER_FOG(o,o.pos); return o;
            }
            // Triangular grooves average to a flat surface when narrower than one screen pixel.
            half ribs(float q) {
                float fade=saturate(1.0-fwidth(q)*1.85);
                return (1.0-smoothstep(.07,.31,abs(frac(q)-.5)))*fade;
            }
            half4 frag(v2f i):SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 p=i.local;
                half3 n=normalize(i.normal), ln=normalize(i.localN);
                half3 view=normalize(_WorldSpaceCameraPos.xyz-i.world);
                half3 localView=normalize(i.eyeOS);
                half fres=pow(1.0-saturate(dot(n,view)),3.0);
                half3 pigment=_Color.rgb;
                half rough=_Gloss;
                half cloth=0;
                float2 surface=abs(ln.x)>.67 ? p.zy : abs(ln.y)>.67 ? p.xz : p.xy;

                if(_Finish>.5 && _Finish<1.5) {
                    // A single unbroken cloth cut wraps the face, edge and crown of each pad.
                    float2 uv=float2(p.x,(p.y+p.z))/.72;
                    pigment=tex2D(_Moquette,uv).rgb;
                    cloth=1;
                }
                #ifndef RAIL_MOBILE
                    half4 grain=tex2D(_Wear,surface*.38);
                    half micro=tex2D(_Wear,surface*4.1).r;
                    if(_Finish<.5) {
                        pigment*=.80+.17*grain.r+.16*micro;
                        float skirting=saturate((1.28-p.y)*3.0);
                        pigment*=1.0-.19*skirting*(1.0-grain.g);
                        if(abs(p.x)>1.44 || (p.y>2.82 && abs(ln.y)>.65))
                            pigment*=1.0-.34*ribs((p.z+.31)/1.29);
                        rough*=.72+.32*micro;
                    }
                    else if(_Finish>1.5 && _Finish<2.5) {
                        half groove=ribs(p.x*72.);
                        half grit=smoothstep(.57,.75,micro);
                        pigment=pigment*(.61+.40*grit+.24*grain.g)*(1.-.38*groove);
                        rough*=1.25-groove*.8;
                        // Separate rubber runners have pressed butt joints.
                        if(p.y<.42) pigment*=1.-.42*ribs((p.z+.23)/1.21);
                    }
                    else if(_Finish>2.5) {
                        float stroke=surface.x*9.+surface.y*.4;
                        half draw=tex2D(_Wear,float2(stroke,surface.y*.73)).g;
                        pigment*=.68+.36*grain.g+.22*draw;
                        rough*=.68+.44*draw;
                    }
                #endif

                // Treat the two fluorescent ceiling bars as area lights in coach space.
                // The highlight slides along the full diffuser when the passenger moves.
                float nearBar=p.z<0 ? -1.92 : 1.92;
                float lampZ=clamp(p.z,nearBar-1.08,nearBar+1.08);
                float3 ray=float3(0,2.74,lampZ)-p;
                float dist2=dot(ray,ray);
                half3 lampDir=ray*rsqrt(max(.005,dist2));
                half ndl=saturate(dot(ln,lampDir));
                half lamps=1.0/(1.0+.37*dist2);
                half seatDistance=length(float2(max(0.,abs(p.x-.69)-.49),max(0.,abs(abs(p.z)-1.92)-.36)));
                half floorContact=saturate(1.-seatDistance/.60)*saturate((.71-p.y)*4.);
                half edgeContact=smoothstep(1.09,1.54,abs(p.x))*saturate((1.52-p.y)*.92);
                half ao=1.-.54*floorContact-.21*edgeContact;
                half sky=.075+.06*saturate(dot(n,normalize(half3(-.6,.74,.27))))+.025*max(0.,ln.y);
                half3 light=sky*half3(.81,.91,1.) + (ndl*.85+.18)*lamps*half3(1.,.73,.45);
                half3 col=pigment*light*ao;
                half spec=pow(saturate(dot(ln,normalize(lampDir+localView))),lerp(10,62,rough));
                col+=rough*lamps*spec*half3(.65,.42,.20)*ao;
                col+=fres*(cloth>.5 ? half3(.060,.075,.079) : rough*half3(.075,.14,.17));
                #ifndef RAIL_MOBILE
                    if(cloth<.5) {
                        half3 r=reflect(-view,n);
                        half4 probe=UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0,r,lerp(5.1,2.0,rough));
                        col+=DecodeHDR(probe,unity_SpecCube0_HDR)*(.06+.19*fres)*rough*ao;
                    }
                #endif
                #ifndef RAIL_MOBILE
                    col=ApplyRailAtmosphere(col,i.world);
                #endif
                UNITY_APPLY_FOG(i.fogCoord,col);
                return half4(col,1);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
