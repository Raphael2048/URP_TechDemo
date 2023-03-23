using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Scripting;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using System;
using System.IO;
using System.Runtime.InteropServices;
using GPUBaking;
using GPUBaking.Editor;
using System.Reflection;

namespace GPUBaking.Editor
{
	public partial class DawnLightAsset
	{
		private static string emptyLightingDataPath = "Assets/Dawn4Unity/Configs/emptyLightingData.asset";

		[MenuItem("Dawn/Tools/ConvertLightingAsset")]

		public static bool ConvertLightingAsset()
        {
			return ConvertLightingAsset(false, false,false);
		}

		public static bool ConvertLightingAsset(bool bCleanTempBakedAsset, bool bNoLightmaps,bool bUsePrecomputedProbes)
        {
			LightingDataAsset mainLightingAsset = null;
			bool bSuccessed = true;
			for (int SceneIndex = 0; bSuccessed && SceneIndex < EditorSceneManager.sceneCount; ++SceneIndex)
			{
				var Scene = EditorSceneManager.GetSceneAt(SceneIndex);
				if(!Scene.isLoaded)
                {
					continue;
                }
				var lightingDataAsset = ConvertLightingAsset(mainLightingAsset, Scene, bCleanTempBakedAsset, bNoLightmaps, bUsePrecomputedProbes);
				if (lightingDataAsset != null && mainLightingAsset == null)
				{
					mainLightingAsset = lightingDataAsset;
				}
				bSuccessed = bSuccessed && lightingDataAsset != null;
			}
			return bSuccessed;
		}
		public static LightingDataAsset ConvertLightingAsset(LightingDataAsset mainLightingAsset, Scene InScene, bool bCleanTempBakedAsset,bool bNoLightmaps,bool bUsePrecomputedProbes)
		{
			string BakeResultAssetPath = DawnBakePathSetting.GetInstance(InScene).DawnBakeResultAssetPath();

			if (!File.Exists(BakeResultAssetPath))
			{
				DawnDebug.Log(string.Format("Cannot find bake result file: {0}", BakeResultAssetPath));
				return null;
			}

			DawnBakeResultAsset bakeResultAsset = AssetDatabase.LoadMainAssetAtPath(BakeResultAssetPath) as DawnBakeResultAsset;

			LightingDataAsset lightingDataAsset = EnsureLightingAssetData(InScene,true);

			PropertyInfo inspectorModeInfo = typeof(SerializedObject).GetProperty("inspectorMode", BindingFlags.NonPublic | BindingFlags.Instance);
			SerializedObject serializedObject = new SerializedObject(lightingDataAsset);
			inspectorModeInfo.SetValue(serializedObject, InspectorMode.Debug, null);

			var SceneAsset = AssetDatabase.LoadAssetAtPath(InScene.path, typeof(UnityEngine.Object));

			var SceneData = serializedObject.FindProperty("m_Scene");
			if(SceneData!=null)
            {
				SceneData.objectReferenceValue = SceneAsset;
			}

			var LightmapsMode = serializedObject.FindProperty("m_LightmapsMode");
			LightmapsMode.intValue = (int)bakeResultAsset.lightmapsMode;

			if (bNoLightmaps)
            {
				ConvertLightmaps(InScene,ScriptableObject.CreateInstance<DawnBakeResultAsset>(), serializedObject);
			}
            else
            {
				ConvertLightmaps(InScene,bakeResultAsset, serializedObject);
			}

			ConvertReflectionProbes(InScene,bakeResultAsset, serializedObject);

			var LightProbes = serializedObject.FindProperty("m_LightProbes");

			UnityEngine.Object lightProbeAsset = null;

			if (LightProbes!=null)
			{
				lightProbeAsset = LightProbes.objectReferenceValue;
				DawnDebug.LogFormat("LightProbes({0}):{1}", LightProbes.propertyType, lightProbeAsset);
				if (bUsePrecomputedProbes)
                {
					bUsePrecomputedProbes = ConvertLightProbetAsset(InScene, bakeResultAsset, lightProbeAsset as LightProbes);
				}
				else
				{
					
					ConvertLightProbetAsset(bakeResultAsset, lightProbeAsset);
				}
				if (mainLightingAsset == null && lightProbeAsset !=null)
                {
					LightmapSettings.lightProbes = lightProbeAsset as LightProbes;
				}

			}

			var EnlightenDataVersion = serializedObject.FindProperty("m_EnlightenDataVersion");

			if(EnlightenDataVersion!=null)
			{
#if UNITY_2018_1_OR_NEWER
				EnlightenDataVersion.intValue = 112;
#endif
			}

			serializedObject.ApplyModifiedProperties();

#if UNITY_2018_1_OR_NEWER

			if (mainLightingAsset == null)
            {
				mainLightingAsset = lightingDataAsset;				
			}
			else
            {
				string lightingDataAssetPath = AssetDatabase.GetAssetPath(lightingDataAsset);

				AssetDatabase.RemoveObjectFromAsset(lightingDataAsset);
				if (lightProbeAsset!=null)
                {
					AssetDatabase.RemoveObjectFromAsset(lightProbeAsset);
					AssetDatabase.AddObjectToAsset(lightProbeAsset, AssetDatabase.GetAssetPath(mainLightingAsset));
				}				
				AssetDatabase.AddObjectToAsset(lightingDataAsset, AssetDatabase.GetAssetPath(mainLightingAsset));
				AssetDatabase.DeleteAsset(lightingDataAssetPath);
			}
#endif

			var CurrentScene = EditorSceneManager.GetActiveScene();

			EditorSceneManager.SetActiveScene(InScene);

			Lightmapping.lightingDataAsset = lightingDataAsset;

			EditorSceneManager.MarkSceneDirty(InScene);

			EditorSceneManager.SaveScene(InScene);

			EditorSceneManager.SetActiveScene(CurrentScene);

			if (bCleanTempBakedAsset)
			{
				AssetDatabase.DeleteAsset(BakeResultAssetPath);
			}

			return lightingDataAsset;
		}

