using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using NSwarm;
using AgentInterface;

namespace GPUBaking.Editor
{
	public partial class DawnExporter
    {
		void GatherImportantVolumes(DawnBakingContext Context)
        {
            Context.ImportantVolumes = Object.FindObjectsOfType<DawnImportantVolume>();
        }


		bool ExportImportantVolumes(DawnBakingContext Context)
        {
            if(Context.ImportantVolumes != null)
            {
                foreach (var Volume in Context.ImportantVolumes)
                {
                    FSceneBoxBounds VolumeBox = ToBox(Volume.VolumeBounds);
                    SceneInfo.ImportanceVolumes.AddElement(VolumeBox);
                }
            }           
            return true;
		}
    }
}
