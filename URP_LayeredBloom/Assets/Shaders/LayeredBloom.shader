Shader "Hidden/Universal Render Pipeline/LayeredBloom"
{
    Properties
    {
        [HideInInspector] _MainTex("Base (RGB)", 2D) = "white" {}
    }
    
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        float4 _MainTex_TexelSize;


        float _Threshold;
        float _Amplify;
        float4 _LayerIntensity;

        TEXTURE2D(_Blur0);
        SAMPLER(sampler_Blur0);
        TEXTURE2D(_Blur1);
        SAMPLER(sampler_Blur1);
        TEXTURE2D(_Blur2);
        SAMPLER(sampler_Blur2);
        TEXTURE2D(_Blur3);
        SAMPLER(sampler_Blur3);
    
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

            half4 filter(Varyings input) : SV_Target
            {
                float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float3 xyz = max(col.xyz - _Threshold.xxx, 0);
                return float4(xyz * _Amplify, 1);
            }

            half4 blur_h(Varyings input) : SV_Target
            {

                float texelSize = _MainTex_TexelSize.x;
                half3 c0 = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, input.uv - float2(texelSize * 3.23076923, 0.0)).rgb;
                half3 c1 = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, input.uv - float2(texelSize * 1.38461538, 0.0)).rgb;
                half3 c2 = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, input.uv).rgb;  
                half3 c3 = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, input.uv + float2(texelSize * 1.38461538, 0.0)).rgb;
                half3 c4 = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, input.uv + float2(texelSize * 3.23076923, 0.0)).rgb;

                half3 color = c0 * 0.07027027 + c1 * 0.31621622
                        + c2 * 0.22702703
                        + c3 * 0.31621622 + c4 * 0.07027027;
                return half4(color, 1);
            }

            half4 blur_v(Varyings input) : SV_Target
            {
                float texelSize = _MainTex_TexelSize.y;
                half3 c0 = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, input.uv - float2(0.0, texelSize * 3.23076923)).rgb;
                half3 c1 = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, input.uv - float2(0.0, texelSize * 1.38461538)).rgb;
                half3 c2 = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, input.uv).rgb;  
                half3 c3 = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, input.uv + float2(0.0, texelSize * 1.38461538)).rgb;
                half3 c4 = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, input.uv + float2(0.0, texelSize * 3.23076923)).rgb;

                half3 color = c0 * 0.07027027 + c1 * 0.31621622
                        + c2 * 0.22702703
                        + c3 * 0.31621622 + c4 * 0.07027027;

                return half4(color, 1);
            }

            half4 downsample(Varyings input) : SV_target
            {
                float2 size = _ScreenParams.zw - 1.0;
                size *= 0.25f;
                half3 c0 = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, input.uv + size).rgb;
                half3 c1 = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, input.uv - size).rgb;
                half3 c2 = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, input.uv + float2(size.x, -size.y)).rgb;
                half3 c3 = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, input.uv + float2(-size.x, size.y)).rgb;
                return half4((c0 + c1 + c2 + c3) * 0.25f, 1);
            }

            half4 blend(Varyings input) : SV_target
            {
                half3 c0 = SAMPLE_TEXTURE2D_X(_Blur0, sampler_Blur0, input.uv).rgb;
                half3 c1 = SAMPLE_TEXTURE2D_X(_Blur1, sampler_Blur1, input.uv).rgb;
                half3 c2 = SAMPLE_TEXTURE2D_X(_Blur2, sampler_Blur2, input.uv).rgb;
                half3 c3 = SAMPLE_TEXTURE2D_X(_Blur3, sampler_Blur3, input.uv).rgb;

                float3 result =  c0 * _LayerIntensity.x + c1 * _LayerIntensity.y + c2 * _LayerIntensity.z + c3 * _LayerIntensity.w;
                return half4(result, 1);
            }
    ENDHLSL
    
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull OFF ZTest Always
        LOD 200

        Pass
        {
            Name "Filter"
            
            HLSLPROGRAM 
                #pragma vertex vert
                #pragma fragment filter
            ENDHLSL
        }
        
        Pass
        {
            Name "Blur Horizontal"
            
            HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment blur_h
            ENDHLSL
        }
        
        Pass
        {
            Name "Blur Vertical"
            
            HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment blur_v
            ENDHLSL
        }
        
        Pass
        {
            Name "Downsample"
            
            HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment downsample
            ENDHLSL
        }
        
        Pass
        {
            Name "Blend"
            Blend One One   
            HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment blend
            ENDHLSL
        }
            
    }
}