		internal static LightingDataAsset EnsureLightingAssetData(Scene InScene,bool bCleanData = false)
        {
			var lightingDataAssetPath = DawnBakePathSetting.GetInstance(InScene).DawnLightingAssetPath();

			var lightProbeAssetPath = DawnBakePathSetting.GetInstance(InScene).DawnLightProbeAssetPath();

			DawnDebug.LogFormat("lightingDataAssetPath:{0}", lightingDataAssetPath);

			if (bCleanData && File.Exists(lightingDataAssetPath))
			{
				AssetDatabase.DeleteAsset(lightingDataAssetPath);
			}

			if (File.Exists(lightProbeAssetPath))
			{
				AssetDatabase.MoveAsset(lightProbeAssetPath, lightingDataAssetPath);
			}

			if (!File.Exists(lightingDataAssetPath))
			{
				AssetDatabase.CopyAsset(emptyLightingDataPath, lightingDataAssetPath);
				AssetDatabase.Refresh();
			}

			LightingDataAsset lightingDataAsset = AssetDatabase.LoadAssetAtPath(lightingDataAssetPath, typeof(LightingDataAsset)) as LightingDataAsset;
			lightingDataAsset.name = InScene.name;

			return lightingDataAsset;
		}

		static void ConvertLightmaps(Scene InScene,DawnBakeResultAsset bakeResultAsset, SerializedObject serializedObject)
        {
			var Lightmaps = serializedObject.FindProperty("m_Lightmaps");
			var LightmappedRendererData = serializedObject.FindProperty("m_LightmappedRendererData");
			var LightmappedRendererDataIDs = serializedObject.FindProperty("m_LightmappedRendererDataIDs");

			var Lights = serializedObject.FindProperty("m_Lights");
			var LightBakingOutputs = serializedObject.FindProperty("m_LightBakingOutputs");

			Lightmaps.arraySize = bakeResultAsset.BakedLightmaps.Count;

			for (int LightmapIndex = 0; LightmapIndex < Lightmaps.arraySize; ++LightmapIndex)
			{
				var LightmapData = Lightmaps.GetArrayElementAtIndex(LightmapIndex);

				var LightmapTexture = LightmapData.FindPropertyRelative("m_Lightmap");
				var DirLightmapTexture = LightmapData.FindPropertyRelative("m_DirLightmap");
				var ShadowMaskTexture = LightmapData.FindPropertyRelative("m_ShadowMask");

				var LMTexture = bakeResultAsset.BakedLightmaps[LightmapIndex];
				LightmapTexture.objectReferenceInstanceIDValue = LMTexture.GetInstanceID();
				if (LightmapIndex < bakeResultAsset.BakedShadowMasks.Count)
				{
					var ShadowTexture = bakeResultAsset.BakedShadowMasks[LightmapIndex];
					ShadowMaskTexture.objectReferenceInstanceIDValue = ShadowTexture.GetInstanceID();
				}
				if (LightmapIndex < bakeResultAsset.BakedDirectionalLightmaps.Count)
				{
					var DirLMTexture = bakeResultAsset.BakedDirectionalLightmaps[LightmapIndex];
					DirLightmapTexture.objectReferenceInstanceIDValue = DirLMTexture.GetInstanceID();
				}

				DawnDebug.LogFormat("Lightmap({0}):{1}<={2}", LightmapIndex, LightmapTexture.objectReferenceValue, LMTexture);
			}

			var Renderers = new System.Collections.Generic.Dictionary<GUID, MeshRenderer>();
			var Landscapes = new System.Collections.Generic.Dictionary<GUID, Terrain>();
			var LightDict = new System.Collections.Generic.Dictionary<GUID, DawnBaseLight>();

			var RootObjects = InScene.GetRootGameObjects();

			foreach (var RootObject in RootObjects)
            {
				var AllMeshRenderers = RootObject.GetComponentsInChildren<MeshRenderer>();
				foreach (var MeshRenderer in AllMeshRenderers)
				{
					var GameObjectID = GUID.CreateGUID(MeshRenderer);
					Renderers.Add(GameObjectID, MeshRenderer);
				}
				var AllLandscapes = RootObject.GetComponentsInChildren<Terrain>();
				foreach (var Landscape in AllLandscapes)
				{
					var GameObjectID = GUID.CreateGUID(Landscape);
					Landscapes.Add(GameObjectID, Landscape);
				}
				var AllLights = RootObject.GetComponentsInChildren<DawnBaseLight>();
				foreach (var Light in AllLights)
				{
					var GameObjectID = GUID.CreateGUID(Light);
					LightDict.Add(GameObjectID, Light);
				}
			}

			LightmappedRendererDataIDs.arraySize = bakeResultAsset.MeshInfos.Count + bakeResultAsset.Landscapes.Count;
			LightmappedRendererData.arraySize = bakeResultAsset.MeshInfos.Count + bakeResultAsset.Landscapes.Count;

			var DefaultLightmapST = new Vector4(1.0f, 1.0f, 0.0f, 0.0f);

			for (int RendererIndex = 0; RendererIndex < LightmappedRendererDataIDs.arraySize; ++RendererIndex)
			{
				var RendererDataID = LightmappedRendererDataIDs.GetArrayElementAtIndex(RendererIndex);
				var RendererData = LightmappedRendererData.GetArrayElementAtIndex(RendererIndex);

				var lightmapIndex = RendererData.FindPropertyRelative("lightmapIndex");
				var lightmapST = RendererData.FindPropertyRelative("lightmapST");

				var lightmapIndexDynamic = RendererData.FindPropertyRelative("lightmapIndexDynamic");
				var lightmapSTDynamic = RendererData.FindPropertyRelative("lightmapSTDynamic");

				var terrainDynamicUVST = RendererData.FindPropertyRelative("terrainDynamicUVST");
				var terrainChunkDynamicUVST = RendererData.FindPropertyRelative("terrainChunkDynamicUVST");

				var targetObject = RendererDataID.FindPropertyRelative("targetObject");
				var targetPrefab = RendererDataID.FindPropertyRelative("targetPrefab");

				lightmapIndexDynamic.intValue = ushort.MaxValue;
				lightmapSTDynamic.vector4Value = DefaultLightmapST;
				terrainDynamicUVST.vector4Value = DefaultLightmapST;
				terrainChunkDynamicUVST.vector4Value = DefaultLightmapST;

				UnityEngine.Object renderer = null;

				if (RendererIndex < bakeResultAsset.MeshInfos.Count)
				{
					var MeshInfo = bakeResultAsset.MeshInfos[RendererIndex];
					renderer = Renderers[MeshInfo.MeshInstanceID];
					lightmapIndex.intValue = MeshInfo.LightmapIndex;
					lightmapST.vector4Value = MeshInfo.LightmapOffset;
				}
				else
				{
					var LandscapeInfo = bakeResultAsset.Landscapes[RendererIndex - bakeResultAsset.MeshInfos.Count];
					renderer = Landscapes[LandscapeInfo.LandscapeID];
					lightmapIndex.intValue = LandscapeInfo.LandscapeIndex;
					lightmapST.vector4Value = LandscapeInfo.LandscapeOffset;
				}

				var ObjectIdentifier = GUID.CreateGUID(renderer);
				targetObject.longValue = (long)ObjectIdentifier.High;
				targetPrefab.longValue = (long)ObjectIdentifier.Low;

				DawnDebug.Print("Renderer({0}/{1}):LightmapIndex={2},LightmapScaleOffset:{3}", targetObject.longValue, targetPrefab.longValue, lightmapIndex.intValue, lightmapST.vector4Value);
			}

			Lights.arraySize = bakeResultAsset.BakedLightInfos.Count;
			LightBakingOutputs.arraySize = bakeResultAsset.BakedLightInfos.Count;

			for (int LightIndex = 0; LightIndex < Lights.arraySize; ++LightIndex)
			{
				var LightID = Lights.GetArrayElementAtIndex(LightIndex);
				var LightBakingOutput = LightBakingOutputs.GetArrayElementAtIndex(LightIndex);

				var occlusionMaskChannel = LightBakingOutput.FindPropertyRelative("occlusionMaskChannel");
				var lightmappingMask = LightBakingOutput.FindPropertyRelative("lightmappingMask");
				var lightmapBakeType = LightBakingOutput.FindPropertyRelative("lightmapBakeMode.lightmapBakeType");
				var mixedLightingMode = LightBakingOutput.FindPropertyRelative("lightmapBakeMode.mixedLightingMode");
				var isBaked = LightBakingOutput.FindPropertyRelative("isBaked");

				var targetObject = LightID.FindPropertyRelative("targetObject");
				var targetPrefab = LightID.FindPropertyRelative("targetPrefab");

				var LightInfo = bakeResultAsset.BakedLightInfos[LightIndex];
				var Light = LightDict[LightInfo.LightID];

				var ObjectIdentifier = GUID.CreateGUID(Light);

				targetObject.longValue = (long)ObjectIdentifier.High;
				targetPrefab.longValue = (long)ObjectIdentifier.Low;

				occlusionMaskChannel.intValue = LightInfo.LightBakedData.ChannelIndex;

				if (lightmappingMask != null)
				{
					lightmappingMask.intValue = (int)LightInfo.LightBakedData.LightingMask;
				}

				if (lightmapBakeType != null)
				{
					switch (Light.lightmapBakeType)
					{
						case LightmapBakeType.Mixed:
							lightmapBakeType.enumValueIndex = 0;
							break;
						case LightmapBakeType.Baked:
							lightmapBakeType.enumValueIndex = 1;
							break;
						case LightmapBakeType.Realtime:
							lightmapBakeType.enumValueIndex = 2;
							break;
					}
				}

				if (mixedLightingMode != null)
				{
					mixedLightingMode.enumValueIndex = (int)LightInfo.LightBakedData.BakedMode;
				}
				if (isBaked != null)
				{
					isBaked.boolValue = true;
				}

				if (lightmapBakeType != null)
				{
					DawnDebug.Print("Light({0}):occlusionMaskChannel={1},lightmapBakeMode={2},mixedLightingMode={3},isBaked={4}",
					targetObject.longValue, occlusionMaskChannel.intValue, lightmapBakeType.enumValueIndex, mixedLightingMode.enumValueIndex, isBaked.boolValue);
				}
				else if (lightmappingMask != null)
				{
					DawnDebug.Print("Light({0}):occlusionMaskChannel={1},lightmappingMask={2}",
					targetObject.longValue, occlusionMaskChannel.intValue, lightmappingMask.intValue);
				}
			}
		}
		static void ConvertReflectionProbes(Scene InScene, DawnBakeResultAsset bakeResultAsset, SerializedObject serializedObject)
        {
			var BakedReflectionProbeCubemaps = serializedObject.FindProperty("m_BakedReflectionProbeCubemaps");
			var BakedReflectionProbes = serializedObject.FindProperty("m_BakedReflectionProbes");
			var BakedAmbientProbeInLinear = serializedObject.FindProperty("m_BakedAmbientProbeInLinear");

			int ProbeCubemapOffset = bakeResultAsset.SkyReflectionCubemap != null ? 1 : 0;			

			var RootObjects = InScene.GetRootGameObjects();

			List<ReflectionProbe> Probes = new List<ReflectionProbe>();

			foreach (var RootObject in RootObjects)
            {
				var ReflectionProbes = RootObject.GetComponentsInChildren<ReflectionProbe>();
				if(ReflectionProbes.Length > 0)
                {
					Probes.AddRange(ReflectionProbes);
				}
			}			

			BakedReflectionProbeCubemaps.arraySize = ProbeCubemapOffset + Probes.Count;
			BakedReflectionProbes.arraySize = Probes.Count;

			if (bakeResultAsset.SkyReflectionCubemap != null)
			{
				var ProbeCubemap = BakedReflectionProbeCubemaps.GetArrayElementAtIndex(0);
				ProbeCubemap.objectReferenceInstanceIDValue = bakeResultAsset.SkyReflectionCubemap.GetInstanceID();
			}

			for (int ProbeIndex = 0; ProbeIndex < Probes.Count; ++ProbeIndex)
			{
				var Probe = Probes[ProbeIndex];
				var ProbeElement = BakedReflectionProbes.GetArrayElementAtIndex(ProbeIndex);
				var ProbeCubemap = BakedReflectionProbeCubemaps.GetArrayElementAtIndex(ProbeIndex + ProbeCubemapOffset);

				{
					var targetObject = ProbeElement.FindPropertyRelative("targetObject");
					var targetPrefab = ProbeElement.FindPropertyRelative("targetPrefab");

					var ObjectIdentifier = GUID.CreateGUID(Probe);
					targetObject.longValue = (long)ObjectIdentifier.High;
					targetPrefab.longValue = (long)ObjectIdentifier.Low;
				}

				if(Probe.bakedTexture != null)
                {
					ProbeCubemap.objectReferenceInstanceIDValue = Probe.bakedTexture.GetInstanceID();
				}
                else
                {
					ProbeCubemap.objectReferenceInstanceIDValue = 0;
					DawnDebug.LogWarningFormat("Reflection Not Baked For :{0}", Probe.name);
				}
			}

            {
				EnsureSphericalHarmonicsL2Names();
				var SHInfo = bakeResultAsset.BakedAmbientProbe;
				for (int SHIndex = 0; SHIndex < SHInfo.SHValue.Length; ++SHIndex)
				{
					var SHValue = BakedAmbientProbeInLinear.FindPropertyRelative(SphericalHarmonicsL2Names[SHIndex]);
					SHValue.floatValue = SHInfo.SHValue[SHIndex];
				}
			}
		}

