using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System;
using System.Threading;

using NSwarm;
using AgentInterface;

namespace GPUBaking.Editor
{
	public partial class DawnImporter
    {
        DawnLightingSystem LightingSystem;

		object ImportEvent;
		int NumOfThread;
		bool bRunning ;
		bool bError;

		int ImportedIndex;
		int ImportPerThread = 1;

		public DawnImporter(DawnLightingSystem LightingSystem,int NumOfThread = 5)
        {
            this.LightingSystem = LightingSystem;
			this.NumOfThread = NumOfThread;
			this.bRunning = false;
			this.bError = false;
			this.ImportedIndex = -1;
			this.ImportEvent = new object ();
        }

		public void Reset()
		{
			this.ImportedIndex = -1;
		}

		public bool ImportResult(FGuid TaskGuid,DawnTask PendingTask)
		{
			bool bSucessed = false;
			if(PendingTask.Status == ETaskStatus.COMPLETED)
			{
				var Context = LightingSystem.BakingContext;

				switch (PendingTask.Type) {
				case ETaskType.LIGHTMAP2D:
					if (ImportLightmap2D(Context, TaskGuid, PendingTask as DawnLightmap2DTask))
					{
						PendingTask.Status = ETaskStatus.IMPORTED;
						bSucessed = true;
					}
					break;
				case ETaskType.SHADOWMASK:
					if (ImportShadowMask(Context, TaskGuid, PendingTask as DawnShadowMaskTask))
					{
						PendingTask.Status = ETaskStatus.IMPORTED;
						bSucessed = true;
					}
					break;
				case ETaskType.LIGHTPROBE:
					if (ImportLightProbes(Context, TaskGuid, PendingTask as DawnLightProbeTask))
					{
						PendingTask.Status = ETaskStatus.IMPORTED;
						bSucessed = true;
					}
					break;
				case ETaskType.AUTO_GENERATION_LIGHTPROBE:
					if (ImportSparseLightProbes(Context, TaskGuid, PendingTask as DawnAutoGenerationLightProbeTask))
					{
						PendingTask.Status = ETaskStatus.IMPORTED;
						bSucessed = true;
					}
					break;
				case ETaskType.LIGHTMAP2D_GROUP:
					if (ImportLightmap2DGroup(Context, TaskGuid, PendingTask as DawnLightmap2DGroupTask))
					{
						PendingTask.Status = ETaskStatus.IMPORTED;
						bSucessed = true;
					}
					break;
				case ETaskType.SHADOWMASK_GROUP:
					if (ImportShadowMaskGroup(Context, TaskGuid, PendingTask as DawnShadowMaskGroupTask))
					{
						PendingTask.Status = ETaskStatus.IMPORTED;
						bSucessed = true;
					}
					break;
				default:
					DawnDebug.LogWarningFormat ("Import({0}) For {1} Not Support!!!", PendingTask.Type, TaskGuid);
					break;
				}
			}
			return bSucessed;
		}

		public bool StartImport()
		{
			Debug.Assert (bRunning == false);
			bRunning = true;
			bError = false;
			ImportedIndex = 0;

			bool bSuccessed = false;
			for (int ThreadIndex = 0; ThreadIndex < NumOfThread; ++ThreadIndex) {
				bSuccessed = ThreadPool.QueueUserWorkItem (ImportInBackground,ThreadIndex) || bSuccessed;
			}
			return bSuccessed;
		}

		public void StopImport()
		{
			bRunning = false;
			Reset ();
		}

		bool ImportResults()
		{
			bool bSuccessed = true;

			List<FGuid> CompletedTasks = new List<FGuid>();

			int TaskNum = 0;
			lock (LightingSystem.CompletedTasks) {
				TaskNum = Math.Min(LightingSystem.CompletedTasks.Count - ImportedIndex,ImportPerThread);
				if (TaskNum > 0) {
					CompletedTasks = LightingSystem.CompletedTasks.GetRange (ImportedIndex, TaskNum);
					ImportedIndex += TaskNum;
				}
			}

			foreach(var TaskGuid in CompletedTasks)
			{
				if (!bRunning)
					break;
				DawnDebug.Print("PendingTasks:{0},{1},{2},{3}", TaskGuid.A, TaskGuid.B, TaskGuid.C, TaskGuid.D);
				var PendingTask = LightingSystem.PendingTasks[TaskGuid];
				if (PendingTask.Status != ETaskStatus.COMPLETED) {
					continue;
				}
				if (ImportResult (TaskGuid, PendingTask)) {
					lock (LightingSystem.ImportedTasks) {
						LightingSystem.ImportedTasks.Add (TaskGuid);
					}
				} else {
					bSuccessed = false;
					break;
				}	
			}
			return bSuccessed;
		}

		void ImportInBackground(object state)
		{
			bool bHasTask = false;
			while (bRunning) {
				bHasTask = ImportedIndex < LightingSystem.NumOfTaskCompleted;
				if (bHasTask) {
                    try
                    {
						if (!ImportResults())
						{
							bError = true;
							bRunning = false;
							break;
						}
					}
					catch(Exception e)
                    {
						bError = true;
						bRunning = false;
						LightingSystem.BakingContext.LogErrorFormat("ImportResults Failure With {0}", e);
					}
					
				} else {
					lock (ImportEvent) {
						Monitor.Wait (ImportEvent, 500);
					}
				}
			}
			DawnDebug.Print ("Import Worker({0}) Stoped",state);
		}

		public void Notify()
		{
			lock (ImportEvent) {
				Monitor.PulseAll (ImportEvent);
			}
		}

		public bool IsDone
		{
			get {
				return LightingSystem.NumOfTaskCompleted == LightingSystem.NumOfTaskImported;
			}
		}

		public bool HasError
		{
			get{
				return bError;
			}			
		}

		Color ToColor(ref float4 Input)
		{
			return new Color (Input.x, Input.y, Input.z, Input.w);
		}

		Color ToColor(ref float4 Input,float Alpha)
		{
			return new Color (Input.x, Input.y, Input.z, 1.0f);
		}

		Vector4 ToVector4(ref float4 Input)
		{
			return new Vector4 (Input.x, Input.y, Input.z, Input.w);
		}

		Vector3 ToVector3(ref float3 Input)
		{
			return new Vector4 (Input.x, Input.y, Input.z);
		}

		Vector2 ToVector2(ref float2 Input)
		{
			return new Vector2 (Input.x, Input.y);
		}
    }
}
