Shader "Stillwater/Eclipse sky"
{
    Properties { _Horizon("Horizon",Color)=(.15,.22,.25,1) }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct Input { float4 vertex:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Output { float4 pos:SV_POSITION; float3 dir:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            half4 _Horizon;
            Output vert(Input v)
            {
                Output o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos=UnityObjectToClipPos(v.vertex);
                o.dir=v.vertex.xyz;
                return o;
            }
            float hash(float3 p)
            {
                p=frac(p*.1031);
                p+=dot(p,p.yzx+33.33);
                return frac((p.x+p.y)*p.z);
            }
            half4 frag(Output i):SV_Target
            {
                float3 direction=normalize(i.dir);
                const float3 axis=float3(0,.743144825,.669130606);
                float along=dot(axis,direction);
                // Angular distance on the celestial sphere keeps the aperture circular.
                float radius=acos(clamp(along,-1.,1.));
                const float edge=.183;
                if(radius<edge) return half4(0,0,0,1);
                float w=radius-edge;
                float tangent=atan2(direction.x,dot(direction,float3(0,.669130606,-.743144825)));
                float skyHeight=saturate(direction.y);
                float3 color=lerp(float3(.080,.134,.160),float3(.009,.021,.040),pow(skyHeight,.7));
                color+=float3(.030,.035,.028)*exp(-abs(direction.y-.025)*16.);
                // Sparse stationary stars live outside the ring.
                float3 grid=direction*182.;
                float3 cell=floor(grid);
                float star=hash(cell);
                float amp=smoothstep(.986,1.,star);
                float spot=saturate(1.-length(frac(grid)-.5)*3.45);
                color+=amp*spot*spot*float3(.49,.60,.61)*smoothstep(.12,.49,skyHeight);
                float turbulence=.78+.135*sin(tangent*5.+.8)+.058*sin(11.*tangent-1.4)+.033*cos(tangent*23.);
                float twist=sin(4.*tangent+35.*w)*.0007+sin(13.*tangent-19.*w)*.00038;
                float inner=exp(-w*w/.0000032);
                float band=exp(-abs(w-.0037-twist)*420.);
                float sister=exp(-abs(w-.0135-2.2*twist)*630.);
                float filament=exp(-abs(w-.021-3.*twist)*560.)*(.4+.45*cos(3.*tangent));
                float vapor=exp(-w*65.)*.45+exp(-w*18.)*.115;
                color+=turbulence*(float3(1.,.91,.71)*inner+float3(1.,.71,.40)*band+
                        float3(.85,.45,.22)*(sister*.57+filament*.47)+float3(.92,.51,.26)*vapor);
                #ifndef UNITY_COLORSPACE_GAMMA
                    color=GammaToLinearSpace(color);
                #endif
                return half4(color,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
