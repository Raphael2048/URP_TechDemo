Shader "Hidden/UsualReflectionIntensity"
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
		
			Texture2D _ReflectionDepth;
			float4x4 _InvVPMatrix;
			float4 _ReflectionPlane;
			float4 _BlurParams;
		
		
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
				float distance = dot(_ReflectionPlane.xyz, worldpos.xyz) + _ReflectionPlane.w;
				
				return saturate(distance / _BlurParams.x);
			}
		ENDHLSL
		// 0
		// 使用Usual模式时，从深度值反算出距离值
		Pass
		{
			Cull Off
			ZWrite Off

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag_blur_intensity
			ENDHLSL
		}
	}
}