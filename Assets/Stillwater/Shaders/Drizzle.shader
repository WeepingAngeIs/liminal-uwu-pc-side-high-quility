Shader "Stillwater/Drizzle"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct Input { float4 vertex:POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varying { float4 pos:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            Varying vert(Input v)
            {
                Varying o;
                UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos=UnityObjectToClipPos(v.vertex); o.color=v.color; o.uv=v.uv;
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
                return o;
            }
            fixed4 frag(Varying i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 ray=normalize(i.world-_WorldSpaceCameraPos);
                // Keep the angular aperture entirely clear, including foreground rain.
                clip(.9826-dot(ray,float3(0,.743144825,.669130606)));
                float2 p=(i.uv-.5)*2.;
                return fixed4(i.color.rgb,i.color.a*saturate(1.-abs(p.x))*saturate(1.-p.y*p.y));
            }
            ENDCG
        }
    }
    Fallback Off
}
