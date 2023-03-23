using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace GPUBaking.Editor
{
	public partial class DawnLightingSystem
	{
		public void CalculateLightmapScales(DawnSettings BakingSettings,bool bForceScaleLightmap,bool bIsBaking)
		{
			

			

			DawnProfiler.BeginSample ("CalculateLightmapScales");

			if (!bIsBaking) {
				BakingContext.Reset ();
				BakingContext.Settings = BakingSettings;
				BakingContext.IsEnableExportCache = false;
				BakingContext.bEnableUniformLightmapScale = true;

				Exporter = new DawnExporter(this);
				Gather ();
			}
			
			// try to update lightmap scale in groups
			// find out all groups
			Dictionary<string, List<DawnLightmap2DTask>> groupDict = new Dictionary<string, List<DawnLightmap2DTask>>(); 
			List<DawnLightmap2DTask> defaultGroup = new List<DawnLightmap2DTask>();
			foreach (var lightmapTask in BakingContext.LightmapList)
			{
				string groupName = lightmapTask.LightmapGroup != null ? lightmapTask.LightmapGroup.GroupName : null;
				if (groupName == null)
				{
					defaultGroup.Add(lightmapTask);
				}
				else
				{
					List<DawnLightmap2DTask> groupTaskList = null;
					if (!groupDict.TryGetValue(groupName, out groupTaskList))
					{
						groupTaskList = new List<DawnLightmap2DTask>();
						groupDict.Add(groupName, groupTaskList);
					}
					groupDict[groupName].Add(lightmapTask);
					
				}
			}
			groupDict.Add("DefaultGroup", defaultGroup);
			DawnDebug.LogFormat ("Found {0} groups. ",groupDict.Count);
			
			foreach (var group in groupDict)
			{
				if (group.Value.Count == 0)
				{
					continue;
				}
				long MaxLightmapTexels = 0;
				long LightmapTexels = 0;
				foreach (var lightmapTask in group.Value)
				{
					LightmapTexels += lightmapTask.Allocation.UVWidthPixel * lightmapTask.Allocation.UVHeightPixel;
					if(lightmapTask.LightmapGroup == null)
                    {
						MaxLightmapTexels = BakingContext.Settings.AtlasSettings.MaxLightmapSize;
                    }
					else if (MaxLightmapTexels == 0)
					{
						MaxLightmapTexels = lightmapTask.LightmapGroup.AtlasSize;
					}
					
					DawnDebug.AssertFormat (LightmapTexels >= 0 ,"Group: {0} LightmapTexels is {1}",group.Key, LightmapTexels);
				}
				MaxLightmapTexels = MaxLightmapTexels * MaxLightmapTexels *
				                    BakingSettings.AtlasSettings.MaxLightmapCount;
				if (MaxLightmapTexels < 0) {
					return;
				}
				double LightmapScaleInverse = LightmapTexels / (double)MaxLightmapTexels;
				if (LightmapTexels > MaxLightmapTexels || bForceScaleLightmap)
				{
					float LightmapScale = (float)(BakingSettings.MiscSettings.LightmapScaleFraction/ LightmapScaleInverse);
					DawnDebug.LogErrorFormat ("Group: {0} Lightmap Scales Updated With {1} !!!",group.Key,LightmapScale);
					RescaleLightmaps (LightmapScale,bIsBaking, group.Value);
				} else {
					DawnDebug.LogErrorFormat ("Group: {0} Lightmap Scales Not Updated With {2}/{3} = {1} !!!",group.Key,LightmapScaleInverse,LightmapTexels,MaxLightmapTexels);
				}
			}
			DawnProfiler.EndSample ();
		}
		private void RescaleLightmaps(float LightmapScale,bool bIsBaking, List<DawnLightmap2DTask> LightmapTasks)
		{
			DawnProfiler.BeginSample ("RescaleLightmaps");

			foreach (var lightmapTask in LightmapTasks) {
				
				if (lightmapTask.renderer == null && !lightmapTask.bIsLandscapeLightmap)
				{
					continue;
				}
				
				SerializedObject SerializeObj = null;
                if (lightmapTask.renderer != null)
                    SerializeObj = new SerializedObject(lightmapTask.renderer);
                else if (lightmapTask.bIsLandscapeLightmap)
                    SerializeObj = new SerializedObject(lightmapTask.gameObject.GetComponent<Terrain>());

				var ScaleField = SerializeObj.FindProperty("m_ScaleInLightmap");

				float ScaleInLightmap = Mathf.Max(1.0f,ScaleField.floatValue);

				ScaleField.floatValue = Mathf.Clamp(ScaleInLightmap * LightmapScale,0.1f,5.0f);

				SerializeObj.ApplyModifiedPropertiesWithoutUndo ();

				SerializeObj.Dispose ();
			}

			if (bIsBaking) {
				foreach (var lightmapTask in LightmapTasks) {
					lightmapTask.Allocation.Width = Mathf.RoundToInt(LightmapScale * lightmapTask.Allocation.Width);
					lightmapTask.Allocation.Height = Mathf.RoundToInt(LightmapScale * lightmapTask.Allocation.Height);
					
					// recalculate uv bounds
					lightmapTask.CalculateUVBounds();
				}
			}

			DawnProfiler.EndSample ();
		}
		private void RescaleLightmaps(float LightmapScale,bool bIsBaking)
		{
			DawnProfiler.BeginSample ("RescaleLightmaps");

			foreach (var MeshInstance in BakingContext.MeshInstances) {
				if (MeshInstance.Renderer == null)
				{
					continue;
				}
				
				var SerializeObj = new SerializedObject(MeshInstance.Renderer);

				var ScaleField = SerializeObj.FindProperty("m_ScaleInLightmap");

				ScaleField.floatValue = Mathf.Clamp(LightmapScale, 0.1f, 5.0f);

				SerializeObj.ApplyModifiedPropertiesWithoutUndo ();

				SerializeObj.Dispose ();
			}

			if (bIsBaking) {
				foreach (var LightmapTask in BakingContext.LightmapList) {
					LightmapTask.Allocation.Width = Mathf.RoundToInt(LightmapScale * LightmapTask.Allocation.Width);
					LightmapTask.Allocation.Height = Mathf.RoundToInt(LightmapScale * LightmapTask.Allocation.Height);
					
					// recalculate uv bounds
					LightmapTask.CalculateUVBounds();
				}
			}

			DawnProfiler.EndSample ();
		}

		

		public void ResetLighmapsScale(DawnSettings BakingSettings, bool bIsBaking)
		{
			DawnProfiler.BeginSample ("ResetLightmapsScale");
			if (!bIsBaking)
			{
				BakingContext.Reset ();
				BakingContext.Settings = BakingSettings;
				BakingContext.IsEnableExportCache = false;

				Exporter = new DawnExporter(this);
				Gather ();
			}
			RescaleLightmaps(1.0f, bIsBaking);

			DawnProfiler.EndSample ();
		}
	}
}