using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;


using NSwarm;
using AgentInterface;

namespace GPUBaking.Editor
{
	public partial class DawnImporter
    {
		public bool ImportLightmap2DGroup(DawnBakingContext Context, NSwarm.FGuid Guid, DawnLightmap2DGroupTask LightmapGroupTask)
		{
			bool bSuccessed = true;

			foreach(var LightmapItem in LightmapGroupTask.LightmapItems)
			{
				if (LightmapItem.Status == ETaskStatus.PENDDING) {
					lock (LightingSystem.CompletedTasks) {
						LightmapItem.Status = ETaskStatus.IMPORTED;
						LightingSystem.CompletedTasks.Add (LightmapItem.TaskGuid);
					}
					bSuccessed = bSuccessed && ImportLightmap2D (Context,LightmapItem.TaskGuid,LightmapItem);
					if (bSuccessed) {
						lock (LightingSystem.ImportedTasks) {
							LightmapItem.Status = ETaskStatus.IMPORTED;
							LightingSystem.ImportedTasks.Add (LightmapItem.TaskGuid);
						}
					}
				} else {
					DawnDebug.LogErrorFormat ("Task {0} Has Processed!!!", LightmapItem.TaskGuid.D);
				}
			}

			return bSuccessed;
        }
    }
}
