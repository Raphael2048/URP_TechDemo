using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using NSwarm;
using AgentInterface;
using Object = UnityEngine.Object;

namespace GPUBaking.Editor
{
	public class LandscapeGatherInfo
	{
		public FGuid LandscapeGuid;
		public Terrain Landscape;
		public List<int> LandscapeMaterials = new List<int>();
		
		private float SurfaceArea = 0.0f;
		public List<int> RelevantLights = new List<int>();
		
		public string name{
			get { 
				return Landscape.name;
			}
		}

		public Bounds bounds
		{
			get {
				Bounds worldBounds = new Bounds(Landscape.terrainData.bounds.center + Landscape.transform.position, Landscape.terrainData.bounds.size);
				return worldBounds;
			}
		}
		
		public int lightmapIndex
		{
			set{ 
				Landscape.lightmapIndex = value;
			}
		}
		public Vector4 lightmapScaleOffset{
			set{ 
				Landscape.lightmapScaleOffset = value;
			}
		}

		public float CacheLandscapeArea
		{
			get
			{
				if (SurfaceArea < float.Epsilon)
				{
					// Use bound's xz-plane area as LandscapeArea
					SurfaceArea = bounds.size.x * bounds.size.z;
				}

				return SurfaceArea;
			}
		}

		public DawnLightmapGroupSelector LightmapGroup
		{
			get{
				return Landscape.GetComponentInParent<DawnLightmapGroupSelector> ();
			}
		}

		public DawnProbeGenerationSelector ProbeGeneration
		{
			get
			{
				return Landscape.GetComponentInParent<DawnProbeGenerationSelector>();
			}
		}
	}
	
	public partial class DawnExporter
    {
		void GatherLandscapes(DawnBakingContext Context)
        {
			var Landscapes = GameObject.FindObjectsOfType<Terrain> ();

			foreach(var Landscape in Landscapes)
			{
				#if UNITY_5 || UNITY_2017 || UNITY_2018
				if (GameObjectUtility.AreStaticEditorFlagsSet(Landscape.gameObject,StaticEditorFlags.LightmapStatic))
				#else
				if(GameObjectUtility.AreStaticEditorFlagsSet(Landscape.gameObject,StaticEditorFlags.ContributeGI))
				#endif
				{
					GatherLandscape(Context,Landscape);
				}
			}
		}

