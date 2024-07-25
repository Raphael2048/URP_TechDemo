Shader "SDF/SDFSphere" {

	CGINCLUDE
		#include "UnityCG.cginc"
		struct VertexData {
			float4 vertex : POSITION;
			float2 uv : TEXCOORD0;
		};

		struct Interpolators {
			float4 pos : SV_POSITION;
			float2 uv : TEXCOORD0;
			float3 positionWS : TEXCOORD1;
			float3 positionOS : TEXCOORD2;
		};

		Interpolators Vertex (VertexData v) {
			Interpolators i;
			i.positionWS = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0f));
			i.positionOS = v.vertex.xyz;
			i.pos = UnityObjectToClipPos(v.vertex);
			i.uv = v.uv;
			return i;
		}

		float sdSphere(float3 p, float r)
		{
			return length(p) - r;
		}
		
	    float map(float3 p)
	    {
	        return sdSphere(p, 0.5);
	    }
		
		float3 calcNormal( in float3 pos )
        {
            float2 e = float2(1.0,-1.0)*0.5773;
            const float eps = 0.0005;
            return normalize( e.xyy*map( pos + e.xyy*eps ) + 
        					  e.yyx*map( pos + e.yyx*eps ) + 
        					  e.yxy*map( pos + e.yxy*eps ) + 
        					  e.xxx*map( pos + e.xxx*eps ) );
        }
	    
		float3 Fragement(Interpolators interpolators) : SV_Target
		{
			float3 V = interpolators.positionWS -  _WorldSpaceCameraPos;
			//转换到本地坐标系
			float3 dir = mul((float3x3)unity_WorldToObject, V);
			dir = normalize(dir);
			float3 begin = interpolators.positionOS;
			float depth = 0;
			for(int i = 0; i < 50; ++i)
			{
				float dist = map(begin + depth * dir);
				if(dist < 0.01f)
				{
					float3 normal = calcNormal(begin + depth * dir);
					float3 lightDirection = mul((float3x3)unity_WorldToObject, _WorldSpaceLightPos0.xyz);
					return float3(0.2, 0.3, 0.4) + saturate(dot(normal, lightDirection)) * 0.5f;
				}
				depth += dist;
				if(depth >= 20)
				{
					discard;
				}
			}
			discard;
			return 0;
		}
	ENDCG

	SubShader {
		Cull Back
		ZTest Always
		ZWrite On
		Tags{"RenderType"="Opaque"  "Queue"="AlphaTest"}
		Pass {
			CGPROGRAM
			#pragma enable_d3d11_debug_symbols
				#pragma vertex Vertex
				#pragma fragment Fragement
			ENDCG
		}

	}
}