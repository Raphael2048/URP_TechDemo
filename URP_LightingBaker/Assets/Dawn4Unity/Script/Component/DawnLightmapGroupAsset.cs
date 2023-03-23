using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR

namespace GPUBaking
{
	public enum DawnLightmapPackMode
    {
		Default = 0,
		OriginalUV = 1,
    }

	[CreateAssetMenu(fileName = "DawnLightmapGroupAsset", menuName = "Dawn/Dawn Lightmap Group Asset", order = 0)]
	public class DawnLightmapGroupAsset : ScriptableObject
	{
		public string GroupName;

		public int AtlasSize = -1;

		public DawnLightmapPackMode PackMode = DawnLightmapPackMode.Default;
	}
}

#endif