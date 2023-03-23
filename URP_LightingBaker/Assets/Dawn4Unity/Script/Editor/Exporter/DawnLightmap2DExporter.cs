using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

using NSwarm;
using AgentInterface;

namespace GPUBaking.Editor
{
	public partial class DawnExporter
    {
		bool WillBakingLightmap(DawnBakingContext Context, DawnLightmap2DTask Lightmap2D)
        {
			bool bResult = false;
			switch(Context.BakingMode)
            {
				case DawnBakingMode.BakingSelected:
					var lightmapGameObject = Lightmap2D.gameObject;
					foreach (var selectedGameObject in Selection.gameObjects)
                    {
                        if (lightmapGameObject == selectedGameObject) 
						{
							bResult = true;
							break; 
						}
					}
					break;
				default:
					bResult = true;
					break;
			}
			return bResult;
		}
		void GatherLightmap2DJobs(DawnBakingContext Context)
		{
			foreach (var Lightmap2D in Context.LightmapList) {
				// check repeated tasks
				if (!LightingSystem.PendingTasks.ContainsKey(Lightmap2D.Guid))
                {
					if(WillBakingLightmap(Context, Lightmap2D))
                    {
						Lightmap2D.TaskGuid = Lightmap2D.Guid;
						LightingSystem.PendingTasks.Add(Lightmap2D.TaskGuid, Lightmap2D);
					}					
				}
					
			}
		}

		DawnLightmap2DTask GatherLightmap2DJob(DawnBakingContext Context, MeshGatherInfo MeshInstance)
		{
			DawnProfiler.BeginSample ("GatherLightmap2DJob");

			int LightmapSize = CalculateLightmapSize(Context,MeshInstance);

			DawnDebug.Print ("GatherLightmap {0}x{1} For {2}", LightmapSize, LightmapSize, MeshInstance.name);

			var Lightmap2D = new DawnLightmap2DTask (MeshInstance,LightmapSize,LightmapSize, Context.Settings.AtlasSettings.Padding);

			DawnProfiler.EndSample ();
			return Lightmap2D;
		}
		
		DawnLightmap2DTask GatherLightmap2DJob(DawnBakingContext Context, LandscapeGatherInfo LandscapeInfo)
		{
			DawnProfiler.BeginSample ("GatherLightmap2DJobForLandscape");

			int LightmapSize = CalculateLightmapSize(Context, LandscapeInfo);

			DawnDebug.Print ("GatherLightmap {0}x{1} For {2}", LightmapSize, LightmapSize, LandscapeInfo.name);

			var Lightmap2D = new DawnLightmap2DTask (LandscapeInfo,LightmapSize,LightmapSize, Context.Settings.AtlasSettings.Padding);

			DawnProfiler.EndSample ();
			return Lightmap2D;
		}


		public static int GetRequestLightmapSize(DawnSettings Settings, DawnMeshComponent MeshComponent, Renderer Renderer, MeshFilter Filter,DawnLightmapGroupSelector LightmapGroup)
		{
			var SerializeObj = new SerializedObject(Renderer);

			var ScaleField = SerializeObj.FindProperty("m_ScaleInLightmap");

			float ScaleInLightmap = ScaleField!= null ? ScaleField.floatValue : 1;

			float SurfaceArea = MeshComponent.GetCachedSurfaceArea (Filter);

			Rect UVBounds = ToRect(MeshComponent.GetUVBounds(Filter));// get the UV bounds to clip the lightmap

			if (UVBounds.height * UVBounds.width <= 0.0f)
			{
				UVBounds.height = UVBounds.width = 1.0f;
			}

			int MaxLightMapSize = Settings.AtlasSettings.MaxLightmapSize;

			if (LightmapGroup != null && LightmapGroup.AtlasSize > 0) {
				MaxLightMapSize = LightmapGroup.AtlasSize;
			}

			int LightmapSize = CalculateLightmapSize(ScaleInLightmap,SurfaceArea, Settings.AtlasSettings.TexelPerUnit, UVBounds);

			return LightmapSize;
		}

