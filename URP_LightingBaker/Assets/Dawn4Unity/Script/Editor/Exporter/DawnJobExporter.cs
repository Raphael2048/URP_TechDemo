using UnityEngine;
using UnityEditor;

using NSwarm;

namespace GPUBaking.Editor
{
	public partial class DawnExporter
    {
		public void GatherJobs(DawnBakingContext Context)
        {
			DawnProfiler.BeginSample ("GatherLightmap2DJobs");
			GatherLightmap2DJobs (Context);
			DawnProfiler.EndSample ();

			DawnProfiler.BeginSample ("GatherShadowMaskJobs");
			GatherShadowMaskJobs(Context);
			DawnProfiler.EndSample ();

			if (Context.BakingMode == DawnBakingMode.Default)
			{
				DawnProfiler.BeginSample ("GatherLightProbes");
				GatherLightProbes (Context);
				DawnProfiler.EndSample ();
			}

			PackLightmaps(Context, Context.Settings.AtlasSettings.MaxLightmapSize, Context.Settings.AtlasSettings.MaxLightmapSize);

			{
				DawnProfiler.BeginSample ("GatherLightmap2DJobGroups");
				GatherLightmap2DJobGroups (Context);
				DawnProfiler.EndSample ();

				DawnProfiler.BeginSample ("GatherShadowMaskJobGroups");
				GatherShadowMaskJobGroups (Context);
				DawnProfiler.EndSample ();
			}
        }

		public bool ExportJobs(DawnBakingContext Context,ref FGuid SceneGuid)
		{
			bool bSuccessed = ExportJobsInternel (Context,ref SceneGuid);

			State = EExportingState.JOB_SERIALIZING;

			bSuccessed = bSuccessed && ImportExportUtil.SerializeJobs (LightingSystem.SwarmInterface, ref SceneGuid,ref BakingJobInputs,true);

			return bSuccessed;
		}

		public bool ExportJobs(DawnBakingContext Context,ref FGuid SceneGuid,string Path)
		{
			bool bSuccessed = ExportJobsInternel (Context,ref SceneGuid);

			bSuccessed = bSuccessed && ImportExportUtil.SerializeJobs (Path, ref SceneGuid,ref BakingJobInputs,true);

			return bSuccessed;
		}

		bool ExportJobsInternel(DawnBakingContext Context,ref FGuid SceneGuid)
		{
			State = EExportingState.JOB_SETTINGS;

			DawnProfiler.BeginSample ("ExportSettings");

			bool bSuccessed = ExportSettings (Context);

			DawnProfiler.EndSample ();

			State = EExportingState.JOB_LIGHTMAP2D;

			DawnProfiler.BeginSample ("ExportLightmap2DJobs");

			ExportLightmap2DJobs (Context);

			DawnProfiler.EndSample ();

			State = EExportingState.JOB_SHADOWMASK;

			DawnProfiler.BeginSample ("ExportShadowMaskJobs");

			bSuccessed = bSuccessed && ExportShadowMaskJobs(Context);

			DawnProfiler.EndSample ();

			State = EExportingState.JOB_LIGHTMAP2D;

			DawnProfiler.BeginSample ("ExportLightmap2DJobGroups");

			ExportLightmap2DJobGroups (Context);

			DawnProfiler.EndSample ();

			DawnProfiler.BeginSample ("ExportShadowMaskJobGroups");

			ExportShadowMaskJobGroups (Context);

			DawnProfiler.EndSample ();

			State = EExportingState.JOB_LIGHTPROBE;

			DawnProfiler.BeginSample ("ExportLightProbeJobs");

			bSuccessed = bSuccessed && ExportLightProbeJobs(Context);

			DawnProfiler.EndSample ();

			ExportDebugInput (Context,ref Context.Settings, ref BakingJobInputs.DebugInput);

			return bSuccessed;
		}

