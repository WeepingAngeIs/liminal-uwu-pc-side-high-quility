Shader "Liminal uwu/Billboard video"
{
    Properties
    {
        _MainTex ("Video", 2D) = "black" {}
        _VideoActive ("Playing", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Geometry+1" "RenderType"="TransparentCutout" }
        Cull Back ZWrite On
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _VideoActive;
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Output { float4 position:SV_POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Output vert(Input v)
            {
                Output o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.position=UnityObjectToClipPos(v.vertex);o.uv=v.uv;
                return o;
            }
            half4 frag(Output i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                clip(_VideoActive-.5);
                return half4(tex2D(_MainTex,i.uv).rgb,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
