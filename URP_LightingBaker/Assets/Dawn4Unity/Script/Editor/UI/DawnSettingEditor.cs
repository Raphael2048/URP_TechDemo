using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Scripting;
using System.IO;
using UnityEngine.SceneManagement;

namespace GPUBaking.Editor
{
	
	public class DawnSettingEditor : AutoUIScript{

		public DawnSettings BakingSetting;

		SerializedObject SettingObject;

		public DawnDebugSettings DebugSetting;

		SerializedObject DebugSettingObject;

		int SelectedTabIndex = 0;

		bool bNewSetting = false;

		public void OnGUI()
		{
			SelectedTabIndex = GUILayout.Toolbar(SelectedTabIndex,new string[]{ DawnLocalizationAsset.GetDisplayName("Basic"), DawnLocalizationAsset.GetDisplayName("Developer")});

			if(SelectedTabIndex == 1 && DebugSetting !=null)
			{
				DebugSetting.bDebugLightingSystem = EditorGUILayout.Toggle ("Debug LightingSystem", DebugSetting.bDebugLightingSystem);

				DebugSetting.bDebugLightmapTexel = EditorGUILayout.Toggle("Debug Lightmap Texel", DebugSetting.bDebugLightmapTexel);

				if (DebugSetting.bDebugLightmapTexel)
                {
					if(DebugSettingObject == null || DebugSetting.RayTracingSettings == null)
                    {
						DebugSettingObject = new SerializedObject(DebugSetting.GetRayTracingSettings());
					}
					ReflectiveStructure(DebugSettingObject, DawnLocalizationAsset.Instance);
					if (GUI.changed && DebugSetting.RayTracingSettings!= null)
					{
						DebugSettingObject.ApplyModifiedProperties();
					}
				}
				DawnProfiler.Enable = EditorGUILayout.Toggle ("Enable Profiling", DawnProfiler.Enable);
				if (DawnProfiler.Enable) {
					DawnProfiler.ProfilingThreadhold = EditorGUILayout.FloatField ("Min Profiling Threadhold", DawnProfiler.ProfilingThreadhold);
				}
				DawnDebug.LogLevel = (GPUBaking.EDawnLogLevel)EditorGUILayout.EnumPopup("LogLevel",DawnDebug.LogLevel);

				var NewLocalizationAsset = EditorGUILayout.ObjectField("Localization Config", DawnLocalizationAsset.Instance, typeof(DawnLocalizationAsset), false);

				if(NewLocalizationAsset!= DawnLocalizationAsset.Instance)
                {
					DawnLocalizationAsset.Instance = NewLocalizationAsset as DawnLocalizationAsset;
				}
			}
			else{
				EditorGUILayout.BeginHorizontal();
				var NewBakingSetting = EditorGUILayout.ObjectField(DawnLocalizationAsset.GetDisplayName("Setting Configs"), BakingSetting, typeof(DawnSettings),false);
				if(GUILayout.Button(DawnLocalizationAsset.GetDisplayName("Reset"),GUILayout.MaxWidth(50)))
                {
					NewBakingSetting = ScriptableObject.CreateInstance<DawnSettings>();
				}
				// create new dawn setting
				if(!bNewSetting)
                {
					if (GUILayout.Button(DawnLocalizationAsset.GetDisplayName("New"), GUILayout.MaxWidth(50)))
					{
						bNewSetting = true;
					}
				}
				else
                {
					if (GUILayout.Button(DawnLocalizationAsset.GetDisplayName("Cancel"), GUILayout.MaxWidth(50)))
					{
						bNewSetting = false;
					}
				}
				if (GUILayout.Button(DawnLocalizationAsset.GetDisplayName("Copy Unity Settings")))
				{
					CopyUnityLightmapSettings(NewBakingSetting as DawnSettings);
				}
				EditorGUILayout.EndHorizontal();
				EditorGUILayout.BeginHorizontal();
				// create new dawn setting text field
				if(bNewSetting)
                {
					var NewSettingName = EditorGUILayout.TextField(DawnLocalizationAsset.GetDisplayName("New Setting Name"), SceneManager.GetActiveScene().name + "DawnSettings", GUILayout.MinWidth(100));
					if(GUILayout.Button(DawnLocalizationAsset.GetDisplayName("Save")))
                    {
						NewBakingSetting = ScriptableObject.CreateInstance<DawnSettings>();
						Dawn4Unity.SaveLightingSetting(NewBakingSetting as DawnSettings, NewSettingName);
						bNewSetting = false;
					}
                }
				EditorGUILayout.EndHorizontal();

				if (NewBakingSetting!= BakingSetting && NewBakingSetting != null)
                {
					SetSetting(NewBakingSetting as DawnSettings, DebugSetting);
				}
				ReflectiveStructure (SettingObject, DawnLocalizationAsset.Instance);
				if (GUI.changed)
				{
					SettingObject.ApplyModifiedProperties ();
					Dawn4Unity.SaveLightingSetting (BakingSetting);
				}
			}
		}

