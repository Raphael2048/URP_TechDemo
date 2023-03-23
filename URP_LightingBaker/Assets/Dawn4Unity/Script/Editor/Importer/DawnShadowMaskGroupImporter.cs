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
		public bool ImportShadowMaskGroup(DawnBakingContext Context, NSwarm.FGuid Guid, DawnShadowMaskGroupTask ShadowMaskGroupTask)
		{
			bool bSuccessed = true;

			foreach(var LightmapItem in ShadowMaskGroupTask.ShadowMaskItems)
			{
				if (LightmapItem.Status == ETaskStatus.PENDDING) {
					lock (LightingSystem.CompletedTasks) {
						LightmapItem.Status = ETaskStatus.IMPORTED;
						LightingSystem.CompletedTasks.Add (LightmapItem.TaskGuid);
					}
					bSuccessed = bSuccessed && ImportShadowMask (Context,LightmapItem.TaskGuid,LightmapItem);
					if (bSuccessed) {
						lock (LightingSystem.ImportedTasks) {
							LightmapItem.Status = ETaskStatus.IMPORTED;
							LightingSystem.ImportedTasks.Add (LightmapItem.TaskGuid);
						}
					}
				} else {
					Context.LogErrorFormat ("Task {0} Has Processed!!!", LightmapItem.TaskGuid.D);
				}
			}

			return bSuccessed;
        }
    }
}
