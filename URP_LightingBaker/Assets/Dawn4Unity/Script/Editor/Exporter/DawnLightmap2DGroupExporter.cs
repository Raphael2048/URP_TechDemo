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
		void GatherLightmap2DJobGroups(DawnBakingContext Context)
		{
			Context.LightmapGroupList.Clear ();
			DawnLightmap2DGroupTask LightmapTaskGroup = null;

			int GroupIndex = 0;
			foreach (var LightingData in Context.SceneLightingData)
			{
				var LightmapTextures = LightingData.LightmapTextures;
				for (int LightmapIndex = 0; LightmapIndex < LightmapTextures.Count; ++LightmapIndex)
				{
					var LightmapTexture = LightmapTextures[LightmapIndex];
					LightmapTaskGroup = new DawnLightmap2DGroupTask(GroupIndex, LightmapTexture.Width, LightmapTexture.Height);

					foreach (var Allocation in LightmapTexture.Allocations)
                    {
						if(Allocation.Owner.bIsLandscapeLightmap || Allocation.Owner.LightMapBakingParameters !=null)
                        {
							continue;
						}
						LightmapTaskGroup.AddTexture(Allocation.Owner);
					}
					if(LightmapTaskGroup.LightmapItems.Count > 0)
                    {
						Context.AddLightmap2DGroup(LightmapTaskGroup);
						GroupIndex++;
					}
				}
			}
		}

		bool ExportLightmap2DJobGroups(DawnBakingContext Context)
		{
			foreach (var Lightmap2DGroup in Context.LightmapGroupList) {
				FMappingGroupInput LightmapGroupInput = new FMappingGroupInput ();
				ExportLightmap2DJobGroup (Context,Lightmap2DGroup,ref LightmapGroupInput);
				BakingJobInputs.MappingGroupJobs.AddElement (ref LightmapGroupInput);
				LightingSystem.PendingTasks.Add (Lightmap2DGroup.TaskGuid,Lightmap2DGroup);
			}
			return true;
		}

		void ExportLightmap2DJobGroup(DawnBakingContext Context,DawnLightmap2DGroupTask LightmapGroup,ref FMappingGroupInput LightmapGroupInput)
		{
			LightmapGroupInput.JobID = ToGuidInfo (LightmapGroup.TaskGuid);
			LightmapGroupInput.GroupIndex = LightmapGroup.GroupIndex;
			LightmapGroupInput.GroupedSize = Mathf.Max(LightmapGroup.Width, LightmapGroup.Height);

			foreach (var LightmapItem in LightmapGroup.LightmapItems) {
				LightmapItem.Status = ETaskStatus.PENDDING;
				LightmapGroupInput.MappingOffsets.AddElement (new int3(LightmapItem.Allocation.EncodeOffset.x, LightmapItem.Allocation.EncodeOffset.y, LightmapItem.LightmapIndex));
			}
		}
    }
}
