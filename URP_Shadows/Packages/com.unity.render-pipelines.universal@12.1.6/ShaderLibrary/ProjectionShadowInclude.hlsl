#ifndef PROJECTION_SHADOW_INCLUDED
#define PROJECTION_SHADOW_INCLUDED
#include "ShadowFilter.hlsl"
#include "ShadowPCSSFilter.hlsl"
#include "Shadows.hlsl"
 
TEXTURE2D_X(_MainLightColorShadowmapTexture);
float4 _MainLightColorShadowmapTexture_TexelSize;

// xyz投射阴影颜色, 
float4 _H3D_ModulatedShadowColor;
// x 投影计算模式 0 PCF，1 PCSS; y : filater 宽度； z filter 最大宽度
float4 _H3D_ModulatedShadowParams;


// Per Object
// x:0 无投射阴影，1 有投射阴影;
float4 _H3D_ProjectionShadowParams;

float3 GetShadowPosOffset(float nDotL, float3 normal, float3 lightDirection, float bias, float normalBias, float scale = 2)
{
    float3 offset = bias * lightDirection;
    offset += saturate(1 - nDotL) * normal * normalBias;
    return offset * scale;
}

float3 GetMainLightShadowPosOffset(float nDotL, float3 normal, float3 lightDirection, float scale = 2)
{
    return GetShadowPosOffset(nDotL, normal, lightDirection, _MainLightShadowParams.y, _MainLightShadowParams.z, scale);
}

float TransparentSelfShaodwIntensity(float2 colorUV)
{
    if(_MainLightShadowParams.w > 1)
    {
        float color = SAMPLE_TEXTURE2D(_MainLightColorShadowmapTexture, sampler_LinearClamp, colorUV).r;
        return color;
    }
    else
    {
        return 1;
    }
}

#endif