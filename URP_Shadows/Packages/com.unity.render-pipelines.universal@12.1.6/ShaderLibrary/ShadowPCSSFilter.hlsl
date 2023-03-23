#ifndef SHADOW_PCSS_FILTER_INCLUDE
#define SHADOW_PCSS_FILTER_INCLUDE
#include "UnityInput.hlsl"
struct PCSSShadowSettings
{
    float SceneDepth;
    float2 ShadowPosition;
    int2 SVPosition;
    Texture2D Shadowmap;
    SamplerComparisonState ShadowmapSampler;
    float4 ShadowmapTexelSize;
    float MaxFilterWidth;
    float UsePCSS;
    float PCSSFading;
};

#define PCSS_SEARCH_SAMPLE 16
#define PCSS_FILTER_SAMPLE 32
float DirectionalPCSS(PCSSShadowSettings Settings)
{
    float3 ShadowPosition = float3(Settings.ShadowPosition, Settings.SceneDepth);
    float3 ShadowPositionDDX = ddx_coarse(ShadowPosition);
    float3 ShadowPositionDDY = ddy_coarse(ShadowPosition);
    float3 DepthBiasPlaneNormal = cross(ShadowPositionDDX, ShadowPositionDDY);
    float DepthBiasFactor = 1 / max((DepthBiasPlaneNormal.z), length(DepthBiasPlaneNormal) * 0.0872665);
    float2 DepthBiasDotFactors = DepthBiasPlaneNormal.xy * DepthBiasFactor;
    
    float Angle = RandomAngle(Settings.SVPosition, 0);
    float SinAngle, CosAngle;
    sincos(Angle, SinAngle, CosAngle);
    float2x2 RotMatrix = float2x2(CosAngle, -SinAngle, SinAngle, CosAngle);

    float StepAngle = 2.39996322;

    float FilterWidth = Settings.MaxFilterWidth;
    if(Settings.UsePCSS)
    {
        float SearchRadius = 4;
        float DepthSum = 0;
        float SumWeight = 0;
        for (int i = 0; i < PCSS_SEARCH_SAMPLE; ++i)
        {
            Angle = StepAngle * i;
            sincos(Angle, SinAngle, CosAngle);
            float2 SampleUVOffset = mul(RotMatrix, float2(SinAngle, CosAngle)) * sqrt(((float)i) / PCSS_SEARCH_SAMPLE) * SearchRadius * Settings.ShadowmapTexelSize.xy;
            float Bias = dot(SampleUVOffset, DepthBiasDotFactors);
            float SampleShadowDepth = Settings.Shadowmap.SampleLevel(sampler_LinearClamp, Settings.ShadowPosition + SampleUVOffset, 0).r;
            float ShadowDepthCompare = Settings.SceneDepth - Bias - SampleShadowDepth;
            if(ShadowDepthCompare < 0)
            {
                DepthSum += SampleShadowDepth;
                SumWeight += 1;
            }
        }

        if (SumWeight > 0)
        {
            float AvgBlockDepth = DepthSum / SumWeight;
            FilterWidth = (AvgBlockDepth - Settings.SceneDepth) * Settings.PCSSFading;
            FilterWidth = clamp(FilterWidth, 1, Settings.MaxFilterWidth);
        }
        else
        {
            return 1;
        }
    }
    
    float sum = 0;
    for (int i = 0; i < PCSS_FILTER_SAMPLE; ++i)
    {
        Angle = StepAngle * i;
        sincos(Angle, SinAngle, CosAngle);
        float2 SampleUVOffset = mul(RotMatrix, float2(SinAngle, CosAngle)) * sqrt(((float)i) / PCSS_FILTER_SAMPLE) * FilterWidth * Settings.ShadowmapTexelSize.xy;
        float Bias = dot(SampleUVOffset, DepthBiasDotFactors);
        sum += SAMPLE_TEXTURE2D_SHADOW(Settings.Shadowmap, Settings.ShadowmapSampler,
            float3(Settings.ShadowPosition + SampleUVOffset , Settings.SceneDepth - Bias));
    }
    return sum / PCSS_FILTER_SAMPLE;
}


#endif