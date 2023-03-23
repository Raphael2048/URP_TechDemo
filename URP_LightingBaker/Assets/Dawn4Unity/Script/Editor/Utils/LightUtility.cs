using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Diagnostics;

namespace GPUBaking.Editor
{
	public enum EDawnBakingType
	{
		Mixed = 0,
		Baked,
		Realtime,
	};
	public enum EDawnLightingMode
	{
		IndirectOnly = 0,
		Subtractive,
		ShadowMask,		
	}
	public enum EDawnLightingMask
	{
		StaticBaked    = 131074,
		MixedBaked    = 131080,
		None               = 0,
	}

	[Serializable]
	public class LightBakingInfo
	{
		[SerializeField]
		public int LightIndex;

		[SerializeField]
		public int ChannelIndex;

		[SerializeField]
		public EDawnLightingMask LightingMask;

		[SerializeField]
		public EDawnLightingMode BakedMode;

		#if UNITY_EDITOR

		public LightBakingInfo(int InLightIndex)
		{
			LightIndex = InLightIndex;
			ChannelIndex = -1;
			LightingMask = EDawnLightingMask.None;
			BakedMode = EDawnLightingMode.IndirectOnly;
		}

		public LightBakingInfo(int InLightIndex,int InChannelIndex, EDawnLightingMask InLightmapMask, EDawnLightingMode InMixedType)
		{
			this.LightIndex = InLightIndex;
			this.ChannelIndex = InChannelIndex;
			this.LightingMask = InLightmapMask;
			this.BakedMode = InMixedType;
		}

		public LightBakingInfo(Light InLight)
		{
			var SerializeObj = new SerializedObject(InLight);
			var maskChannel = SerializeObj.FindProperty("m_BakingOutput.occlusionMaskChannel");
			var lightmappingMask = SerializeObj.FindProperty("m_BakingOutput.lightmappingMask");
			var mixedLightingMode = SerializeObj.FindProperty("m_BakingOutput.lightmapBakeMode.mixedLightingMode");

			ChannelIndex = maskChannel.intValue;

			if(lightmappingMask!=null)
			{
				LightingMask = (EDawnLightingMask)lightmappingMask.intValue;
			}
			if(mixedLightingMode!=null)
			{
				BakedMode = (EDawnLightingMode)mixedLightingMode.enumValueIndex;
			}
			SerializeObj.Dispose ();
		}

		public void ApplyBakedData(Light InLight)
		{
			var SerializeObj = new SerializedObject(InLight);
			var probeOcclusionLightIndex = SerializeObj.FindProperty("m_BakingOutput.probeOcclusionLightIndex");
			var maskChannel = SerializeObj.FindProperty("m_BakingOutput.occlusionMaskChannel");
			var lightmappingMask = SerializeObj.FindProperty("m_BakingOutput.lightmappingMask");

			var isBaked = SerializeObj.FindProperty("m_BakingOutput.isBaked");
			var lightmapBakeType = SerializeObj.FindProperty("m_BakingOutput.lightmapBakeMode.lightmapBakeType");
			var mixedLightingMode = SerializeObj.FindProperty("m_BakingOutput.lightmapBakeMode.mixedLightingMode");

			maskChannel.intValue = ChannelIndex;

			if (probeOcclusionLightIndex != null)
			{
				probeOcclusionLightIndex.intValue = LightIndex;
			}

			if (lightmappingMask!=null)
			{
				lightmappingMask.intValue = (int)LightingMask;
			}
			if(lightmapBakeType!=null)
			{
				lightmapBakeType.enumValueIndex = (int)GetLightmapBakeType(InLight.lightmapBakeType);
			}
			if(mixedLightingMode!=null)
			{
				mixedLightingMode.enumValueIndex = (int)BakedMode;
			}
			if(isBaked!=null)
			{
				isBaked.boolValue = true;
			}

			SerializeObj.ApplyModifiedPropertiesWithoutUndo();
			SerializeObj.Dispose ();
		}

		static EDawnBakingType GetLightmapBakeType(LightmapBakeType InLightBakeType)
		{
			EDawnBakingType OutLightmapBakeType = EDawnBakingType.Baked;
			switch(InLightBakeType)
			{
			case LightmapBakeType.Baked:
				OutLightmapBakeType = EDawnBakingType.Baked;
				break;
			case LightmapBakeType.Mixed:
				OutLightmapBakeType = EDawnBakingType.Mixed;
				break;
			case LightmapBakeType.Realtime:
				OutLightmapBakeType = EDawnBakingType.Realtime;
				break;
			default:
				break;
			}
			return OutLightmapBakeType;
		}

		internal static EDawnLightingMode GetMixedLightingMode(EDawnBakingMode InBakingMode)
		{
			EDawnLightingMode OutMixedLightingMode = EDawnLightingMode.ShadowMask;
			switch (InBakingMode)
			{
				case EDawnBakingMode.IndirectOnly:
					OutMixedLightingMode = EDawnLightingMode.IndirectOnly;
					break;
				case EDawnBakingMode.ShadowMask:
					OutMixedLightingMode = EDawnLightingMode.ShadowMask;
					break;
				case EDawnBakingMode.Subtractive:
					OutMixedLightingMode = EDawnLightingMode.Subtractive;
					break;
				default:
					break;
			}
			return OutMixedLightingMode;
		}
#endif
	}
}

