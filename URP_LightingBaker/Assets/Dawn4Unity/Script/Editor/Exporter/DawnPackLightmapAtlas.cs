//#define DEBUG_LIGHTMAP_PACK

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

using NSwarm;
using AgentInterface;
using UnityEngine.Assertions;
using Debug = UnityEngine.Debug;

namespace GPUBaking.Editor
{
	public partial class DawnExporter
    {
		public bool PackLightmaps(DawnBakingContext Context, int MaxPackedWidth, int MaxPackedHeight)
		{
			DawnDebug.Print("PackLightmaps:{0}x{1}", MaxPackedWidth, MaxPackedHeight);

			bool bSucessed = true;

			List<DawnTask> PendingTasks = new List<DawnTask>();
			
			foreach (var PendingTask in LightingSystem.PendingTasks)
			{
				if (PendingTask.Value is DawnLightmap2DTask)
				{
					PendingTasks.Add(PendingTask.Value);
				}
			}
			
			DawnProfiler.BeginSample("DawnImporter.PackLightmapsByGroup");
			PackLightmapsByGroup(Context, PendingTasks, MaxPackedWidth, MaxPackedHeight);
			DawnProfiler.EndSample();

			// Resize packed lightmap
			{
				foreach (var LightingData in Context.SceneLightingData)
				{
					foreach (var LightmapTexture in LightingData.LightmapTextures)
					{
						LightmapTexture.ResizeToMinTexture();
					}
				}
			}

			return bSucessed;
		}

		protected void PackLightmapsByGroup(DawnBakingContext Context, List<DawnTask> PendingTasks, int MaxPackedWidth, int MaxPackedHeight)
		{
			// sorted task according to Max Side first
			PendingTasks.Sort(delegate (DawnTask x, DawnTask y) {
				var taskX = x as DawnLightmap2DTask;
				var taskY = y as DawnLightmap2DTask;
				int maxSideX = Math.Max(taskX.Allocation.UVWidthPixel, taskX.Allocation.UVHeightPixel);
				int maxSideY = Math.Max(taskY.Allocation.UVWidthPixel, taskY.Allocation.UVHeightPixel);
				if (maxSideX > maxSideY)
				{
					return -1;
				}
				else
				{
					return 1;
				}
			});
			foreach (var PendingTask in PendingTasks)
			{
				DawnLightmap2DTask Lightmap2D = PendingTask as DawnLightmap2DTask;

				if (Lightmap2D != null)
				{
					var LightmapGroup = Lightmap2D.LightmapGroup;

					DawnSceneInfo SceneInfo = Context.GetSceneLightingData(Lightmap2D.scene);
					var LightmapTextures = SceneInfo.LightmapTextures;

					// try to find the LightmapTexture which have enough space
					bool bAddedTexture = false;
					for (int LightmapIndex = 0; LightmapIndex < LightmapTextures.Count; ++LightmapIndex)
					{
						var LightmapTexture = LightmapTextures[LightmapIndex];
						if (LightmapTexture.GroupAsset != LightmapGroup)
						{
							continue;
						}
						if (LightmapTexture.AddTexture(Lightmap2D.bounds, ref Lightmap2D.Allocation, Lightmap2D.bIsLandscapeLightmap))
						{
							Lightmap2D.Allocation.LightmapIndex = LightmapIndex;
							bAddedTexture = true;
							break;
						}
					}

					int GroupAtlasWidth = MaxPackedWidth;
					int GroupAtlasHeight = MaxPackedHeight;

					if (LightmapGroup != null && LightmapGroup.AtlasSize > 0)
					{
						GroupAtlasWidth = GroupAtlasHeight = LightmapGroup.AtlasSize;
					}

					// if all the existed lightmap texture doesn't have enough space, create a new one
					if (!bAddedTexture)
					{
						LightMapAllocationTexture LightmapTexture = new LightMapAllocationTexture(GroupAtlasWidth, GroupAtlasHeight);
						LightmapTexture.GroupAsset = LightmapGroup;
						LightmapTexture.AddTexture(Lightmap2D.bounds, ref Lightmap2D.Allocation, Lightmap2D.bIsLandscapeLightmap);

						LightmapTextures.Add(LightmapTexture);
						Lightmap2D.Allocation.LightmapIndex = LightmapTextures.Count - 1;
					}
				}
			}

		}
	}

	public class LightMapAllocationTexture
	{
		public DawnLightmapGroupAsset GroupAsset;

		public int Width;
		public int Height;

		// Add for support packing multi texture
		private int _maxWidth;
		private int _maxHeight;

		private BinaryRectWrapper _RectWrapper;

		public Bounds AtlasBounds;

		public List<Rect> TextureLayouts;
		public List<LightMapAllocation> Allocations;

		public bool bHasShadowMask = false;

		public LightMapAllocationTexture(int Width, int Height)
		{
			this.Width = Width;
			this.Height = Height;
			this.TextureLayouts = new List<Rect>();
			this.Allocations = new List<LightMapAllocation>();
			this.bHasShadowMask = false;

			// Add for support packing multi texture
			_maxWidth = Width;
			_maxHeight = Height;

			_RectWrapper = new BinaryRectWrapper(_maxWidth, _maxHeight);
		}

		public bool AddTexture(Bounds AllocationBounds, ref LightMapAllocation Allocation, bool bIsLandscapeLightmap)
		{
			if(GroupAsset !=null && GroupAsset.PackMode == DawnLightmapPackMode.OriginalUV)
            {
				return AddTextureWithOriginalUV(ref Allocation, bIsLandscapeLightmap);

			}
			return AddTextureWithPackingMode(AllocationBounds,ref Allocation, bIsLandscapeLightmap);
		}