		bool ExportSettings(DawnBakingContext Context)
		{
			ExportBakingParameters (Context,ref Context.Settings, ref BakingJobInputs.BakingParameters);

			ExportSparseVolumetricSamplesParameter(Context, ref Context.Settings.LightProbeSettings, ref BakingJobInputs.SparseVolumetricSamplesParameter);
			BakingJobInputs.AdaptiveVolumetricLightmapParameter.WindowingTargetLaplacian = Context.Settings.LightProbeSettings.WindowingTargetLaplacian;
			
			BakingJobInputs.ShadowSettingParameter.ApproximateHighResTexelsPerMaxTransitionDistance = (int)Context.Settings.ShadowSettings.ApproximateMaxTransitionDistance;
			BakingJobInputs.ShadowSettingParameter.bAllowSignedDistanceFieldShadows = ToByte(Context.Settings.ShadowSettings.Enabled);
			BakingJobInputs.ShadowSettingParameter.MaxTransitionDistanceWorldSpace = Context.Settings.ShadowSettings.MaxTransitionDistanceWorldSpace;
			BakingJobInputs.ShadowSettingParameter.MinDistanceFieldUpsampleFactor = Context.Settings.ShadowSettings.MinDistanceFieldUpsampleFactor;

			BakingJobInputs.PrecomputedVisibilityParameter.HeaderInfo.bPlaceCellsOnOpaqueOnly = ToByte(Context.Settings.LightmapSettings.DirectionalMode == EDawnDirectionalMode.Directional);

			return true;
		}

		void ExportBakingParameters(DawnBakingContext Context,ref DawnSettings Settings, ref FBakingJobParameters BakingParameters)
		{
			BakingParameters.bUseFastVoxelization = 0;
			{
				BakingParameters.Lightmap2DParameter.DenoiserMode = (int)Settings.LightmapSettings.Denoiser;
				BakingParameters.Lightmap2DParameter.IterationNum = Settings.LightmapSettings.SamplesPerPixel;
				BakingParameters.Lightmap2DParameter.MaxDepth = Settings.LightmapSettings.MaxBounces;
				BakingParameters.Lightmap2DParameter.MaxSkyBounces = Settings.LightmapSettings.MaxSkyBounces;
				BakingParameters.Lightmap2DParameter.PenumbraShadowFraction = Settings.LightmapSettings.PenumbraShadowFraction;
				BakingParameters.Lightmap2DParameter.SuperSampleFactor = 1;
				BakingParameters.Lightmap2DParameter.OffsetScale = 1;
			}
			{
				BakingParameters.SdfShadowParameter.MaxUpsamplingFactor = Context.Settings.ShadowSettings.MaxUpSamplingFactor;
			}
			{
				BakingParameters.AmbientOcclusionParameter.bDebugAmbientOcclusion = ToByte (false);
				BakingParameters.AmbientOcclusionParameter.bUseAmbientOcclusion = (uint)(Settings.OcclusionSettings.Enabled ? 1 : 0);
				BakingParameters.AmbientOcclusionParameter.MaxAmbientOcclusion = Mathf.Clamp(Settings.OcclusionSettings.MaxAmbientDistance,0.05f,2.0f);
				BakingParameters.AmbientOcclusionParameter.DirectAmbientOcclusionFactor = Settings.OcclusionSettings.DirectFraction;
				BakingParameters.AmbientOcclusionParameter.IndirectAmbientOcclusionFactor = Settings.OcclusionSettings.IndirectFraction;
				BakingParameters.AmbientOcclusionParameter.OcclusionExponent = Mathf.Clamp(Settings.OcclusionSettings.OcclusionExponent,1.0f,4.0f);
			}
			{
				BakingParameters.AdaptiveSamplingParameter.AdaptiveStartBounces = 3;
				BakingParameters.AdaptiveSamplingParameter.AdaptiveIterationStartNum = 16;
				BakingParameters.AdaptiveSamplingParameter.AdaptiveIterationStep = 32;
				BakingParameters.AdaptiveSamplingParameter.AdaptiveMaxError = 0.05f;
			}
			{
				BakingParameters.LightProbeParameter.IterationNumForLightProbe = Settings.LightProbeSettings.SamplesPerPixel;
				BakingParameters.LightProbeParameter.MaxDepthForLightProbe = Settings.LightProbeSettings.MaxBounces;
			}
			{
				BakingParameters.ArtifactParameter.bFillUnmappedTexel = ToByte(true);
				BakingParameters.ArtifactParameter.bSeamFixed = Settings.SeamSettings.EnableFix ? 1 : 0;
				BakingParameters.ArtifactParameter.SeamSampleIteration = Settings.SeamSettings.NumSamples;
				BakingParameters.ArtifactParameter.SeamLambda = Settings.SeamSettings.Lambda;
				BakingParameters.ArtifactParameter.SeamCosNormalThreshold = Settings.SeamSettings.CosNormalThreshold;
			}
			{
				BakingParameters.PrecomputedTransferParameters.bPrecomputedRadianceTransfer = ToByte (false);
				BakingParameters.PrecomputedTransferParameters.bPrecomputedRadianceTransferForSurface = ToByte (false);
				BakingParameters.PrecomputedTransferParameters.bPrecomputedLocalRadianceTransfer = ToByte (false);
			}
			{
				BakingParameters.RasterizationParameter.bUseMaxWeight = ToByte (true);
				BakingParameters.RasterizationParameter.RasterizationBias = Settings.LightmapSettings.RasterizationBias;
				BakingParameters.RasterizationParameter.bUseConservativeTexelRasterization = ToByte (Settings.MiscSettings.UseConservativeTexelRasterization);
				BakingParameters.RasterizationParameter.SmallestTexelRadius = Settings.MiscSettings.SmallestTexelRadius;
			}
			{
				BakingParameters.RayTracingParameter.MinNormalOffset = 0.5f;
				BakingParameters.RayTracingParameter.MaxRayOffset = Settings.MiscSettings.MaxOffset;
				BakingParameters.RayTracingParameter.MinRayOffset = 0.0001f;
				BakingParameters.RayTracingParameter.NormalOffsetSampleRadiusScale = Settings.MiscSettings.NormalOffsetScale;
				BakingParameters.RayTracingParameter.TangentOffsetSampleRadiusScale = Settings.MiscSettings.TangentOffsetScale;
			}
		}

