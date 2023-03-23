Shader "Hidden/PPRReflectionIntensity"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True"}
        LOD 300
        
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
            #include "PPRFunction.hlsl"

            #pragma multi_compile_local_fragment _ _ENABLE_MIX_REFLECTION_PROBE
        
            Texture2D<uint> _IntermediateTexture;
            Texture2D<float4> _CameraColorTexture;
            SAMPLER(sampler_CameraColorTexture);
        
            float4 _TextureSize;
            float4 _ReflectionPlane;
            float4x4 _ClipToWorldMatrix;

            struct ProceduralAttributes
		    {
			    uint vertexID : VERTEXID_SEMANTIC;
		    };
        
            struct ProceduralVaryings
		    {
			    float4 positionCS : SV_POSITION;
			    float2 uv : TEXCOORD;
		    };

            ProceduralVaryings ProceduralVert(ProceduralAttributes input)
		    {
			    ProceduralVaryings output;
			    output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
			    output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
			    return output;
		    }
        
            void LoadInput(ProceduralVaryings input, out half3 color, out float intensity)
            {
                int2 coord = input.positionCS.xy;
                color = half3(0, 0, 0);
                intensity = 0;
                uint EncodeValue = _IntermediateTexture.Load(int3(coord, 0));
                UNITY_BRANCH
                if (EncodeValue < PROJECTION_CLEAR_VALUE)
                {
                    int2 offset;
                    int distance;
                    DecodeProjectionBufferValue(EncodeValue, offset, distance);
                    intensity = distance * 0.015625;
                    int2 ReflectedCoord = coord - offset;
                    half2 uv = (ReflectedCoord + 0.5f) * _TextureSize.zw;
                    color = SAMPLE_TEXTURE2D(_CameraColorTexture, sampler_CameraColorTexture, uv);

                    half2 vignette = saturate(abs(uv * 2.0 - 1.0) * 5.0 - 4.0);
                    float alpha = saturate(1 - dot(vignette, vignette));
                    color *= alpha;
                }
            }

            half4 frag(ProceduralVaryings input) : SV_Target
            {
                half3 color;
                float intensity;
                LoadInput(input, color, intensity);
                return float4(color, 1);
            }

            struct ReflectionOutput
            {
                half3 color : SV_Target0;
                half  blur   : SV_Target1;
            };

            ReflectionOutput frag_mrt(ProceduralVaryings input)
            {
                half3 color;
                float intensity;
                LoadInput(input, color, intensity);
                ReflectionOutput output;
                output.color = color;
                output.blur = intensity;
                return output;
            }
        ENDHLSL

        Pass
        {
            Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True"}
            ZTEST ALWAYS ZWRITE OFF CULL OFF
            
            HLSLPROGRAM
                #pragma vertex ProceduralVert
                #pragma fragment frag
            ENDHLSL
        }
        
        Pass
        {
            Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True"}
            ZTEST ALWAYS ZWRITE OFF CULL OFF
            
            HLSLPROGRAM
                #pragma vertex ProceduralVert
                #pragma fragment frag_mrt
            ENDHLSL
        }
    }
}
