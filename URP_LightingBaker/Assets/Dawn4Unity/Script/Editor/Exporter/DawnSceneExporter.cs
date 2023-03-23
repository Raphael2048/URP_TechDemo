using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using NSwarm;
using AgentInterface;

namespace GPUBaking.Editor
{
	public partial class DawnExporter
    {
		public void GatherScene(DawnBakingContext Context)
        {
			DawnProfiler.BeginSample ("GatherLights");

			GatherLights (Context);

			DawnProfiler.EndSample ();

			DawnProfiler.BeginSample ("GatherMeshInstances");

			GatherMeshInstances (Context);

			DawnProfiler.EndSample ();

			DawnProfiler.BeginSample ("GatherLandscapes");

			GatherLandscapes (Context);

			DawnProfiler.EndSample ();

			DawnProfiler.BeginSample("GatherImportantVolumes");

			GatherImportantVolumes(Context);

			DawnProfiler.EndSample();
		}

		public bool ExportScene(DawnBakingContext Context,ref FGuid SceneGuid)
        {
			bool bSuccessed = ExportSceneInternel(Context,ref SceneGuid);

			bSuccessed = bSuccessed && ImportExportUtil.SerializeScene (LightingSystem.SwarmInterface, ref SceneGuid,ref SceneInfo,true);
	
            return bSuccessed;
        }

		public bool ExportScene(DawnBakingContext Context,ref FGuid SceneGuid,string Path)
        {
			bool bSuccessed = ExportSceneInternel(Context,ref SceneGuid);

			State = EExportingState.SCENE_SERIALIZING;

			bSuccessed = bSuccessed && ImportExportUtil.SerializeScene (Path, ref SceneGuid,ref SceneInfo,true);

            return bSuccessed;
        }

		bool ExportSceneInternel(DawnBakingContext Context,ref FGuid SceneGuid)
        {
			State = EExportingState.SCENE_SETTINGS;

			DawnProfiler.BeginSample ("ExportSceneBounds");

			bool bSuccessed = ExportSceneBounds (Context);

			DawnProfiler.EndSample ();

			DawnProfiler.BeginSample("ExportImportantVolumes");

			bSuccessed = bSuccessed && ExportImportantVolumes(Context);

			DawnProfiler.EndSample();

			State = EExportingState.SCENE_LIGHTS;

			DawnProfiler.BeginSample ("ExportLights");

			bSuccessed = bSuccessed && ExportLights (Context);

			DawnProfiler.EndSample ();

			State = EExportingState.SCENE_MESH;

			DawnProfiler.BeginSample ("ExportMeshes");

			bSuccessed = bSuccessed && ExportMeshes (Context);

			DawnProfiler.EndSample ();

			State = EExportingState.SCENE_MATERIAL;

			DawnProfiler.BeginSample ("ExportMaterials");

			bSuccessed = bSuccessed && ExportMaterials (Context);

			DawnProfiler.EndSample ();

			State = EExportingState.SCENE_MESH_INSTANCE;

			DawnProfiler.BeginSample ("ExportMeshInstances");

			bSuccessed = bSuccessed && ExportMeshInstances (Context);

			DawnProfiler.EndSample ();

			State = EExportingState.SCENE_LANDSCAPE;

			DawnProfiler.BeginSample ("ExportLandscapes");

			bSuccessed = bSuccessed && ExportLandscapes (Context);

			DawnProfiler.EndSample ();

            return bSuccessed;
        }

		bool ExportSceneBounds(DawnBakingContext Context)
		{
			SceneInfo.HeaderInfo.Bounds = ToBounds(Context.SceneBounds);
			SceneInfo.HeaderInfo.ImportanceBounds = ToBounds(Context.SceneBounds);
			return true;
		}

		public void ClearSceneData(DawnBakingContext Context)
		{
			SceneInfo = null;
			BakingJobInputs = null;

			Context.Materials.Clear();
			Context.MeshList.Clear();
			Context.MeshInstances.Clear();
			Context.LandscapeList.Clear();
			Context.MeshIndices.Clear();
			Context.MaterialIndices.Clear();

			System.GC.Collect();
		}
    }
}
