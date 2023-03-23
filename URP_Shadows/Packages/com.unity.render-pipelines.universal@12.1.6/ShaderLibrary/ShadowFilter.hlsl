#ifndef SHADOW_FILTER_INCLUDED
#define SHADOW_FILTER_INCLUDED

struct ShadowSettings
{
    float Fade;
    float SceneDepth;
    float2 ShadowPosition;
    Texture2D Shadowmap;
    SamplerState ShadowmapSampler;
    float4 ShadowmapTexelSize;
    bool Subsurface;
};

// #define USE_GATHER 1

float4 FastExp(float4 x)
{
    return exp2(1.442695f * x);
}

float4 CalculateOcclusion(float4 SamplesDepth, ShadowSettings Settings)
{
    [branch]
    if(Settings.Subsurface)
    {
#if UNITY_REVERSED_Z
        float4 Thickness = max(SamplesDepth - Settings.SceneDepth, 0);
#else
        float4 Thickness = max(Settings.SceneDepth - SamplesDepth, 0);
#endif
        float4 Occlusion = saturate(FastExp(-Thickness * Settings.Fade));
        return Occlusion;
    }
    else
    {
#if UNITY_REVERSED_Z
        float4 Contrast = ( Settings.SceneDepth - SamplesDepth) * Settings.Fade;
#else
        float4 Contrast = (SamplesDepth - Settings.SceneDepth) * Settings.Fade;
#endif
        float4 ShadowFactor = saturate(1 + Contrast);
        return ShadowFactor;
    }
}

// lowest quality ith PCF
float PCF1x1(float2 Fraction, float4 Values00)
{
    float2 HorizontalLerp00 = lerp(Values00.wx, Values00.zy, Fraction.xx);

    return lerp(HorizontalLerp00.x, HorizontalLerp00.y, Fraction.y);
}

float ManualPCF(ShadowSettings Settings)
{
    float2 TexelPos = Settings.ShadowPosition * Settings.ShadowmapTexelSize.zw - 0.5f;
    float2 Fraction = frac(TexelPos);
    float2 TexelCenter = floor(TexelPos) + 0.5f;
    float4 Samples;
   
#if USE_GATHER
    Samples = GATHER_TEXTURE2D_X(Settings.Shadowmap, Settings.ShadowmapSampler, TexelCenter * Settings.ShadowmapTexelSize.xy); 
#else
    Samples.x = Settings.Shadowmap.Sample(Settings.ShadowmapSampler, (TexelCenter + float2(0, 1)) * Settings.ShadowmapTexelSize.xy).r;
    Samples.y = Settings.Shadowmap.Sample(Settings.ShadowmapSampler, (TexelCenter + float2(1, 1)) * Settings.ShadowmapTexelSize.xy).r;
    Samples.z = Settings.Shadowmap.Sample(Settings.ShadowmapSampler, (TexelCenter + float2(1, 0)) * Settings.ShadowmapTexelSize.xy).r;
    Samples.w = Settings.Shadowmap.Sample(Settings.ShadowmapSampler, (TexelCenter + float2(0, 0)) * Settings.ShadowmapTexelSize.xy).r;
#endif
    
    float4 Values00 = CalculateOcclusion(Samples, Settings);
    return PCF1x1(Fraction, Values00);
}

float4 FetchRowOfFour(float2 Sample00TexelCenter, float VerticalOffset, ShadowSettings Settings)
{
    float4 Values0;
    Values0.x = Settings.Shadowmap.Sample(Settings.ShadowmapSampler, (Sample00TexelCenter + float2(0, VerticalOffset)) * Settings.ShadowmapTexelSize.xy).r;
    Values0.y = Settings.Shadowmap.Sample(Settings.ShadowmapSampler, (Sample00TexelCenter + float2(1, VerticalOffset)) * Settings.ShadowmapTexelSize.xy).r;
    Values0.z = Settings.Shadowmap.Sample(Settings.ShadowmapSampler, (Sample00TexelCenter + float2(2, VerticalOffset)) * Settings.ShadowmapTexelSize.xy).r;
    Values0.w = Settings.Shadowmap.Sample(Settings.ShadowmapSampler, (Sample00TexelCenter + float2(3, VerticalOffset)) * Settings.ShadowmapTexelSize.xy).r;
    Values0 = CalculateOcclusion(Values0, Settings);
    return Values0;
}

