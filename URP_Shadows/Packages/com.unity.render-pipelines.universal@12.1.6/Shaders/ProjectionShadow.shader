Shader "Hidden/H3D/Shadow/Render"
{
	Properties
	{
		 [MainTexture] _MainTex("Albedo", 2D) = "white" {}
	}

	SubShader
	{
		Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="true" "DisableBatching"="true" }
		LOD 100
		HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProjectionShadowInclude.hlsl"
		#pragma enable_d3d11_debug_symbols

		struct appdata
		{
			float4 vertex : POSITION;
			float2 uv     : TEXCOORD0;
		};

		struct v2f
		{
			float4 vertex : SV_POSITION;
			float2 uv     : TEXCOORD0;
		};
		
		TEXTURE2D(_MainTex);
        float4 _MainTex_TexelSize;
		Texture2D _CameraDepthTexture;
		float _ShadowMode;

		v2f vert (appdata v)
		{
			v2f o;
			o.vertex = TransformObjectToHClip(v.vertex.xyz);
			o.uv = v.uv;
			return o;
		}
		
		float4 frag (v2f input) : SV_Target
		{
			// return 0;
			float depth = _CameraDepthTexture[input.vertex.xy];
#if !UNITY_REVERSED_Z
			depth = depth * 2 - 1;
#endif
			
			float2 uv = input.vertex.xy * _ScreenSize.zw;
#if UNITY_REVERSED_Z
			uv.y = 1 - uv.y;
#endif
			float4 clipPos = float4(2.0f * uv - 1.0f, depth, 1.0);
			
			float4 worldSpacePos = mul(UNITY_MATRIX_I_VP, clipPos);
			worldSpacePos /= worldSpacePos.w;
			
			float4 projectorPos = mul(_MainLightWorldToShadow[0], worldSpacePos);
			
#if UNITY_REVERSED_Z
			projectorPos.z = clamp(projectorPos.z, 0.0001, 1);
#else
			projectorPos.z = clamp(projectorPos.z, 0, 0.9999);
#endif

			clip(projectorPos.xy);
			clip(1 - projectorPos.xy);
			
			float shadow = 1;

			if(_H3D_ModulatedShadowParams.x == 0)
			{
				shadow = SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmapTexture, sampler_MainLightShadowmapTexture, projectorPos.xyz).r;
			}
			else if(_H3D_ModulatedShadowParams.x == 1)
			{
				static float2 poissonDisk[16] =
			    {
			        float2( -0.94201624, -0.39906216 ), 
			        float2( 0.94558609, -0.76890725 ), 
			        float2( -0.094184101, -0.92938870 ), 
			        float2( 0.34495938, 0.29387760 ), 
			        float2( -0.91588581, 0.45771432 ), 
			        float2( -0.81544232, -0.87912464 ), 
			        float2( -0.38277543, 0.27676845 ), 
			        float2( 0.97484398, 0.75648379 ), 
			        float2( 0.44323325, -0.97511554 ), 
			        float2( 0.53742981, -0.47373420 ), 
			        float2( -0.26496911, -0.41893023 ), 
			        float2( 0.79197514, 0.19090188 ), 
			        float2( -0.24188840, 0.99706507 ), 
			        float2( -0.81409955, 0.91437590 ), 
			        float2( 0.19984126, 0.78641367 ), 
			        float2( 0.14383161, -0.14100790 )
			    };

				float zReceiver = 1 - projectorPos.z;
				
				float avgBlockerDepth = 0;
				float numBlocks = 0;
				float searchWidth = abs(zReceiver) * _H3D_ModulatedShadowParams.y;
				for(int i = 0; i < 16; ++i)
				{
					float s = 1 - SAMPLE_TEXTURE2D(_MainLightShadowmapTexture, sampler_PointClamp, projectorPos.xy + poissonDisk[i] * _MainLightShadowmapSize.xy * searchWidth);
					if(s < zReceiver)
					{
						avgBlockerDepth += s;
						numBlocks++;
					}
				}
				if(numBlocks == 0)
				{
					shadow = 1;
				}
				else
				{
					avgBlockerDepth /= numBlocks;
					float penumbraSize = (zReceiver - avgBlockerDepth) / avgBlockerDepth;
					float angle = InterleavedGradientNoise(input.vertex.xy, 1);
					float cosA, sinA;
				    sincos(angle * TWO_PI, sinA, cosA);
				    float2x2 m = {
				        cosA, sinA, -sinA, cosA
				    };
					float all = 0;
					for (int i = 0; i < 16; ++i)
				    {
				        float2 offset = mul(m, poissonDisk[i]) * penumbraSize * searchWidth;
				        all += SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmapTexture, sampler_MainLightShadowmapTexture, projectorPos + float3(offset * _MainLightShadowmapSize.xy, 0)).r;
				    }
					shadow = all / 16;
				}
			}
			float3 color = lerp(_H3D_ModulatedShadowColor.xyz, 1, shadow);
			return float4(color, 0); 
		}
		
		
		ENDHLSL
		
		// 0 绘制阴影
		Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            
			// #pragma enable_d3d11_debug_symbols
            // -------------------------------------
            // Material Keywords
            #pragma shader_feature _ALPHATEST_ON

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            // #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
		
		// ShadowVolume 标记阴影区域 ZPass算法， 这两个只有在需要手动剔除阴影区域时才会用到
		Pass
		{
			ZWrite Off
			Cull Off
			Stencil
			{
				Comp always
				Fail Keep
				ZFail Keep
				PassFront IncrWrap
				PassBack DecrWrap
			}
			ColorMask 0
		}
		
		// ShadowVolume 标记阴影区域 ZFail算法
		Pass
		{
			ZWrite Off
			Cull Off
			Stencil
			{
				Comp always
				Fail Keep
				Pass Keep
				ZFail Keep
				ZFailFront DecrWrap 
				ZFailBack IncrWrap
			}
			ColorMask 0
		}
		
		// 3
		Pass
		{
			ZWrite Off
			ZTest Off
			Blend DstColor Zero
			Cull Back
			Stencil
			{
				Ref  0
				Comp Equal
			}

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			// #pragma enable_d3d11_debug_symbols
			
			ENDHLSL
		}
		
		// 4
		Pass
		{
			ZWrite Off
			ZTest Off
			Blend DstColor Zero
			Cull Front
			Stencil
			{
				Ref  0
				Comp Equal
			}

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			// #pragma enable_d3d11_debug_symbols
			
			ENDHLSL
		}
	}
}
