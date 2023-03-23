using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using NSwarm;
using AgentInterface;

namespace GPUBaking.Editor
{	
	public class DawnShadowMaskTask : DawnTask
	{
		public MeshGatherInfo MeshInstance;
		public LandscapeGatherInfo LandscapeInfo;
		public LightMapAllocation Allocation;
		public readonly string Name;
		public int LightmapIndex = -1;

		public List<DawnBaseLight> Lights = new List<DawnBaseLight>();

		public FGuid Guid
		{
			get
			{
				return MeshInstance != null ? MeshInstance.MeshGuid : LandscapeInfo.LandscapeGuid;
			}
		}
		
		public DawnShadowMaskTask(MeshGatherInfo MeshInstance,LightMapAllocation Allocation)
		{
			this.MeshInstance = MeshInstance;
			this.Allocation = Allocation;
			this.Name = MeshInstance.name;
		}
		
		public DawnShadowMaskTask(LandscapeGatherInfo LandscapeInfo,LightMapAllocation Allocation)
		{
			this.LandscapeInfo = LandscapeInfo;
			this.Allocation = Allocation;
			this.Name = LandscapeInfo.name;
		}

		public override bool ApplyResult(DawnBakingContext Context)
		{
			return true;
		}

		public override bool ExportResult(DawnBakingContext Context)
		{
			return true;
		}

		public override int Cost
		{
			get { return Allocation.Width * Allocation.Height;}
		}

		public override ETaskType Type
		{
			get {return ETaskType.SHADOWMASK;}
		}
		public DawnLightmapBakingParameters LightMapBakingParameters
		{
			get
			{
				DawnLightmapBakingParameters dawnLightmapBakingParameters = null;

				if (MeshInstance == null && LandscapeInfo == null)
				{
					return dawnLightmapBakingParameters;
				}

				if (MeshInstance != null)
				{
					dawnLightmapBakingParameters = MeshInstance.Filter.GetComponentInParent<DawnLightmapBakingParameters>();
				}
				else
				{
					dawnLightmapBakingParameters = LandscapeInfo.Landscape.GetComponent<DawnLightmapBakingParameters>();
				}

				return dawnLightmapBakingParameters;
			}
		}
	}
}