		void ExportSparseVolumetricSamplesParameter(DawnBakingContext Context, ref DawnLightProbeSettings LightProbeSettings, ref FSparseVolumetricSamplesParameters SparseVolumetricSamplesParameter)
        {
			SparseVolumetricSamplesParameter.NumSurfaceSampleLayers = LightProbeSettings.AutoGenerationSetting.NumSurfaceLayers;
			SparseVolumetricSamplesParameter.SurfaceLightSampleSpacing = LightProbeSettings.AutoGenerationSetting.SurfaceSpacing;
			SparseVolumetricSamplesParameter.FirstSurfaceSampleLayerHeight = LightProbeSettings.AutoGenerationSetting.FirstLayerHeight;
			SparseVolumetricSamplesParameter.SurfaceSampleLayerHeightSpacing = LightProbeSettings.AutoGenerationSetting.LayerHeight;
			SparseVolumetricSamplesParameter.DetailVolumeSampleSpacing = LightProbeSettings.AutoGenerationSetting.DetailVolumeSampleSpacing;
			SparseVolumetricSamplesParameter.VolumeLightSampleSpacing = LightProbeSettings.AutoGenerationSetting.VolumeLightSampleSpacing;
			SparseVolumetricSamplesParameter.MaxVolumeSamples = LightProbeSettings.AutoGenerationSetting.MaxVolumeSamples;
			SparseVolumetricSamplesParameter.MaxSurfaceLightSamples = LightProbeSettings.AutoGenerationSetting.MaxSurfaceLightSamples;
			SparseVolumetricSamplesParameter.bUseMaxSurfaceSampleNum = ToByte(LightProbeSettings.AutoGenerationSetting.bUseMaxSurfaceSampleNum);
		}
		void ExportDebugInput (DawnBakingContext Context,ref DawnSettings Settings, ref  FBakingDebugInput DebugInput)
		{
			if (SceneView.lastActiveSceneView != null) {
				var Camera = SceneView.lastActiveSceneView.camera;
				DebugInput.CameraPosition = ToFloat3 (Camera.transform.position);
				DebugInput.LookatPosition = ToFloat3 (Camera.transform.position + Camera.transform.forward * 30);
			}

			if (Dawn4UnityDebugEditor.SelectionHitInfo.Renderer != null && LightingSystem.bDebugLightmapTexel) {
				var DebugMesh = Dawn4UnityDebugEditor.SelectionHitInfo;
				DebugInput.LocalX = DebugMesh.DebugUV.x;
				DebugInput.LocalY = DebugMesh.DebugUV.y;
				DebugInput.SizeX = DebugMesh.DebugSize.x;
				DebugInput.SizeY = DebugMesh.DebugSize.y;
				DebugInput.MeshID = DebugMesh.MeshID;

				DawnDebug.LogFormat ("DebugMesh:{0},X={1},Y={2},W={3},H={4}",DebugMesh.Renderer.name,DebugInput.LocalX,DebugInput.LocalY,DebugInput.SizeX,DebugInput.SizeY);
			}
		}
    }
}
