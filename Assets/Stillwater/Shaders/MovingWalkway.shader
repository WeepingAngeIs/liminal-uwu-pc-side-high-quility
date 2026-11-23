Shader "Liminal uwu/Moving walkway" {
SubShader { Tags { "RenderType"="Opaque" }
Pass {
CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_instancing
#include "UnityCG.cginc"
struct V { float4 pos:SV_POSITION; float2 belt:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
V vert(appdata_full v) {
 V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
 o.pos=UnityObjectToClipPos(v.vertex);
 o.belt=float2(v.vertex.x,v.vertex.z*length(mul((float3x3)unity_ObjectToWorld,float3(0,0,1))));
 return o;
}
fixed4 frag(V i):SV_Target {
 float tread=abs(frac(i.belt.y*2.-_Time.y*2.)-.5);
 float ink=1.-smoothstep(.025,.025+max(.02,fwidth(tread)),tread);
 float arrow=abs(frac(i.belt.y*.65+abs(i.belt.x)*.42-_Time.y*.65)-.5);
 float marking=(1.-smoothstep(.035,.065,arrow))*(1.-smoothstep(.19,.24,abs(i.belt.x)));
 return fixed4(lerp(float3(.09,.14,.15)+ink*.08,float3(.41,.78,.69),max(marking,smoothstep(.45,.48,abs(i.belt.x)))),1);
}
ENDCG
} } }
