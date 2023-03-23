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
		public static bool ValidateLightingDataAssetForLightProbes()
        {
			var probeGroups = LightProbeGroup.FindObjectsOfType<LightProbeGroup>();
			if (probeGroups.Length == 0)
			{
				return false;
			}

			Scene CurrentActiveScene = SceneManager.GetActiveScene();

			string CurrentScenePath = CurrentActiveScene.path;
			var TempScenePath = DawnBakePathSetting.GetInstance(CurrentActiveScene).DawnBakeTempFilePath("DawnTempScene.unity");
			var lightingAssetPath = DawnBakePathSetting.GetInstance(CurrentActiveScene).DawnLightProbeAssetPath();

			Scene TempScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
			SceneManager.SetActiveScene(TempScene);
			RenderSettings.skybox = null;
			LightmapSettings.lightmapsMode = LightmapsMode.NonDirectional;

			var probeGroupClones = new GameObject[probeGroups.Length];
			for (int i = 0; i < probeGroups.Length; i++)
			{
				var g = new GameObject();
				g.transform.position = probeGroups[i].transform.position;
				g.transform.rotation = probeGroups[i].transform.rotation;
				g.transform.localScale = probeGroups[i].transform.lossyScale;
				var p = g.AddComponent<LightProbeGroup>();
				p.probePositions = probeGroups[i].probePositions;
				SceneManager.MoveGameObjectToScene(g, TempScene);
				probeGroupClones[i] = g;
			}

			if (File.Exists(TempScenePath))
			{
				File.Delete(TempScenePath);
			}

			EditorSceneManager.SaveScene(TempScene, TempScenePath);

#if UNITY_5
			var paths = new string[1];
			paths[0] = TempScenePath;
			Lightmapping.BakeMultipleScenes(paths);
#else
			SceneSetup[] sceneManagerSetup = EditorSceneManager.GetSceneManagerSetup();
			EditorSceneManager.OpenScene(TempScenePath);
			Lightmapping.Bake();
			EditorSceneManager.SaveOpenScenes();
			EditorSceneManager.RestoreSceneManagerSetup(sceneManagerSetup);
#endif

			var lightingDataAsset = Lightmapping.lightingDataAsset;
			if (lightingDataAsset != null)
			{
				AssetDatabase.MoveAsset(AssetDatabase.GetAssetPath(lightingDataAsset), lightingAssetPath);
			}
			else
            {
				DawnDebug.LogError("RenderLightProbes error: lightingDataAsset was not generated");
			}

			CurrentActiveScene = EditorSceneManager.GetSceneByPath(CurrentScenePath);
			TempScene = EditorSceneManager.GetSceneByPath(TempScenePath);

			EditorSceneManager.SetActiveScene(CurrentActiveScene);

			var Operation = EditorSceneManager.UnloadSceneAsync(TempScene);
			if(Operation.isDone)
            {
				OnCleanupTempScene(CurrentScenePath,TempScenePath);
			}
#if UNITY_2018_1_OR_NEWER
			else
            {
				Operation.completed += delegate(AsyncOperation op) {
					OnCleanupTempScene(CurrentScenePath, TempScenePath);
				};
			}
#endif

			return true;
		}

		static void OnCleanupTempScene(string CurrentScenePath,string TempScenePath)
        {
			AssetDatabase.DeleteAsset(TempScenePath);
        }

		static bool ConvertLightProbetAsset(Scene InScene, DawnBakeResultAsset bakeResultAsset, LightProbes lightProbes)
		{
			if(lightProbes == null)
            {
				DawnDebug.LogErrorFormat("Precomputed BakedProbes is null: {0}", InScene.name);
				return false;
            }
			if(bakeResultAsset.BakedLightProbeCeffs.Count!= lightProbes.bakedProbes.Length)
            {
				DawnDebug.LogErrorFormat("BakedProbes Num Not Equal: {0} != {1}",
					bakeResultAsset.BakedLightProbeCeffs.Count, lightProbes.bakedProbes.Length);
				return false;
            }

			List<SphericalHarmonicsL2> ProbeCeffs = new List<SphericalHarmonicsL2>();

			for(int ProbeIndex = 0;ProbeIndex < bakeResultAsset.BakedLightProbeCeffs.Count;++ProbeIndex)
            {
				ProbeCeffs.Add(bakeResultAsset.BakedLightProbeCeffs[ProbeIndex].ToSH());
			}

			lightProbes.bakedProbes = ProbeCeffs.ToArray();

			bool bConvertOcclusion = false;

			foreach(var LightInfo in bakeResultAsset.BakedLightInfos)
            {
				if(LightInfo.LightBakedData.BakedMode == EDawnLightingMode.ShadowMask)
                {
					bConvertOcclusion = true;
					break;
				}
			}

			if(bConvertOcclusion)
            {
				DawnDebug.LogFormat("Converting LightProbe Occlusions For {0} With {1}", InScene.name, lightProbes.count);
				EnsureSphericalHarmonicsL2Names();

				PropertyInfo inspectorModeInfo = typeof(SerializedObject).GetProperty("inspectorMode", BindingFlags.NonPublic | BindingFlags.Instance);
				SerializedObject lightProbeAssetObject = new SerializedObject(lightProbes);
				inspectorModeInfo.SetValue(lightProbeAssetObject, InspectorMode.Debug, null);

				var BakedLightOcclusion = lightProbeAssetObject.FindProperty("m_BakedLightOcclusion");

				BakedLightOcclusion.arraySize = bakeResultAsset.BakedLightProbeOcclusions.Count;

				for (int ProbeIndex = 0; ProbeIndex < bakeResultAsset.BakedLightProbeOcclusions.Count; ++ProbeIndex)
				{
					var BakedLightOcclusionValue = BakedLightOcclusion.GetArrayElementAtIndex(ProbeIndex);

					var OcclusionArray = BakedLightOcclusionValue.FindPropertyRelative("m_Occlusion");
					var ProbeOcclusionLightIndexArray = BakedLightOcclusionValue.FindPropertyRelative("m_ProbeOcclusionLightIndex");
					var OcclusionMaskChannelArray = BakedLightOcclusionValue.FindPropertyRelative("m_OcclusionMaskChannel");

					var ProbeOcclusions = bakeResultAsset.BakedLightProbeOcclusions[ProbeIndex];
					OcclusionArray.arraySize = ProbeOcclusionLightIndexArray.arraySize = OcclusionMaskChannelArray.arraySize = 4;

					for (int Index = 0; Index < OcclusionArray.arraySize; ++Index)
					{
						var OcclusionValue = OcclusionArray.GetArrayElementAtIndex(Index);
						OcclusionValue.floatValue = ProbeOcclusions.Occlusion[Index];
					}
					for (int Index = 0; Index < ProbeOcclusionLightIndexArray.arraySize; ++Index)
					{
						var ProbeOcclusionLightIndex = ProbeOcclusionLightIndexArray.GetArrayElementAtIndex(Index);
						ProbeOcclusionLightIndex.intValue = ProbeOcclusions.ProbeOcclusionLightIndex[Index];
					}
					for (int Index = 0; Index < OcclusionMaskChannelArray.arraySize; ++Index)
					{
						var OcclusionMaskChannel = OcclusionMaskChannelArray.GetArrayElementAtIndex(Index);
						OcclusionMaskChannel.intValue = ProbeOcclusions.OcclusionMaskChannel[Index];
					}
				}

				lightProbeAssetObject.ApplyModifiedProperties();
			}

			return true;
		}
	}
}