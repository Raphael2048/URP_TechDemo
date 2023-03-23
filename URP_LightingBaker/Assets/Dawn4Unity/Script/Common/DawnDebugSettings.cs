using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GPUBaking
{
	[System.Serializable]
	public class RayTracingDebugSettings : ScriptableObject
	{
		public bool bShowDebugRay = false;
		public int DebugMaxBounces = 4;
		public int DebugSamplesPerPixel = 32;
		public bool bDrawPoints = false;
		public bool bDrawAlbedoColor = false;
		public bool bDrawShadowLight = true;
		public bool bDrawLightColor = true;
		public bool bDrawSkyLightRay = false;
	}

	[System.Serializable]
	public class DawnDebugSettings {

		public bool bDebugLightingSystem = false;

		public bool bDebugLightmapTexel = false;

		public RayTracingDebugSettings RayTracingSettings;
		
		public DawnDebugSettings()
		{
			
		}
		public RayTracingDebugSettings GetRayTracingSettings()
        {
			if(RayTracingSettings == null)
            {
				//RayTracingSettings = ScriptableObject.CreateInstance<RayTracingDebugSettings>();
				RayTracingSettings = new RayTracingDebugSettings();
			}			
			return RayTracingSettings;
		}
	}
}