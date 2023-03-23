Shader "Hidden/SunShaft"
{
    Properties
    {
        [HideInInspector] _MainTex("Base (RGB)", 2D) = "white" {}
    }
    HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_MainTex);
        TEXTURE2D(_CameraDepthTexture);
        sampler sampler_LinearClamp;
        float4 Params[3];
        #define _BloomThreshold Params[0].x
        #define _BloomScale Params[0].y
        #define _BloomMaxBrightness Params[0].z
        #define _MaskDepth Params[0].w
        #define _BlurCenter Params[1].xy
        #define _BlurRadius Params[1].z
        #define _BlurSamples Params[1].w
        #define _Color Params[2].xyz
        #define _ScreenFade Params[2].w

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

        half3 downsample(Varyings i) : SV_Target
        {
            half3 SceneColor = SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, i.uv).rgb;
            half Z = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_LinearClamp, i.uv);
            half EyeDepth = LinearEyeDepth(Z, _ZBufferParams);
            
            half EdgeMask = 1.0f - i.uv.x * (1.0f - i.uv.x) * i.uv.y * (1.0f - i.uv.y) * 8.0f;
            EdgeMask = EdgeMask * EdgeMask * EdgeMask * EdgeMask;
            half Luminance = max(dot(SceneColor, half3(.3f, .59f, .11f)), 6.10352e-5);
            half AdjustedLuminance = clamp(Luminance - _BloomThreshold, 0.0f, _BloomMaxBrightness);
            half3 BloomColor = _BloomScale * SceneColor / Luminance * AdjustedLuminance * 2.0f;
            half DistanceMask = saturate(EyeDepth / _MaskDepth);
            half ScreenMask = 1.0f - saturate(length(_BlurCenter - i.uv)) * _ScreenFade;
            half3 color = BloomColor *  (1 - EdgeMask) * _Color.rgb * DistanceMask * ScreenMask;
            return color;
        }

        half3 blur(half2 uv, half size)
        {
            half3 sum = 0;
            float2 BlurVector = (_BlurCenter - uv) * size / _BlurSamples;
            for (int SampleIndex = 0; SampleIndex < _BlurSamples; SampleIndex++)
            {
                float2 SampleUVs = uv + SampleIndex * BlurVector;
                // Needed because sometimes the source texture is larger than the part we are reading from
                float2 ClampedUVs = clamp(SampleUVs, 0, 1);
                float3 SampleValue = SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, ClampedUVs).rgb;
                sum += SampleValue;
            }
            return sum / _BlurSamples;
        }
    
        half3 blur1(Varyings i) : SV_Target
        {
            return blur(i.uv, 0.1 * _BlurRadius);
        }

        half3 blur2(Varyings i) : SV_Target
        {
            return blur(i.uv, 0.3 * _BlurRadius);
        }

        half3 blur3(Varyings i) : SV_Target
        {
            return blur(i.uv, 0.9 * _BlurRadius);
        }

        half3 combine(Varyings i) : SV_Target
        {
            half3 BlurColor = SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, i.uv).rgb;
            return BlurColor;
        }

    ENDHLSL

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment downsample
            ENDHLSL
        }
        
        Pass
        {
            HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment blur1
            ENDHLSL
        }
        
        Pass
        {
            HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment blur2
            ENDHLSL
        }
        
        Pass
        {
            HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment blur3
            ENDHLSL
        }
        
        Pass
        {
            Blend One One
            HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment combine
            ENDHLSL
        }
    }
}
