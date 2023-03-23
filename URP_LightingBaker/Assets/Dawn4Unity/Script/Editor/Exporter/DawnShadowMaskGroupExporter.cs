using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

using NSwarm;
using AgentInterface;

namespace GPUBaking.Editor
{
	public partial class DawnExporter
    {
		void GatherShadowMaskJobGroups(DawnBakingContext Context)
		{
			Context.ShadowMaskGroupList.Clear();
			DawnShadowMaskGroupTask ShadowTaskGroup = null;

			int GroupIndex = 0;
			foreach (var LightingData in Context.SceneLightingData)
			{
				var LightmapTextures = LightingData.LightmapTextures;
				for (int LightmapIndex = 0; LightmapIndex < LightmapTextures.Count; ++LightmapIndex)
				{
					var LightmapTexture = LightmapTextures[LightmapIndex];
					ShadowTaskGroup = new DawnShadowMaskGroupTask(GroupIndex, LightmapTexture.Width, LightmapTexture.Height);

					foreach (var Allocation in LightmapTexture.Allocations)
					{
						if(Allocation.Owner.LightmapIndex>=0)
                        {
							ShadowTaskGroup.AddTexture(Allocation.Owner.ShadowMaskTask);
						}
					}
					if(ShadowTaskGroup.ShadowMaskItems.Count>0)
                    {
						Context.AddShadowMaskGroup(ShadowTaskGroup);
						GroupIndex++;
					}					
				}
			}
		}

		bool ExportShadowMaskJobGroups(DawnBakingContext Context)
		{
			foreach (var ShadowMaskGroup in Context.ShadowMaskGroupList) {
				FSDFShadowGroupInput ShadowMaskGroupInput = new FSDFShadowGroupInput ();
				ExportShadowMaskJobGroup (Context,ShadowMaskGroup,ref ShadowMaskGroupInput);
				BakingJobInputs.SDFShadowGroupJobs.AddElement (ref ShadowMaskGroupInput);
				LightingSystem.PendingTasks.Add (ShadowMaskGroup.TaskGuid,ShadowMaskGroup);
			}
			return true;
		}

		void ExportShadowMaskJobGroup(DawnBakingContext Context,DawnShadowMaskGroupTask ShadowMaskGroup,ref FSDFShadowGroupInput ShadowMaskGroupInput)
		{
			ShadowMaskGroupInput.JobID = ToGuidInfo (ShadowMaskGroup.TaskGuid);
			ShadowMaskGroupInput.GroupSize = ShadowMaskGroup.Width;
			ShadowMaskGroupInput.UpsampleFactor = Context.Settings.ShadowSettings.MaxUpSamplingFactor;

			foreach (var LightmapItem in ShadowMaskGroup.ShadowMaskItems) {
				LightmapItem.Status = ETaskStatus.PENDDING;
				ShadowMaskGroupInput.SDFShadowJobIndices.AddElement (LightmapItem.LightmapIndex);
			}
		}
    }
}
