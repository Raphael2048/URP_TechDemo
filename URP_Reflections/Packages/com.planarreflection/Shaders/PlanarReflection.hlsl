#ifndef PLANAR_REFLECTION
#define PLANAR_REFLECTION

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

TEXTURE2D(_PlanarReflectionTexture);
TEXTURE2D(_PlanarReflectionIntensity);
float4 _ReflectionPlane;
float4 _ReflectionPlaneParams;

SamplerState sampler_LinearClamp;

float3 SamplePlanarReflection(float2 uv)
{
    return _PlanarReflectionTexture.SampleLevel(sampler_LinearClamp, uv, 0).rgb;
}

float SamplePlanarReflectionIntensity(float2 uv)
{
    return _PlanarReflectionIntensity.SampleLevel(sampler_LinearClamp, uv, 0).r;
}

float3 SamplePlanarReflectionResultWithRoughness(float2 uv, float3 V, float roughness)
{
    float3 color = SamplePlanarReflection(uv);

    if(_ReflectionPlaneParams.x == 1)
    {
        float intensity = SamplePlanarReflectionIntensity(uv);
        
        half2 vignette = saturate(abs(uv * 2.0 - 1.0) * 5.0 - 4.0);
        float alpha = saturate(1 - dot(vignette, vignette));

        if (intensity <= 0.01) alpha = 0;
        
        UNITY_BRANCH
        if(alpha < 1)
        {
            float3 ReflectDir = reflect(V, _ReflectionPlane.xyz);
            half3 ReflectColor = GlossyEnvironmentReflection(-ReflectDir, roughness, 1);
            color = lerp(ReflectColor, color, alpha);
        }
    }
    
    return color;
}

#endif