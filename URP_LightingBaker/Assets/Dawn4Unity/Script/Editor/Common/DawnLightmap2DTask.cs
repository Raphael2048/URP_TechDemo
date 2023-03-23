using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using NSwarm;
using AgentInterface;
using UnityEditor;

namespace GPUBaking.Editor
{
	public class LightMapAllocation
	{
		public DawnLightmap2DTask Owner;

		public int LightmapIndex;
		public Vector4 lightmapScaleOffset;
		public int2 EncodeOffset;

		public int Width;
		public int Height;
		public int Padding;

		/// <summary>
		/// UV bounds Pixel unit, no padding
		/// </summary>
		public int4 UVBoundsPixel;

		/// <summary>
		/// UV width in pixel, include padding
		/// </summary>
		public int UVWidthPixel;
		/// <summary>
		/// UV height in pixel, include padding
		/// </summary>
		public int UVHeightPixel;
		
		public Vector4 UVBounds;

		public List<Color> Texels;
		public List<Color> ShadowTexels;
		public List<Color> DirectionalTexels;

		public LightMapAllocation(DawnLightmap2DTask Owner,int Width,int Height)
		{
			this.Owner = Owner;
			this.Width = Width;
			this.Height = Height;
			this.Padding = 0;
		}

		public void Clear()
		{
			if (Texels != null) {
				Texels.Clear ();
				Texels = null;
			}
			if (ShadowTexels != null) {
				ShadowTexels.Clear ();
				ShadowTexels = null;
			}
			if (DirectionalTexels != null) {
				DirectionalTexels.Clear ();
				DirectionalTexels = null;
			}
		}
	}

	public class DawnLightmap2DTask : DawnTask
	{
		private MeshGatherInfo MeshInstance;
		private LandscapeGatherInfo LandscapeInfo;
		public LightMapAllocation Allocation;
		public readonly string Name;
		public int LightmapIndex = -1;
		public DawnShadowMaskTask ShadowMaskTask;

		public FGuid Guid
		{
			get
			{
				return !bIsLandscapeLightmap ? MeshInstance.MeshGuid : LandscapeInfo.LandscapeGuid;
			}
		}

		public List<int> RelevantLights
		{
			get
			{
				return !bIsLandscapeLightmap ? MeshInstance.RelevantLights : LandscapeInfo.RelevantLights;
			}
		}

		public Bounds bounds
		{
			get
			{
				return !bIsLandscapeLightmap ? MeshInstance.bounds : LandscapeInfo.bounds;
			}
		}

		public bool bIsLandscapeLightmap
		{
			get
			{
				return MeshInstance == null;
			}
		}

		public FGuid UniqueObjID
		{
			get
			{
				return bIsLandscapeLightmap
					? GUIDUtility.GetObjectGuid(LandscapeInfo.Landscape)
					: GUIDUtility.GetObjectGuid(MeshInstance.Filter);
			}
		}

		public Renderer renderer
		{
			get { 
				return !bIsLandscapeLightmap ? MeshInstance.Renderer : null;
			}
		}

		public GameObject gameObject
		{
			get
			{
				if(bIsLandscapeLightmap && LandscapeInfo.Landscape!=null)
                {
					return LandscapeInfo.Landscape.gameObject;

				}
				var meshRenderer = renderer;
				return meshRenderer !=null ? meshRenderer.gameObject : null;
			}
		}

		public string sceneName
        {
			get
			{
				var ownerObject = gameObject;
				return ownerObject != null ? ownerObject.scene.path : null;
			}
        }

		public UnityEngine.SceneManagement.Scene scene
		{
			get
			{
				return gameObject.scene;
			}
		}

		public Vector4 UVBounds
		{
			get
			{
				return bIsLandscapeLightmap ? new Vector4(0,0,1,1) : MeshInstance.CachedUVBounds;
			}
		}

		private void Allocate(int Width,int Height, bool bPadding)
		{
			this.Allocation = new LightMapAllocation (this,Width, Height);
			this.Allocation.Padding = bPadding ? 1 : 0;
			CalculateUVBounds();
		}

		public void CalculateUVBounds(bool bUseConservativeBounds = true)
		{
			if (MeshInstance != null)
			{
				this.Allocation.UVBounds = MeshInstance.CachedUVBounds;
				// need to consider about padding
				int WidthNoPadding = this.Allocation.Width;
				int HeightNoPadding = this.Allocation.Height;

				// min and max 
				int minX = (int)Mathf.RoundToInt(WidthNoPadding * this.Allocation.UVBounds.x);
				int minY = (int)Mathf.RoundToInt(HeightNoPadding * this.Allocation.UVBounds.y);
				int maxX = (int)Mathf.RoundToInt(WidthNoPadding * this.Allocation.UVBounds.z);
				int maxY = (int)Mathf.RoundToInt(HeightNoPadding * this.Allocation.UVBounds.w);

				if(bUseConservativeBounds)
				{
					// min and max 
					minX = (int) Math.Floor(WidthNoPadding * this.Allocation.UVBounds.x);
					minY = (int) Math.Floor(HeightNoPadding * this.Allocation.UVBounds.y);
					maxX = (int) Math.Ceiling(WidthNoPadding * this.Allocation.UVBounds.z);
					maxY = (int) Math.Ceiling(HeightNoPadding * this.Allocation.UVBounds.w);
					if (maxX - minX < 2)
					{
						if (maxX == WidthNoPadding)
						{
							minX--;
						}
						else
						{
							maxX++;
						}
					}

					if (maxY - minY < 2)
					{
						if (maxY == HeightNoPadding)
						{
							minY--;
						}
						else
						{
							maxY++;
						}
					}
				}
				this.Allocation.UVBoundsPixel = new int4(minX, minY, maxX, maxY);
				
				// uv size in pixel include padding 
				this.Allocation.UVWidthPixel = Allocation.UVBoundsPixel.z - Allocation.UVBoundsPixel.x;
				this.Allocation.UVHeightPixel = Allocation.UVBoundsPixel.w - Allocation.UVBoundsPixel.y;
			}
			else
			{
				this.Allocation.UVBoundsPixel = new int4(0, 0, this.Allocation.Width, this.Allocation.Height);
				this.Allocation.UVWidthPixel = this.Allocation.Width;
				this.Allocation.UVHeightPixel = this.Allocation.Height;
				this.Allocation.UVBounds = UVBounds;
			}
		}
		public DawnLightmap2DTask(MeshGatherInfo MeshInstance,int Width,int Height, bool bPadding)
		{
			this.MeshInstance = MeshInstance;
			this.Name = MeshInstance.name;
			Allocate(Width, Height, bPadding);
		}

