using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using NSwarm;
using AgentInterface;
using UnityEditor;

namespace GPUBaking.Editor
{
	public class DawnShadowMaskGroupTask : DawnTask
	{
		public int Width;
		public int Height;

		internal List<DawnShadowMaskTask> ShadowMaskItems = new List<DawnShadowMaskTask>();
	
		internal int TotalCost = 0;

		public DawnShadowMaskGroupTask(int Index,int Width,int Height)
		{
			this.TaskGuid = new FGuid(0,(uint)EJobGuidType.EGUID_B_FOR_SHADOWMAP,(uint)Index,(uint)EJobGuidType.EGUID_D_FOR_PACKING);
			this.Width = Width;
			this.Height = Height;
			this.TotalCost = 0;
		}

		public bool AddTexture(DawnShadowMaskTask ShadowMask)
		{
			if (ShadowMaskItems.Count > 10) {
				return false;
			}
			ShadowMaskItems.Add (ShadowMask);
			TotalCost += ShadowMask.Cost;
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
			get {return ETaskType.SHADOWMASK_GROUP;}
		}
	}
}