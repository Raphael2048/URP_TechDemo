using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;

using NSwarm;
using AgentInterface;

namespace GPUBaking.Editor
{
	public enum EExportingState
	{
		IDLE,
		SCENE_SETTINGS,
		SCENE_LIGHTS,
		SCENE_MESH,
		SCENE_MATERIAL,
		SCENE_MESH_INSTANCE,
		SCENE_LANDSCAPE,
		SCENE_SERIALIZING,
		JOB_SETTINGS,
		JOB_LIGHTMAP2D,
		JOB_SHADOWMASK,
		JOB_LIGHTPROBE,
		JOB_SERIALIZING,
		EXPORT_DONE
	}

	class DawnExportContext
	{
		public DawnExportContext(DawnBakingContext Context,ref FGuid SceneGuid)
		{
			this.BakingContext = Context;
			this.SceneGuid = SceneGuid;
		}
		public DawnBakingContext BakingContext;
		public FGuid SceneGuid;
	}

	public partial class DawnExporter
    {
        DawnLightingSystem LightingSystem;

		FSceneInfo SceneInfo;

		FBakingJobInputs BakingJobInputs;

		EExportingState State = EExportingState.IDLE;

		float	Progress = 0;

		EBakingError Error = EBakingError.NONE;

        public DawnExporter(DawnLightingSystem LightingSystem)
        {
            this.LightingSystem = LightingSystem;
			this.SceneInfo = new FSceneInfo ();
			this.BakingJobInputs = new FBakingJobInputs ();

			this.State = EExportingState.IDLE;
			this.Progress = 0;
			this.Error = EBakingError.NONE;
        }

		public void StartExport(DawnBakingContext Context,ref FGuid SceneGuid)
		{
			this.State = EExportingState.IDLE;
			this.Progress = 0;
			this.Error = EBakingError.NONE;

			//ThreadPool.QueueUserWorkItem(ExportInBackground,new DawnExportContext(Context,ref SceneGuid));
			ExportInBackground(new DawnExportContext(Context,ref SceneGuid));
		}

		void ExportInBackground(object state)
		{
			DawnExportContext Context = (DawnExportContext)state;
			bool bSucessed = true;

			DawnDebug.LogFormat ("Exporting Scene...");

			bSucessed = bSucessed && ExportScene(Context.BakingContext,ref Context.SceneGuid);

			if (!bSucessed) {
				Error = EBakingError.EXPORT_SCENE_FAILURE;
			}

			if (bSucessed) {
				DawnDebug.LogFormat ("Exporting Scene Successed");
			}

			if(bSucessed)
            {
				DawnDebug.LogFormat("Exporting Jobs...");

				bSucessed = bSucessed && ExportJobs(Context.BakingContext, ref Context.SceneGuid);

				if (!bSucessed)
				{
					Error = EBakingError.EXPORT_JOB_FAILURE;
				}

				if (bSucessed)
				{
					DawnDebug.LogFormat("Exporting Jobs Successed");
				}
			}		

			State = EExportingState.EXPORT_DONE;
		}

		public EExportingState ExportingState
		{
			get{
				return State;
			}
		}

		public float ExportingProgress
		{
			get{
				return Progress;
			}
		}
		public EBakingError ExportError
		{
			get{
				return Error;
			}
		}

		float3 ToFloat3(HDRColor Input)
		{
			return ToFloat3(Input.GetBaseColor()) * Input.GetIntensity();
		}

		float3 ToFloat3(Vector3 Input)
		{
			return new float3 (Input.x, Input.y, Input.z);
		}

		float4 ToFloat4(Vector3 Input,float W)
		{
			return new float4 (Input.x, Input.y, Input.z,W);
		}

		float3 ToFloat3(Color Input)
		{
			return new float3 (Input.r, Input.g, Input.b);
		}

		float4 ToFloat4(Color Input)
		{
			return new float4 (Input.r, Input.g, Input.b,Input.a);
		}

		float4 ToFloat4(Vector4 Input)
		{
			return new float4 (Input.x, Input.y, Input.z,Input.w);
		}

		float2 ToFloat2(Vector2 Input)
		{
			return new float2 (Input.x, Input.y);
		}

		void ToMatrix4x4(ref UnityEngine.Matrix4x4 Input,ref GPUBaking.Editor.Matrix4x4 Output,bool bTransposed = true)
		{
			Output.mm = new float[16];
			for (int Row = 0; Row < 4; ++Row) {
				for (int Colume = 0; Colume < 4; ++Colume) {
					Output.mm [Row * 4 + Colume] = bTransposed ?  Input [Colume,Row] : Input [Row,Colume];
				}
			}
		}

		FSceneBoxBounds ToBox(Bounds Input)
		{
			return new FSceneBoxBounds (ToFloat3(Input.min), ToFloat3(Input.max));
		}

		FSceneBounds ToBounds(Bounds Input)
		{
			float SphereRadius = Input.extents.magnitude;
			return new FSceneBounds (ToFloat3(Input.center), ToFloat3(Input.extents),SphereRadius);
		}

		FGuidInfo ToGuidInfo(FGuid Input)
		{
			return new FGuidInfo (Input.A, Input.B, Input.C, Input.D);
		}

		byte ToByte(bool Input)
		{
			return Input ? (byte)1 : (byte)0;
		}
    }
}
