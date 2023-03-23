using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System;
using System.Runtime.InteropServices;
using NSwarm;
using AgentInterface;

namespace GPUBaking.Editor
{
    public partial class DawnLightingSystem
    {
		internal Dictionary<FGuid, DawnTask> PendingTasks = new Dictionary<FGuid, DawnTask>();

		internal List<FGuid> CompletedTasks = new List<FGuid> ();

		internal List<FGuid> ImportedTasks = new List<FGuid> ();

		void ClearTasks()
		{
			PendingTasks.Clear ();
			CompletedTasks.Clear ();
			ImportedTasks.Clear ();
		}

        int AddTasks()
        {
            int ErrorCode = 0;

			foreach (var PendingTask in PendingTasks) {
				FGuid TaskGuid = PendingTask.Key;
				if (PendingTask.Value.Status == ETaskStatus.IDLE) {
					FTaskSpecification NewTaskSpecification = new FTaskSpecification(TaskGuid, PendingTask.Value.Type.ToString(), EJobTaskFlags.TASK_FLAG_USE_DEFAULTS);
					NewTaskSpecification.Cost = (uint)PendingTask.Value.Cost;
					ErrorCode = SwarmInterface.AddTask(NewTaskSpecification);
					DawnDebug.Print("AddTask:{0} Error={1}" ,TaskGuid.D, ErrorCode);
					if (ErrorCode != 0) {
						break;
					}
					PendingTask.Value.Status = ETaskStatus.PENDDING;
				}
			}

            return ErrorCode;
        }

		void ProcessTaskState(ref FTaskState TaskStateMessage)
		{
			DawnDebug.Print("TaskState:{0} With {1}" ,TaskStateMessage.TaskGuid.D,TaskStateMessage.TaskState);
			switch (TaskStateMessage.TaskState)
			{
			case EJobTaskState.TASK_STATE_COMPLETE_SUCCESS:
				ProcessCompletedTask (ref TaskStateMessage.TaskGuid);
				break;
			case EJobTaskState.TASK_STATE_COMPLETE_FAILURE:
				Error = EBakingError.TASK_FAILURE;
				break;
			case EJobTaskState.TASK_STATE_REJECTED:
				Error = EBakingError.TASK_REJECTED;
				break;
			}
		}

		void ProcessCompletedTask(ref FGuid TaskGuid)
		{
			DawnTask PenddingTask = null;
			if (PendingTasks.TryGetValue (TaskGuid, out PenddingTask)) {
				if (PenddingTask.Status == ETaskStatus.PENDDING) {
					PenddingTask.Status = ETaskStatus.COMPLETED;
					lock (CompletedTasks) {
						CompletedTasks.Add (TaskGuid);
					}
					Importer.Notify ();
				} else {
					DawnDebug.LogErrorFormat ("Task {0} Has Processed!!!", TaskGuid.D);
				}
			} else {
				DawnDebug.LogWarningFormat ("Task ({0}) Not Found!!!", TaskGuid.D);
			}
		}

		public int NumOfTask{
			get { return PendingTasks.Count;}
		}

		public int NumOfTaskCompleted{
			get {
				int Result = 0;
				lock (CompletedTasks) {
					Result +=  CompletedTasks.Count;
				}
				return Result ;
			}
		}

		public int NumOfTaskImported{
			get {
				int Result = 0;
				lock (ImportedTasks) {
					Result +=  ImportedTasks.Count;
				}
				return Result ;
			}
		}

		public float BuildingProgress{
			get { return (float)NumOfTaskCompleted / Math.Max(1, NumOfTask);}
		}
    }
}