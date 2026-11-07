Shader "Liminal uwu/Orbital hull"
{
    Properties
    {
        _MainTex("Fine hull grain",2D)="gray"{}
        _Color("Hull alloy",Color)=(.53,.61,.67,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma shader_feature_local _ ORBITAL_LITE
            #include "UnityCG.cginc"
            struct Input { float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;float4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Output { float4 pos:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;half4 color:COLOR;UNITY_FOG_COORDS(3) UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex;half4 _Color;
            Output vert(Input v)
            {
                Output o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
                o.normal=UnityObjectToWorldNormal(v.normal);o.uv=v.uv;o.color=v.color;
                UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            half4 frag(Output i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 normal=normalize(i.normal);
                float3 key=normalize(float3(-.38,.67,-.44));
                float diffuse=saturate(dot(normal,key));
                float sky=.5+.5*normal.y;
                float3 light=lerp(float3(.035,.052,.076),float3(.18,.25,.34),sky);
                light+=float3(.75,.80,.86)*diffuse;
                half grain=1;
                float glint=0;
                #ifndef ORBITAL_LITE
                    grain=.88+.22*tex2D(_MainTex,i.uv*float2(38,19)).r;
                    float3 view=normalize(_WorldSpaceCameraPos-i.world);
                    float3 halfway=normalize(key+view);
                    glint=pow(saturate(dot(normal,halfway)),72)*.25;
                #endif
                half3 colour=_Color.rgb*i.color.rgb*grain*light+glint*float3(.73,.86,1.);
                half4 result=half4(colour,1);UNITY_APPLY_FOG(i.fogCoord,result);return result;
            }
            ENDCG
        }
    }
    Fallback Off
}
