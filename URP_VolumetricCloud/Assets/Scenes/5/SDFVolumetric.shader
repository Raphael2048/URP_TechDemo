Shader "SDF/Volumetric" {

	Properties
	{
		[NoScaleOffset] _SDF("SDF", 3D) = "white" {}
		_StepSize("StepSize", Range(0.001, 0.1)) = 0.02 
		_StepSize2("StepSize2", Range(0.01, 0.1)) = 0.02 
		_PhaseG("PhaseG", Range(-1, 1)) = 0
		[HDR]_SigmaT("SigmaT", Color) = (1, 1, 1)
		[HDR]_SigmaA("SigmaA", Color) = (0, 0, 0)
	}
	CGINCLUDE
		#include "UnityCG.cginc"
		#include "UnityLightingCommon.cginc"
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
		float _StepSize2;
		float _PhaseG;
		float3 _SigmaT;
		float3 _SigmaA;
	
		// Henyey-Greenstein 相函数
		// 夹角余弦，相函数参数值
        float hg(float a, float g) {
            float g2 = g*g;
            return (1-g2) / (4*3.1415*pow(1+g2-2*g*(a), 1.5));
        }

		float3 Transmittance(float d, float3 sigmaE)
        {
			return exp(-d * sigmaE);        
        }
	
	    float map(float3 p)
	    {
	        return tex3D(_SDF, p + 0.5f);
	    }

		float rand(float2 pix)
        {
	        return frac(sin(pix.x * 199 + pix.y) * 1000);
        }
	    
		float4 Fragement(Interpolators interpolators) : SV_Target
		{
			const float3 _SigmaE = _SigmaT + _SigmaA; 
			float3 V = interpolators.positionWS -  _WorldSpaceCameraPos;
			//转换到本地坐标系
			float3 dir = mul((float3x3)unity_WorldToObject, V);
			dir = normalize(dir);
			float3 begin = interpolators.positionOS;
			float depth = rand(interpolators.pos.xy) * _StepSize;

			// 本地空间的光照方向
			float3 localLightDirection = normalize(mul((float3x3)unity_WorldToObject, _WorldSpaceLightPos0.xyz));
			float cosTheta = dot(localLightDirection, dir);
			float phase = hg(cosTheta, _PhaseG);
			
			// xyz 表示当前累积的能量，w表示当前的穿透比率
			float4 ScatteringResult = float4(0, 0, 0, 1);
			UNITY_LOOP
			for(int i = 0; i < 500; ++i)
			{
				float3 currentPos = begin + depth * dir;
				float dist = map(currentPos);
				if(dist < 0)
				{
					//沿着 光源方向，进行二次 RayMarching
					float3 beginPos2 = currentPos;
					float distanceToLight = _StepSize2;
					float t2 = _StepSize2;
					for(int j = 0; j < 30; ++j)
					{
						float dist2 = map(beginPos2 + t2 * localLightDirection);
						if(dist2 < 0)
						{
							distanceToLight += max(_StepSize2, abs(dist2));
						}
						t2 += max(_StepSize2, abs(dist2));
					}
					float3 currentColor = _LightColor0 * Transmittance(distanceToLight, _SigmaE) * phase * UNITY_PI * _SigmaT;
					float3 DeltaT = Transmittance(_StepSize, _SigmaE);
					// 能量守恒的计算方式
					ScatteringResult.xyz += currentColor * (1 - DeltaT)  * ScatteringResult.w / _SigmaE;
					// 能量不守恒的计算方式
					// ScatteringResult.xyz += currentColor * ScatteringResult.w * _StepSize;
					ScatteringResult.w *= DeltaT;
				}
				// 每次至少向前步进的距离
				depth += max(dist, _StepSize);
				if(any(currentPos < -0.5f) || any(currentPos > 0.5f))
				{
					break;
				}
			}
			return ScatteringResult;
		}
	ENDCG

	SubShader {
		Tags{"RenderType"="Transparent"  "Queue"="Transparent"}
		
		Pass {
			Blend  OneMinusSrcAlpha SrcAlpha
			CGPROGRAM
				#pragma enable_d3d11_debug_symbols
				#pragma vertex Vertex
				#pragma fragment Fragement
			ENDCG
		}
	}
}