using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace GPUBaking.Editor
{
	public enum DawnBakingMode
    {
		Default,
		BakingSelected,
		Interactive,
    }
	public class DawnSceneInfo
	{
		public Scene scene;

		public int BaseLightmapIndex = 0;

		public List<LightMapAllocationTexture> LightmapTextures = new List<LightMapAllocationTexture>();

		public List<LightmapData> LightmapDatas = new List<LightmapData>();

		public DawnSceneBakeResult SceneBakeResult = null;
	}

	public class DawnException : System.Exception
    {
		public DawnException(string message) : base(message) { }
	}

	public class DawnBakingContext
	{
		internal DawnBakingMode BakingMode = DawnBakingMode.Default;

		internal DawnSettings Settings;

		internal bool bEnableExportCache = true;

		internal bool bEnableUniformLightmapScale = false;

		internal bool bLinearColorSpace = true;

		internal bool bUsePrecomputedProbes = false;

		internal Bounds SceneBounds;

		internal bool IsSceneBoundsValid = false;

		internal DawnImportantVolume[] ImportantVolumes = null;

		internal List<DawnBaseLight> Lights = new List<DawnBaseLight>();

		internal List<DawnBaseLight> MixedLights = new List<DawnBaseLight>();

		internal List<DawnLightMesh> LightMeshes = new List<DawnLightMesh>();

		internal Dictionary<DawnBaseLight, FGuidInfo> LightGuids = new Dictionary<DawnBaseLight, FGuidInfo>();

		internal List<MeshGatherInfo> MeshInstances = new List<MeshGatherInfo>();

		internal List<StaticMeshInfo> MeshList = new List<StaticMeshInfo>();

		internal List<LandscapeGatherInfo> LandscapeList = new List<LandscapeGatherInfo>();

		internal List<Material> Materials = new List<Material>();

		internal List<DawnLightmap2DGroupTask> LightmapGroupList = new List<DawnLightmap2DGroupTask>();

		internal List<DawnShadowMaskGroupTask> ShadowMaskGroupList = new List<DawnShadowMaskGroupTask>();

		internal List<DawnLightmap2DTask> LightmapList = new List<DawnLightmap2DTask>();
	
		internal List<DawnShadowMaskTask> ShadowmaskList = new List<DawnShadowMaskTask>();

		internal List<DawnLightProbeTask> LightProbeList = new List<DawnLightProbeTask>();

		internal List<SphericalHarmonicsL2> ProbeCeffs = new List<SphericalHarmonicsL2>();

		internal List<Vector3> ProbePositions = new List<Vector3>();

		internal Cubemap SkyReflectionCubemap = null;
		internal SphericalHarmonicsL2 BakedAmbientProbe = new SphericalHarmonicsL2();

		internal Dictionary<StaticMeshInfo, int> MeshIndices = new Dictionary<StaticMeshInfo, int>();
		internal Dictionary<Material, int> MaterialIndices = new Dictionary<Material, int>();

		Dictionary<Scene, DawnSceneInfo> SceneIndices = new Dictionary<Scene, DawnSceneInfo>();
		internal List<DawnSceneInfo> SceneLightingData = new List<DawnSceneInfo>();

		private string LastErrorString = string.Empty;

		public void Reset()
		{
			BakingMode = DawnBakingMode.Default;
			bLinearColorSpace = PlayerSettings.colorSpace == ColorSpace.Linear;
			bEnableExportCache = true;
			bEnableUniformLightmapScale = false;
			bUsePrecomputedProbes = false;
			IsSceneBoundsValid = false;
			ImportantVolumes = null;
			Lights.Clear ();
			MixedLights.Clear ();
			LightGuids.Clear();
			MeshInstances.Clear ();
			MeshList.Clear ();
			LandscapeList.Clear();
			Materials.Clear ();
			LightmapList.Clear ();
			ShadowmaskList.Clear();
			LightmapGroupList.Clear ();
			ShadowMaskGroupList.Clear ();

			LightProbeList.Clear ();
			ProbeCeffs.Clear ();
			ProbePositions.Clear ();

			MeshIndices.Clear();
			MaterialIndices.Clear();

			SkyReflectionCubemap = null;
			BakedAmbientProbe.Clear();

			InitSceneLightings();

			LastErrorString = string.Empty;
		}

		public void AddLight(DawnBaseLight InLight)
		{
			Lights.Add (InLight);
			if (InLight.lightmapBakeType == LightmapBakeType.Mixed && Settings.BakingMode == EDawnBakingMode.ShadowMask) {
				MixedLights.Add (InLight);
			}
		}

		public void AddLight(DawnLightMesh InLight)
		{
			LightMeshes.Add (InLight);
		}

		public void SetLightGuid(DawnBaseLight InLight,ref FGuidInfo InGuid)
		{
			LightGuids.Add(InLight,InGuid);
		}

		public int AddMesh(Mesh InMesh,int LODIndex)
		{
			var StaticMesh = new StaticMeshInfo(InMesh,LODIndex);
            int MeshIndex;
            if (!MeshIndices.TryGetValue(StaticMesh, out MeshIndex))
            {
                MeshIndex = MeshList.Count;
                MeshList.Add(StaticMesh);
                MeshIndices.Add(StaticMesh,MeshIndex);
            }

            return MeshIndex;
        }

		public int AddMaterial(Material InMaterial)
		{
            int MatIndex;
            if (!MaterialIndices.TryGetValue(InMaterial, out MatIndex))
            {
                MatIndex = Materials.Count;
                Materials.Add(InMaterial);
                MaterialIndices.Add(InMaterial, MatIndex);
            }

            return MatIndex;
        }

		internal void AddMeshInstance(MeshGatherInfo InMeshInstance)
		{
			if (!IsSceneBoundsValid) {
				SceneBounds = InMeshInstance.bounds;
				IsSceneBoundsValid = true;
			} else {
				SceneBounds.min = Vector3.Min (SceneBounds.min, InMeshInstance.bounds.min);
				SceneBounds.max = Vector3.Max (SceneBounds.max, InMeshInstance.bounds.max);
			}
			MeshInstances.Add (InMeshInstance);
		}

		internal void AddLandscape(LandscapeGatherInfo InLandscape)
		{
			if (!IsSceneBoundsValid) {
				SceneBounds = InLandscape.bounds;
				IsSceneBoundsValid = true;
			} else {
				SceneBounds.min = Vector3.Min (SceneBounds.min, InLandscape.bounds.min);
				SceneBounds.max = Vector3.Max (SceneBounds.max, InLandscape.bounds.max);
			}
			LandscapeList.Add (InLandscape);
		}

		public void AddLightmap2D(DawnLightmap2DTask LightMap2D)
		{
			LightmapList.Add (LightMap2D);
		}

		public void AddShadowMask(DawnShadowMaskTask ShadowMask)
		{
			if(ShadowMask!=null)
			{
				ShadowmaskList.Add (ShadowMask);
			}
		}

		public void AddLightmap2DGroup(DawnLightmap2DGroupTask LightMap2DGroup)
		{
			LightmapGroupList.Add (LightMap2DGroup);
		}

		public void AddShadowMaskGroup(DawnShadowMaskGroupTask ShadowMaskGroup)
		{
			ShadowMaskGroupList.Add (ShadowMaskGroup);
		}

		public void SetLightChannel(DawnBaseLight InLight,int LightIndex,int ChannelIndex,EDawnLightingMask LightmapMask, EDawnLightingMode MixedMode)
		{
			
			if(InLight.UnityLight != null)
            {
				var BakedOutput = new LightBakingInfo(LightIndex, ChannelIndex, LightmapMask, MixedMode);
				BakedOutput.ApplyBakedData(InLight.UnityLight);
			}
			InLight.LightIndex = LightIndex;
			InLight.ChannelIndex = ChannelIndex;
			InLight.LightingMask = (int)LightmapMask;
			InLight.BakedMode = (int)MixedMode;
		}

		public Color GetLinearColor(Color InColor)
		{
			if (bLinearColorSpace) {
				return InColor;
			}
			return InColor.linear;
		}

		public void LogErrorFormat(string format, params object[] args)
        {
			LastErrorString = string.Format(format,args);
			DawnDebug.LogError(LastErrorString);
        }

		public void ThrowException(string format, params object[] args)
		{
			LastErrorString = string.Format(format, args);
			DawnDebug.LogError(LastErrorString);
			throw new DawnException(LastErrorString);
		}

		public void Print()
		{
			DawnDebug.LogFormat ("SceneRaius:{0}", SceneRadius);
			DawnDebug.LogFormat ("GatherImportantVolumes:{0}", ImportantVolumes != null ? ImportantVolumes.Length : 0);
			DawnDebug.LogFormat ("GatherLights:{0}", Lights.Count);
			DawnDebug.LogFormat ("GatherMeshInstance:{0}", MeshInstances.Count);
			DawnDebug.LogFormat ("GatherMesh:{0}", MeshList.Count);
			DawnDebug.LogFormat ("GatherLandscape:{0}", LandscapeList.Count);
			DawnDebug.LogFormat ("GatherMaterials:{0}", Materials.Count);
			DawnDebug.LogFormat ("GatherLightmap:{0}", LightmapList.Count);
			DawnDebug.LogFormat ("GatherShadowMask:{0}", ShadowmaskList.Count);
			DawnDebug.LogFormat ("GatherLightmapGroup:{0}", LightmapGroupList.Count);
			
			int LightProbeSampleNum = 0;
			foreach(var Probe in LightProbeList)
			{
				LightProbeSampleNum+= Probe.SamplePostions.Count;
			}
			DawnDebug.LogFormat ("GatherLightProbe Task:{0} Samples:{1}", LightProbeList.Count,LightProbeSampleNum);
		}

		internal bool IsEnableResultConverting
        {
			get {
				return Settings.BakingResultMode == EDawnBakingResultMode.UnityLightingAsset
					|| Settings.BakingResultMode == EDawnBakingResultMode.DawnAndUnityAsset
					|| Settings.LightProbeSettings.AutoGenerationSetting.AutoGeneration
					|| LightProbeList.Count > 0;
			}
        }

		internal void InitSceneLightings()
        {
			SceneLightingData.Clear();
			SceneIndices.Clear();

			for (int SceneIndex = 0; SceneIndex < EditorSceneManager.sceneCount;SceneIndex++)
            {
				var Scene = EditorSceneManager.GetSceneAt(SceneIndex);
				if (!Scene.isLoaded)
				{
					continue;
				}
				GetSceneLightingData(Scene, true);
			}
        }

		internal DawnSceneInfo GetSceneLightingData(Scene InScene,bool bCreateIfNotExists = false)
		{
			DawnSceneInfo SceneInfo = null;
			if (!SceneIndices.TryGetValue(InScene, out SceneInfo) && bCreateIfNotExists)
			{
				SceneInfo = new DawnSceneInfo();
				SceneInfo.scene = InScene;
				SceneInfo.SceneBakeResult = new DawnSceneBakeResult(InScene);
				SceneIndices.Add(InScene, SceneInfo);
				SceneLightingData.Add(SceneInfo);
			}
			return SceneInfo;
		}

		internal bool IsGenerationProbe(DawnProbeGenerationSelector ProbeGeneration)
        {
			if (ProbeGeneration != null)
			{
				if (ProbeGeneration.bGeneration)
				{
					return true;
				}
			}
			else if (Settings.LightProbeSettings.AutoGenerationSetting.ForAllSurface)
			{
				return true;
			}

			return false;
		}

		internal float SceneRadius
		{
			get {
				if (IsSceneBoundsValid) {
					return SceneBounds.extents.magnitude;
				}
				return 0.0f;
			}
		}

		public bool IsEnableExportCache
		{
			get {
				return bEnableExportCache;
			}
			set{
				this.bEnableExportCache = value;
			}
			
		}

		public string LastErrorInfo
        {
			get{ 
				return LastErrorString; 
			}
        }
	}
}

