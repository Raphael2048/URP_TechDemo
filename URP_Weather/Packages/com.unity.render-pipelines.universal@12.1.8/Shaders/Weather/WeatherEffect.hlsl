#pragma once
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// x:RainEffectIntensity, y:RippleIntensity, z WindIntensity, W: HasMask
float4 _RainParams;
Texture2D _RainRippleSheet;

// x:SnowIntensity w: HasMask
float4 _SnowParams;
Texture2D _SnowMasksTex;

// xyz ：windDirection, ｗ windIntensity
float4 _WindParams;

sampler sampler_linear_clamp;

Texture2D _RainSnowHeightMap;
float4x4 _RainSnowHeightMapMatrix;
float4x4 _RainSnowHeightMapInvMatrix;


half SampleShdowIntensity(float3 positionWS)
{
    float3 shadowUV = mul(_RainSnowHeightMapMatrix, float4(positionWS.x, positionWS.y + 0.5f, positionWS.z, 1.0f)).xyz;
    if(any(shadowUV.xy < 0) || any(shadowUV.xy > 1.0f)) return 1;
    half2 v2 = SAMPLE_TEXTURE2D(_RainSnowHeightMap, sampler_linear_clamp, shadowUV.xy);
    half sigma2 = v2.y - v2.x * v2.x;
    half shadow = 1.0f;
#if UNITY_REVERSED_Z
    if(shadowUV.z < v2.x)
    {
        shadow = saturate(sigma2 / (sigma2 + -(shadowUV.z - v2.x)));
    }
#else
    if(shadowUV.z > v2.x)
    {
        shadow = saturate(sigma2 / (sigma2 + (ShadowUV.z - v2.x)));
    }
#endif
    return shadow;
}

float WetRoughness = 0.2f;
void Unity_Flipbook_float(inout float2 UV, half Width, half Height, half Tile)
{
    Tile = fmod(Tile, Width * Height);
    half2 tileCount = rcp(float2(Width, Height));
    half tileY, tileX;
    tileY = floor(Tile * tileCount.x);
    tileX = Tile - (tileY * Width);
    UV = (UV + float2(tileX, tileY)) * tileCount;
}

void ApplyRainWetEffect(inout half3 albedo, inout half roughness, inout half occlusion, inout half3 normalWS, half3 positionWS, half3 vertexNormal, half intensity = 1.0f)
{
    half shadowFactor = 1.0f;
    if(_RainParams.w == 1.0f)
    {
        shadowFactor = SampleShdowIntensity(positionWS);
    }
    if(shadowFactor > 0.2)
    {
        shadowFactor = saturate(shadowFactor * intensity);
        half wetRatio = _RainParams.x * shadowFactor  * 0.5f * saturate(vertexNormal.y * vertexNormal.y);
        albedo = lerp(albedo, albedo * 0.7f, wetRatio);
        roughness = lerp(roughness, WetRoughness, wetRatio);
        occlusion = lerp(occlusion, occlusion * 0.5f, wetRatio);
    
        // RIPPLE
        if(_RainParams.y > 0.0f && normalWS.y > 0.95f)
        {
            float2 uv = frac(positionWS.xz * _RainParams.z); 
            half tile = floor(_Time.y * 30.0f);
            Unity_Flipbook_float(uv, 4, 4, tile);
            half4 rr = SAMPLE_TEXTURE2D_X(_RainRippleSheet, sampler_linear_clamp, uv) - 0.5f;
            half2 snormal = lerp( rr.zw * _WindParams.w, rr.xy, _RainParams.x * shadowFactor) * _RainParams.y;
            normalWS = normalize(lerp(normalWS, float3(snormal.x, 1.0f, snormal.y), shadowFactor * 0.5f));
        }
        else
        {
            normalWS = lerp(normalWS, vertexNormal, _RainParams.x);
        }
    }
}

void ApplySnowyEffect(inout half3 albedo, inout half roughness, inout half3 normalWS, half3 positionWS, half3 vertexNormal, half intensity = 1.0f)
{
    half shadowFactor = 1.0f;
    if(_SnowParams.w == 1.0f)
    {
        shadowFactor = SampleShdowIntensity(positionWS);
    }
    if(shadowFactor > 0 && normalWS.y > 0)
    {
        float2 uv = frac(positionWS.xz * _SnowParams.z); 
        half4 v = SAMPLE_TEXTURE2D_X(_SnowMasksTex, sampler_linear_clamp, uv);
        float k = saturate(v.g * _SnowParams.x * shadowFactor * normalWS.y * 2 * intensity);
        albedo = lerp(albedo, 1.0f, k);
        roughness = lerp(roughness, v.r, k);
        normalWS = lerp(normalWS, vertexNormal, k);
    }
}

#define POSITIONS_TEX_WIDTH 64
int2 IndexToID(uint index)
{
    return int2(index % POSITIONS_TEX_WIDTH, index / POSITIONS_TEX_WIDTH);
}

int IDToIndex(int2 id)
{
    return (id.y * POSITIONS_TEX_WIDTH) + id.x;
}

