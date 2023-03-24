Shader "Hidden/HE/Weather/RainAndSnow"
{
	SubShader
	{
		Pass // Rain
		{
			Cull Off ZWrite Off ZTest Always
			Blend SrcAlpha OneMinusSrcAlpha
			HLSLPROGRAM
			#pragma vertex LitPassVertex
			#pragma fragment LitPassFragment
			#pragma enable_d3d11_debug_symbols
			#pragma multi_compile_fragment _ _USE_MASK
			
			#include "WeatherEffect.hlsl"
			struct Attributes
			{
				float4 positionOS : POSITION;
				float2 texcoord : TEXCOORD0;
			};

			struct Varyings
			{
				float4 uv0 : TEXCOORD0;
				float2 positionSS : TEXCOORD2;
				float4 positionCS : SV_POSITION;
			};

			Texture2D _RainTex;
			SamplerState sampler_linear_repeat;
			Texture2D _CameraDepthTexture;
			
			float4 _RainLayerParams;
			float4x4 _ScreenToWorld;

			Varyings LitPassVertex (Attributes input)
			{
				VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
				Varyings output = (Varyings)0;
				output.uv0.xy = input.texcoord * _RainLayerParams.x - float2(0,  _Time.y * _RainLayerParams.y * 0.01f);
				output.uv0.zw = output.uv0.xy + float2(0.5f, 0.5f);
				output.positionCS = vertexInput.positionCS;
				output.positionSS = vertexInput.positionNDC.xy;
				return output;
			}

			float EyeDepthToDeviceDepth(float depth)
			{
			    return (1.0f - _ZBufferParams.w * depth) / (_ZBufferParams.z * depth);
			}

			// 指定雨幕的距离，是在水平方向的距离，而非观察空间的距离，因此需要重新计算位置
			float3 ActualWorldSpacePos(float2 screenXY, float eyeDepth)
			{
				float4 positionWS = mul(_ScreenToWorld, float4(screenXY, EyeDepthToDeviceDepth(eyeDepth), 1.0f));
				positionWS *= rcp(positionWS.w);
				float3 V = positionWS - _WorldSpaceCameraPos;
				float3 dir = normalize(V);
				float cosTheta = SinFromCos(dir.y);
				positionWS.xyz = _WorldSpaceCameraPos + V * rcp(cosTheta);
				return positionWS;
			}

			bool CanBeLookedFromTop(float3 positionWS)
			{
#if _USE_MASK
				if(positionWS.y < 0.0f) return false;
				float shadowFactor = SampleShdowIntensity(positionWS);
				return shadowFactor > 0.99f;
#else
				return true;
#endif
			}

			float4 LitPassFragment (Varyings input) : SV_Target
			{
				half alpha = 0.0f;
				half deviceDepth = LOAD_TEXTURE2D_X(_CameraDepthTexture, input.positionCS.xy).r;
				
				half sceneViewDepth = LinearEyeDepth(deviceDepth, _ZBufferParams);
				half4 v1 = SAMPLE_TEXTURE2D_X(_RainTex, sampler_linear_repeat, input.uv0.xy);
				half layerDepth1 = v1.b * _RainLayerParams.z + _RainLayerParams.w;
				half3 worldSpacePos1 = ActualWorldSpacePos(input.positionCS.xy, layerDepth1);
				layerDepth1 = -mul(UNITY_MATRIX_V, half4(worldSpacePos1, 1)).z;
				if(sceneViewDepth > layerDepth1)
				{
					if(CanBeLookedFromTop(worldSpacePos1))
					{
						alpha += v1.r;
					}
					half4 v2 = SAMPLE_TEXTURE2D_X(_RainTex, sampler_linear_repeat, input.uv0.zw);
					half layerDepth2 = v2.b * _RainLayerParams.z + _RainLayerParams.w * 2;
					half3 worldSpacePos2 = ActualWorldSpacePos(input.positionCS.xy, layerDepth2);
					layerDepth2 = -mul(UNITY_MATRIX_V, half4(worldSpacePos2, 1)).z;
					if(sceneViewDepth > layerDepth2 && CanBeLookedFromTop(worldSpacePos2))
					{
						alpha += v2.r;
					}
				}
				float3 rainColor = float3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w) + _MainLightColor.xyz;
				return float4(rainColor, alpha * _RainParams.x);
			}
			ENDHLSL
		}
		
		
		Pass // Splash
		{
			Cull Off ZWrite Off
			Blend SrcAlpha OneMinusSrcAlpha
			HLSLPROGRAM
			#pragma vertex LitPassVertex
			#pragma fragment LitPassFragment
			// #pragma enable_d3d11_debug_symbols
			
			#include "WeatherEffect.hlsl"
			struct Attributes
			{
				float3 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float2 texcoord : TEXCOORD0;
				float2 texcoord2 : TEXCOORD1;
			};

			struct Varyings
			{
				float2 uv : TEXCOORD0;
				float2 color: TEXCOORD1;
				float4 positionCS : SV_POSITION;
			};

			Texture2D _RainSplashSheet;
			SamplerState sampler_linear_repeat;
			// x: SplashSize, y: SplashRange z: SplashRange * HalfBoundSize
			float4 _RainSplashParams;

			Varyings LitPassVertex (Attributes input)
			{
				float3 positionOS = input.positionOS * _RainSplashParams.y;
				float halfRange = _RainSplashParams.z;
				float range = halfRange * 2.0f;;
				// valid range
				float2 displayRangeBegin = float2(_WorldSpaceCameraPos.x - halfRange , _WorldSpaceCameraPos.z - halfRange);

				float rangeDiv = rcp(range);
				// makes displayRangeBegin.x < x + m * Range < displayRangeBegin.x + Range
				float m = ceil((displayRangeBegin.x - positionOS.x) * rangeDiv);
				// makes displayRangeBegin.y < z + n * Range < displayRangeBegin.y + Range 
				float n = ceil((displayRangeBegin.y - positionOS.z) * rangeDiv);
				// float3 positionWS = mul(GetObjectToWorldMatrix(), float4(input.positionOS.xyz, 1.0)).xyz;
				float3 positionWS = float3(positionOS.x + m * range, 0, positionOS.z + n * range);

				float3 heightMapUV = mul(_RainSnowHeightMapMatrix, float4(positionWS, 1.0f)).xyz;
				heightMapUV.z = _RainSnowHeightMap.SampleLevel(sampler_linear_clamp, heightMapUV.xy, 0).r;
				positionWS.y = mul(_RainSnowHeightMapInvMatrix, float4(heightMapUV, 1.0f)).y + _RainSplashParams.x;
				
				float4x4 viewMatrix = GetWorldToViewMatrix();
				// Camera Up Vector
				positionWS += viewMatrix[0].xyz * input.normalOS.xxx * _RainSplashParams.x;
				// Camera Right Vector
				positionWS += viewMatrix[1].xyz * input.normalOS.yyy * _RainSplashParams.x;
				float4 positionCS = TransformWorldToHClip(positionWS);
				
				float3 V = normalize(_WorldSpaceCameraPos - positionWS);
				float k = saturate(5 * (V.y - 0.5f));
				float2 ratio = saturate(float2(1 - k, k));
				
				Varyings output = (Varyings)0;
				output.positionCS = positionCS;

				float frameCount = 20.0f;
				float phase = frac(_Time.y + input.texcoord2.y);
				float frame = floor(phase * frameCount);
				// 开始4帧是动画，其余时间待机
				if(frame > 3.0f)
				{
					output.color = 0;
				}
				else
				{
					output.color = ratio * input.texcoord2.x;
				}
				float tileY = floor(frame * 0.5f);
				float tileX = frame - tileY * 2.0f;
				output.uv = (input.texcoord + float2(tileX, tileY)) * 0.5f;
				return output;
			}

			half4 LitPassFragment (Varyings input) : SV_Target
			{
				half alpha = 0;
				if(any(input.color > 0))
				{
					half2 t = SAMPLE_TEXTURE2D_X(_RainSplashSheet, sampler_linear_clamp, input.uv).xy;
					alpha = t.x * input.color.x + t.y * input.color.y;
				}
				float3 rainColor = float3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w) + _MainLightColor.xyz;
				return half4(rainColor, alpha * _RainParams.x);
			}
			ENDHLSL
		}
		
		Pass // Snow
		{
			Cull Off ZWrite Off
			Blend SrcAlpha OneMinusSrcAlpha
			HLSLPROGRAM
			#pragma vertex LitPassVertex
			#pragma fragment LitPassFragment
			#pragma enable_d3d11_debug_symbols
			
			Texture2D _SnowTexture;
			Texture2D _SnowPositions;
			
			#include "WeatherEffect.hlsl"
			struct Attributes
			{
				float3 positionOS : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct Varyings
			{
				float2 uv : TEXCOORD0;
				float4 positionCS : SV_POSITION;
			};

			Varyings LitPassVertex (Attributes input, uint instanceID : SV_InstanceID)
			{
				Varyings varyings = (Varyings)0;
				float4 positionAndScale = _SnowPositions[IndexToID(instanceID)];
				float3 position = positionAndScale.xyz;
				float4x4 viewMatrix = GetWorldToViewMatrix();
				uint textureID = asuint(positionAndScale.w);
				// float scale = (scaleAndTextureID & 0xFFFF) / 65535.0f;
				// uint textureID = scaleAndTextureID >> 16;
				// Camera Up Vector
				position += viewMatrix[0].xyz * (input.uv.y - 0.5f) * 0.02f;
				// Camera Right Vector
				position += viewMatrix[1].xyz * (input.uv.x - 0.5f) * 0.02f;
				varyings.positionCS = TransformWorldToHClip(position);
				varyings.uv = (input.uv + float2(textureID & 1, (textureID >> 1) & 1)) * 0.5f;
				// varyings.uv = float2(textureID & 1, (textureID >> 1) & 1);
				// varyings.uv = input.uv * 0.5f;
				return varyings;
			}

			half4 LitPassFragment (Varyings input) : SV_Target
			{
				float3 snowColor = float3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w) + _MainLightColor.xyz;
				float a = SAMPLE_TEXTURE2D_X(_SnowTexture, sampler_linear_clamp, input.uv).g;
				// return half4(input.uv, 0, 1);
				return half4(snowColor, a);
			}
			ENDHLSL
		}
		
		Pass // VSM Filter Horizon
		{
			Cull Off ZWrite Off
			Blend One Zero
			HLSLPROGRAM
			#pragma vertex LitPassVertex
			#pragma fragment LitPassFragment
			// #pragma enable_d3d11_debug_symbols
			
			Texture2D _RainSnowHeightMapDepth;
			float4 _RainSnowHeightMapDepth_TexelSize;
			SamplerState sampler_point_clamp;
			
			#include "WeatherEffect.hlsl"
			
			struct Attributes
			{
				uint vertexID : SV_VertexID;
			};

			struct Varyings
			{
				float2 uv : TEXCOORD0;
				float4 positionCS : SV_POSITION;
			};

			Varyings LitPassVertex (Attributes input, uint instanceID : SV_InstanceID)
			{
				
				Varyings varyings = (Varyings)0;
				varyings.uv = GetFullScreenTriangleTexCoord(input.vertexID);
				varyings.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
				return varyings;
			}
			
			float4 LitPassFragment (Varyings input) : SV_Target
			{
				float texelSize = _RainSnowHeightMapDepth_TexelSize.x;
				float2 uv = input.uv;
				half c0 = _RainSnowHeightMapDepth.SampleLevel(sampler_point_clamp, uv - float2(texelSize * 4.0, 0.0), 0).x;
	            half c1 = _RainSnowHeightMapDepth.SampleLevel(sampler_point_clamp, uv - float2(texelSize * 3.0, 0.0), 0).x;
	            half c2 = _RainSnowHeightMapDepth.SampleLevel(sampler_point_clamp, uv - float2(texelSize * 2.0, 0.0), 0).x;
	            half c3 = _RainSnowHeightMapDepth.SampleLevel(sampler_point_clamp, uv - float2(texelSize * 1.0, 0.0), 0).x;
	            half c4 = _RainSnowHeightMapDepth.SampleLevel(sampler_point_clamp, uv                               , 0).x;
	            half c5 = _RainSnowHeightMapDepth.SampleLevel(sampler_point_clamp, uv + float2(texelSize * 1.0, 0.0), 0).x;
	            half c6 = _RainSnowHeightMapDepth.SampleLevel(sampler_point_clamp, uv + float2(texelSize * 2.0, 0.0), 0).x;
	            half c7 = _RainSnowHeightMapDepth.SampleLevel(sampler_point_clamp, uv + float2(texelSize * 3.0, 0.0), 0).x;
	            half c8 = _RainSnowHeightMapDepth.SampleLevel(sampler_point_clamp, uv + float2(texelSize * 4.0, 0.0), 0).x;

				half2 avg =  half2(c0, c0 * c0) * 0.01621622 + half2(c1, c1 * c1) * 0.05405405
						+ half2(c2, c2 * c2) * 0.12162162 + half2(c3, c3 * c3) * 0.19459459
                        + half2(c4, c4 * c4) * 0.22702703
                        + half2(c5, c5 * c5) * 0.19459459 + half2(c6, c6 * c6) * 0.12162162
						+ half2(c7, c7 * c7) * 0.05405405 + half2(c8, c8 * c8) * 0.01621622;
				return float4(avg, 0, 0);
			}
			ENDHLSL
		}
		
		Pass // VSM Filter Vertical
		{
			Cull Off ZWrite Off
			Blend One Zero
			HLSLPROGRAM
			#pragma vertex LitPassVertex
			#pragma fragment LitPassFragment
			// #pragma enable_d3d11_debug_symbols
			
			Texture2D _RainSnowHeightMapTemp;
			float4 _RainSnowHeightMapTemp_TexelSize;
			SamplerState sampler_LinearClamp;
			
			#include "WeatherEffect.hlsl"
			
			struct Attributes
			{
				uint vertexID : SV_VertexID;
			};

			struct Varyings
			{
				float2 uv : TEXCOORD0;
				float4 positionCS : SV_POSITION;
			};

			Varyings LitPassVertex (Attributes input, uint instanceID : SV_InstanceID)
			{
				
				Varyings varyings = (Varyings)0;
				varyings.uv = GetFullScreenTriangleTexCoord(input.vertexID);
				varyings.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
				return varyings;
			}
			
			float4 LitPassFragment (Varyings input) : SV_Target
			{
				float2 uv = input.uv;
				float texelSize = _RainSnowHeightMapTemp_TexelSize.x;
				half2 c0 = _RainSnowHeightMapTemp.SampleLevel(sampler_LinearClamp, uv - float2(0.0, texelSize * 3.23076923), 0).xy;
	            half2 c1 = _RainSnowHeightMapTemp.SampleLevel(sampler_LinearClamp, uv - float2(0.0, texelSize * 1.38461538), 0).xy;
	            half2 c2 = _RainSnowHeightMapTemp.SampleLevel(sampler_LinearClamp, uv                                      , 0).xy;
	            half2 c3 = _RainSnowHeightMapTemp.SampleLevel(sampler_LinearClamp, uv + float2(0.0, texelSize * 1.38461538), 0).xy;
	            half2 c4 = _RainSnowHeightMapTemp.SampleLevel(sampler_LinearClamp, uv + float2(0.0, texelSize * 3.23076923), 0).xy;

	            half2 color = c0 * 0.07027027 + c1 * 0.31621622
	                        + c2 * 0.22702703
	                        + c3 * 0.31621622 + c4 * 0.07027027;
				return float4(color, 0, 0);
			}
			ENDHLSL
		}
	}
}
