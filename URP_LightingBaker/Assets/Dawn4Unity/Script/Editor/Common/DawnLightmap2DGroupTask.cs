using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using NSwarm;
using AgentInterface;
using UnityEditor;

namespace GPUBaking.Editor
{
	public class DawnLightmap2DGroupTask : DawnTask
	{
		public int Width;
		public int Height;
		public int GroupIndex;
		internal int TotalCost = 0;

		internal List<DawnLightmap2DTask> LightmapItems = new List<DawnLightmap2DTask>();

		public DawnLightmap2DGroupTask(int Index,int Width,int Height)
		{
			this.TaskGuid = new FGuid(0,0,(uint)Index,(uint)EJobGuidType.EGUID_D_FOR_PACKING);
			this.GroupIndex = Index;
			this.Width = Width;
			this.Height = Height;
			this.TotalCost = 0;
		}

		public bool AddTexture(DawnLightmap2DTask Lightmap2D)
		{
			LightmapItems.Add (Lightmap2D);

			TotalCost += Lightmap2D.Cost;

			return true;
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
			get { 
				return TotalCost;
			}
		}

		public override ETaskType Type
		{
			get {return ETaskType.LIGHTMAP2D_GROUP;}
		}
	}
}