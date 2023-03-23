using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace GPUBaking.Editor
{
    [InitializeOnLoad]
    public class DawnEditorAPI
    {
		static DawnEditorAPI()
		{
			GPUBaking.DawnEditorAPI.StartBaking = Dawn4Unity.BuildLighting;
			GPUBaking.DawnEditorAPI.StopBaking = Dawn4Unity.CancelBuildLighting;
			GPUBaking.DawnEditorAPI.BakeReflectionProbe = Dawn4Unity.RenderReflectionProbe;
			Dawn4Unity.OnCompleteEvent = OnBakingCompleteEvent;

			GPUBaking.DawnEditorAPI.ClearAllBakedResult = DawnStorage.ClearAllBakedResult;
			GPUBaking.DawnEditorAPI.ImportBakedResult = DawnStorage.ImportBakeResult;
			GPUBaking.DawnEditorAPI.ExportBakedResult = DawnStorage.ExportBakeResult;

			GPUBaking.DawnEditorAPI.ClearLightingData = Dawn4Unity.ClearLightingData;
			GPUBaking.DawnEditorAPI.ConvertLightingData = DawnLightAsset.ConvertLightingAsset;
		}

		static void OnBakingCompleteEvent(DawnLightingSystem LightingSystem, bool bSuccessed)
		{
			bool bHandled = false;
			if(GPUBaking.DawnEditorAPI.OnCompleteEvent!=null)
            {
				bHandled = GPUBaking.DawnEditorAPI.OnCompleteEvent(bSuccessed);
			}
			if(!bHandled)
            {
				Dawn4UnityWindow.ShowCompleteUI(LightingSystem, bSuccessed);
			}
		}
	}
}
