using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR

namespace GPUBaking
{
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    public class DawnLightmapGroupSelector : MonoBehaviour
    {
		public DawnLightmapGroupAsset groupAsset;

		public string GroupName
		{
			get {
				if(groupAsset == null)
				{
					return string.Empty;
				}
				return groupAsset.GroupName;
			}
		}

		public int AtlasSize
		{
			get {
				if(groupAsset == null)
				{
					return -1;
				}
				return groupAsset.AtlasSize;
			}
		}
    }
}
#endif