		void GatherLandscape(DawnBakingContext Context,Terrain Landscape)
		{
			//TODO Gather landscape info
			LandscapeGatherInfo LandscapeInfo = new LandscapeGatherInfo();
			LandscapeInfo.LandscapeGuid = new FGuid(0, 0, 0, (uint) Landscape.GetInstanceID());
			LandscapeInfo.Landscape = Landscape;

			bool bExportMaterial = true;
			if (bExportMaterial)
			{
				// Gen terrain texture
				var terr = Landscape;
				var tdata = terr.terrainData;
				var obj = terr.gameObject;

				var oldMat = terr.materialTemplate;
#if !UNITY_2019_1_OR_NEWER
				var oldMatType = terr.materialType;
#endif
				var oldPos = obj.transform.position;
				var unlitTerrainMat = new Material(Shader.Find("Hidden/ftUnlitTerrain"));
				terr.materialTemplate = unlitTerrainMat;
#if !UNITY_2019_1_OR_NEWER
				terr.materialType = Terrain.MaterialType.Custom;
#endif
				obj.transform.position = new Vector3(-10000, -10000, -10000); // let's hope it's not the worst idea
				var tempCamGO = new GameObject();
				tempCamGO.transform.parent = obj.transform;
				tempCamGO.transform.localPosition =
					new Vector3(tdata.size.x * 0.5f, tdata.size.y + 1, tdata.size.z * 0.5f);
				tempCamGO.transform.eulerAngles = new Vector3(90, 0, 0);
				var tempCam = tempCamGO.AddComponent<Camera>();
				tempCam.orthographic = true;
				tempCam.orthographicSize = Mathf.Max(tdata.size.x, tdata.size.z) * 0.5f;
				tempCam.aspect = Mathf.Max(tdata.size.x, tdata.size.z) / Mathf.Min(tdata.size.x, tdata.size.z);
				tempCam.enabled = false;
				tempCam.clearFlags = CameraClearFlags.SolidColor;
				tempCam.backgroundColor = new Color(0, 0, 0, 0);
				tempCam.targetTexture =
					new RenderTexture(tdata.baseMapResolution, tdata.baseMapResolution, 0, RenderTextureFormat.ARGB32,
						RenderTextureReadWrite.sRGB);
				var tex = new Texture2D(tdata.baseMapResolution, tdata.baseMapResolution, TextureFormat.ARGB32, true,
					false);
				RenderTexture.active = tempCam.targetTexture;
				tempCam.Render();
				terr.materialTemplate = oldMat;
#if !UNITY_2019_1_OR_NEWER
				terr.materialType = oldMatType;
#endif
				obj.transform.position = oldPos;
				RenderTexture.active = tempCam.targetTexture;
				tex.ReadPixels(new Rect(0, 0, tdata.baseMapResolution, tdata.baseMapResolution), 0, 0, true);
				tex.Apply();
				unlitTerrainMat.mainTexture = tex;
				Graphics.SetRenderTarget(null);
				Object.DestroyImmediate(tempCamGO, false);

				LandscapeInfo.LandscapeMaterials.Add(Context.AddMaterial(unlitTerrainMat));
				
				// Can not get material guid unless export mat to external space
				AssetDatabase.CreateAsset(unlitTerrainMat, DawnBakePathSetting.GetInstance().DawnBakeTempLandscapeMatPath(Landscape));
				AssetDatabase.Refresh();
			}

			for (int LightIndex = 0; LightIndex < Context.Lights.Count; ++LightIndex) {
				var Light = Context.Lights[LightIndex];
				if (Light.type == LightType.Directional) {
					LandscapeInfo.RelevantLights.Add (LightIndex);
				} else {
					var LightBounds = new Bounds(Light.transform.position,new Vector3(Light.range,Light.range,Light.range) * 2);
					if (LandscapeInfo.bounds.Intersects(LightBounds)) {
						LandscapeInfo.RelevantLights.Add (LightIndex);
					}
				}
			}
			
			var Lightmap2D = GatherLightmap2DJob(Context, LandscapeInfo);
			Context.AddLightmap2D (Lightmap2D);

			var ShadowMask = GatherShadowMaskJob(Context, LandscapeInfo, Lightmap2D.Allocation);
			Context.AddShadowMask (ShadowMask);
			Lightmap2D.ShadowMaskTask = ShadowMask;
			
			DawnDebug.Print("Landscape Heightmap({0}x{1})",Landscape.terrainData.size.x,Landscape.terrainData.size.y);

			Context.AddLandscape(LandscapeInfo);
		}

		bool ExportLandscapes(DawnBakingContext Context)
        {
			bool bSuccessed = true;
			foreach(var Landscape in Context.LandscapeList)
			{
				bSuccessed = bSuccessed && ExportLandscape(Context,Landscape);
			}
			return bSuccessed;
		}

