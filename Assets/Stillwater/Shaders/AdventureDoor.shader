Shader "Stillwater/Winking doorway"
{
 Properties { _Color("Glow", Color)=(.25,.85,1,1) }
 SubShader { Tags { "RenderType"="Opaque" } Cull Off
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "UnityCG.cginc"
 struct v2f {float4 pos:SV_POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO};
 v2f vert(appdata_base v) {v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord.xy;return o;}
 fixed4 _Color;
 float stroke(float d,float width) {return 1-smoothstep(width,width+.008,d);}
 fixed4 frag(v2f i):SV_Target {
   UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
   float2 p=(i.uv-.5)*float2(1.75,1);
   float wink=step(.9,frac(_Time.y*.19));
   float eyes=stroke(abs(length((p-float2(-.23,.09))*float2(1,1.8))-.065),.014)*step(p.y,.09);
   float2 e=p-float2(.23,.09);
   eyes=max(eyes,lerp(stroke(abs(length(e*float2(1,1.8))-.065),.014)*step(p.y,.09),stroke(abs(e.y),.014)*step(abs(e.x),.09),wink));
   float mouth=max(stroke(abs(length(p-float2(-.065,-.06))-.075),.012)*step(p.y,-.06),stroke(abs(length(p-float2(.065,-.06))-.075),.012)*step(p.y,-.06));
   float border=pow(saturate(max(abs(p.x)/.875,abs(p.y)/.5)),18);
   float ribbon=.5+.5*sin(p.y*35-_Time.y*.8+sin(p.x*9)*2);
   float3 color=_Color.rgb*(.025+.055*ribbon+.8*border);
   color+=float3(1,.46,.78)*max(eyes,mouth)*1.5;
   return fixed4(color,1);
 }
 ENDCG }
 }
}
