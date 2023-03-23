using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Scripting;
using UnityEditor.SceneManagement;
using System;
using System.IO;
using GPUBaking;
using GPUBaking.Editor;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public partial class Dawn4Unity
{
	public delegate void LightingUpdateDelegate(DawnLightingSystem LightingSystem);

	public delegate void LightingCompleteDelegate(DawnLightingSystem LightingSystem,bool bSuccessed);

	private static DawnLightingSystem LightingSystem = null;

	private static DawnSettings LightingSetting = null;

	private static DawnDebugSettings DebugSetting = null;

	public static LightingUpdateDelegate OnUpdateEvent;

	public static LightingCompleteDelegate OnCompleteEvent;

	public static DawnSceneStorage DawnSceneStorage = null;

	static Dawn4Unity()
	{
		EnsureUnityListeners ();
	}

	[MenuItem("Dawn/About")]
	public static void ShowVersion()
	{
		EditorUtility.DisplayDialog("Dawn" + Dawn4UnityVersion.Version, "GPU Baking Tools", "OK");
	}

	[MenuItem("Dawn/Settings")]
    public static void ShowSettings()
    {
		Dawn4UnityWindow window = EditorWindow.GetWindow<Dawn4UnityWindow>("Dawn");
        window.Show();
    }	

	[MenuItem("Dawn/Tools/OptimizeLightmaps")]
	public static void OptimizeLightmaps()
	{
		LightingSystem = null;
		GetLightingSystem ().CalculateLightmapScales (GetLightingSetting(), true, false);
		LightingSystem = null;
		UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes ();
	}
	
	[MenuItem("Dawn/Tools/ResetLightmapsScale")]
	public static void ResetLightmapsScale()
	{
		LightingSystem = null;
		GetLightingSystem ().ResetLighmapsScale(GetLightingSetting(),false);
		LightingSystem = null;
		UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes ();
	}

    [MenuItem("Dawn/BuildLighting")]
    public static void BuildLighting()
    {
		var Settings = GetLightingSetting ();
		DawnBakePathSetting.SelectedStyle = Settings.MiscSettings.bUseSceneNameForOutputPath ? BakingResultPathStyle.FullName : BakingResultPathStyle.Default;

		EditorSceneManager.SaveOpenScenes ();

#if UNITY_2018_1_OR_NEWER
		bool bUsePrecomputedProbes = !Settings.LightProbeSettings.AutoGenerationSetting.AutoGeneration ? 
			DawnLightAsset.ValidateLightingDataAssetForLightProbes() : false;
#else
		bool bUsePrecomputedProbes = false;
#endif

		LightingSystem = null;
		GetLightingSystem().Start(Settings, DebugSetting.bDebugLightingSystem, DebugSetting.bDebugLightmapTexel, bUsePrecomputedProbes);
    }

	[MenuItem("Dawn/BuildSelected")]
	public static void BuildLightingForSelection()
	{
		var Settings = GetLightingSetting();
		DawnBakePathSetting.SelectedStyle = Settings.MiscSettings.bUseSceneNameForOutputPath ? BakingResultPathStyle.FullName : BakingResultPathStyle.Default;

		EditorSceneManager.SaveOpenScenes();

		LightingSystem = null;
		GetLightingSystem().Start(Settings, DebugSetting.bDebugLightingSystem, DebugSetting.bDebugLightmapTexel,false, DawnBakingMode.BakingSelected);
	}

	[MenuItem("Dawn/Cancel Build")]
    public static void CancelBuildLighting()
    {
        if (LightingSystem != null)
        {
            LightingSystem.Cancel();
        }
    }

	[MenuItem("Dawn/Render Reflection Probe")]
    public static void RenderReflectionProbe()
    {
		LightingSystem = null;
		GetLightingSystem().RenderReflectionProbe();
		LightingSystem = null;
	}

	[MenuItem("Dawn/Clear LightingData")]
	public static void ClearLightingData()
	{
		Lightmapping.ClearLightingDataAsset();
		Lightmapping.lightingDataAsset = null;

		for (int SceneIndex = 0; SceneIndex < EditorSceneManager.sceneCount; ++SceneIndex)
		{
			var Scene = EditorSceneManager.GetSceneAt(SceneIndex);
			var ExportFolder = DawnBakePathSetting.GetInstance(Scene).BakeResultFolderPath(false);
			if (AssetDatabase.IsValidFolder(ExportFolder))
			{
				LightmapSettings.lightmaps = null;
				AssetDatabase.DeleteAsset(ExportFolder);
			}
		}
	}
	
	[MenuItem("Dawn/Clear Cached Data")]
	public static void ClearCachedData()
	{
		// Clear Dawn Mesh Components
		var DawnMeshComponents = GameObject.FindObjectsOfType<DawnMeshComponent> ();
		foreach (var comp in DawnMeshComponents)
		{
			comp.ClearCachedSurfaceArea();
			comp.ClearUVBounds();
		}
		Debug.Log("Cleared " + DawnMeshComponents.Length + " Dawn Mesh Components.");
	}
	
	[MenuItem("Dawn/Tools/Debug/ExportDebugFile")]
    static void ExportScene()
    {
		string ExportPath = EditorUtility.OpenFolderPanel("Select Export Folder", Application.dataPath+"../Dawn", "");
        if (string.IsNullOrEmpty(ExportPath))
        {
           return;
        }
		EnsureSettings ();
		var LightingSystem = new DawnLightingSystem(new NSwarm.FGuid(0x0123, 0x4567, 0x89AB, 0xCDEF));
		LightingSystem.Export(ExportPath,GetLightingSetting());
    }

	static void Update()
	{
		if (LightingSystem != null)
		{
			try
			{
				LightingSystem.Update();
			}
			catch (Exception e)
			{
				if(!(e is DawnException))
                {
					Debug.LogException(e);
				}
				LightingSystem.Cancel(EBakingError.EXCEPTION);
			}
			if (OnUpdateEvent != null)
			{
				OnUpdateEvent (LightingSystem);
			}
			if (LightingSystem.IsBuildCompleted)
			{
				if (OnCompleteEvent != null)
				{
					OnCompleteEvent(LightingSystem, LightingSystem.IsBuildSuccessed);
				}
				LightingSystem.IsBuildCompleted = false;
				LightingSystem.ClearError();
			}
		}
	}

	static bool bUnityListenerRegister = false;

	static void EnsureUnityListeners ()
	{
		if (!bUnityListenerRegister) {
			EditorApplication.update += Update;
			EditorSceneManager.sceneOpened += LoadDawnSceneStorage;
			bUnityListenerRegister = true;
		}
	}

	public static string DebugScenePath = "Assets/Dawn4Unity/Example/Scene-0000012300004567000089AB0000CDEF/0000012300004567000089AB0000CDEF.scene";
	public static string DebugJobPath = "Assets/Dawn4Unity/Example/Scene-0000012300004567000089AB0000CDEF/0000012300004567000089AB0000CDEF.jobs";

	static bool EnsureLightingSystem(){
		if(LightingSystem == null)
		{
			if (DebugSetting !=null && DebugSetting.bDebugLightingSystem) {
				LightingSystem = new DawnLightingSystem(new NSwarm.FGuid(0x0123, 0x4567, 0x89AB, 0xCDEF));
			} else {
				LightingSystem = new DawnLightingSystem(DawnLightingSystem.CreateGuid());
			}
		}
		return LightingSystem != null;
	}

	private static string DefaultSettingPath = "Assets/Dawn4Unity/Configs/DefaultDawnSettings.asset";
	private static string DefaultSettingDir = "Assets/Dawn4Unity/Configs/";

	static void EnsureSettings(){
		if (LightingSetting == null) 
		{
			LightingSetting = AssetDatabase.LoadAssetAtPath<DawnSettings>(DefaultSettingPath);
		}
		if (LightingSetting == null) 
		{
			LightingSetting = ScriptableObject.CreateInstance <DawnSettings>();
		}

		if (DebugSetting == null)
		{
			DebugSetting = new DawnDebugSettings();
		}

		SyncDawnSetting();
	}

	public static DawnLightingSystem GetLightingSystem()
	{
		EnsureLightingSystem ();
		return LightingSystem;
	}

	public static DawnSettings GetLightingSetting()
	{
		EnsureSettings ();
		return LightingSetting;
	}

	public static void SetLightingSetting(DawnSettings NewSettings)
	{
		LightingSetting = NewSettings;
		SyncDawnSetting();

	}

	public static DawnDebugSettings GetDebugSetting()
	{
		EnsureSettings();
		return DebugSetting;
	}

	public static void SaveLightingSetting(DawnSettings InSetting)
	{
		LightingSetting = InSetting;
		if (!AssetDatabase.IsMainAsset (LightingSetting)) {
			AssetDatabase.CreateAsset (LightingSetting, DefaultSettingPath);
		} else {
			EditorUtility.SetDirty (LightingSetting);
		}
		if(DawnSceneStorage == null)
        {
			DawnSceneStorage = GetDawnSceneStorage();
        }
		DawnSceneStorage.DawnSetting = LightingSetting;
		EditorUtility.SetDirty(DawnSceneStorage);

		SyncDawnSetting();
	}

	public static void SaveLightingSetting(DawnSettings InSetting, string Name)
    {
		LightingSetting = InSetting;
		if (!AssetDatabase.IsMainAsset(LightingSetting))
		{
			AssetDatabase.CreateAsset(LightingSetting, DefaultSettingDir + Name + ".asset");
		}
		else
		{
			EditorUtility.SetDirty(LightingSetting);
		}
		if (DawnSceneStorage == null)
		{
			DawnSceneStorage = GetDawnSceneStorage();
		}
		DawnSceneStorage.DawnSetting = LightingSetting;
		EditorUtility.SetDirty(DawnSceneStorage);

		SyncDawnSetting();
	}

	static void SyncDawnSetting()
	{
		DawnSettings.Instance = LightingSetting;		
	}

	static void LoadDawnSceneStorage(Scene scene, OpenSceneMode mode)
    {
		LightingSetting = GetDawnSceneStorage().DawnSetting;
		EnsureLightingSystem();
    }

	static DawnSceneStorage GetDawnSceneStorage()
    {
		GameObject go = null;
		List<GameObject> roots = new List<GameObject>();
		SceneManager.GetActiveScene().GetRootGameObjects(roots);
		go = roots.Find(g => g.name == "DawnSceneStorage");

		if (go == null) go = GameObject.Find("DawnSceneStorage");
		if (go == null)
		{
			go = new GameObject();
			go.name = "DawnSceneStorage";
			go.hideFlags = HideFlags.HideInHierarchy;
		}
		DawnSceneStorage = go.GetComponent<DawnSceneStorage>();
		if (DawnSceneStorage == null)
		{
			DawnSceneStorage = go.AddComponent<DawnSceneStorage>();
		}
		return DawnSceneStorage;
	}

}

public struct Dawn4UnityVersion
{
	private const string DefaultVersionPath = "Assets/Dawn4Unity/Configs/Version.txt";

	private static int MAJOR_VERSION = 1;
	private static int MINOR_VERSION = 1;
	private static int BUILD_VERSION = 0;

	public static string Version
	{
		get
		{
			var VersionInfo = EnsureVersion();
			if(VersionInfo!=null)
            {
				return VersionInfo;
			}
			return string.Format("{0}.{1}.{2}", MAJOR_VERSION, MINOR_VERSION, BUILD_VERSION);
		}
	}

	private static string EnsureVersion()
	{
		var VersionText = AssetDatabase.LoadAssetAtPath<TextAsset>(DefaultVersionPath);
		return VersionText!=null && !string.IsNullOrWhiteSpace(VersionText.text) ? VersionText.text : null;
	}
}