		static void ClearReflectionProbes(SerializedObject serializedObject)
		{
			var BakedReflectionProbeCubemaps = serializedObject.FindProperty("m_BakedReflectionProbeCubemaps");
			var BakedReflectionProbes = serializedObject.FindProperty("m_BakedReflectionProbes");

			BakedReflectionProbeCubemaps.arraySize = 0;
			BakedReflectionProbes.arraySize = 0;
		}

		public static void ClearReflectionProbes(UnityEditor.LightingDataAsset lightingDataAsset)
        {
			PropertyInfo inspectorModeInfo = typeof(SerializedObject).GetProperty("inspectorMode", BindingFlags.NonPublic | BindingFlags.Instance);
			SerializedObject serializedObject = new SerializedObject(lightingDataAsset);
			inspectorModeInfo.SetValue(serializedObject, InspectorMode.Debug, null);
			ClearReflectionProbes(serializedObject);
			serializedObject.ApplyModifiedProperties();
		}


		static string[] SphericalHarmonicsL2Names = null;

		static void EnsureSphericalHarmonicsL2Names()
        {
			if (SphericalHarmonicsL2Names == null)
			{
				SphericalHarmonicsL2Names = new string[27];
				for (int SHIndex = 0; SHIndex < 27; ++SHIndex)
				{
					SphericalHarmonicsL2Names[SHIndex] = string.Format("sh[{0}{1}]", SHIndex <= 9 ? " " : "", SHIndex);
				}
			}
		}
	}
}