		public void SetSetting(DawnSettings InSetting,DawnDebugSettings InDebugSetting)
		{
			BakingSetting = InSetting;
			SettingObject = new SerializedObject (BakingSetting);

			DebugSetting = InDebugSetting;
		}

		void CopyUnityLightmapSettings(DawnSettings InSetting)
        {
#if UNITY_2020_1_OR_NEWER
			var LightingSettings = Lightmapping.lightingSettings;
#endif

#if UNITY_2018_1_OR_NEWER

			switch (LightmapEditorSettings.mixedBakeMode)
            {
				case MixedLightingMode.IndirectOnly:
					InSetting.BakingMode = EDawnBakingMode.IndirectOnly;
					break;
				case MixedLightingMode.Shadowmask:
					InSetting.BakingMode = EDawnBakingMode.ShadowMask;
					break;
				case MixedLightingMode.Subtractive:
					InSetting.BakingMode = EDawnBakingMode.Subtractive;
					break;
			}
			
			InSetting.AtlasSettings.MaxLightmapSize = LightmapEditorSettings.maxAtlasSize;
			InSetting.AtlasSettings.TexelPerUnit = (int)LightmapEditorSettings.bakeResolution;

			InSetting.LightmapSettings.DirectionalMode = 
				LightmapEditorSettings.lightmapsMode == LightmapsMode.CombinedDirectional ?
				EDawnDirectionalMode.Directional : EDawnDirectionalMode.NonDirectional;
			InSetting.LightmapSettings.SamplesPerPixel = LightmapEditorSettings.indirectSampleCount;
			InSetting.LightmapSettings.PenumbraShadowFraction = Mathf.Clamp(LightmapEditorSettings.directSampleCount / LightmapEditorSettings.indirectSampleCount,0.1f,1.0f);
			InSetting.LightmapSettings.MaxBounces = InSetting.LightmapSettings.MaxSkyBounces = LightmapEditorSettings.bounces;


#if UNITY_2020_1_OR_NEWER
			InSetting.LightProbeSettings.SamplesPerPixel = Mathf.RoundToInt(InSetting.LightmapSettings.SamplesPerPixel * LightingSettings.lightProbeSampleCountMultiplier);
#elif UNITY_2019_1_OR_NEWER
			InSetting.LightProbeSettings.SamplesPerPixel = Mathf.RoundToInt(InSetting.LightmapSettings.SamplesPerPixel * LightmapEditorSettings.lightProbeSampleCountMultiplier);
#else
			InSetting.LightProbeSettings.SamplesPerPixel = InSetting.LightmapSettings.SamplesPerPixel;
#endif
			InSetting.LightProbeSettings.MaxBounces = LightmapEditorSettings.bounces;

			InSetting.OcclusionSettings.Enabled = LightmapEditorSettings.enableAmbientOcclusion;
			InSetting.OcclusionSettings.MaxAmbientDistance = LightmapEditorSettings.aoMaxDistance;
			//InSetting.OcclusionSettings.OcclusionExponent = LightmapEditorSettings.aoContrast;
			InSetting.OcclusionSettings.DirectFraction =  LightmapEditorSettings.aoExponentDirect;
			InSetting.OcclusionSettings.IndirectFraction =  LightmapEditorSettings.aoExponentIndirect;

#if UNITY_2020_1_OR_NEWER
			InSetting.MiscSettings.AlbedoBoost = LightingSettings.albedoBoost;
			InSetting.MiscSettings.IndirectIntensity = LightingSettings.indirectScale;
#endif
#else
			EditorUtility.DisplayDialog("Warnning", "Not Support For " + Application.unityVersion, "OK");
#endif
		}
	}
}