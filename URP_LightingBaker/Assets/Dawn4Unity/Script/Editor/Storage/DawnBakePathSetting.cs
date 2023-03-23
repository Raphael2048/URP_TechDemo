using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GPUBaking.Editor
{
	public enum BakingResultPathStyle
	{
		Default,
		FullName,
	}

	public abstract class DawnBakePathSetting
    {
		public static BakingResultPathStyle SelectedStyle = BakingResultPathStyle.Default;

		

		public static DawnBakePathSetting GetInstance()
        {
			return GetInstance(EditorSceneManager.GetActiveScene());
		}

		public static DawnBakePathSetting GetInstance(Scene CurrentScene)
		{
			if (SelectedStyle == BakingResultPathStyle.FullName) {
				return new CustomDawnBakePathSetting(CurrentScene);
			}
			return new DefaultDawnBakePathSetting(CurrentScene);
		}

		protected Scene CurrentScene;

		protected DawnBakePathSetting(Scene CurrentScene)
        {
			this.CurrentScene = CurrentScene;
		}

		public abstract string BakeResultFolderPath (bool bCreateIfNotExist);

		protected abstract string BakeResultFolderPathPrefix ();

		protected abstract string BakeTempFolderName ();

		protected virtual string BakeTempFolderPath (bool bCreateIfNotExist = true)
		{
			string TempFolderName = BakeTempFolderName();
			string TempFolderPath = string.Format("{0}/{1}", BakeResultFolderPath(bCreateIfNotExist), TempFolderName);
			if (bCreateIfNotExist) {
				CreateFolderIfNotExist (TempFolderPath);
			}
			return TempFolderPath;
		}

		public virtual string DawnLightProbeAssetPath(bool bCreateIfNotExist = true)
		{
			BakeResultFolderPath(bCreateIfNotExist);
			return String.Format("{0}DawnLightingProbeData.asset", BakeResultFolderPathPrefix());
		}

		public virtual string DawnLightingAssetPath(bool bCreateIfNotExist = true)
		{
			BakeResultFolderPath(bCreateIfNotExist);
			return String.Format("{0}DawnLightingData.asset", BakeResultFolderPathPrefix());
		}

		public virtual string DawnBakeResultAssetPath()
		{
			return String.Format("{0}DawnBakedAssetResult.asset", BakeResultFolderPathPrefix());
		}

		public virtual string DawnBakeLightmapPath(int LightmapIndex,string LightmapName, string LightmapTextureExtension)
		{
			if(!string.IsNullOrWhiteSpace(LightmapName))
            {
				return string.Format("{0}{1}_{2}{3}", BakeResultFolderPathPrefix(), LightmapName, LightmapIndex, LightmapTextureExtension);
			}
			return string.Format("{0}DawnBakedLightmap_{1}{2}", BakeResultFolderPathPrefix(), LightmapIndex, LightmapTextureExtension);
		}

		public virtual string DawnBakeShadowMaskPath(int ShadowMaskIndex,string LightmapName, string ShadowMaskTextureExtension)
		{
			if (!string.IsNullOrWhiteSpace(LightmapName))
			{
				return string.Format("{0}{1}_Shadow_{2}{3}", BakeResultFolderPathPrefix(), LightmapName, ShadowMaskIndex, ShadowMaskTextureExtension);
			}
			return string.Format("{0}DawnBakedShadowMask_{1}{2}", BakeResultFolderPathPrefix(), ShadowMaskIndex, ShadowMaskTextureExtension);
		}

		public virtual string DawnBakeDirectionLMPath(int DirectionLMIndex, string LightmapName, string DirectionLMTextureExtension)
		{
			if (!string.IsNullOrWhiteSpace(LightmapName))
			{
				return string.Format("{0}{1}_DL_{2}{3}", BakeResultFolderPathPrefix(), LightmapName, DirectionLMIndex, DirectionLMTextureExtension);
			}
			return string.Format("{0}DawnBakedDirecitonLM_{1}{2}", BakeResultFolderPathPrefix(), DirectionLMIndex, DirectionLMTextureExtension);
		}

		public virtual string DawnReflectionProbePath(int ReflectionProbeIndex,bool bTempFile)
		{
			return string.Format("{0}{1}DawnReflectionProbe_{2}.exr", BakeResultFolderPathPrefix(), bTempFile ? "Temp" :"", ReflectionProbeIndex);
		}

		public virtual string DawnBakeTempFilePath(string TempFileName)
		{
			return string.Format("{0}/{1}", BakeTempFolderPath(true),TempFileName);
		}

		public virtual string DawnBakeTempLandscapeMatPath(Terrain Landscape)
		{
			return string.Format("{0}/{1}_{2}_dawn.mat", BakeTempFolderPath(true), Landscape.name, Landscape.GetInstanceID());
		}

		public virtual string DawnBakeTempEmissiveMatPath(Material EmissiveMat)
		{
			return string.Format("{0}/DawnEmissiveMaterial_{1}.mat", BakeTempFolderPath(true), EmissiveMat.GetInstanceID());
		}

		protected bool CreateFolderIfNotExist(string FolderPath)
		{
			if (!AssetDatabase.IsValidFolder(FolderPath))
			{
				int PathIndex = FolderPath.LastIndexOf ("/");
				Debug.AssertFormat (PathIndex >= 0 && PathIndex < FolderPath.Length - 1,"CreateFolder {0} Error!!!"+FolderPath);
				string ParentPath = FolderPath.Substring (0,PathIndex);
				string FolderName = FolderPath.Substring (PathIndex+1);
				AssetDatabase.CreateFolder(ParentPath,FolderName);
			}
			return true;
		}
    }

	public class DefaultDawnBakePathSetting : DawnBakePathSetting
	{
		public DefaultDawnBakePathSetting(Scene CurrentScene): base(CurrentScene)
		{
        }
		// This path should begin with "Assets/"
		public override string BakeResultFolderPath(bool bCreateIfNotExist = false)
		{
			string ExportFolder = CurrentScene.path.Split('.')[0];
			if (bCreateIfNotExist) {
				CreateFolderIfNotExist (ExportFolder);
			}
			return ExportFolder;
		}

		protected override string BakeResultFolderPathPrefix ()
		{
			return BakeResultFolderPath () + "/";
		}

		protected override string BakeTempFolderName ()
		{
			return "DawnTemp";
		}
	}

	public class CustomDawnBakePathSetting : DawnBakePathSetting
	{
		public CustomDawnBakePathSetting(Scene CurrentScene) : base(CurrentScene)
		{
		}
		// This path should begin with "Assets/DawnLightmaps/"
		public override string BakeResultFolderPath(bool bCreateIfNotExist = false)
		{
			string ExportFolder = "Assets/DawnLightmaps";
			if (bCreateIfNotExist) {
				CreateFolderIfNotExist (ExportFolder);
			}
			return ExportFolder;
		}

		protected override string BakeResultFolderPathPrefix ()
		{
			return BakeResultFolderPath () + "/"+CurrentScene.name+"_";
		}

		protected override string BakeTempFolderName ()
		{
			return CurrentScene.name;
		}
	}
}