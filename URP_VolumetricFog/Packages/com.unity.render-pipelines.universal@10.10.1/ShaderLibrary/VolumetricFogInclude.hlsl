#ifndef VOLUMETRICFOG_INCLUDE
#define VOLUMETRICFOG_INCLUDE
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityInput.hlsl"
Texture3D<float4> _ScatteringLightResult;

float4 _VolumetricFogParams[11];

#define VFog_InvVolumeSize _VolumetricFogParams[0].xyz
#define VFog_SkipHistory _VolumetricFogParams[0].w
#define VFog_GridZParams _VolumetricFogParams[1]
#define VFog_Jitter _VolumetricFogParams[2].xyz
#define VFog_Extinction _VolumetricFogParams[2].w
#define VFog_ForwardScatteringColor _VolumetricFogParams[3].xyz
#define VFog_OutsideIntensity _VolumetricFogParams[3].w
#define VFog_BackwardScatteringColor _VolumetricFogParams[4].xyz
#define VFog_HeightFalloff _VolumetricFogParams[4].w
#define VFog_AmbientLight _VolumetricFogParams[5].xyz
#define VFog_DetailTextureIntensity _VolumetricFogParams[5].w
#define VFog_DetailTexture_Speed _VolumetricFogParams[6].xyz
#define VFog_DetailTexture_Tiling _VolumetricFogParams[6].w
#define VFog_NoiseTexture_Speed _VolumetricFogParams[7].xyz
#define VFog_NoiseTexture_Tiling _VolumetricFogParams[7].w
#define VFog_MaxFogDistance _VolumetricFogParams[8].x
#define VFog_HorizonHeight _VolumetricFogParams[8].y
#define VFog_LightRangeMulValue _VolumetricFogParams[8].z
#define VFog_LocalVolumetricFogStartPos _VolumetricFogParams[9].xyz
#define VFog_UseLocalVolumetricFog _VolumetricFogParams[9].w
#define VFog_LocalVolumetricFogInvSize _VolumetricFogParams[10].xyz
#define VFog_LocalVolumetricFogInvEdgeFade _VolumetricFogParams[10].w

float EyeDepthToVolumeW(float Depth)
{
    return log2(Depth * VFog_GridZParams.x + VFog_GridZParams.y) * VFog_GridZParams.z;
}

float VolumeWToEyeDepth(float W)
{
    return (exp2(W / VFog_GridZParams.z) - VFog_GridZParams.y) / VFog_GridZParams.x;
}

// 将观察深度（正值，不是ViewSpace的负值深度），转到裁剪空间深度
float EyeToClipDepth(float depth)
{
    float z = ((1.0f / depth) - _ZBufferParams.w) / _ZBufferParams.z;
    #if !UNITY_REVERSED_Z
    z = z * 2 - 1;
    #endif
    return z;
}

// 将裁剪空间深度，转到观察深度（正值，不是ViewSpace的负值深度）
float ClipToEyeDepth(float z)
{
    #if !UNITY_REVERSED_Z
    z = z * 0.5 + 0.5;
    #endif
    return LinearEyeDepth(z, _ZBufferParams);
}

float NDCZToVolumeW(float z)
{
    #if !UNITY_REVERSED_Z
        z = z * 0.5 + 0.5;
    #endif
    return EyeDepthToVolumeW(LinearEyeDepth(z, _ZBufferParams));
}

float3 NDCToWorldPostion(float3 NDC)
{
    #if UNITY_REVERSED_Z
        NDC.y = -NDC.y;
    #endif
    float4 WorldPos = mul(UNITY_MATRIX_I_VP, float4(NDC, 1));
    WorldPos /= WorldPos.w;
    return WorldPos.xyz;
}

float3 CoordinateToWorldPosition(uint3 Coordinate, float3 Offset)
{
    float3 uvw = (Coordinate + Offset) * VFog_InvVolumeSize.xyz;
    
    // 到相机的距离
    float Depth = VolumeWToEyeDepth(uvw.z);
    float3 NDC = float3(uvw.xy * 2 - 1, EyeToClipDepth(Depth));
    return NDCToWorldPostion(NDC);
}

float CalFog(float3 Dir, float Distance)
{
    float DistanceSum;
    float MaxT = min(Distance, VFog_MaxFogDistance);
    if(VFog_HeightFalloff != 0)
    {
        //防止高度过高时，导致数值溢出
        float Falloff =  VFog_HeightFalloff * Dir.y;
        float EffectRange = max(0, MaxT - VFog_GridZParams.w);
        if(Falloff < 0)
        {
            EffectRange = min(EffectRange, 50.0f / (-Falloff));
        }
        float3 BeginPos = _WorldSpaceCameraPos + Dir * (MaxT - EffectRange);
        float OriginTerm = exp(- VFog_HeightFalloff * (BeginPos.y - VFog_HorizonHeight));
        if (abs(Falloff) < 0.00001)
        {
            //使用泰勒展开推导得出
            DistanceSum = EffectRange;
        }
        else
        {
            DistanceSum = ((1 - exp(-Falloff * EffectRange)) / Falloff);
        }
        DistanceSum =  DistanceSum * OriginTerm ;
    }
    else
    {
        DistanceSum = max(MaxT - VFog_GridZParams.w, 0);
    }
    
    float Transmitte = exp(-DistanceSum * VFog_OutsideIntensity * 0.01);
    return Transmitte;
}

float4 SampleVolumetricFog(float2 uv, float z)
{
    float4 AccumulatedLighting = float4(0, 0, 0, 1);
    float EyeSpaceZ = LinearEyeDepth(z, _ZBufferParams);
    if(VFog_Extinction > 0)
    {
        float3 uvw = float3(uv, EyeDepthToVolumeW(EyeSpaceZ));
        AccumulatedLighting = _ScatteringLightResult.Sample(sampler_TrilinearClamp, uvw);
    }
    if(VFog_OutsideIntensity > 0)
    {
        float3 WorldPos = NDCToWorldPostion(float3(uv * 2 - 1, z));
        float3 VectorToTarget = WorldPos.xyz - _WorldSpaceCameraPos;
        float Distance = sqrt(dot(VectorToTarget, VectorToTarget));

        if(Distance > VFog_GridZParams.w)
        {
            float3 Dir = VectorToTarget / Distance;
            float3 V = -Dir;
            
            float Transmittance = CalFog(Dir, Distance);
            float3 L = -_MainLightPosition.xyz;
            float3 LightScattering = VFog_AmbientLight + _MainLightColor.xyz * lerp(VFog_ForwardScatteringColor, VFog_BackwardScatteringColor, dot(V, L) * 0.5 + 0.5) * INV_TWO_PI;
            AccumulatedLighting.rgb += LightScattering * (1 - Transmittance) * AccumulatedLighting.w;
            AccumulatedLighting.w *= Transmittance;
        }
    }
    return AccumulatedLighting;
}

float3 ApplyVolumetricFog(float3 color, float3 clipPos)
{
    float2 uv = clipPos.xy * _ScreenSize.zw;
    float4 AccumulatedLighting = SampleVolumetricFog(uv, clipPos.z);
    float3 result = AccumulatedLighting.rgb + AccumulatedLighting.w * color;
    return result;
}

#endif
