Shader "H3DShaders/PlaneReflection"
{
	Properties
	{
	}

	SubShader
	{
		Tags{ "Queue" = "Geometry" "RenderType" = "Opaque" }

		Pass
		{
			Tags{ "LightMode" = "UniversalForward" }

			HLSLPROGRAM
			#pragma target 3.0
			#pragma vertex vert
			#pragma fragment frag
			//#pragma enable_d3d11_debug_symbols
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.planarreflection/Shaders/PlanarReflection.hlsl"

			struct vsInput
			{
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
			};

			struct v2f
			{
				float4 pos : SV_POSITION;
				float3 worldPos : TEXCOORD1;
			};
			

			v2f vert(vsInput v)
			{
				v2f o;
				o.worldPos = TransformObjectToWorld(v.vertex.xyz);
				
				o.pos = TransformWorldToHClip(o.worldPos);
				return o;
			}

			real3 frag(v2f i) : SV_Target
			{
				real2 uv = i.pos.xy * (_ScreenParams.zw - 1.0f);
				// real3 refl = SamplePlanarReflection(uv);
				real3 V = normalize(GetCameraPositionWS() - i.worldPos);
				real3 refl = SamplePlanarReflectionResultWithRoughness(uv, V, 0.05);
				return refl;
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
            Cull[_Cull]

            HLSLPROGRAM

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}

            ZWrite On
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

	}
}