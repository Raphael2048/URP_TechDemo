using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

using NSwarm;
using AgentInterface;
using UnityEngine.Assertions;


namespace GPUBaking.Editor
{
	

	public partial class DawnImporter
	{
		public bool EncodeTextures(DawnBakingContext Context)
        {
			bool bSucessed = true;
			List<LightmapData> LightmapDataList = new List<LightmapData> ();
			if (Context.BakingMode == DawnBakingMode.BakingSelected)
			{
				bSucessed = EncodeExistingTextures(Context, LightmapDataList);
			}

			bSucessed = bSucessed && EncodeNewTextures(Context,LightmapDataList);

			LightmapSettings.lightmapsMode = Context.Settings.LightmapSettings.DirectionalMode == EDawnDirectionalMode.Directional ? LightmapsMode.CombinedDirectional : LightmapsMode.NonDirectional;
			LightmapSettings.lightmaps = LightmapDataList.ToArray();

			return bSucessed;
        }

		bool EncodeNewTextures(DawnBakingContext Context,List<LightmapData> LightmapDataList)
        {
			bool bSucessed = true;
			int BaseLightmapIndex = 0;
			foreach (var LightingData in Context.SceneLightingData)
			{
				LightingData.BaseLightmapIndex = BaseLightmapIndex;
				var LightmapTextures = LightingData.LightmapTextures;
				for (int LightmapIndex = 0; LightmapIndex < LightmapTextures.Count; ++LightmapIndex)
				{
					var LightmapTexture = LightmapTextures[LightmapIndex];
					var UnityLightmapData = new LightmapData();
					DawnProfiler.BeginSample("DawnImporter.EncodeTextures");
					EncodeTexture(Context, LightmapIndex, LightmapTextures.Count, LightmapTexture, ref UnityLightmapData);
					DawnProfiler.EndSample();
					LightmapDataList.Add(UnityLightmapData);
					LightingData.LightmapDatas.Add(UnityLightmapData);
				}
				BaseLightmapIndex += LightmapTextures.Count;
			}
			return bSucessed;
		}

		bool EncodeExistingTextures(DawnBakingContext Context, List<LightmapData> LightmapDataList)
        {
            bool bSucessed = true;

			LightmapDataList.Clear();
			LightmapDataList.AddRange(LightmapSettings.lightmaps);

			if (LightmapDataList.Count > 0)
			{
				foreach (var LightingData in Context.SceneLightingData)
				{
					var LightmapTextures = LightingData.LightmapTextures;
					for (int LightmapIndex = 0; LightmapIndex < LightmapTextures.Count; ++LightmapIndex)
					{
						var LightmapTexture = LightmapTextures[LightmapIndex];
						for (int Index = 0; Index < LightmapTexture.Allocations.Count; ++Index)
						{
							var Allocation = LightmapTexture.Allocations[Index];
							Allocation.LightmapIndex = LightmapIndex + LightmapDataList.Count;
						}
					}
				}
			}
			return bSucessed;
		}

		void EncodeTexture(DawnBakingContext Context, int LightmapIndex,int LightmapCount,LightMapAllocationTexture LightmapTexture,ref LightmapData UnityLightmapData)
		{
			DawnDebug.LogFormat("EncodeTexture({0}):{1}x{2}",LightmapIndex,LightmapCount,LightmapTexture.Width,LightmapTexture.Height);

			Texture2D LightmapColor = new Texture2D (LightmapTexture.Width, LightmapTexture.Height,TextureFormat.RGBAHalf,false,Context.bLinearColorSpace || Context.Settings.LightmapSettings.bUseHDRLightmap);
			Texture2D LightmapDir = Context.Settings.LightmapSettings.DirectionalMode == EDawnDirectionalMode.Directional ? new Texture2D (LightmapTexture.Width, LightmapTexture.Height, TextureFormat.RGBA32,false) : null;
			Texture2D ShadowMaskColor = LightmapTexture.bHasShadowMask ? new Texture2D (LightmapTexture.Width, LightmapTexture.Height) : null;

			if(LightmapTexture.GroupAsset!=null)
            {
				LightmapColor.name = LightmapTexture.GroupAsset.GroupName ;
				if (ShadowMaskColor != null)
				{
					ShadowMaskColor.name = LightmapTexture.GroupAsset.GroupName;
				}
				if (LightmapDir != null)
				{
					LightmapDir.name = LightmapTexture.GroupAsset.GroupName;
				}
			}

			for (int Index = 0; Index < LightmapTexture.Allocations.Count; ++Index) {
				var Allocation = LightmapTexture.Allocations [Index];

				int4 UVBoundsPixel = Allocation.UVBoundsPixel;
				// uv size in pixel include padding 
				int UVWidthPixel = UVBoundsPixel.z - UVBoundsPixel.x + 2 * Allocation.Padding;
				int UVHeightPixel = UVBoundsPixel.w - UVBoundsPixel.y + 2 * Allocation.Padding;
				
				int Width = UVWidthPixel;
				int Height = UVHeightPixel;
				
				Debug.Assert (Height * Width == Allocation.Texels.Count);
				
				var EncodeOffset = Allocation.EncodeOffset;

				DawnProfiler.BeginSample("EncodeTexture.SetPixels");

				LightmapColor.SetPixels (EncodeOffset.x, EncodeOffset.y, Width, Height, Allocation.Texels.ToArray ());
				
				if(ShadowMaskColor!=null)
				{
					ShadowMaskColor.SetPixels (EncodeOffset.x, EncodeOffset.y, Width, Height, Allocation.ShadowTexels.ToArray ());
				}

				if (LightmapDir != null && Allocation.DirectionalTexels!=null && Allocation.DirectionalTexels.Count > 0)
				{
					LightmapDir.SetPixels (EncodeOffset.x, EncodeOffset.y, Width, Height, Allocation.DirectionalTexels.ToArray ());
				}

				DawnProfiler.EndSample ();

				Allocation.Clear ();
			}
			LightmapColor.Apply ();
			UnityLightmapData.lightmapColor = LightmapColor;

			if(ShadowMaskColor!=null)
			{
				ShadowMaskColor.Apply();
				UnityLightmapData.shadowMask = ShadowMaskColor;
			}
			else{
				UnityLightmapData.shadowMask = null;
			}

			if (LightmapDir!=null)
			{
				LightmapDir.Apply();
				UnityLightmapData.lightmapDir = LightmapDir;
			}
			else{
				UnityLightmapData.lightmapDir = null;
			}
		}

		public Color EncodeLightmap(DawnBakingContext Context,ref float4 Input)
		{
			//return EncodeLightmapRGBM (ref Input,1.0f);
			if (Context.bLinearColorSpace || Context.Settings.LightmapSettings.bUseHDRLightmap)
			{
				return ToColor(ref Input,1.0f);
			}
			return ToColor(ref Input,1.0f).gamma;
		}

		public Color EncodeDirectionLightmap(DawnBakingContext Context, ref float4 Input)
		{
			//return EncodeLightmapRGBM (ref Input,1.0f);
			if (Context.bLinearColorSpace)
			{
				return ToColor(ref Input);
			}
			return ToColor(ref Input);
		}

		Color EncodeLightmapRGBM(ref float4 Input)
		{
			float M = Mathf.Max(Mathf.Max(Input.x, Input.y),Input.z);
			if (M <= 0) {
				M = 1.0f;
			}
			return new Color (Input.x / M, Input.y / M, Input.z / M,M);
		}
    }
}

