using System.IO;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace GPUBaking.Editor
{
    public partial class DawnLightingSystem
	{
		public void RenderReflectionProbe ()
		{
			var bakeFunc = typeof(Lightmapping).GetMethod ("BakeAllReflectionProbesSnapshots",
				               BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

			Lightmapping.lightingDataAsset = DawnLightAsset.EnsureLightingAssetData(SceneManager.GetActiveScene(),false);

			if (Lightmapping.lightingDataAsset != null)
            {
				DawnLightAsset.ClearReflectionProbes(Lightmapping.lightingDataAsset);

				// render reflection probe
				bakeFunc.Invoke(null, null);

				EditorSceneManager.MarkAllScenesDirty();
			}
			else
            {
				DawnDebug.LogError("Can't render reflection probe without lighting data asset.");
			}
		}
    }
}