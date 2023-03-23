using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace GPUBaking.Editor
{
	public partial class DawnImporter
    {
		public bool SaveResults(DawnBakingContext Context)
		{
			DawnLightingStatistics.BeginEvent(EBakingState.SAVING);
			bool bExportSuccess = true;

			DawnStorage.BakedContext = Context;

			foreach(var SceneLighting in Context.SceneLightingData)
            {
				var ResultManager = DawnStorage.GetSceneLightingData(SceneLighting.scene);
				foreach(var LightmapData in SceneLighting.LightmapDatas)
                {
					ResultManager.AddBakeLightmap(LightmapData.lightmapColor);
					if(LightmapData.shadowMask!=null)
                    {
						ResultManager.AddBakeShadowMask(LightmapData.shadowMask);
					}
					if (LightmapData.lightmapDir != null)
					{
						ResultManager.AddBakeDirectionalLightmap(LightmapData.lightmapDir);
					}
				}				
			}

			foreach (var PendingTask in LightingSystem.PendingTasks)
			{
				if (PendingTask.Value.Status == ETaskStatus.COMPLETED)
				{
					bExportSuccess = PendingTask.Value.ExportResult(Context);
					if (!bExportSuccess)
					{
						break;
					}
				}
			}

			if (bExportSuccess) {
				foreach (var Light in Context.Lights)
				{
					DawnStorage.GetSceneLightingData(Light.gameObject.scene).AddBakedLight(Light);
				}
				DawnStorage.SetSkyReflectionCubemap(Context.SkyReflectionCubemap);
			}

			if (bExportSuccess)
			{
				DawnProfiler.BeginSample("DawnImporter.ExportBakeResult");
				bExportSuccess = DawnStorage.ExportBakeResult();
				DawnProfiler.EndSample ();

			}
			DawnLightingStatistics.EndEvent(EBakingState.SAVING);
			return bExportSuccess;
		}

		public bool ConvertResults(DawnBakingContext Context)
		{
			DawnLightingStatistics.BeginEvent(EBakingState.CONVERT_LIGHTINGDATA);

			bool bExportSuccess = DawnLightAsset.ConvertLightingAsset(
				Context.Settings.BakingResultMode == EDawnBakingResultMode.UnityLightingAsset,
				Context.Settings.BakingResultMode == EDawnBakingResultMode.DawnBakeResultAsset,
				Context.bUsePrecomputedProbes);

			DawnLightingStatistics.EndEvent(EBakingState.CONVERT_LIGHTINGDATA);
			return bExportSuccess;
		}
    }
}
