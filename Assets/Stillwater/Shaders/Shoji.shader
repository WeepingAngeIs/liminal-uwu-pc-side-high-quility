Shader "Liminal uwu/Shoji paper"
{
    Properties{
        _MainTex("Paper fibre",2D)="white"{}
        _Color("Lantern colour",Color)=(1,.78,.51,1)
    }
    SubShader{
        Tags{ "RenderType"="Opaque" }
        Pass{
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            struct input{float4 vertex:POSITION;float3 normal:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct output{float4 pos:SV_POSITION;float3 p:TEXCOORD0;float3 n:TEXCOORD1;UNITY_FOG_COORDS(2) UNITY_VERTEX_OUTPUT_STEREO};
            sampler2D _MainTex;
            half4 _Color;
            output vert(input v){
                output o; UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos=UnityObjectToClipPos(v.vertex);o.p=v.vertex.xyz;o.n=v.normal;
                UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            half4 frag(output i):SV_Target{
                float lateral=abs(i.n.x)>.7?i.p.z:i.p.x;
                float level=i.p.y<7.?0.:i.p.y<14.?1.:i.p.y<20.?2.:i.p.y<25.?3.:4.;
                float bay=lateral/(level<1.?2.50:level<2.?2.06:level<3.?2.28:1.74);
                float warmth=.5+.5*cos(floor(bay)*2.3+level*1.62+i.n.z*.85);
                float glow=.41+.54*warmth+.085*cos(i.p.y*1.31);
                half3 paper=tex2D(_MainTex,float2(lateral,i.p.y)/1.45).rgb;
                half3 color=_Color.rgb*glow*(.66+.35*paper);
                UNITY_APPLY_FOG(i.fogCoord,color);return half4(color,1);
            }
            ENDCG
        }
    }
    Fallback "Unlit/Texture"
}
