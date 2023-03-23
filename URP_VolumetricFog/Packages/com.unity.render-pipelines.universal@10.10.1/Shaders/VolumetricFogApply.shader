Shader "Unlit/VolumeApply"
{   
	SubShader
	{
		Pass 
		{
			ZTest Always Cull Off ZWrite Off
			Blend One SrcAlpha

			HLSLPROGRAM
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
			#pragma multi_compile_local _ _IgnoreSkybox
			#pragma vertex ProceduralVert
			#pragma fragment frag
			
			TEXTURE2D(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

			struct ProceduralAttributes
			{
			    uint vertexID : VERTEXID_SEMANTIC;
			};

			struct ProceduralVaryings
			{
			    float4 positionCS : SV_POSITION;
			    float2 uv : TEXCOORD;
			};

			ProceduralVaryings ProceduralVert (ProceduralAttributes input)
			{
			    ProceduralVaryings output;
			    output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
			    output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
			    return output;
			}
			
			float4 frag(ProceduralVaryings input) : SV_Target
            {
            	float z = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, input.uv);
            	#if _IgnoreSkybox
            		if (z == UNITY_RAW_FAR_CLIP_VALUE)
            			return float4(0, 0, 0, 1);
            	#endif

            	float4 AccumulatedLighting = SampleVolumetricFog(input.uv, z);
            	return AccumulatedLighting;
			}

			ENDHLSL
		}
	}
}
