using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using NSwarm;
using AgentInterface;

namespace GPUBaking.Editor
{
    public enum ETaskStatus
    {
        SUCESSED = 0,
        IDLE = 1,
        PENDDING = 2,
        IMPORTED = 3,
        PACKED = 4,
        ENCODED = 5,
        COMPLETED = 6,
        FAILURE = -1,
        REJECTED = -2,
    }

    public enum ETaskType
    {
        LIGHTMAP2D = 1,
        SHADOWMASK = 2,
        LIGHTPROBE = 3,
		AUTO_GENERATION_LIGHTPROBE = 4,
		LIGHTMAP2D_GROUP = 11,
		SHADOWMASK_GROUP = 12,
    }
    public abstract class DawnTask
    {
		public FGuid TaskGuid;

		protected ETaskStatus TaskStatus = ETaskStatus.IDLE;

		public abstract bool ApplyResult (DawnBakingContext Context);

		public virtual bool ExportResult (DawnBakingContext Context)
		{
			return true;
		}
		
		public abstract int Cost {
			get;
		}

		public abstract ETaskType Type {
			get;
		}

		public ETaskStatus Status
		{
			get{
				lock (this) {
					return TaskStatus;
				}
			}
			set{
				lock (this) {
					TaskStatus = value;
				}
			}
		}
    }
}