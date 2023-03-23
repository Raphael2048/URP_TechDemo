Shader "Skin"
{
    Properties
    {
		[Header(Light)]
		_MainLightIntensity("主光强度",Range(0,10)) = 1
		_AdditionLightIntensity("辅助光强度",Range(0,10)) = 1
		[Header(Material)]
        [NoScaleOffset]_MainTex ("基础颜色(RGB)光滑度(A)", 2D) = "white" {}
		_BaseColorTint("基础颜色叠加",Color) = (1,1,1,1)
		_SkinGloss("光滑度控制", Range(0, 2)) = 1
        [NoScaleOffset]_NormalTex("法线贴图(RG)AO贴图(B)散射贴图(A)", 2D) = "bump" {}
		_NormalIntensity("法线贴图强度",float) = 1
    	_SkinScattering("散射强度控制", Range(0, 2)) = 1
    	_OutlineIntensity("轮廓光强度控制", Range(0, 10)) = 0.5
		[Header(Shadow)] 
		_ShadowIntensity("阴影强度", Range(0, 1)) = 1.0
		_ShadowWidth("阴影柔和度", Range(1, 10)) = 2.0
		[Header(Light)]
		// 颜色值都是HDR，否则在Gamma空间，设置的值和传到shader中的值不一样        
        [HDR]_SkinSunLightColor("光照颜色控制",Color) = (1, 1, 1,1)
        [HDR]_SkinSkyLightColor("天光", Color) = (0.4910612, 0.4596333, 0.4671486)
        [HDR]_SkinGroundLightColor("地光", Color) = (0.899654, 0.670304, 0.7814789)
        [HDR]_SkinCameraLightingColor("正面光", Color) = (0.267897, 0.2826775, 0.2998847)
    }
	
    SubShader
    {
		Tags{"RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True"}

        Pass
        {
			Tags{"LightMode" = "UniversalForward"}
			Cull off
        	Stencil
			{
				Ref  32
				Comp Always
				Pass Replace
			}
			HLSLPROGRAM
			#pragma prefer_hlslcc gles
			// -------------------------------------
			// Universal Pipeline keywords
			
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma multi_compile _ _SHADOWS_SOFT
			#pragma multi_compile _ _CHEEKMAKEUP
			#pragma multi_compile _ _SCREEN_SPACE_OCCLUSION
			//#pragma multi_compile _ _DirectionOcclusion
			//#pragma enable_d3d11_debug_symbols

            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProjectionShadowInclude.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1      : TEXCOORD1;
				float2 uv2      : TEXCOORD2;
				float2 uv3      : TEXCOORD3;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 uv01 : TEXCOORD0;
				float4 uv23 : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                float4 T2W0 : TEXCOORD3;
                float4 T2W1 : TEXCOORD4;
                float4 T2W2 : TEXCOORD5;
            };

			half _MainLightIntensity;
			half _AdditionLightIntensity;			
			half _NormalIntensity;
			float4 _SkinSunLightColor;
			float4 _SkinSkyLightColor;
			float4 _SkinGroundLightColor;
			float4 _SkinCameraLightingColor;
			float _SkinGloss;
			float _SkinScattering;
			float _ShadowIntensity;
			float _ShadowWidth;
			float _OutlineIntensity;
			half4 _BaseColorTint;
			
			
			TEXTURE2D(_MainTex);       SAMPLER(sampler_MainTex);
			TEXTURE2D(_NormalTex);   SAMPLER(sampler_NormalTex);			

			inline real3 UnpackNormal(real3 packedNormal, real scale = 1.0)
			{
				real3 normal;
				normal.xy = packedNormal.xy * 2.0 - 1.0;
				normal.z = sqrt(1.0 - saturate(dot(normal.xy, normal.xy)));
				normal.xy *= scale;
				return normalize(normal);
			}

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv01.xy = v.uv0;
				o.uv01.zw = v.uv1;
				o.uv23.xy = v.uv2;
				o.uv23.zw = v.uv3;

                float3 posWS = TransformObjectToWorld(v.vertex.xyz);
                o.viewDirWS = GetCameraPositionWS() - posWS;

                float3 normalWS = TransformObjectToWorldNormal(v.normal);
                float3 tangentWS = TransformObjectToWorldDir(v.tangent.xyz);
                float tangentSign = v.tangent.w * GetOddNegativeScale();
                float3 bitangentWS = cross(normalWS, tangentWS) * tangentSign;

                o.T2W0.xyz = tangentWS;
                o.T2W1.xyz = bitangentWS;
                o.T2W2.xyz = normalWS;
                
                o.T2W0.w = posWS.x;
                o.T2W1.w = posWS.y;
                o.T2W2.w = posWS.z;
                return o;
            }

            float3 ScatteringEffect(float Scattering, float ProcessedScattering, float UnclampedNoL)
            {
                float temp7 = (UnclampedNoL + 0.1) * 0.909 + ProcessedScattering;
                float temp = temp7 * 0.5 + 0.5;
                
                 float temp4 = 0.5 - abs(Scattering - 0.5);
                
                float temp2 = Scattering * (1 - temp4);
                
                float3 temp3 = float3(0.875, 0.98900002, 0.99800003) * Scattering + 1 - Scattering;
                
                float3 temp5 = temp4 * float3(0.1, 0.0049999999, 0.0020000001) + temp2 * float3(0.2, 0.017999999, 0.015);
                float3 temp6 = temp4 * float3(0.60000002, 0.2, 0.14) + temp2 * float3(0.5, 0.37, 0.23999999);
                float3 temp8 = temp7 * temp3;

                float3 temp9 = (temp5 * float3(temp7, temp7, temp7)) - temp8;

                temp8 = (1 - temp) * temp5 + temp8;

                temp5 = saturate( temp9 / temp6 + 0.5);

                temp6 = (1 - temp5) * temp5 * temp6;
                float3 result = temp5 * temp9 + temp8 + temp6 * 0.5;
                return result;
            }

            //  K = 4 * Roughness + 2
            float DualSpecularGGX(float Alpha2A, float Alpha2B, float KA, float KB, float NoH2, float VoH2)
            {
                float ClampVoH2 = clamp(VoH2, 0.1, 1.0f);

                float DA = NoH2 * (Alpha2A - 1) + 1;
                float GGXA = Alpha2A / (DA * DA * KA * ClampVoH2);

                float DB = NoH2 * (Alpha2B - 1) + 1;
                float GGXB = Alpha2B / (DB * DB * KB * ClampVoH2);

                return lerp(GGXA, GGXB, 0.15);
            }
			float4 frag (v2f i) : SV_Target
            {
				float2 uv0 = i.uv01.xy;//uv0用于采样基础贴图

                float4 BaseColorSample = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex, uv0);
                
                float3 BaseColor = BaseColorSample.rgb*_BaseColorTint.rgb;
                
                float4 NormalSample = SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, uv0);

				float3 normalTS = UnpackNormal(NormalSample.xyz,_NormalIntensity);

                float AO = NormalSample.z;
				float3 indrectBounceColor = 0;

                float AO8 = AO * AO; 
                AO8 = AO8 * AO8;
                AO8 = AO8 * AO8;
                AO = (1 - AO) * AO + AO;

                float3x3 T2W = float3x3(normalize(i.T2W0.xyz),normalize(i.T2W1.xyz), normalize(i.T2W2.xyz));
				float3 WorldPos = float3(i.T2W0.w, i.T2W1.w, i.T2W2.w);

                half3 N = normalize( mul(normalTS,T2W));
                float Roughness = (2 - _SkinGloss) * (1 - BaseColorSample.w);

            	InputData input;
            	input = (InputData)0;
            	input.positionWS = WorldPos;
            	input.normalWS = N;
            	Light light = GetProjectionMainLightPoissonDisk(input, _ShadowWidth);
				light.shadowAttenuation = lerp(1 - _ShadowIntensity, 1, light.shadowAttenuation);
            	light.color *=_MainLightIntensity;
				
                float3 L = light.direction;
                float3 V = normalize(i.viewDirWS);
                float3 H = normalize(L + V); 
                float NoH = saturate(dot(N, H));
                float NoV = saturate(dot(N, V));
                float UnClampedNoL = dot(N, L);
                float NoL = saturate(UnClampedNoL);
                float VoH = saturate(dot(V, H));

                float NoH2 = NoH * NoH;
                float VoH2 = VoH * VoH;
            	
				float ExtraRoughness = Roughness * 0.2 + 0.37;
				
                //EnvBRDFApproxLazarovNoMetal
                float2 T1 = (ExtraRoughness * float2(-1, -0.0275) ) + float2(1, 0.0425);
                // 拟合GF项,菲涅尔系数，计算轮廓光用
                float GF = min(T1.x * T1.x, exp2(NoV * -9.28)) * T1.x + T1.y;

            	//散射
                float Scattering = _SkinScattering * NormalSample.w;
                float ProcessedScattering = Scattering * (1 - NoV) * (1 - NoV) * 0.5;
            	float3 SunColor = light.color;
                float3 ScatteredSunColor = ScatteringEffect(Scattering, ProcessedScattering, UnClampedNoL) * SunColor * _SkinSunLightColor.rgb;
            	
                float FirstAlpha2 = max(Pow4(Roughness), 0.01);
                // 次高光
                float ExtraAlpha2 = max(Pow4(ExtraRoughness), 0.01);
                // K = 4 * Roughness + 2
            	float KA = Roughness * 4 + 2;
                float KB = ExtraRoughness * 4 + 2.0;

                float D_GGX = DualSpecularGGX(FirstAlpha2, ExtraAlpha2, KA, KB, NoH2, VoH2);
                //主光源高光
                float3 Specular = min(0.028 * D_GGX, 1) * NoL * light.shadowAttenuation * PI;

                //来自摄影机朝上方向，打辅助光，这个光比较弱，比较难看出来
                float3 LP = normalize(V + float3(0, 0.5, 0));
                float NoLP = saturate(dot(LP, N));
                float3 HP = normalize(LP + V);
                float NoHP = saturate(dot(N, HP));
                float NoHP2 = NoHP * NoHP;
                float VoHp = saturate(dot(V, HP));
                float VoHp2 = VoHp * VoHp;

                float Second_D_GGX = DualSpecularGGX(FirstAlpha2, ExtraAlpha2, KA, KB, NoHP2, VoHp2);
                //辅助光源高光
                float3 Specular2 = Second_D_GGX * PI * NoLP * 0.028;            	
            	//环境光
                float3 SkyLight = SampleSH(N) * _SkinSkyLightColor.xyz;

            	// 地面光强度
				float GIntensity = 0.7 - i.T2W2.y * 0.3;
                float3 GroundLight = GIntensity * SunColor * 0.5 * _SkinGroundLightColor.rgb;
            	
                float ScatteredNoLp = ProcessedScattering + NoLP;
                float3 J0 = saturate(ScatteredNoLp) * 0.66 + float3(0.34, 0.14, 0.07);
                ScatteredNoLp = saturate(ScatteredNoLp + 0.2);
                float PartVoL = -dot(V.xz, L.xz) * 0.5 + 0.5;
                float3 CameraLight = J0 * ScatteredNoLp * PartVoL * SunColor * _SkinCameraLightingColor.rgb;

				float3 Result = (SkyLight + CameraLight + GroundLight) * AO;
            	
            	//边缘光
            	float3 JLight = Result * GF * AO8;

            	Result += ScatteredSunColor * light.shadowAttenuation;
            	Result += indrectBounceColor;
            	// 漫反射部分，乘以皮肤颜色
            	Result *= BaseColor;
                // 主高光
                Result += Specular * SunColor * _SkinSunLightColor.rgb;
                // 辅助光高光
                Result += PartVoL * Specular2 * _SkinCameraLightingColor.rgb * SunColor;
                // 边缘光
                Result += JLight * _OutlineIntensity;
                return float4(Result, 1.0f);
                
            }
			ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            // Required to compile gles 2.0 with standard srp library
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            // -------------------------------------
            // Material Keywords
           // #pragma shader_feature _ALPHATEST_ON

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            //#pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
			
			float3 _LightDirection;
			
			struct Attributes
			{
			    float4 positionOS   : POSITION;
			    float3 normalOS     : NORMAL;
			    UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			
			struct Varyings
			{
			    float2 uv           : TEXCOORD0;
			    float4 positionCS   : SV_POSITION;
			};
			
			float4 GetShadowPositionHClip(Attributes input)
			{
			    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
			    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
			
			    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
			
			#if UNITY_REVERSED_Z
			    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
			#else
			    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
			#endif
			
			    return positionCS;
			}
			
			Varyings ShadowPassVertex(Attributes input)
			{
			    Varyings output;
			    UNITY_SETUP_INSTANCE_ID(input);
			    output.positionCS = GetShadowPositionHClip(input);
			    return output;
			}
			
			half4 ShadowPassFragment(Varyings input) : SV_TARGET
			{			    
			    return 0;
			}
			
		    ENDHLSL
		}

        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}

            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            // Required to compile gles 2.0 with standard srp library
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #pragma multi_compile_instancing
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			
			struct Attributes
			{
				float4 position : POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			
			struct Varyings
			{
			    float2 uv           : TEXCOORD0;
			    float4 positionCS   : SV_POSITION;
			    UNITY_VERTEX_INPUT_INSTANCE_ID
			    UNITY_VERTEX_OUTPUT_STEREO
			};
			
			Varyings DepthOnlyVertex(Attributes input)
			{
			    Varyings output = (Varyings)0;
			    UNITY_SETUP_INSTANCE_ID(input);
			    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

			    output.positionCS = TransformObjectToHClip(input.position.xyz);
			    return output;
			}
			

			half4 DepthOnlyFragment(Varyings input) : SV_TARGET
			{
			    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

			    return 0;
			}
			ENDHLSL
        }


			Pass
			{
			    Name "DepthNormals"
			    Tags{"LightMode" = "DepthNormals"}

				ZWrite On
				//ColorMask 0
				Cull Back

				HLSLPROGRAM
				// Required to compile gles 2.0 with standard srp library
				#pragma prefer_hlslcc gles
				#pragma exclude_renderers d3d11_9x
				#pragma target 2.0

				#pragma vertex DepthNormalsVertex
				#pragma fragment DepthNormalsFragment
				#pragma enable_d3d11_debug_symbols
				#pragma multi_compile_instancing
				#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


			    struct Attributes
			    {
			    	float4 position     : POSITION;
					float2 uv0 : TEXCOORD0;
					float2 uv1      : TEXCOORD1;
					float2 uv2      : TEXCOORD2;
					float2 uv3      : TEXCOORD3;
					float3 normal : NORMAL;
					float4 tangent : TANGENT;
			    	UNITY_VERTEX_INPUT_INSTANCE_ID
			    };
			    
			    struct Varyings
			    {
			    	float4 positionCS   : SV_POSITION;

					float4 uv01 : TEXCOORD0;
					float4 T2W0 : TEXCOORD1;
					float4 T2W1 : TEXCOORD2;
					float4 T2W2 : TEXCOORD3;

			    	UNITY_VERTEX_INPUT_INSTANCE_ID
			    	UNITY_VERTEX_OUTPUT_STEREO
			    };
			    
				half _MainLightIntensity;
				half _AdditionLightIntensity;
				half _NormalIntensity;
				float4 _SkinSunLightColor;
				float4 _SkinSkyLightColor;
				float4 _SkinGroundLightColor;
				float4 _SkinCameraLightingColor;
				float _SkinGloss;
				float _SkinScattering;
				float _ShadowWidth;
				float _OutlineIntensity;
				half4 _BaseColorTint;

				TEXTURE2D(_MainTex);       SAMPLER(sampler_MainTex);
				TEXTURE2D(_NormalTex);   SAMPLER(sampler_NormalTex);

				inline real3 UnpackNormal(real3 packedNormal, real scale = 1.0)
				{
					real3 normal;
					normal.xy = packedNormal.xy * 2.0 - 1.0;
					normal.z = sqrt(1.0 - saturate(dot(normal.xy, normal.xy)));
					normal.xy *= scale;
					return normalize(normal);
				}
			    Varyings DepthNormalsVertex(Attributes input)
			    {
			    	Varyings output = (Varyings)0;
			    	UNITY_SETUP_INSTANCE_ID(input);
			    	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
			    
			    	output.positionCS = TransformObjectToHClip(input.position.xyz);


					//output.vertex = TransformObjectToHClip(v.vertex.xyz);
					output.uv01.xy = input.uv0;
					output.uv01.zw = input.uv1;

					float3 posWS = TransformObjectToWorld(input.position.xyz);
					//output.viewDirWS = GetCameraPositionWS() - posWS;

					float3 normalWS = TransformObjectToWorldNormal(input.normal);
					float3 tangentWS = TransformObjectToWorldDir(input.tangent.xyz);
					float tangentSign = input.tangent.w * GetOddNegativeScale();
					float3 bitangentWS = cross(normalWS, tangentWS) * tangentSign;

					output.T2W0.xyz = tangentWS;
					output.T2W1.xyz = bitangentWS;
					output.T2W2.xyz = normalWS;

					output.T2W0.w = posWS.x;
					output.T2W1.w = posWS.y;
					output.T2W2.w = posWS.z;

			    	return output;
			    }
			    
			    
				half4 DepthNormalsFragment(Varyings input) : SV_TARGET
			    {
			    	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
			    
				    float2 uv0 = input.uv01.xy;//uv0用于采样基础贴图
				    float2 uv1 = input.uv01.zw;//uv1用于采样脸颊妆容				    
				    float4 NormalSample = SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, uv0);
				    float3 normalTS = UnpackNormal(NormalSample.xyz, _NormalIntensity);
				    
				    
				    float3x3 T2W = float3x3(normalize(input.T2W0.xyz), normalize(input.T2W1.xyz), normalize(input.T2W2.xyz));
				    half3 N = normalize(mul(normalTS, T2W));
					return half4(N, 1.0f);
			    }
				ENDHLSL
			}
	}	
}
