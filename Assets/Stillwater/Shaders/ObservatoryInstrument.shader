Shader "Liminal uwu/Observatory instrument"
{
    Properties
    {
        _MainTex ("Enamel and dial markings", 2D) = "white" {}
        _BumpMap ("Stamped metal relief", 2D) = "bump" {}
        _MetallicGlossMap ("Metal and gloss", 2D) = "black" {}
        _Lamp ("Header strip center", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            sampler2D _MainTex, _BumpMap, _MetallicGlossMap;
            float4 _Lamp;
            struct Interpolators {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 world : TEXCOORD1;
                half3 normal : TEXCOORD2;
                half4 tangent : TEXCOORD3;
                UNITY_FOG_COORDS(4)
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Interpolators vert(appdata_full v)
            {
                Interpolators o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.position = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.tangent = half4(UnityObjectToWorldDir(v.tangent.xyz), v.tangent.w * unity_WorldTransformParams.w);
                o.uv = v.texcoord.xy;
                UNITY_TRANSFER_FOG(o, o.position);
                return o;
            }
            half4 frag(Interpolators i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 albedo = tex2D(_MainTex, i.uv).rgb;
                half4 finish = tex2D(_MetallicGlossMap, i.uv);
                half3 relief = UnpackNormal(tex2D(_BumpMap, i.uv));
                half3 normal = normalize(i.normal);
                half3 bitangent = cross(normal, i.tangent.xyz) * i.tangent.w;
                normal = normalize(normal * relief.z + i.tangent.xyz * relief.x + bitangent * relief.y);
                float3 closest = _Lamp.xyz;
                closest.x += clamp(i.world.x - closest.x, -1.5, 1.5);
                half3 lightDir = normalize(closest - i.world);
                half3 eyeDir = normalize(_WorldSpaceCameraPos.xyz - i.world);
                half diffuse = saturate(dot(normal, lightDir));
                half gloss = pow(saturate(dot(normal, normalize(lightDir + eyeDir))), lerp(9.0h, 68.0h, finish.a));
                half3 warm = half3(1.0h, .85h, .63h);
                half3 enamel = albedo * (half3(.67h, .72h, .77h) + warm * diffuse * .58h);
                enamel += warm * gloss * lerp(.035h, .24h, finish.r);
                half4 result = half4(enamel, 1.0h);
                UNITY_APPLY_FOG(i.fogCoord, result);
                return result;
            }
            ENDCG
        }
    }
    Fallback "Unlit/Texture"
}
