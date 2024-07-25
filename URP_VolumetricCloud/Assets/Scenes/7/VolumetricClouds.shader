Shader "SDF/Noise" {

	Properties
	{
		[NoScaleOffset] _Distribute("Distribute", 2D) = "white" {}
		[NoScaleOffset] _Noise("Noise", 3D) = "white" {}
		_NoiseScale("NoiseScale", Range(1, 10)) = 5
		_NoiseSpeed("NoiseSpeed", Range(0, 0.3)) = 0.3
		_NoiseIntensity("NoiseIntensty", Range(0, 1)) = 0.5
		_StepSize("StepSize", Range(0.001, 0.1)) = 0.02 
		_StepSize2("StepSize2", Range(0.01, 0.1)) = 0.01
		_SigmaT("SigmaT", Range(0, 200)) = 15
		_AmbientIntensity("AmbientIntensity", Range(0, 0.1)) = 0.02
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

		sampler2D _Distribute;
		sampler3D _Noise;
		float _NoiseScale;
		float _NoiseSpeed;
		float _NoiseIntensity;
		float _StepSize;
		float _StepSize2;
		float _SigmaT;
		float _AmbientIntensity;
	
		// Henyey-Greenstein 相函数
		// 夹角余弦，相函数参数值
        float hg(float a, float g) {
            float g2 = g*g;
            return (1-g2) / (4*3.1415*pow(1+g2-2*g*(a), 1.5));
        }

		float hg2(float a)
        {
        	return lerp(hg(a, -0.3), hg(a, 0.3), 0.7);
        }

		float rand(float2 pix)
        {
	        return frac(sin(pix.x * 199 + pix.y) * 1000);
        }

		float3 Transmittance(float d, float3 sigmaE)
        {
			return exp(-d * sigmaE);
        }

		float remap(float x, float low1, float high1, float low2, float high2){
            return low2 + (x - low1) * (high2 - low2) / (high1 - low1);
        }

		// http://magnuswrenninge.com/wp-content/uploads/2010/03/Wrenninge-OzTheGreatAndVolumetric.pdf
		// 模拟多次散射的效果
		float multipleOctaves(float depth, float mu)
        {
			float luminance = 0;
        	int octaves = 4;
        	// Attenuation
		    float a = 1.0;
		    // Contribution
		    float b = 1.0;
		    // Phase attenuation
		    float c = 1.0;
		    
		    float phase;

        	for(int i = 0; i < octaves; ++i)
        	{
        		phase = lerp(hg( mu, -0.3 * c), hg(mu, 0.3 * c), 0.7f);
        		luminance += b * phase * Transmittance(depth * a, _SigmaT);
        		a *= 0.2f;
        		b *= 0.5f;
        		c *= 0.5f;
        	}
        	// return hg2(mu) * Transmittance(depth, _SigmaT);
        	return luminance;
        }
	

		float cloud(float3 p)
        {
	        p += 0.5f;
        	if(p.y < 0 || p.y > 0.1) return 0;
        	float cloud = tex2D(_Distribute, p.xz);
        	if(cloud == 0) return 0;
        	
        	float cloudHeight = p.y * 10;
        	// 
        	float height = pow(cloud, 0.75);
        	cloud *= saturate(remap(cloudHeight, 0.0, 0.25 * (1.0-cloud), 0.0, 1.0))
           * saturate(remap(cloudHeight, 0.75 * height, height, 1.0, 0.0));
        	float noise = tex3D(_Noise, p * _NoiseScale + _Time.z * _NoiseSpeed);
        	cloud = saturate(remap(cloud, noise * _NoiseIntensity, 1, 0, 1));
        	return cloud;
        }

		// https://gist.github.com/DomNomNom/46bb1ce47f68d255fd5d
		// Compute the near and far intersections using the slab method.
		// No intersection if tNear > tFar.
		float2 intersectAABB(float3 rayOrigin, float3 rayDir, float3 boxMin, float3 boxMax) {
		    float3 tMin = (boxMin - rayOrigin) / rayDir;
		    float3 tMax = (boxMax - rayOrigin) / rayDir;
		    float3 t1 = min(tMin, tMax);
		    float3 t2 = max(tMin, tMax);
		    float tNear = max(max(t1.x, t1.y), t1.z);
		    float tFar = min(min(t2.x, t2.y), t2.z);
		    return float2(tNear, tFar);
		}
	    
		float4 Fragement(Interpolators interpolators) : SV_Target
		{
			float3 ObjectSpaceCameraPos = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1)).xyz;
			float3 dir = normalize(interpolators.positionOS - ObjectSpaceCameraPos);

			float2 inter = intersectAABB(ObjectSpaceCameraPos, dir, float3(-0.5, -0.5, -0.5), float3(0.5, 0.5, 0.5));
			float3 begin = ObjectSpaceCameraPos +  dir * max(inter.x, 0);
			
			float depth =  _StepSize * rand(interpolators.pos.xy);

			// 本地空间的光照方向
			float3 localLightDirection = normalize(mul((float3x3)unity_WorldToObject, _WorldSpaceLightPos0.xyz));
			float cosTheta = dot(localLightDirection, dir);
			float phase = hg2(cosTheta);
			
			// xyz 表示当前累积的能量，w表示当前的穿透比率
			float4 ScatteringResult = float4(0, 0, 0, 1);
			UNITY_LOOP
			for(int i = 0; i < 500; ++i)
			{
				float3 currentPos = begin + depth * dir;

				float densityNow = cloud(currentPos);
				
				float distanceToLight = _StepSize2;
				if(densityNow > 0)
				{
					//沿着 光源方向，进行二次 RayMarching
					float3 beginPos2 = currentPos;

					float t2 = _StepSize2;
					for(int j = 0; j < 200; ++j)
					{
						float3 currentPos2 = beginPos2 + t2 * localLightDirection;
						distanceToLight += cloud(currentPos2) * _StepSize2;
						t2 += _StepSize2;
					}

					// 模拟环境光照
					float3 ambient = _LightColor0 * lerp((0.2), (0.8), (currentPos.y + 0.5) * 10) * _AmbientIntensity;
					float3 currentColor = _LightColor0 * multipleOctaves(distanceToLight, cosTheta) * phase * UNITY_PI * _SigmaT * densityNow;
					currentColor += ambient * _SigmaT * densityNow;
					
					float DeltaT = Transmittance(_StepSize , _SigmaT * densityNow);
					ScatteringResult.xyz += currentColor * (1 - DeltaT) / (_SigmaT * densityNow) * ScatteringResult.w;
					// ScatteringResult.xyz += currentColor * ScatteringResult.w * _StepSize;
					ScatteringResult.w *= DeltaT;
				}
				// 每次至少向前步进的距离
				depth += _StepSize;
				if(any(currentPos < -0.5) || any(currentPos > 0.5)) break;
				if(currentPos.y > 0.5f || currentPos.y < -0.5f)
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
			Cull Front
			CGPROGRAM
				#pragma enable_d3d11_debug_symbols
				#pragma vertex Vertex
				#pragma fragment Fragement
			ENDCG
		}
	}
}