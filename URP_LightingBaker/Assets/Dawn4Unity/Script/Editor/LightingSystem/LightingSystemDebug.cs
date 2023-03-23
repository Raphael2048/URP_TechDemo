using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor;

namespace GPUBaking.Editor
{
	public partial class DawnLightingSystem
	{
		public void Export(string ExportDir,DawnSettings BakingSettings)
        {
			bool LastProfilerStatus = DawnProfiler.Enable;
			DawnProfiler.Enable = true;

			DawnProfiler.BeginSample ("ExportDebugInfo");

            PendingTasks.Clear ();
			CompletedTasks.Clear ();
			ImportedTasks.Clear ();

			BakingContext.Reset ();
			BakingContext.Settings = BakingSettings;
            BakingContext.IsEnableExportCache = false;
    
            Exporter = new DawnExporter(this);

			Exporter.GatherScene(BakingContext);
			Exporter.GatherJobs(BakingContext);

            bool bSucessed = true;

            string SceneName = string.Format("{0:X8}{1:X8}{2:X8}{3:X8}",SceneGuid.A,SceneGuid.B,SceneGuid.C,SceneGuid.D);

            string ScenePath = string.Format("{0}/{1}.scene",ExportDir,SceneName);

            string JobPath = string.Format("{0}/{1}.jobs",ExportDir,SceneName);

			DawnDebug.LogFormat ("Exporting Scene To {0}",ScenePath);

			bSucessed = bSucessed && Exporter.ExportScene(BakingContext,ref SceneGuid,ScenePath);

            DawnDebug.LogFormat ("Exporting Jobs To {0}",JobPath);

			bSucessed = bSucessed && Exporter.ExportJobs(BakingContext,ref SceneGuid,JobPath);

			DawnProfiler.EndSample ();

            BakingContext.Print();

			DawnProfiler.Print();

            if (bSucessed) 
            {
				DawnDebug.LogFormat ("Export Successed");
			}
            else
            {
                DawnDebug.LogError ("Export Failure!");
            }

			DawnProfiler.Enable = LastProfilerStatus;
        }

		public static TSerializedArray<FPathVertex> GDebugPathVerticeList;

		bool ShowDebugInfo()
		{
            if (GDebugPathVerticeList != null)
            {
				GDebugPathVerticeList.Clear();
			}			

			if (SwarmInterface == null)
				return false;

			var ChannelName = ImportExportUtil.CreateChannelName(new NSwarm.FGuid(1, 0, 0, 0), ImportExportUtil.LM_SCENE_VERSION, ImportExportUtil.LM_DEBUG_EXTENSION);

			TSerializedArray<FPathVertex> DebugOutput = new TSerializedArray<FPathVertex>();
			if (!ImportExportUtil.SerializeArray(SwarmInterface, ChannelName, DebugOutput, false, false))
			{

				DawnDebug.LogError("Import Debug Output Failure!!!");
				return false;
			}			

			DawnDebug.LogFormat("DebugOutput:{0}", DebugOutput.NumElements);

			GDebugPathVerticeList = DebugOutput;

			return true;
		}
	}
}

