Shader "Liminal uwu/Practical glow"
{
    Properties { _Color ("Light color", Color) = (1,.24,.46,1) }
    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Cull Off ZWrite Off Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct Input {float4 vertex:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Output {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
            fixed4 _Color;
            Output vert(Input v){Output o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv*2.-1.;return o;}
            half4 frag(Output i):SV_Target
            {
                float d=dot(i.uv,i.uv);
                float halo=exp(-d*5.5)*saturate(1.-d);
                return half4(_Color.rgb*(_Color.a*halo),0);
            }
            ENDCG
        }
    }
}