float PCF3x3(float2 Fraction, float4 Values0, float4 Values1, float4 Values2, float4 Values3)
{
    float4 Results;

    Results.x = Values0.x * (1.0f - Fraction.x);
    Results.y = Values1.x * (1.0f - Fraction.x);
    Results.z = Values2.x * (1.0f - Fraction.x);
    Results.w = Values3.x * (1.0f - Fraction.x);
    Results.x += Values0.y;
    Results.y += Values1.y;
    Results.z += Values2.y;
    Results.w += Values3.y;
    Results.x += Values0.z;
    Results.y += Values1.z;
    Results.z += Values2.z;
    Results.w += Values3.z;
    Results.x += Values0.w * Fraction.x;
    Results.y += Values1.w * Fraction.x;
    Results.z += Values2.w * Fraction.x;
    Results.w += Values3.w * Fraction.x;

    return saturate(dot(Results, float4(1.0f - Fraction.y, 1.0f, 1.0f, Fraction.y)) * (1.0f / 9.0f));
}

// linear PCF, input 4x4
// using Gather: xyzw in counter clockwise order starting with the sample to the lower left of the queried location
// @param Values0 left top
// @param Values1 right top
// @param Values2 left bottom
// @param Values3 right bottom
float PCF3x3gather(float2 Fraction, float4 Values0, float4 Values1, float4 Values2, float4 Values3)
{
    float4 Results;

    Results.x = Values0.w * (1.0 - Fraction.x);
    Results.y = Values0.x * (1.0 - Fraction.x);
    Results.z = Values2.w * (1.0 - Fraction.x);
    Results.w = Values2.x * (1.0 - Fraction.x);
    Results.x += Values0.z;
    Results.y += Values0.y;
    Results.z += Values2.z;
    Results.w += Values2.y;
    Results.x += Values1.w;
    Results.y += Values1.x;
    Results.z += Values3.w;
    Results.w += Values3.x;
    Results.x += Values1.z * Fraction.x;
    Results.y += Values1.y * Fraction.x;
    Results.z += Values3.z * Fraction.x;
    Results.w += Values3.y * Fraction.x;

    return dot( Results, float4( 1.0 - Fraction.y, 1.0, 1.0, Fraction.y) * ( 1.0 / 9.0) );
}

float Manual3x3PCF(ShadowSettings Settings)
{
    float2 TexelPos = Settings.ShadowPosition * Settings.ShadowmapTexelSize.zw - 0.5f;
    float2 Fraction = frac(TexelPos);
    float2 TexelCenter = floor(TexelPos) + 0.5f;
    float2 Sample00TexelCenter = TexelCenter - float2(1, 1);

    float4 SampleValues0, SampleValues1, SampleValues2, SampleValues3;
#if USE_GATHER
    float2 SamplePos = TexelCenter * Settings.ShadowmapTexelSize.xy;	// bias to get reliable texel center content
    SampleValues0 = CalculateOcclusion(Settings.Shadowmap.GatherRed(Settings.ShadowmapSampler, SamplePos, int2(-1, -1)), Settings);
    SampleValues1 = CalculateOcclusion(Settings.Shadowmap.GatherRed(Settings.ShadowmapSampler, SamplePos, int2(1, -1)),  Settings);
    SampleValues2 = CalculateOcclusion(Settings.Shadowmap.GatherRed(Settings.ShadowmapSampler, SamplePos, int2(-1, 1)),  Settings);
    SampleValues3 = CalculateOcclusion(Settings.Shadowmap.GatherRed(Settings.ShadowmapSampler, SamplePos, int2(1, 1)),   Settings);
    return PCF3x3gather(Fraction, SampleValues0, SampleValues1, SampleValues2, SampleValues3);
#else
    SampleValues0 = FetchRowOfFour(Sample00TexelCenter, 0, Settings);
    SampleValues1 = FetchRowOfFour(Sample00TexelCenter, 1, Settings);
    SampleValues2 = FetchRowOfFour(Sample00TexelCenter, 2, Settings);
    SampleValues3 = FetchRowOfFour(Sample00TexelCenter, 3, Settings);
    return PCF3x3(Fraction, SampleValues0, SampleValues1, SampleValues2, SampleValues3);
#endif
}

