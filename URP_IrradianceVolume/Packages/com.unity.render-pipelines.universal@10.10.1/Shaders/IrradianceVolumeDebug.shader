Shader "Hidden/IrradianceVolumeDebug"
{
    Properties
    {
    
    }

    SubShader
    {
        // Universal Pipeline tag is required. If Universal render pipeline is not set in the graphics settings
        // this Subshader will fail. One can add a subshader below or fallback to Standard built-in to make this
        // material work with both Universal Render Pipeline and Builtin Unity Pipeline
        Tags{"RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "Lit" "IgnoreProjector" = "True" "ShaderModel"="4.5"}
        LOD 300

        // ------------------------------------------------------------------
        //  Forward pass. Shades all light in a single pass. GI + emission + Fog
        Pass
        {
            // Lightmode matches the ShaderPassName set in UniversalRenderPipeline.cs. SRPDefaultUnlit and passes with
            // no LightMode tag are also rendered by Universal Render Pipeline
            Name "ForwardLit"
            Tags{"LightMode" = "UniversalForward"}

            Blend One Zero
            ZWrite ON
            Cull Back
            ZTest ON

            HLSLPROGRAM
            #pragma target 4.5

            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
            // UNITY_INSTANCING_BUFFER_START(prop)
                // UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            // UNITY_INSTANCING_BUFFER_END(prop)

            // CBUFFER_START(UnityPerMaterial)
            //
            // CBUFFER_END

            StructuredBuffer<float3> _PositionsBuffer;
            
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                uint id:SV_INSTANCEID;
            };

            struct Varyings
            {
                float3 positionWS               : TEXCOORD1;
                half3 normalWS                  : TEXCOORD2;
                float4 positionCS               : SV_POSITION;
            };
            
            Varyings LitPassVertex(Attributes input)
            {
                Varyings varyings;
                varyings.positionWS = input.positionOS * 0.1f + _PositionsBuffer[input.id];
                varyings.positionCS = TransformWorldToHClip(varyings.positionWS);
                varyings.normalWS = input.normalOS;
                return varyings;
            }
            
            half3 LitPassFragment(Varyings input) : SV_Target
            {
                float3 position = input.positionWS;
                float3 normal = input.normalWS;
                float3 result = SampleIrradianceVolume(position, normal);
                return result;
            }
            ENDHLSL
        }
    }
}
