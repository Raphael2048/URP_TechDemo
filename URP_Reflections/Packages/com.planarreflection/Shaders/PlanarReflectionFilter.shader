Shader "Hidden/PlanarReflectionFilter"
{
	Properties
	{
		_MainTex("Base (RGB)", 2D) = "white" {}
	}

	SubShader
	{
		Tags{ "Queue" = "Overlay" "RenderType" = "Overlay" }
		
		HLSLINCLUDE
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"

			Texture2D _MainTex;
			float4 _MainTex_TexelSize;
			Texture2D _ReflectionDepth;
			Texture2D _PlanarReflectionIntensity;
			float4x4 _InvVPMatrix;
			float4 _Plane;
			float4 _BlurParams;
			SamplerState sampler_LinearClamp;

			#define _BlurIntensity 1

			const static int kTapCount = 5;
	        const static float kOffsets[] = {
	            -3.23076923,
	            -1.38461538,
	             0.00000000,
	             1.38461538,
	             3.23076923
	        };
	        const static half kCoeffs[] = {
	             0.07027027,
	             0.31621622,
	             0.22702703,
	             0.31621622,
	             0.07027027
	        };
		
			struct Attributes
	        {
	            float4 positionOS       : POSITION;
	            float2 uv               : TEXCOORD0;
	        };

	        struct Varyings
	        {
	            float2 uv        : TEXCOORD0;
	            float4 vertex : SV_POSITION;
	        };

	        Varyings vert(Attributes input)
	        {
	            Varyings output = (Varyings)0;

	            VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
	            output.vertex = vertexInput.positionCS;
	            output.uv = input.uv;

	            return output;
	        }

			float frag_blur_intensity(Varyings i) : SV_Target
			{
				float d = _ReflectionDepth[i.vertex.xy];
			#if !UNITY_REVERSED_Z
				d = d * 2 - 1;
			#endif
				float4 clippos = float4(i.uv * 2 - 1, d, 1);
				float4 worldpos = mul( _InvVPMatrix, clippos);
				worldpos /= worldpos.w;
				float distance = dot(_Plane.xyz, worldpos.xyz) + _Plane.w;
				
				return saturate(distance / _BlurParams.x);
			}

			half4 blur_h(Varyings input) : SV_Target
	        {
	            float texelSize = _MainTex_TexelSize.x * _BlurIntensity;
	            half3 color = 0;
	            UNITY_UNROLL
	            for (int i = 0; i < kTapCount; i++)
	            {
	                color += SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, input.uv + float2(texelSize * kOffsets[i], 0)).rgb * kCoeffs[i];
	            }
	            return half4(color, 1);
	        }

	        half4 blur_v(Varyings input) : SV_Target
	        {
	            float texelSize = _MainTex_TexelSize.y * _BlurIntensity;
	            half3 color = 0;
	            UNITY_UNROLL
	            for (int i = 0; i < kTapCount; i++)
	            {
	                color += SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, input.uv + float2(0, texelSize * kOffsets[i])).rgb * kCoeffs[i];
	            }
	            return half4(color, 1);
	        }

			half4 blur_h_adaptive(Varyings input) : SV_Target
	        {
	            half BaseIntensity = SAMPLE_TEXTURE2D_X(_PlanarReflectionIntensity, sampler_LinearClamp, input.uv);
	            float texelSize = _MainTex_TexelSize.x * _BlurIntensity * BaseIntensity;
	            
	            float4 acc = 0;
	            UNITY_UNROLL
	            for (int i = 0; i < kTapCount; i++)
	            {
	                float2 SampleCoord = input.uv + float2(texelSize * kOffsets[i], 0);
	                half sampleIntensity = SAMPLE_TEXTURE2D_X(_PlanarReflectionIntensity, sampler_LinearClamp, SampleCoord);
	                half3 sampleColor = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, SampleCoord);

	                float weight = saturate(1.0 - (sampleIntensity - BaseIntensity));
	                acc += half4(sampleColor, 1) * kCoeffs[i] * weight;
	            }
	            acc.xyz /= acc.w + 1e-4;
	            return half4(acc.xyz, 1.0);
	        }

	        float4 blur_v_adaptive(Varyings input) : SV_Target
	        {
	            half BaseIntensity = SAMPLE_TEXTURE2D_X(_PlanarReflectionIntensity, sampler_LinearClamp, input.uv);
	            float texelSize = _MainTex_TexelSize.y * _BlurIntensity * BaseIntensity;
	            
	            float4 acc = 0;
	            UNITY_UNROLL
	            for (int i = 0; i < kTapCount; i++)
	            {
	                float2 SampleCoord = input.uv + float2(0, texelSize * kOffsets[i]);
	                half sampleIntensity = SAMPLE_TEXTURE2D_X(_PlanarReflectionIntensity, sampler_LinearClamp, SampleCoord);
	                half3 sampleColor = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, SampleCoord);

	                float weight = saturate(1.0 - (sampleIntensity - BaseIntensity));
	                acc += half4(sampleColor, 1) * kCoeffs[i] * weight;
	            }
	            acc.xyz /= acc.w + 1e-4;
	            return half4(acc.xyz, 1.0);
	        }
		ENDHLSL
		// 0
		Pass
		{
			Cull Off
			ZWrite Off

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment blur_h
			ENDHLSL
		}
		// 1
		Pass
		{
			Cull Off
			ZWrite Off

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment blur_v
			ENDHLSL
		}
		
		Pass
        {
            Name "Blur H Adaptive"
            HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment blur_h_adaptive
            ENDHLSL
        }
        
        Pass
        {
            Name "Blur V Adaptive"
            HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment blur_v_adaptive
            ENDHLSL
        }
	}
}