// horizontal PCF, input 6x2
float2 HorizontalPCF5x2(float2 Fraction, float4 Values00, float4 Values20, float4 Values40)
{
    float Results0;
    float Results1;

    Results0 = Values00.w * (1.0 - Fraction.x);
    Results1 = Values00.x * (1.0 - Fraction.x);
    Results0 += Values00.z;
    Results1 += Values00.y;
    Results0 += Values20.w;
    Results1 += Values20.x;
    Results0 += Values20.z;
    Results1 += Values20.y;
    Results0 += Values40.w;
    Results1 += Values40.x;
    Results0 += Values40.z * Fraction.x;
    Results1 += Values40.y * Fraction.x;

    return float2(Results0, Results1);
}

float Manual6x6PCF(ShadowSettings Settings)
{
#if USE_GATHER
    float2 TexelPos = Settings.ShadowPosition * Settings.ShadowmapTexelSize.zw - 0.5f;	// bias to be consistent with texture filtering hardware
	float2 Fraction = frac(TexelPos);
	float2 TexelCenter = floor(TexelPos);
	float2 SamplePos = (TexelCenter + 0.5f) * Settings.ShadowmapTexelSize.xy;	// bias to get reliable texel center content

	float Results;

	float4 Values00 = CalculateOcclusion(Settings.Shadowmap.Gather(Settings.ShadowmapSampler, SamplePos, int2(-2, -2)), Settings);
	float4 Values20 = CalculateOcclusion(Settings.Shadowmap.Gather(Settings.ShadowmapSampler, SamplePos, int2(0, -2)), Settings);
	float4 Values40 = CalculateOcclusion(Settings.Shadowmap.Gather(Settings.ShadowmapSampler, SamplePos, int2(2, -2)), Settings);

	float2 Row0 = HorizontalPCF5x2(Fraction, Values00, Values20, Values40);
	Results = Row0.x * (1.0f - Fraction.y) + Row0.y;

	float4 Values02 = CalculateOcclusion(Settings.Shadowmap.Gather(Settings.ShadowmapSampler, SamplePos, int2(-2, 0)), Settings);
	float4 Values22 = CalculateOcclusion(Settings.Shadowmap.Gather(Settings.ShadowmapSampler, SamplePos, int2(0, 0)), Settings);
	float4 Values42 = CalculateOcclusion(Settings.Shadowmap.Gather(Settings.ShadowmapSampler, SamplePos, int2(2, 0)), Settings);

	float2 Row1 = HorizontalPCF5x2(Fraction, Values02, Values22, Values42);
	Results += Row1.x + Row1.y;

	float4 Values04 = CalculateOcclusion(Settings.Shadowmap.Gather(Settings.ShadowmapSampler, SamplePos, int2(-2, 2)), Settings);
	float4 Values24 = CalculateOcclusion(Settings.Shadowmap.Gather(Settings.ShadowmapSampler, SamplePos, int2(0, 2)), Settings);
	float4 Values44 = CalculateOcclusion(Settings.Shadowmap.Gather(Settings.ShadowmapSampler, SamplePos, int2(2, 2)), Settings);

	float2 Row2 = HorizontalPCF5x2(Fraction, Values04, Values24, Values44);
	Results += Row2.x + Row2.y * Fraction.y;

	return 0.04f * Results;
#else
    return Manual3x3PCF(Settings);
#endif
}
#endif