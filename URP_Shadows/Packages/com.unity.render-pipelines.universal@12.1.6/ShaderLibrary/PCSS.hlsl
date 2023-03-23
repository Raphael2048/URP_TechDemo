#ifndef PCSS_INCLUDE
#define PCSS_INCLUDE

#include "UnityInput.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

static const float2 PoissonOffsets[32] = {
    float2(0.06407013, 0.05409927),
    float2(0.7366577, 0.5789394),
    float2(-0.6270542, -0.5320278),
    float2(-0.4096107, 0.8411095),
    float2(0.6849564, -0.4990818),
    float2(-0.874181, -0.04579735),
    float2(0.9989998, 0.0009880066),
    float2(-0.004920578, -0.9151649),
    float2(0.1805763, 0.9747483),
    float2(-0.2138451, 0.2635818),
    float2(0.109845, 0.3884785),
    float2(0.06876755, -0.3581074),
    float2(0.374073, -0.7661266),
    float2(0.3079132, -0.1216763),
    float2(-0.3794335, -0.8271583),
    float2(-0.203878, -0.07715034),
    float2(0.5912697, 0.1469799),
    float2(-0.88069, 0.3031784),
    float2(0.5040108, 0.8283722),
    float2(-0.5844124, 0.5494877),
    float2(0.6017799, -0.1726654),
    float2(-0.5554981, 0.1559997),
    float2(-0.3016369, -0.3900928),
    float2(-0.5550632, -0.1723762),
    float2(0.925029, 0.2995041),
    float2(-0.2473137, 0.5538505),
    float2(0.9183037, -0.2862392),
    float2(0.2469421, 0.6718712),
    float2(0.3916397, -0.4328209),
    float2(-0.03576927, -0.6220032),
    float2(-0.04661255, 0.7995201),
    float2(0.4402924, 0.3640312),
};

float2 getReceiverPlaneDepthBias (float3 shadowCoord)
{
    float2 biasUV;
    float3 dx = ddx (shadowCoord);
    float3 dy = ddy (shadowCoord);

    biasUV.x = dy.y * dx.z - dx.y * dy.z;
    biasUV.y = dx.x * dy.z - dy.x * dx.z;
    biasUV *= 1.0f / ((dx.x * dy.y) - (dx.y * dy.x));
    return biasUV;
}


float _PCSSLightWidth;

real SampleShadowmapPCSS(TEXTURE2D_SHADOW_PARAM(ShadowMap, sampler_ShadowMap), float4 shadowCoord, float2 OneOverShadowMapSize)
{
    real attenuation;

    float2 DepthBiasDotFactors = getReceiverPlaneDepthBias(shadowCoord.xyz);
    
    float2 shadowUV = shadowCoord.xy;
    float DReceive = shadowCoord.z;

    float fractionalSamplingError = 2.0 * dot(OneOverShadowMapSize, abs(DepthBiasDotFactors));
    fractionalSamplingError = min(fractionalSamplingError, 0.01f);
#if defined(UNITY_REVERSED_Z)
    fractionalSamplingError *= -1.0;
#endif

    DReceive -= fractionalSamplingError;

    //搜索DB的范围 和 当前采样点的距离有关，距离灯光越近，范围越小
    float SearchWidth = _PCSSLightWidth * (DReceive - 0.05) / DReceive;
    
    float DAverageBlocker = 0;
    float BlockerSum = 0.0;
    float BlockCount = 0.0001f;

    float Angle = RandomAngle(0, 0);
    float SinAngle, CosAngle;
    sincos(Angle, SinAngle, CosAngle);
    float2x2 RotMatrix = float2x2(CosAngle, -SinAngle, SinAngle, CosAngle);
    
    //1.求平均Distance Blocker
    for (int i = 0; i < 32; i++)
    {
        float2 offset = PoissonOffsets[i] * SearchWidth;
        offset = mul(RotMatrix, offset);
        float D_sample = ShadowMap.SampleLevel(sampler_LinearClamp, shadowUV + offset, 0).r;
        
#if  defined(UNITY_REVERSED_Z)
        if(D_sample < DReceive)
#else
        if(D_sample > DReceive)
#endif         
        {
            BlockerSum += D_sample;
            BlockCount += 1.0;
        }
    }
    
#if  defined(UNITY_REVERSED_Z)
    DAverageBlocker = 1 - DAverageBlocker;
#endif

    //2.计算软的范围
    float W_Penumbra = abs(DReceive - DAverageBlocker) * _PCSSLightWidth / DAverageBlocker;

    //3.根据W范围做PCF
    float sum = 0;
    for (int i = 0; i < 32; i++)
    {
        float2 offset = PoissonOffsets[i] * W_Penumbra * OneOverShadowMapSize;
        offset = mul(RotMatrix, offset);
        sum += SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(shadowUV + offset, DReceive)).r;
    }

    attenuation = sum / 32;

    return attenuation;
}

#endif