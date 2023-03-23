using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace GPUBaking.Editor
{
	public partial class DawnImporter
    {
		public bool ApplyResults(DawnBakingContext Context)
        {
			DawnLightingStatistics.BeginEvent(EBakingState.APPLYING);

			DawnProfiler.BeginSample("DawnImporter.ApplyLightChannels");
			bool bSucceed = ApplyLightChannels(Context);
			DawnProfiler.EndSample ();

            foreach (var PendingTask in LightingSystem.PendingTasks)
            {
                if (PendingTask.Value.Status == ETaskStatus.IMPORTED)
                {
					if (PendingTask.Value.ApplyResult (Context)) {
						PendingTask.Value.Status = ETaskStatus.COMPLETED;
					} else {
						bSucceed = false;
					}
                }
            }
			//bSucceed = bSucceed && ApplyLightProbeResult (Context);
			DawnLightingStatistics.EndEvent(EBakingState.APPLYING);

			return bSucceed;
        }

		internal bool ApplyLightChannels(DawnBakingContext Context)
		{
			var MixedLightingMode = LightBakingInfo.GetMixedLightingMode(Context.Settings.BakingMode);
			var LightIndices = new System.Collections.Generic.Dictionary<DawnBaseLight, int>();
			for (int LightIndex = 0; LightIndex < Context.Lights.Count; ++LightIndex)
			{
				var LightInfo = Context.Lights[LightIndex];
				LightIndices.Add(LightInfo, LightIndex);
				Context.SetLightChannel (LightInfo, LightIndex, -1,EDawnLightingMask.StaticBaked, LightInfo.lightmapBakeType == LightmapBakeType.Mixed ? MixedLightingMode: EDawnLightingMode.IndirectOnly);
			}			
			for (int LightIndex = 0; LightIndex < Context.MixedLights.Count;++LightIndex)
			{
				var LightInfo = Context.MixedLights[LightIndex];
				Context.SetLightChannel (LightInfo, LightIndices[LightInfo], LightIndex, EDawnLightingMask.MixedBaked, MixedLightingMode);
			}
			return true;
		}

		internal bool ApplyLightProbeResult(DawnBakingContext Context)
		{
			if (Context.ProbeCeffs.Count == 0) {
				return true;
			}

			var Probes = LightmapSettings.lightProbes;

			DawnDebug.AssertFormat (Probes!=null,"Baked Cells:{0} For LightProbes Invalid",Context.ProbeCeffs.Count);

			if(Probes == null)
            {
				DawnDebug.LogWarning("No light probes in the scene, cannot apply the light probes result automatically!");
				return true;
            }
			Probes.bakedProbes = Context.ProbeCeffs.ToArray();

			//TODO ADD Postions for lightprobes
			//Probes.positions = Context.ProbePositions.ToArray();
			LightmapSettings.lightProbes = Probes;

			EditorUtility.SetDirty(LightmapSettings.lightProbes);
			EditorSceneManager.MarkAllScenesDirty();
			
			return true;
		}
    }
}