		internal bool AddTextureWithOriginalUV(ref LightMapAllocation Allocation, bool bIsLandscapeLightmap)
		{
			bool bHasShadowMaskForAllocation = Allocation.Owner.ShadowMaskTask != null;
			bHasShadowMask = bHasShadowMask || bHasShadowMaskForAllocation;

			Allocation.Width = Width;
			Allocation.Height = Height;
			Allocation.Padding = 0;
			Allocation.Owner.CalculateUVBounds(false);
			Allocation.EncodeOffset.x = Allocation.UVBoundsPixel.x;
			Allocation.EncodeOffset.y = Allocation.UVBoundsPixel.y;
			Allocation.lightmapScaleOffset = new Vector4(1.0f, 1.0f, 0.0f, 0.0f);
			Allocations.Add(Allocation);

			Debug.AssertFormat(
				Allocation.UVBoundsPixel.x >= 0 && Allocation.UVBoundsPixel.y >= 0 
				&& Allocation.UVBoundsPixel.z <= Width && Allocation.UVBoundsPixel.w <= Height,
				"uvbound not valid for {0}",Allocation.Owner.Name);

			return true;
		}

		internal bool AddTextureWithPackingMode(Bounds AllocationBounds, ref LightMapAllocation Allocation, bool bIsLandscapeLightmap)
		{
			bool bHasShadowMaskForAllocation = Allocation.Owner.ShadowMaskTask != null;
			if (Allocations.Count > 0 && bHasShadowMask != bHasShadowMaskForAllocation)
			{
				return false;
			}
			int Padding = Allocation.Padding;

			float AllocationWidth = Allocation.Width;
			float AllocationHeight = Allocation.Height;

			int4 Lightmap2DRect = new int4(0, 0, Allocation.UVWidthPixel + 2 * Padding, Allocation.UVHeightPixel + 2 * Padding);

			if (!_RectWrapper.AddRectangle(Lightmap2DRect.z, Lightmap2DRect.w, out Lightmap2DRect.x, out Lightmap2DRect.y))
			{
				return false;
			}	

			// calculate the actual uv(0,0) offset. UVBounds.xy is the minUV with padding, not the uv(0,0)
			float OffsetX = (float)(Lightmap2DRect.x - Allocation.UVBoundsPixel.x + Allocation.Padding) / Width;
			float OffsetY = (float)(Lightmap2DRect.y - Allocation.UVBoundsPixel.y + Allocation.Padding) / Height;

			if (bIsLandscapeLightmap)
			{
				float LandscapeOffsetX = (float)(Lightmap2DRect.x) / Width;
				float LandscapeOffsetY = (float)(Lightmap2DRect.y + Lightmap2DRect.w) / Height;
				Allocation.lightmapScaleOffset = new Vector4(AllocationWidth / Width, -AllocationHeight / Height, LandscapeOffsetX, LandscapeOffsetY);
			}
			else
			{
				Allocation.lightmapScaleOffset = new Vector4((float)AllocationWidth / Width, (float)AllocationHeight / Height, OffsetX, OffsetY);
			}

			Allocation.EncodeOffset = new int2(Lightmap2DRect.x, Lightmap2DRect.y);

			bHasShadowMask = bHasShadowMask || bHasShadowMaskForAllocation;
			Allocations.Add(Allocation);

			return true;
		}

		public bool ResizeToMinTexture()
		{
			if (GroupAsset != null && GroupAsset.PackMode == DawnLightmapPackMode.OriginalUV)
			{
				return ResizeToMinTextureForOriginalUV();
			}
			return ResizeToMinTextureForPackingMode();
		}

		internal bool ResizeToMinTextureForOriginalUV()
        {
			int4 PixelBounds = new int4(0, 0, Width, Height);
			foreach(var Allocation in Allocations)
            {
				PixelBounds.x = Mathf.Min(Allocation.EncodeOffset.x);
				PixelBounds.x = Mathf.Min(Allocation.EncodeOffset.x);
				PixelBounds.w = Mathf.Max(Allocation.UVBoundsPixel.z);
				PixelBounds.z = Mathf.Max(Allocation.UVBoundsPixel.w);
			}
			Debug.AssertFormat(PixelBounds.x >=0 && PixelBounds.y <= 0 && PixelBounds.z <= Width && PixelBounds.w <= Height,
				"Packing For {0} Invalid!",GroupAsset.GroupName);
			return true;
        }

		internal bool ResizeToMinTextureForPackingMode()
		{
			//var MinRect = _BinTree.GetBinTreeValidRect();
			int ValidWidth = 0;
			int ValidHeight = 0;
			_RectWrapper.GetMinValidRect(out ValidWidth, out ValidHeight);
			if (ValidWidth == 0 || ValidHeight == 0)
			{
				return false;
			}

			int OldWidth = Width;
			int OldHeight = Height;

			Width = Mathf.NextPowerOfTwo(ValidWidth);
			Height = Mathf.NextPowerOfTwo(ValidHeight);

			if(OldWidth !=Width || OldHeight!= Height)
            {
				float WidthScale = (float)Width / OldWidth;
				float HeightScale = (float)Height / OldHeight;

				// Recalculate allocation scale offset
				foreach (var Allocation in Allocations)
				{
					float TilingX = Allocation.lightmapScaleOffset.x / WidthScale;
					float TilingY = Allocation.lightmapScaleOffset.y / HeightScale;
					float OffsetX = Allocation.lightmapScaleOffset.z / WidthScale;
					float OffsetY = Allocation.lightmapScaleOffset.w / HeightScale;

					Allocation.lightmapScaleOffset = new Vector4(TilingX, TilingY, OffsetX, OffsetY);
				}
			}

			return true;
		}
	}
}
