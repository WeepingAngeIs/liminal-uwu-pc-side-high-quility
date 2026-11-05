Shader "Liminal uwu/Embankment transmission"
{
    Properties
    {
        _Film ("Embankment film strip", 2D) = "black" {}
        _BroadcastSecond ("Synchronized receiver time", Float) = 2
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ SW_MOBILE
            #include "UnityCG.cginc"
            sampler2D _Film;
            float4 _Film_TexelSize;
            float _BroadcastSecond;
            struct Input {float4 vertex:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Output {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
            Output vert(Input v)
            {
                Output o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;
                return o;
            }
            half3 sampleFrame(float index,float2 uv,float mip)
            {
                float2 tile=float2(fmod(index,8.),3.-floor(index/8.));
                float2 guard=_Film_TexelSize.xy*float2(8.,4.)*(.60*exp2(mip));
                float2 location=(tile+lerp(guard,1.-guard,saturate(uv)))/float2(8.,4.);
                return tex2Dlod(_Film,float4(location,0,mip)).rgb;
            }
            half4 frag(Output i):SV_Target
            {
                float sec=_BroadcastSecond;
                if(sec>=22.)return half4(0,0,0,1);
                // Hold the unbroken city, play the evacuation, freeze on the empty
                // embankment, and close the monitor's shutter before the next cycle.
                float frame=saturate((sec-4.)/14.5)*31.;
                float fade=smoothstep(0.,.43,sec)*(1.-smoothstep(21.1,22.,sec));
                float2 uv=i.uv;
                float2 corner=uv*2.-1.;
                uv+=corner*dot(corner,corner)*.004;
                float2 px=abs(fwidth(uv))*(_Film_TexelSize.zw/float2(8.,4.));
                float mip=clamp(log2(max(1.,max(px.x,px.y))),0.,3.);
                half3 image=sampleFrame(floor(frame),uv,mip);
                #ifndef SW_MOBILE
                    image=lerp(image,sampleFrame(min(31.,floor(frame)+1.),uv,mip),smoothstep(.18,1.,frac(frame)));
                    float scan=1.-.075*(.5+.5*cos(uv.y*288.*6.28318))*saturate(1.-fwidth(uv.y)*240.);
                    image*=scan;
                #endif
                float edge=1.-.16*dot(corner,corner);
                return half4(image*edge*fade*1.15,1);
            }
            ENDCG
        }
    }
}
