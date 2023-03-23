using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GPUBaking
{
	[System.Serializable]
	public enum EDawnQualityLevel
	{
		Preview  = 0,
		Medium = 1,
		Hight = 2,
		Production = 3
	}

	[System.Serializable]
	public enum EDawnBakingMode
	{
		IndirectOnly  = 0,
		ShadowMask = 1,
		Subtractive = 2,
	}

	[System.Serializable]
	public enum ELightmapDenoiserMode
	{
		Optix = 0,
		OpenImage_LM,
		OpenImage_RT,
		Disable
	}

	[System.Serializable]
	public enum EDawnBakingResultMode
	{
		DawnBakeResultAsset = 0,
		UnityLightingAsset = 1,
		DawnAndUnityAsset = 2,
	}

	[System.Serializable]
	public enum EDawnDirectionalMode
	{
		NonDirectional = 0,
		Directional = 1,
	}

	[System.Serializable]
	public enum EDawnLightmapCompressionMode
	{
		Uncompressed = 0,
		Compressed = 1,
		CompressedHQ = 2,
		CompressedLQ = 3
	}

	[System.Serializable]
	public struct DawnLightmapAtlasSettings
	{
		[HideInInspector]
		public int MaxLightmapCount;
		public int MaxLightmapSize;
		public int TexelPerUnit;
		public bool Padding;
		public EDawnLightmapCompressionMode CompressMode;
	}

	[System.Serializable]
	public struct DawnLightmap2DSettings
	{
		public int SamplesPerPixel;
		public int MaxBounces;
		public int MaxSkyBounces;
		[HideInInspector]
		public float PenumbraShadowFraction;
		public float RasterizationBias;
		public ELightmapDenoiserMode Denoiser;
		public EDawnDirectionalMode DirectionalMode;
		public bool bUseHDRLightmap;
	}

	[System.Serializable]
	public struct DawnLightProbeAutoGenerationSettings
	{
		public bool AutoGeneration;
		public int NumSurfaceLayers;
		public float SurfaceSpacing;
		public float FirstLayerHeight;
		public float LayerHeight;
		[HideInInspector]
		public float DetailVolumeSampleSpacing;
		[HideInInspector]
		public float VolumeLightSampleSpacing;
		[HideInInspector]
		public int MaxVolumeSamples;
		[HideInInspector]
		public int MaxSurfaceLightSamples;
		[HideInInspector]
		public bool bUseMaxSurfaceSampleNum;
		public bool ForAllSurface;
		public bool GenerateUnityLightProbeGroup;
	}

	[System.Serializable]
	public struct DawnLightProbeSettings
	{	
		public int SamplesPerPixel;
		public int MaxBounces;
		[HideInInspector]
		public float WindowingTargetLaplacian;
		public DawnLightProbeAutoGenerationSettings AutoGenerationSetting;
	}

	[System.Serializable]
	public struct DawnShadowMaskSettings
	{
		public bool Enabled;
		//[HideInInspector]
		public int MaxUpSamplingFactor;
		[HideInInspector]
		public float ApproximateMaxTransitionDistance;
		[HideInInspector]
		public float MaxTransitionDistanceWorldSpace;
		[HideInInspector]
		public int MinDistanceFieldUpsampleFactor;
	}

	[System.Serializable]
	public struct DawnAmbientOcclusionSettings
	{
		public bool Enabled;
		public float MaxAmbientDistance;
		public float DirectFraction;
		public float IndirectFraction;
		public float OcclusionExponent;
	}

	[System.Serializable]
	public struct DawnSeamSettings
	{
		public bool EnableFix;
		[HideInInspector]
		public int NumSamples;
		[HideInInspector]
		public float Lambda;
		[HideInInspector]
		public float CosNormalThreshold;
	}

	[System.Serializable]
	public struct DawnMiscSettings
	{
		[HideInInspector]
		public bool UseConservativeTexelRasterization;
		[HideInInspector]
		public float MaxOffset;
		[HideInInspector]
		public float SmallestTexelRadius;
		public float NormalOffsetScale;
		public float TangentOffsetScale;
		[HideInInspector]
		public int NormalTextureResolution;

		[HideInInspector]
		public float LightmapScaleFraction;

		public bool bUseMetaPass;
		[HideInInspector]
		public bool bUseSceneNameForOutputPath;	

		public float AlbedoBoost;
		public float IndirectIntensity;

		public int SharedLODIndex;
	}
#if UNITY_EDITOR
	[CreateAssetMenu(fileName = "DawnSettingAsset", menuName = "Dawn/Dawn Baking Settings", order = 0)]
#endif
	[System.Serializable]
	public class DawnSettings : ScriptableObject {

		[HideInInspector]
		public EDawnQualityLevel QualityLevel;

		public EDawnBakingMode BakingMode;

		public EDawnBakingResultMode BakingResultMode;

		public DawnLightmapAtlasSettings AtlasSettings;

        public DawnLightmap2DSettings LightmapSettings;

		//[HideInInspector]
		public DawnShadowMaskSettings ShadowSettings;

		public DawnLightProbeSettings LightProbeSettings;

		public DawnAmbientOcclusionSettings OcclusionSettings;

		public DawnSeamSettings SeamSettings;

		public DawnMiscSettings MiscSettings;

		const float UE4_UNITY_UNIT_SCALE = 1 / 100.0f;

		private static DawnSettings InnerInstance = null;
		public static DawnSettings Instance
		{
			get
			{
				if (!InnerInstance)
				{
					InnerInstance = ScriptableObject.CreateInstance<DawnSettings>();
				}

				return InnerInstance;
			}
			set
			{
				InnerInstance = value;
			}
		}
		
		public DawnSettings()
		{
            QualityLevel = EDawnQualityLevel.Production;
			BakingMode = EDawnBakingMode.ShadowMask;
			BakingResultMode = EDawnBakingResultMode.DawnBakeResultAsset;
#if UNITY_2022
			BakingResultMode = EDawnBakingResultMode.UnityLightingAsset;
#endif

			AtlasSettings.MaxLightmapSize = 2048;
			AtlasSettings.MaxLightmapCount = 3;
			AtlasSettings.TexelPerUnit = 10;
			AtlasSettings.Padding = true;
			AtlasSettings.CompressMode = EDawnLightmapCompressionMode.Uncompressed;

			LightmapSettings.Denoiser = ELightmapDenoiserMode.Optix;			
			LightmapSettings.SamplesPerPixel = 1024;
			LightmapSettings.MaxBounces = 4;
			LightmapSettings.MaxSkyBounces = 4;
			LightmapSettings.PenumbraShadowFraction = 0.3f;
			LightmapSettings.RasterizationBias = 1.0f;
			LightmapSettings.DirectionalMode = EDawnDirectionalMode.NonDirectional;
			LightmapSettings.bUseHDRLightmap = true;

			ShadowSettings.Enabled = true;
			ShadowSettings.MaxUpSamplingFactor = 4;
			ShadowSettings.ApproximateMaxTransitionDistance = 1;
			ShadowSettings.MaxTransitionDistanceWorldSpace = 0.5f;
			ShadowSettings.MinDistanceFieldUpsampleFactor = 5;
			
			LightProbeSettings.MaxBounces = 4;
			LightProbeSettings.SamplesPerPixel = 512;
			LightProbeSettings.WindowingTargetLaplacian = 0.0f;

			LightProbeSettings.AutoGenerationSetting.AutoGeneration = false;
			LightProbeSettings.AutoGenerationSetting.NumSurfaceLayers = 2;
			LightProbeSettings.AutoGenerationSetting.SurfaceSpacing = 3.0f;
			LightProbeSettings.AutoGenerationSetting.FirstLayerHeight = 0.5f;
			LightProbeSettings.AutoGenerationSetting.LayerHeight = 2.5f;
			LightProbeSettings.AutoGenerationSetting.DetailVolumeSampleSpacing = 3.0f;
			LightProbeSettings.AutoGenerationSetting.VolumeLightSampleSpacing = 30.0f;
			LightProbeSettings.AutoGenerationSetting.MaxVolumeSamples = 250000;
			LightProbeSettings.AutoGenerationSetting.MaxSurfaceLightSamples = 500000;
			LightProbeSettings.AutoGenerationSetting.bUseMaxSurfaceSampleNum = true;
			LightProbeSettings.AutoGenerationSetting.ForAllSurface = true;
			LightProbeSettings.AutoGenerationSetting.GenerateUnityLightProbeGroup = true;

			OcclusionSettings.Enabled = false;
			OcclusionSettings.MaxAmbientDistance = 0.2f;
			OcclusionSettings.DirectFraction = 1.0f;
			OcclusionSettings.IndirectFraction = 1.0f;
			OcclusionSettings.OcclusionExponent = 2.0f;

			SeamSettings.EnableFix = false;
			SeamSettings.CosNormalThreshold = 0.5f;
			SeamSettings.Lambda = 0.1f;
			SeamSettings.NumSamples = 1;

			MiscSettings.UseConservativeTexelRasterization = true;
			MiscSettings.SmallestTexelRadius = 0.1f * UE4_UNITY_UNIT_SCALE;
			MiscSettings.MaxOffset = 10.0f;
			MiscSettings.NormalOffsetScale = 0.5f;
			MiscSettings.TangentOffsetScale = 0.8f;
            MiscSettings.NormalTextureResolution = 128;

			MiscSettings.LightmapScaleFraction = 0.8f;

			MiscSettings.bUseMetaPass = true;
			MiscSettings.bUseSceneNameForOutputPath = false;

			MiscSettings.AlbedoBoost = 1.0f;
			MiscSettings.IndirectIntensity = 1.0f;

			MiscSettings.SharedLODIndex = -1;
		}
	}
}