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
		void GatherShadowMaskJobs(DawnBakingContext Context)
		{
			foreach (var ShadowMask in Context.ShadowmaskList) {
				if (!LightingSystem.PendingTasks.ContainsKey(ShadowMask.TaskGuid))
                {
					ShadowMask.TaskGuid = ShadowMask.Guid;
					ShadowMask.TaskGuid.B = (uint)EJobGuidType.EGUID_B_FOR_SHADOWMAP;
					LightingSystem.PendingTasks.Add(ShadowMask.TaskGuid, ShadowMask);
				}
			}
		}

		DawnShadowMaskTask GatherShadowMaskJob(DawnBakingContext Context,MeshGatherInfo MeshInstance,LightMapAllocation Allocation)
		{
			if(!Context.Settings.ShadowSettings.Enabled || Context.MixedLights.Count == 0)
			{
				return null;
			}

			var ShadowMask = new DawnShadowMaskTask (MeshInstance,Allocation);

			ShadowMask.Lights = Context.MixedLights;

			return ShadowMask;
		}
		
		DawnShadowMaskTask GatherShadowMaskJob(DawnBakingContext Context, LandscapeGatherInfo LandscapeInfo,LightMapAllocation Allocation)
		{
			if(!Context.Settings.ShadowSettings.Enabled || Context.MixedLights.Count == 0)
			{
				return null;
			}

			var ShadowMask = new DawnShadowMaskTask (LandscapeInfo,Allocation);

			ShadowMask.Lights = Context.MixedLights;			

			return ShadowMask;
		}

		bool ExportShadowMaskJobs(DawnBakingContext Context)
		{
			foreach (var ShadowMask in Context.ShadowmaskList) {
				if (!LightingSystem.PendingTasks.ContainsKey(ShadowMask.TaskGuid))
                {
					continue;
                }
				FSDFShadowInput ShadowInput = new FSDFShadowInput ();
				ExportShadowMaskJob (Context,ShadowMask,ref ShadowInput);
				ShadowMask.LightmapIndex = BakingJobInputs.SDFShadowJobs.NumElements;
				BakingJobInputs.SDFShadowJobs.AddElement (ref ShadowInput);
			}
			return true;
		}

		void ExportShadowMaskJob(DawnBakingContext Context,DawnShadowMaskTask ShadowMask,ref FSDFShadowInput ShadowInput)
		{
			//TODO Implement Lightmap Job Export
			DawnDebug.Print("Export ShadowMask:{0}",ShadowMask.Name);

			bool bPadMapping = ShadowMask.Allocation.Padding > 0;

			foreach (var Light in ShadowMask.Lights) {
				var LightGuid = Context.LightGuids[Light];
				ShadowInput.Lights.AddElement(LightGuid);
			}
			var LightMapBakingParameters = ShadowMask.LightMapBakingParameters;
			ShadowInput.HeaderInfo.JobID = ToGuidInfo (ShadowMask.TaskGuid);
			ShadowInput.HeaderInfo.MeshID = ToGuidInfo (ShadowMask.Guid);
			ShadowInput.HeaderInfo.Size.x = ShadowMask.Allocation.UVWidthPixel;
			ShadowInput.HeaderInfo.Size.y = ShadowMask.Allocation.UVHeightPixel;
			ShadowInput.HeaderInfo.LightmapUVIndex = 1;
			ShadowInput.HeaderInfo.Flags = 0;
			ShadowInput.HeaderInfo.Flags |= bPadMapping ? (uint)ELightmapFlags.LIGHTMAP_FLAGS_PADDING : 0;
			ShadowInput.HeaderInfo.Flags |= (uint)ELightmapFlags.LIGHTMAP_FLAGS_BILINEAR_FILTER;
			if (LightMapBakingParameters != null)
            {
				ShadowInput.HeaderInfo.UpsampleFactor = LightMapBakingParameters.SuperSampleFactor;
				ShadowInput.HeaderInfo.OffsetScale = LightMapBakingParameters.OffsetScale;
			}	
			else
            {
				ShadowInput.HeaderInfo.UpsampleFactor = Context.Settings.ShadowSettings.MaxUpSamplingFactor;
				ShadowInput.HeaderInfo.OffsetScale = 1;
			}
				
		}
    }
}