		bool ExportLandscape(DawnBakingContext Context, LandscapeGatherInfo Landscape)
		{
			FLandscapeInfo LandscapeInfo = new FLandscapeInfo();
			
			// Copy form mesh info's level guid
			LandscapeInfo.HeaderInfo.LevelGuid = new FGuidInfo(0, 0, 0, 0);

			FGuid LandscapeGuid = new FGuid(0, 0, 0, (uint) Landscape.Landscape.GetInstanceID());
			LandscapeInfo.HeaderInfo.Guid = ToGuidInfo(LandscapeGuid);
			
			// Seems constant value ?
			LandscapeInfo.HeaderInfo.ExpandQuadsX = 0;
			LandscapeInfo.HeaderInfo.ExpandQuadsY = 0;
			
			var HeightmapRes = Landscape.Landscape.terrainData.heightmapResolution;
			var LandscapeSize = Landscape.Landscape.terrainData.size;
			var DawnLocalLandscapeSizeX = HeightmapRes - 1;
			var DawnLocalLandscapeSizeY = HeightmapRes - 1;
			var DawnLocalLandscapeSizeZ = 256;
			
			var LandscapeTransform = Landscape.Landscape.transform.localToWorldMatrix;
			var DawnTransform = UnityEngine.Matrix4x4.TRS(
				Vector3.zero, 
				Quaternion.Euler(-90, 0, 0), 
				new Vector3(LandscapeSize.x / DawnLocalLandscapeSizeX,
					LandscapeSize.z / DawnLocalLandscapeSizeY,
					LandscapeSize.y / DawnLocalLandscapeSizeZ));

			DawnTransform =
				UnityEngine.Matrix4x4.TRS(new Vector3(0, 0, LandscapeSize.z), Quaternion.identity, Vector3.one) * DawnTransform;
			
			LandscapeTransform *= DawnTransform;
			ToMatrix4x4(ref LandscapeTransform, ref LandscapeInfo.HeaderInfo.Transform);

			LandscapeInfo.HeaderInfo.BoundingBox = ToBox(Landscape.bounds);

			bool bReverseWinding = LandscapeTransform.determinant < 0.0f;
			LandscapeInfo.HeaderInfo.Flags |= (bReverseWinding) ? (uint) EMeshInstanceFlags.MESH_INSTANCE_FLAGS_REVERSE_WINDING : 0u;
			LandscapeInfo.HeaderInfo.Flags |= (uint) EMeshInstanceFlags.MESH_INSTANCE_FLAGS_LANDSCAPE;
#if UNITY_2019_1_OR_NEWER
			LandscapeInfo.HeaderInfo.Flags |= Landscape.Landscape.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off
				? (uint) EMeshInstanceFlags.MESH_INSTANCE_FLAGS_SHADOWCAST : 0u;
#else
			LandscapeInfo.HeaderInfo.Flags |= Landscape.Landscape.castShadows
				? (uint) EMeshInstanceFlags.MESH_INSTANCE_FLAGS_SHADOWCAST : 0u;
#endif
			if (Context.IsGenerationProbe(Landscape.ProbeGeneration))
			{
				LandscapeInfo.HeaderInfo.Flags |= (uint)EMeshInstanceFlags.MESH_INSTANCE_FLAGS_IS_PROBE_SURFACE;
			}

			bool bTwoSideMaterial = false;
			LandscapeInfo.HeaderInfo.Flags |= bTwoSideMaterial
				? (uint) EMeshInstanceFlags.MESH_INSTANCE_FLAGS_TWOSIDE : 0u;

			// Copy from unreal plugin value
			LandscapeInfo.HeaderInfo.DiffuseBoost = 1.0f;
			LandscapeInfo.HeaderInfo.EmissiveBoost = 1.0f;
			LandscapeInfo.HeaderInfo.LightMapRatio = (float)HeightmapRes / (HeightmapRes - 1);
			
			
			LandscapeInfo.HeaderInfo.ComponentSizeQuads = Landscape.Landscape.terrainData.heightmapResolution - 2 * LandscapeInfo.HeaderInfo.ExpandQuadsX - 1;

			var NumQuads = Landscape.Landscape.terrainData.heightmapResolution - 1;
			// Triangles num is 2 * quad count
			LandscapeInfo.HeaderInfo.NumTriangles = 2 * NumQuads * NumQuads;

			foreach (var Material in Landscape.LandscapeMaterials)
			{
				LandscapeInfo.MaterialOverrides.AddElement((uint)Material);	
			}

			foreach (var LightIndex in Landscape.RelevantLights)
			{
				LandscapeInfo.RelevantLights.AddElement((uint) LightIndex);
			}

			var HeightData = Landscape.Landscape.terrainData.GetHeights(0, 0, HeightmapRes, HeightmapRes);
			var Length = (int)Math.Sqrt(HeightData.Length);

			if (Length * Length != HeightData.Length)
			{
				DawnDebug.LogFormat("[WARN] Get Heightmap data size error, Length is {0}", HeightData.Length);
				return false;
			}
			
			LandscapeInfo.HeightMap.Resize(Length * Length);
			
			for (int x = 0; x < Length; ++x)
			{
				for (int y = 0; y < Length; ++y)
				{
					float NormalX = (float)x / (float)Length;
					float NormalY = (float)y / (float)Length;
					Vector3 Normal = Landscape.Landscape.terrainData.GetInterpolatedNormal(NormalY, NormalX);
					UInt16 Height = (UInt16) (0.5f * HeightData[x, y] * UInt16.MaxValue + 0.5f * UInt16.MaxValue);

					uint ByteX = (uint)((Normal.x + 1) / 2 * 255);
					uint ByteY = (uint)((-Normal.z + 1) / 2 * 255);
					ubyte4 Value = new ubyte4 {z = (byte) (Height >> 8), y = (byte) (Height & 0xff), x = (byte)(ByteX & 0xff), w = (byte)(ByteY & 0xff) };
					LandscapeInfo.HeightMap[(Length - x - 1) * Length + y] = Value;
				}
			}
			
			SceneInfo.Landscapes.AddElement(LandscapeInfo);
			return true;
		}
    }
}
