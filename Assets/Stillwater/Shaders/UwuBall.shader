Shader "Liminal uwu/Soft terminal ball"
{
    Properties {
        _Color("Porcelain rubber", Color)=(.65,.87,1,1)
        _FixtureColor("Fixture irradiance",3D)="black"{}
        _FixtureDirection("Fixture direction",3D)="white"{}
        _FixtureMin("Fixture origin",Vector)=(0,0,0,0)
        _FixtureSize("Fixture size",Vector)=(1,1,1,0)
    }
    SubShader {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma shader_feature_local BALL_LITE
            #pragma shader_feature_local _ SW_FIXTURE_VOLUME
            #include "UnityCG.cginc"
            #include "FixtureLighting.cginc"
            struct input { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct varying { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; half3 normal:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            varying vert(input v) {
                varying o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos=UnityObjectToClipPos(v.vertex); o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
                o.normal=UnityObjectToWorldNormal(v.normal); o.uv=v.uv; return o;
            }
            fixed4 _Color;
            float stroke(float d, float w) { return 1-smoothstep(w,w+.012,d); }
            fixed4 frag(varying i):SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 n=normalize(i.normal), eye=normalize(_WorldSpaceCameraPos-i.world);
                half light=saturate(dot(n,normalize(half3(-.35,.8,-.48))));
                half gloss=pow(saturate(dot(n,normalize(eye+half3(-.35,.8,-.48)))),34);
                half rim=pow(1-saturate(dot(n,eye)),3);
                half3 color=_Color.rgb*(.22+.55*light)+gloss*.65+rim*.13;
                #ifdef SW_FIXTURE_VOLUME
                    half3 fixture=FixtureLight(i.world,n);
                    color=_Color.rgb*fixture+(gloss*.36+rim*.05)*fixture;
                #endif
                #ifndef BALL_LITE
                    half4 reflected=UNITY_SAMPLE_TEXCUBE(unity_SpecCube0,reflect(-eye,n));
                    color+=DecodeHDR(reflected,unity_SpecCube0_HDR)*.13;
                #endif
                // The face repeats on the opposite hemisphere, so either side
                // of a rolling ball remains recognizably uwu. No texture copies.
                float2 p=float2((frac(i.uv.x*2+.5)-.5)*2,(i.uv.y-.5)*2);
                float left=stroke(abs(length((p-float2(-.27,.11))*float2(1,1.45))-.13),.022)*step(p.y,.11);
                float right=stroke(abs(length((p-float2(.27,.11))*float2(1,1.45))-.13),.022)*step(p.y,.11);
                float mouth=max(stroke(abs(length(p-float2(-.095,-.16))-.103),.019)*step(p.y,-.16),stroke(abs(length(p-float2(.095,-.16))-.103),.019)*step(p.y,-.16));
                float blush=(1-smoothstep(.06,.11,length((p-float2(-.38,-.12))*float2(1,1.6))))+(1-smoothstep(.06,.11,length((p-float2(.38,-.12))*float2(1,1.6))));
                color=lerp(color,half3(1,.28,.57),saturate(blush)*.5);
                color=lerp(color,half3(.035,.055,.09),max(max(left,right),mouth));
                return fixed4(color,1);
            }
            ENDCG
        }
    }
    Fallback "Unlit/Color"
}
