Shader "SDF/SDFRayMarching" {

	Properties
	{
		[NoScaleOffset]
		[Texture] _SDF("SDF", 3D) = "white" {}
		_StepSize("StepSize", Range(0.01, 0.1)) = 0.02 
	}
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

		sampler3D _SDF;
		float _StepSize;
		// Henyey-Greenstein 相函数
		// 夹角余弦，相函数参数值
        float hg(float a, float g) {
            float g2 = g*g;
            return (1-g2) / (4*3.1415*pow(1+g2-2*g*(a), 1.5));
        }
	
	    float map(float3 p)
	    {
	        return tex3D(_SDF, p + 0.5f);
	    }
	
	    
		float4 Fragement(Interpolators interpolators) : SV_Target
		{
			float3 V = interpolators.positionWS -  _WorldSpaceCameraPos;
			//转换到本地坐标系
			float3 dir = mul((float3x3)unity_WorldToObject, V);
			dir = normalize(dir);
			float3 begin = interpolators.positionOS;
			float depth = 0;

			float densitySum = 0;
			UNITY_LOOP
			for(int i = 0; i < 50; ++i)
			{
				float dist = map(begin + depth * dir);
				if(dist < 0)
				{
					densitySum += max(abs(dist), _StepSize);
				}
				// 每次至少向前步进的距离
				depth += max(abs(dist), _StepSize);
				if(depth >= 10)
				{
					break;
				}
			}
			return float4(1, 1, 1, densitySum);
		}
	ENDCG

	SubShader {
		Tags{"RenderType"="Transparent"  "Queue"="Transparent"}
		
		Pass {
			Blend SrcAlpha OneMinusSrcAlpha
			CGPROGRAM
				#pragma enable_d3d11_debug_symbols
				#pragma vertex Vertex
				#pragma fragment Fragement
			ENDCG
		}
	}
}