		public DawnLightmap2DTask(LandscapeGatherInfo LandscapeInfo, int Width, int Height, bool bPadding)
		{
			this.LandscapeInfo = LandscapeInfo;
			this.Name = LandscapeInfo.name;
			Allocate(Width, Height, false);
		}

		public override bool ApplyResult(DawnBakingContext Context)
		{
			if (MeshInstance == null && LandscapeInfo == null)
			{
				return false;
			}
			
			DawnDebug.Print ("ApplyResult For {0} With Lightmap:{1} ,ScaleOffset:{2}", Name, Allocation.LightmapIndex,Allocation.lightmapScaleOffset);
			if (MeshInstance != null && MeshInstance.Renderer != null)
			{
				var SceneLightingData = Context.GetSceneLightingData(MeshInstance.Renderer.gameObject.scene);

				MeshInstance.lightmapIndex = Allocation.LightmapIndex + SceneLightingData.BaseLightmapIndex;
				MeshInstance.lightmapScaleOffset = Allocation.lightmapScaleOffset;

				foreach(var OtherMeshInstance in MeshInstance.OtherLODs)
				{
					OtherMeshInstance.lightmapIndex = Allocation.LightmapIndex + SceneLightingData.BaseLightmapIndex;
					OtherMeshInstance.lightmapScaleOffset = Allocation.lightmapScaleOffset;
				}
			}

			if (LandscapeInfo != null)
			{
				var SceneLightingData = Context.GetSceneLightingData(LandscapeInfo.Landscape.gameObject.scene);

				LandscapeInfo.lightmapIndex = Allocation.LightmapIndex + SceneLightingData.BaseLightmapIndex;
				LandscapeInfo.lightmapScaleOffset = Allocation.lightmapScaleOffset;
			}
			
			return true;
		}

		public override bool ExportResult(DawnBakingContext Context)
		{
			if (MeshInstance != null && MeshInstance.Renderer != null)
			{
				var SceneLightingData = DawnStorage.GetSceneLightingData(MeshInstance.Renderer.gameObject.scene);
				SceneLightingData.AddBakedMeshInfo(MeshInstance.Renderer,Allocation.LightmapIndex , Allocation.lightmapScaleOffset);

				foreach (var OtherMeshInstance in MeshInstance.OtherLODs)
				{
					SceneLightingData.AddBakedMeshInfo(OtherMeshInstance as MeshRenderer,Allocation.LightmapIndex, Allocation.lightmapScaleOffset);
				}
				return true;
			}

			if (LandscapeInfo != null)
			{
				var SceneLightingData = DawnStorage.GetSceneLightingData(LandscapeInfo.Landscape.gameObject.scene);
				SceneLightingData.AddBakedLandscapeInfo(LandscapeInfo.Landscape,Allocation.LightmapIndex, Allocation.lightmapScaleOffset);
				return true;
			}
			
			return false;
		}

		public override int Cost
		{
			get { return Allocation.Width * Allocation.Height;}
		}

		public override ETaskType Type
		{
			get {return ETaskType.LIGHTMAP2D;}
		}

		public DawnLightmapGroupAsset LightmapGroup
        {
            get {
                if (MeshInstance == null && LandscapeInfo == null)
                {
					return null;
                }

                DawnLightmapGroupSelector dawnLightmapGroupSelector;
                if (MeshInstance != null)
                {
					dawnLightmapGroupSelector = MeshInstance.LightmapGroup;
                }
                else
                {
					dawnLightmapGroupSelector = LandscapeInfo.LightmapGroup;
                }

				return dawnLightmapGroupSelector!=null ? dawnLightmapGroupSelector.groupAsset : null;
            }
        }


        public DawnLightmapBakingParameters LightMapBakingParameters
        {
            get {
                DawnLightmapBakingParameters dawnLightmapBakingParameters = null;

                if (MeshInstance == null && LandscapeInfo == null)
                {
                    return dawnLightmapBakingParameters;
                }

                if (MeshInstance != null)
                {
                    dawnLightmapBakingParameters = MeshInstance.Filter.GetComponentInParent<DawnLightmapBakingParameters>();
                }
                else
                {
                    dawnLightmapBakingParameters = LandscapeInfo.Landscape.GetComponent<DawnLightmapBakingParameters>();
                }

                return dawnLightmapBakingParameters;
            }
        }
	}
}