		int CalculateLightmapSize(DawnBakingContext Context, MeshGatherInfo MeshInstance)
		{
			Debug.Assert(MeshInstance.Renderer != null);
			
			var SerializeObj = new SerializedObject(MeshInstance.Renderer);

			var ScaleField = SerializeObj.FindProperty("m_ScaleInLightmap");

			float ScaleInLightmap = ScaleField!= null ? ScaleField.floatValue : 1;

			if (Context.bEnableUniformLightmapScale && ScaleInLightmap < 1.0f) {
				ScaleInLightmap = 1.0f;
			}

			float SurfaceArea = MeshInstance.CachedSurfaceArea;

			Rect UVBounds = ToRect(MeshInstance.CachedUVBounds);// get the UV bounds to clip the lightmap

			if (UVBounds.height * UVBounds.width <= 0.0f)
			{
				UVBounds.height = UVBounds.width = 1.0f;
			}

			int MaxLightMapSize = Context.Settings.AtlasSettings.MaxLightmapSize;

			var LightmapGroup = MeshInstance.LightmapGroup;

			if (LightmapGroup != null && LightmapGroup.AtlasSize > 0) {
				MaxLightMapSize = LightmapGroup.AtlasSize;
			}

			int LightmapSize = CalculateLightmapSize(ScaleInLightmap,SurfaceArea, Context.Settings.AtlasSettings.TexelPerUnit, UVBounds);

			//LightmapSize = Mathf.ClosestPowerOfTwo (LightmapSize);

			//DawnDebug.AssertFormat (LightmapSize > 2,"lightmapSize should {0} >= 2",MeshInstance.name,LightmapSize);

			if (Context.Settings.AtlasSettings.Padding && LightmapSize > 2)
			{
				MaxLightMapSize = MaxLightMapSize - 2;
			}

			LightmapSize = Mathf.Clamp (LightmapSize,4, MaxLightMapSize);

			return LightmapSize;
		}

		int CalculateLightmapSize(DawnBakingContext Context, LandscapeGatherInfo Landscape)
		{
			var SerializeObj = new SerializedObject(Landscape.Landscape);

			var ScaleField = SerializeObj.FindProperty("m_ScaleInLightmap");

			float ScaleInLightmap = ScaleField!= null ? ScaleField.floatValue : 1;

			float SurfaceArea = Landscape.CacheLandscapeArea;

			float NormalizedToWorldScale = Mathf.Sqrt(SurfaceArea);

			int MaxLightMapSize = Context.Settings.AtlasSettings.MaxLightmapSize;

			MaxLightMapSize = Mathf.Max(4,MaxLightMapSize);

			int LightmapSize = Mathf.FloorToInt(Context.Settings.AtlasSettings.TexelPerUnit * NormalizedToWorldScale * ScaleInLightmap);

			DawnDebug.AssertFormat (LightmapSize > 2,"lightmapSize should {0} >= 2", Landscape.name,LightmapSize);

			LightmapSize = Mathf.ClosestPowerOfTwo(LightmapSize);
			
			LightmapSize = Mathf.Clamp (LightmapSize,4, MaxLightMapSize);

			return LightmapSize;
		}

		bool ExportLightmap2DJobs(DawnBakingContext Context)
		{
			foreach (var Lightmap2D in Context.LightmapList) {
				if(!LightingSystem.PendingTasks.ContainsKey(Lightmap2D.TaskGuid))
                {
					continue;
                }
				FLightmap2DInput LightmapInput = new FLightmap2DInput ();
				ExportLightmap2DJob (Context,Lightmap2D,ref LightmapInput);
				Lightmap2D.LightmapIndex = BakingJobInputs.Lightmap2DJobs.NumElements;
				BakingJobInputs.Lightmap2DJobs.AddElement (ref LightmapInput);
			}
			return true;
		}

