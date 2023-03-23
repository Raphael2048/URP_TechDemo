using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

using NSwarm;
using UnityEditor;

namespace GPUBaking.Editor
{
	public partial class DawnExporter
	{
		void GatherLightProbes(DawnBakingContext Context)
		{
			bool bPendingCustomTask = true;
			if (Context.Settings.LightProbeSettings.AutoGenerationSetting.AutoGeneration)
			{
				var AutoProbeGenerationTask = new DawnAutoGenerationLightProbeTask();
				LightingSystem.PendingTasks.Add(AutoProbeGenerationTask.TaskGuid, AutoProbeGenerationTask);
				bPendingCustomTask = false;
			}

			bool bUsePrecomputedProbes = false;

			if (Context.bUsePrecomputedProbes && bPendingCustomTask)
            {
				foreach (var SceneLighting in Context.SceneLightingData)
				{
					var lightProbes = GetPrecomputedLightProbes(SceneLighting.scene);

					if (lightProbes != null && lightProbes.count > 0)
					{
						var Task = new DawnLightProbeTask();

						Task.TaskGuid = new FGuid(0, (uint)EJobGuidType.EGUID_B_FOR_LIGHTPROBE, 0, (uint)lightProbes.GetInstanceID());

						foreach (var Position in lightProbes.positions)
						{
							Task.SamplePostions.Add(Position);
						}
						Context.LightProbeList.Add(Task);
						LightingSystem.PendingTasks.Add(Task.TaskGuid, Task);
						bUsePrecomputedProbes = true;
					}
				}
			}

			if(bUsePrecomputedProbes)
            {
				return;
            }

			var lightProbeGroups = Object.FindObjectsOfType<LightProbeGroup>();

			foreach(var lightProbeGroup in lightProbeGroups)
            {
                if (lightProbeGroup.gameObject.name == "DawnAutoLightProbeGroup")
                {
                    continue;
                }
                var Task = new DawnLightProbeTask();

				Task.TaskGuid = new FGuid(0, (uint)EJobGuidType.EGUID_B_FOR_LIGHTPROBE, 0, (uint)lightProbeGroup.GetInstanceID());

				foreach (var Position in lightProbeGroup.probePositions)
				{
					Task.SamplePostions.Add(lightProbeGroup.transform.TransformPoint(Position));
				}
				Context.LightProbeList.Add(Task);
				//if (bPendingCustomTask)
				{
					LightingSystem.PendingTasks.Add(Task.TaskGuid, Task);
				}
			}		
		}

		LightProbes GetPrecomputedLightProbes(UnityEngine.SceneManagement.Scene InScene)
        {
			var LightProbeAssetPath = DawnBakePathSetting.GetInstance(InScene).DawnLightProbeAssetPath();
			var lightingDataAsset = AssetDatabase.LoadAssetAtPath<LightingDataAsset>(LightProbeAssetPath);

			if(lightingDataAsset == null)
            {
				return null;
            }

			PropertyInfo inspectorModeInfo = typeof(SerializedObject).GetProperty("inspectorMode", BindingFlags.NonPublic | BindingFlags.Instance);
			SerializedObject serializedObject = new SerializedObject(lightingDataAsset);
			inspectorModeInfo.SetValue(serializedObject, InspectorMode.Debug, null);

			var LightProbes = serializedObject.FindProperty("m_LightProbes");

			return LightProbes !=null ? LightProbes.objectReferenceValue as LightProbes : null;
		}

		bool ExportLightProbeJobs(DawnBakingContext Context)
		{
			foreach (var LightProbe in Context.LightProbeList) {
				FLightProbeInput LightProbeInput = new FLightProbeInput (); 
				LightProbeInput.JobID = ToGuidInfo(LightProbe.TaskGuid);
				LightProbeInput.LevelID = new FGuidInfo(0,0,0,0);
				foreach(var Postion in LightProbe.SamplePostions)
				{
					LightProbeInput.SamplePositions.AddElement(ToFloat4(Postion));
				}
				BakingJobInputs.LightProbeJobs.AddElement(LightProbeInput);
			}
			return true;
		}
    }
}
