Shader "Liminal uwu/Falling hours water"
{
    Properties
    {
        _Gradient ("Cass Bunting water palette", 2D) = "white" {}
        _Noise ("Flow grain", 2D) = "gray" {}
        _Pool ("Horizontal basin", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+10" }
        Cull Off ZWrite On
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ FALLS_MOBILE
            #include "UnityCG.cginc"
            sampler2D _Gradient, _Noise;
            half _Pool;
            struct app { float4 vertex:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f {
                float4 position:SV_POSITION; float2 uv:TEXCOORD0; float3 world:TEXCOORD1;
                UNITY_FOG_COORDS(2) UNITY_VERTEX_OUTPUT_STEREO
            };
            v2f vert(app v) {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.position=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
                UNITY_TRANSFER_FOG(o,o.position);return o;
            }
            half4 frag(v2f i):SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float t=_Time.y;
                float2 flow=i.uv*float2(36,2.6)+float2(0,t*.13);
                if(_Pool>.5)flow=i.world.xz*.09+float2(t*.006,t*.014);
                half grain=tex2D(_Noise,flow).r;
                half streak=tex2D(_Noise,flow*float2(1.83,.61)+float2(.27,t*.055)).g;
                half ribbons=saturate(grain*.64+streak*.6);
                #ifndef FALLS_MOBILE
                    half fine=tex2D(_Noise,flow*float2(3.3,1.4)+float2(.53,t*.07)).b;
                    ribbons=saturate(ribbons*.78+fine*.27);
                #endif
                half foam=pow(saturate(ribbons),3.5);
                half foot=1-smoothstep(0,.12,i.uv.y);
                half head=smoothstep(.92,1,i.uv.y);
                half density=saturate(.34+ribbons*.45+foot*.17+head*.14);
                half3 water=tex2D(_Gradient,float2(density,.5)).rgb;
                water=lerp(half3(.025,.095,.11),water*.65+half3(.04,.13,.15),.8);
                half3 color=water+foam*half3(.24,.42,.43);
                color+=foot*(.03+streak*.10)*half3(.53,.85,.88);
                if(_Pool>.5) {
                    float2 p=i.world.xz*.5;
                    half wave=sin(p.x+sin(p.y*.7+t*.32))*sin(p.y-t*.51)*.5+.5;
                    color=lerp(half3(.012,.04,.05),water,.32)+pow(wave,12)*half3(.07,.14,.16);
                    #ifndef FALLS_MOBILE
                        half3 normal=normalize(half3(sin(p.x+t*.21)*.035,1,cos(p.y-t*.32)*.04));
                        half3 view=normalize(_WorldSpaceCameraPos.xyz-i.world);
                        half4 encoded=UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0,reflect(-view,normal),1.4);
                        half3 reflection=DecodeHDR(encoded,unity_SpecCube0_HDR);
                        half fresnel=.18+.58*pow(1-saturate(dot(view,normal)),4);
                        color=lerp(color,reflection,fresnel);
                    #endif
                }
                half4 result=half4(color,1);UNITY_APPLY_FOG(i.fogCoord,result);return result;
            }
            ENDCG
        }
    }
    Fallback Off
}