		void ExportLightmap2DJob(DawnBakingContext Context,DawnLightmap2DTask Lightmap2D,ref FLightmap2DInput LightmapInput)
		{
			//TODO Implement Lightmap Job Export
			DawnDebug.Print("Export Lightmap2D:{0}",Lightmap2D.Name);

			bool bPadMapping = Lightmap2D.Allocation.Padding > 0;
			bool bDirectLighting = false;

			//if(Context.Settings.BakingMode != EDawnBakingMode.IndirectOnly)
			{
				foreach (var LightIndex in Lightmap2D.RelevantLights) {
					// Light mesh instance index begins with lights count
					bool bIsLightMesh = LightIndex >= Context.Lights.Count;
					if (!bIsLightMesh)
					{
						if (Context.Lights [LightIndex].lightmapBakeType == LightmapBakeType.Baked) {
							bDirectLighting = true;
						}
						else if (Context.Lights [LightIndex].lightmapBakeType == LightmapBakeType.Mixed && Context.Settings.BakingMode == EDawnBakingMode.Subtractive) {
							bDirectLighting = true;
						}
					}
					else
					{
						bDirectLighting = true;
					}

				}
			}

			LightmapInput.JobID = ToGuidInfo (Lightmap2D.TaskGuid);
			LightmapInput.MeshID = ToGuidInfo (Lightmap2D.Guid);
			LightmapInput.Size.x = Lightmap2D.Allocation.UVWidthPixel;
			LightmapInput.Size.y = Lightmap2D.Allocation.UVHeightPixel;
			LightmapInput.LightmapUVIndex = 1;
			LightmapInput.Flags = (uint)ELightmapFlags.LIGHTMAP_FLAGS_INDIRECT;
			LightmapInput.Flags |= bDirectLighting ? (uint)ELightmapFlags.LIGHTMAP_FLAGS_DIRECT : 0;
			LightmapInput.Flags |= bPadMapping ? (uint)ELightmapFlags.LIGHTMAP_FLAGS_PADDING : 0;
			LightmapInput.Flags |= (uint)ELightmapFlags.LIGHTMAP_FLAGS_BILINEAR_FILTER;
			LightmapInput.Flags |= Context.Settings.LightmapSettings.DirectionalMode == EDawnDirectionalMode.Directional ? (uint)ELightmapFlags.LIGHTMAP_FLAGS_DIRECTION : 0;

			Debug.AssertFormat(
				Mathf.Max(LightmapInput.Size.x, LightmapInput.Size.y) <= Context.Settings.AtlasSettings.MaxLightmapSize,
				"ExportLightmap2D Error: {0} With {1}x{2}",
				Lightmap2D.Name, LightmapInput.Size.x, LightmapInput.Size.y);

			var LightMapBakingParameters = Lightmap2D.LightMapBakingParameters;

			if (LightMapBakingParameters != null)
            {
				DawnDebug.LogFormat("{0} Override Default Baking Params", Lightmap2D.Name);

                LightmapInput.Flags |= (uint)ELightmapFlags.LIGHTMAP_FLAGS_OVERRIDE_PARAMS;

				LightmapInput.OverrideBakingParams.LightmapBakingParams = BakingJobInputs.BakingParameters.Lightmap2DParameter;
				LightmapInput.OverrideBakingParams.ArtifactBakingParams = BakingJobInputs.BakingParameters.ArtifactParameter;
				LightmapInput.OverrideBakingParams.AOBakingParams = BakingJobInputs.BakingParameters.AmbientOcclusionParameter;

				LightmapInput.OverrideBakingParams.LightmapBakingParams.DenoiserMode = (int)LightMapBakingParameters.DenoiserMode;
                LightmapInput.OverrideBakingParams.LightmapBakingParams.IterationNum = LightMapBakingParameters.SamplesPerPixel;
                LightmapInput.OverrideBakingParams.LightmapBakingParams.MaxDepth = LightMapBakingParameters.MaxBounces;
                LightmapInput.OverrideBakingParams.LightmapBakingParams.MaxSkyBounces = LightMapBakingParameters.MaxSkyBounces;
                LightmapInput.OverrideBakingParams.LightmapBakingParams.PenumbraShadowFraction = LightMapBakingParameters.PenumbraShadowFraction;
				LightmapInput.OverrideBakingParams.LightmapBakingParams.OffsetScale = LightMapBakingParameters.OffsetScale;
				LightmapInput.OverrideBakingParams.LightmapBakingParams.SuperSampleFactor = LightMapBakingParameters.SuperSampleFactor;
				LightmapInput.OverrideBakingParams.ArtifactBakingParams.bFillUnmappedTexel = ToByte(true);
				LightmapInput.OverrideBakingParams.ArtifactBakingParams.bSeamFixed = ToByte(LightMapBakingParameters.SeamFixed);

                LightmapInput.OverrideBakingParams.bPrecomputedRadianceTransferForSurface = 0;
            }

			if (LightingSystem.bDebugLightmapTexel) {
				var DebugMesh = Dawn4UnityDebugEditor.SelectionHitInfo;
				if (Lightmap2D.renderer == DebugMesh.Renderer) {
					var uvBounds = Lightmap2D.UVBounds;
					float uvWidth = uvBounds.z - uvBounds.x;
					float uvHeight = uvBounds.w - uvBounds.y;

					Vector2 lightmapUV = new Vector2((DebugMesh.LightmapCoords.x - uvBounds.x) / uvWidth, (DebugMesh.LightmapCoords.y - uvBounds.y) / uvHeight);

					DawnDebug.LogFormat("Debug originuv:({0:N4},{1:N4}),adjustuv:({2:N4},{3:N4})", DebugMesh.LightmapCoords.x, DebugMesh.LightmapCoords.y, lightmapUV.x, lightmapUV.y);

					DebugMesh.MeshID = LightmapInput.MeshID;
					DebugMesh.DebugUV.x = Mathf.RoundToInt(lightmapUV.x * LightmapInput.Size.x - 0.5f);
					DebugMesh.DebugUV.y = Mathf.RoundToInt(lightmapUV.y * LightmapInput.Size.y - 0.5f);
					DebugMesh.DebugSize = LightmapInput.Size;
					Dawn4UnityDebugEditor.SelectionHitInfo = DebugMesh;
				}
			}
        }
    }
}
