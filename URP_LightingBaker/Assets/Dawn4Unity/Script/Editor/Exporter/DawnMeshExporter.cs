using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using NSwarm;
using System;

namespace GPUBaking.Editor
{
	public class StaticMeshInfo
	{
		public Mesh Mesh;
		public int LODIndex;

		public StaticMeshInfo(Mesh InMesh,int LODIndex)
		{
			this.Mesh = InMesh;
			this.LODIndex = LODIndex;
		}

		public override bool Equals(object obj)
		{
			if (obj == null || GetType() != obj.GetType())
			{
				return false;
			}
			var OtherMesh = obj as StaticMeshInfo;
			return Mesh == OtherMesh.Mesh && LODIndex == OtherMesh.LODIndex;
		}

		public override int GetHashCode()
		{
			return Mesh.GetHashCode();
		}
	}
	public class MeshGatherInfo
	{
		internal MeshRenderer Renderer;

		internal DawnMeshComponent LightMesh;
		internal DawnProbeGenerationSelector ProbeGeneration;
		internal MeshFilter Filter;
		
		public FGuid MeshGuid;
		public int MeshIndex;
		public int LODIndex;
		public int[] MaterialIndices;
		public List<int> RelevantLights = new List<int>();

		public List<Renderer> OtherLODs = new List<Renderer>();

		public MeshGatherInfo(MeshRenderer Renderer,int LODIndex)
		{
			this.Renderer = Renderer;
			this.Filter = Renderer.GetComponent<MeshFilter>();
			Debug.Assert(Filter != null);
			
			this.LODIndex = LODIndex;
			this.LightMesh = Renderer.GetComponent<DawnMeshComponent>();
			this.ProbeGeneration = Renderer.GetComponentInParent<DawnProbeGenerationSelector>();
		}

		public MeshGatherInfo(MeshFilter Filter, int LODIndex)
		{
			this.Renderer = null;
			this.Filter = Filter;
			Debug.Assert(Filter != null);
			
			this.LODIndex = LODIndex;
			this.LightMesh = Filter.GetComponent<DawnMeshComponent>();
			this.ProbeGeneration = Renderer.GetComponentInParent<DawnProbeGenerationSelector>();
		}

		public string name{
			get { 
				return Renderer != null? Renderer.name : Filter.name;
			}
		}

		public Bounds bounds
		{
			get { 
				return (Renderer != null) ? Renderer.bounds : new Bounds();
			}
		}

		public int lightmapIndex
		{
			set{
				if (Renderer != null)
				{
					Renderer.lightmapIndex = value;	
				}
			}
		}
		public Vector4 lightmapScaleOffset{
			set{
				if (Renderer != null)
				{
					Renderer.lightmapScaleOffset = value;	
				}
			}
		}

		public float CachedSurfaceArea
		{
			get{
				if(this.LightMesh == null)
				{
					this.LightMesh = Filter.gameObject.AddComponent<DawnMeshComponent>();
				}
				return this.LightMesh.GetCachedSurfaceArea(Filter);
			}
		}

		public Vector4 CachedUVBounds
		{
			get
			{
				if(this.LightMesh == null)
				{
					this.LightMesh = Filter.gameObject.AddComponent<DawnMeshComponent>();
				}
				return this.LightMesh.GetUVBounds(Filter);
			}
		}

		public DawnLightmapGroupSelector LightmapGroup
		{
			get{
				return Filter.GetComponentInParent<DawnLightmapGroupSelector> ();
			}
		}
	}

	public partial class DawnExporter
    {
		void GatherMeshInstances(DawnBakingContext Context)
        {
			Dictionary<MeshRenderer,int> AllLODMeshRenderers = new Dictionary<MeshRenderer,int>();			

			var AllLODGroups = GameObject.FindObjectsOfType<LODGroup> ();

			int SharedLODIndex = Context.Settings.MiscSettings.SharedLODIndex;

			bool bSharedLOD0 = SharedLODIndex >= 0;

			MeshGatherInfo SharedMeshInstance = null;

			foreach(var LODGroup in AllLODGroups)
			{
				var LODs = LODGroup.GetLODs();
				for(int LODIndex = 0 ; LODIndex < LODs.Length;++LODIndex)
				{
					foreach(var Renderer in LODs[LODIndex].renderers)
					{
						var MeshRenderer = Renderer as MeshRenderer;
						if(MeshRenderer == null || MeshRenderer.enabled == false)
						{
							continue;
						}

						if(bSharedLOD0 && LODIndex > SharedLODIndex)
                        {
							if(SharedMeshInstance != null)
                            {
								SharedMeshInstance.OtherLODs.Add(Renderer);
								if (!AllLODMeshRenderers.ContainsKey(MeshRenderer))
								{
									AllLODMeshRenderers.Add(MeshRenderer, LODIndex);
								}
							}
							continue;
                        }
						if(GatherMeshInstance(Context,MeshRenderer,LODIndex))
						{
							// sometimes the same LOD file has been referenced by serveral GameObjects.
							// prevent add LOD file repeatly.
							if (!AllLODMeshRenderers.ContainsKey(MeshRenderer))
							{
								//Debug.Log(MeshRenderer.name + ":" + MeshRenderer.GetInstanceID());
								AllLODMeshRenderers.Add(MeshRenderer, LODIndex);
							}

							if (LODIndex == SharedLODIndex)
							{
								SharedMeshInstance = Context.MeshInstances[Context.MeshInstances.Count - 1];
							}
						}
					}
				}
			}
	
			var AllMeshRenderers = GameObject.FindObjectsOfType<MeshRenderer> ();

			foreach (var MeshRenderer in AllMeshRenderers) {
				if(MeshRenderer.enabled == true && !AllLODMeshRenderers.ContainsKey(MeshRenderer))
				{
					GatherMeshInstance(Context, MeshRenderer,0);
				}
			}
        }

		bool GatherMeshInstance(DawnBakingContext Context, MeshFilter Filter, int LODIndex)
		{
			var Mesh = Filter.sharedMesh;

			DawnDebug.AssertFormat(Mesh!=null, "Mesh For {0} is null",Filter.name);

			if(Mesh == null)
			{
				Context.LogErrorFormat("Mesh For {0} is null", Filter.name);
				return false;
			}
			GatherMeshInstance(Context,null,Filter,LODIndex);
			return true;
		}
		
		bool GatherMeshInstance(DawnBakingContext Context,MeshRenderer MeshRenderer,int LODIndex)
		{
			#if UNITY_5 || UNITY_2017 || UNITY_2018
			if (GameObjectUtility.AreStaticEditorFlagsSet(MeshRenderer.gameObject,StaticEditorFlags.LightmapStatic))
			#else
			if(GameObjectUtility.AreStaticEditorFlagsSet(MeshRenderer.gameObject,StaticEditorFlags.ContributeGI))
			#endif
			{
				var MeshFilter = MeshRenderer.GetComponent<MeshFilter> ();

				var Mesh = MeshFilter.sharedMesh;

				DawnDebug.AssertFormat(Mesh!=null, "Mesh For {0} is null",MeshRenderer.name);

				if(Mesh == null)
				{
					return false;
				}
				GatherMeshInstance(Context,MeshRenderer,MeshFilter,LODIndex);
				return true;
			}

			return false;
		}

		void GatherMeshInstance(DawnBakingContext Context, MeshRenderer MeshRenderer,MeshFilter Filter,int LODIndex)
		{
			Mesh Mesh = Filter.sharedMesh;

			if (Mesh == null)
			{
				Context.ThrowException("Missing Mesh For :{0}", MeshRenderer.name);
			}

			MeshGatherInfo MeshInstance = (MeshRenderer != null) ? new MeshGatherInfo (MeshRenderer,LODIndex) : new MeshGatherInfo(Filter, LODIndex);
			MeshInstance.MeshGuid = new FGuid (0, 0, 0, (uint)Filter.GetInstanceID ());

			MeshInstance.MeshIndex = Context.AddMesh (Mesh,LODIndex);

			int MeshElementNum = Mesh.subMeshCount;

			MeshInstance.MaterialIndices = new int[MeshElementNum];

			int MeshElementIndex = 0;

			if (MeshRenderer != null)
			{
				foreach (var Material in MeshRenderer.sharedMaterials) {
					if(Material == null)
                    {
						Context.ThrowException("Missing Material For :{0}", MeshRenderer.name);
                    }
					if (MeshElementIndex < MeshElementNum) {
						MeshInstance.MaterialIndices[MeshElementIndex++] = Context.AddMaterial(Material);
					}
				}

				DawnDebug.AssertFormat (MeshElementNum == MeshRenderer.sharedMaterials.Length,"Export Mesh({0}) Error For Element({1}) , Materials({2})",MeshRenderer.name,MeshElementNum,MeshRenderer.sharedMaterials.Length);

				for(;MeshElementIndex < MeshElementNum;++MeshElementIndex)
				{
					MeshInstance.MaterialIndices[MeshElementIndex] = Context.AddMaterial(MeshRenderer.sharedMaterial);
				}

				for (int LightIndex = 0; LightIndex < Context.Lights.Count; ++LightIndex) {
					var Light = Context.Lights[LightIndex];
					if (Light.type == LightType.Directional) {
						MeshInstance.RelevantLights.Add (LightIndex);
					} else {
						var LightBounds = new Bounds(Light.transform.position,new Vector3(Light.range,Light.range,Light.range) * 2);
						if (MeshRenderer.bounds.Intersects(LightBounds)) {
							MeshInstance.RelevantLights.Add (LightIndex);
						}
					}
				}

				for (int LightIndex = 0; LightIndex < Context.LightMeshes.Count; ++LightIndex) {
					var LightMesh = Context.LightMeshes[LightIndex];
					var LightBounds = new Bounds(LightMesh.transform.position,new Vector3(LightMesh.LightSourceRadius,LightMesh.LightSourceRadius,LightMesh.LightSourceRadius) * 2);
					if (MeshRenderer.bounds.Intersects(LightBounds)) 
					{
						MeshInstance.RelevantLights.Add (Context.Lights.Count + LightIndex);
					}
				}
			}

			Context.AddMeshInstance (MeshInstance);

			if (MeshRenderer != null)
			{
				var Lightmap2D = GatherLightmap2DJob(Context,MeshInstance);
				Context.AddLightmap2D (Lightmap2D);

				var ShadowMask = GatherShadowMaskJob(Context,MeshInstance,Lightmap2D.Allocation);
				Context.AddShadowMask (ShadowMask);
				Lightmap2D.ShadowMaskTask = ShadowMask;	
			}
		}

		bool ExportMeshInstances(DawnBakingContext Context)
		{
			bool bSuccessed = true;
			for(int MeshIndex = 0; MeshIndex < Context.MeshInstances.Count;MeshIndex++)
			{
				FMeshInstanceInfo MeshInstanceInfo = new FMeshInstanceInfo ();
				var MeshInstance = Context.MeshInstances[MeshIndex];
				bSuccessed = bSuccessed && ExportMeshInstance (Context,MeshIndex, ref MeshInstance,ref MeshInstanceInfo);
				if (bSuccessed) {
					SceneInfo.MesheInstances.AddElement (ref MeshInstanceInfo);
					SceneInfo.MeshGuids.AddElement (ref MeshInstanceInfo.HeaderInfo.Guid);
				}
			}
			SceneInfo.HeaderInfo.NumMeshes = (uint)SceneInfo.MesheInstances.NumElements;
			return bSuccessed;
		}

		bool ExportMeshInstance(DawnBakingContext Context, int MeshInstanceIndex, ref MeshGatherInfo MeshInstance,ref FMeshInstanceInfo MeshInstanceInfo)
		{
			DawnDebug.Print("Export MeshInstance:{0}",MeshInstance.name);

			DawnProfiler.BeginSample ("ExportMeshInstance");

			MeshInstanceInfo.HeaderInfo.Guid = ToGuidInfo (MeshInstance.MeshGuid);
			MeshInstanceInfo.HeaderInfo.LevelGuid = new FGuidInfo (0,0,0,0);
			MeshInstanceInfo.HeaderInfo.DiffuseBoost = 1.0f;
			MeshInstanceInfo.HeaderInfo.EmissiveBoost = 1.0f;
			MeshInstanceInfo.HeaderInfo.MeshIndex = (uint)MeshInstance.MeshIndex;
			MeshInstanceInfo.HeaderInfo.TexcoordIndex = 0;
			MeshInstanceInfo.HeaderInfo.Flags = 0;
			MeshInstanceInfo.HeaderInfo.BoundingBox = ToBox (MeshInstance.bounds);
			
			if (MeshInstance.Renderer != null)
			{
				MeshInstanceInfo.HeaderInfo.Flags |= (uint)(MeshInstance.Renderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off ? EMeshInstanceFlags.MESH_INSTANCE_FLAGS_SHADOWCAST : 0);
				MeshInstanceInfo.HeaderInfo.Flags |= (uint)(MeshInstance.Renderer.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.TwoSided ? EMeshInstanceFlags.MESH_INSTANCE_FLAGS_TWOSIDE : 0);
			}

			if (Context.IsGenerationProbe(MeshInstance.ProbeGeneration))
			{
				MeshInstanceInfo.HeaderInfo.Flags |= (uint)EMeshInstanceFlags.MESH_INSTANCE_FLAGS_IS_PROBE_SURFACE;
			}

			var LocalToWorld = MeshInstance.Filter.transform.localToWorldMatrix;
			var WorldToLocal = MeshInstance.Filter.transform.worldToLocalMatrix;
			var LocalToWorldInverseTranspose = LocalToWorld.inverse.transpose;
			
			ToMatrix4x4 (ref LocalToWorld,ref MeshInstanceInfo.HeaderInfo.Transform);
			ToMatrix4x4 (ref WorldToLocal,ref MeshInstanceInfo.HeaderInfo.InverseTransform);
			ToMatrix4x4 (ref LocalToWorldInverseTranspose,ref MeshInstanceInfo.HeaderInfo.InverseTransposeTransform);
			
			foreach (var LightIndex in MeshInstance.RelevantLights) {
				MeshInstanceInfo.RelevantLights.AddElement ((uint)LightIndex);
			}

			int MeshElementNum = MeshInstance.MaterialIndices.Length;

			for (int ElementIndex = 0; ElementIndex < MeshElementNum; ++ElementIndex) {
				MeshInstanceInfo.MaterialOverrides.AddElement ((uint)MeshInstance.MaterialIndices[ElementIndex]);
			}

			DawnProfiler.EndSample ();
			
			return true;
		}

		bool ExportMeshes(DawnBakingContext Context)
        {
			bool bSuccessed = true;
			for(int MeshIndex = 0; MeshIndex < Context.MeshList.Count && bSuccessed;MeshIndex++)
			{
				var MeshData = Context.MeshList [MeshIndex];
				FGuid MeshGuid = GetMeshGuid (MeshData);
				FMeshInfo MeshInfo = new FMeshInfo ();

				if (!Context.IsEnableExportCache || !ImportExportUtil.IsMeshCached (LightingSystem.SwarmInterface, ref MeshGuid, 0)) {
					bSuccessed = ExportMesh (Context,MeshGuid, MeshIndex, MeshData, ref MeshInfo);
					if (bSuccessed && Context.IsEnableExportCache) {
						DawnProfiler.BeginSample ("SerializeMesh");
						bSuccessed = ImportExportUtil.SerializeMesh (LightingSystem.SwarmInterface, ref MeshGuid, ref MeshInfo, 0, true);
						DawnProfiler.EndSample ();
					}
				}
				if(Context.IsEnableExportCache)
				{
					MeshInfo = new FMeshInfo ();
					MeshInfo.HeaderInfo.Guid = ToGuidInfo (MeshGuid);
				}
				if (bSuccessed) {
					SceneInfo.Meshes.AddElement (ref MeshInfo);
				}else{
					DawnDebug.LogErrorFormat ("Export Mesh:{0} Failure!!!", MeshData.Mesh.name);
				}
			}
			return bSuccessed;
        }

		bool ExportMesh(DawnBakingContext Context,FGuid MeshGuid,int MeshIndex,StaticMeshInfo StaticMesh,ref FMeshInfo OutMeshInfo)
		{
			var MeshData = StaticMesh.Mesh;
			DawnDebug.Print("Export Mesh({0}):{1}",MeshIndex,MeshData.name);

			DawnProfiler.BeginSample ("ExportMesh");

			OutMeshInfo.HeaderInfo.Guid = ToGuidInfo(MeshGuid);;
			OutMeshInfo.HeaderInfo.LODIndex = (uint)StaticMesh.LODIndex;
			OutMeshInfo.HeaderInfo.LightmapUVIndex = 1;
			OutMeshInfo.HeaderInfo.LightmapUVDensity = 1.0f;
			OutMeshInfo.HeaderInfo.LightmapTexelDensity = 1.0f;

			int MeshElementNum = MeshData.subMeshCount;
			OutMeshInfo.HeaderInfo.NumElements = (uint)MeshElementNum;
			OutMeshInfo.HeaderInfo.NumVertices = (uint)MeshData.vertexCount;
			OutMeshInfo.HeaderInfo.NumTriangles = (uint)MeshData.triangles.Length / 3;

			FMeshTexcoordBuffer TextureBuffer0 = new FMeshTexcoordBuffer(); 
			FMeshTexcoordBuffer TextureBuffer2 = new FMeshTexcoordBuffer();
			FMeshTexcoordBuffer OriginalUVTextureBuffer2 = new FMeshTexcoordBuffer();

			bool bHasUV0 = MeshData.uv.Length > 0;
			bool bHasUV2 = MeshData.uv2.Length > 0;
			bool bHasNormal = MeshData.normals.Length > 0;
			bool bHasTangent = MeshData.tangents.Length > 0;

			if(!bHasUV2 && !bHasUV0)
            {
				Context.LogErrorFormat("{0} Not Has Lightmap UVs", MeshData.name);
				return false;
            }

			if (!bHasNormal)
			{
				DawnDebug.Print("{0} Not Has Normal", MeshData.name);
				MeshData.RecalculateNormals();
			}

			if (!bHasTangent)
			{
				DawnProfiler.BeginSample("RecalculateTangents");
				MeshData.RecalculateTangents();
				DawnProfiler.EndSample();
			}
			
			DawnProfiler.BeginSample("ExportMesh.FillBuffers");
			var vertices = MeshData.vertices;
			var normals = MeshData.normals;
			var tangents = MeshData.tangents;
			var uvs = bHasUV0 ? MeshData.uv : new Vector2[MeshData.vertexCount];
			var uv2s = bHasUV2 ? MeshData.uv2 : uvs;
			
			Vector4 uvBounds = MeshUtils.CalculateUVBounds(uv2s);

			float uvWidth = uvBounds.z - uvBounds.x;
			float uvHeight = uvBounds.w - uvBounds.y;
			
			for (int VertexIndex = 0; VertexIndex < MeshData.vertexCount; ++VertexIndex) {
				Vector3 Position = vertices [VertexIndex];
				Vector3 Normal = normals [VertexIndex];
				Vector3 Tangent = tangents [VertexIndex];
				Vector2 UV0 = uvs [VertexIndex];
				Vector2 UV2 = new Vector2((uv2s [VertexIndex].x - uvBounds.x)/uvWidth, (uv2s[VertexIndex].y - uvBounds.y) / uvHeight);
				Vector2 OriginalUV2 = uv2s[VertexIndex];

				OutMeshInfo.VertexBuffer.AddElement (ToFloat4 (Position,1.0f));
				OutMeshInfo.TangentBuffer.AddElement (ToFloat4 (Vector3.Cross(Tangent,Normal),0.0f));
				OutMeshInfo.BiTangentBuffer.AddElement (ToFloat4 (Tangent,0.0f));
				OutMeshInfo.NormalBuffer.AddElement (ToFloat4 (Normal,0.0f));

				TextureBuffer0.TexcoordBuffer.AddElement (ToFloat2 (UV0));
				TextureBuffer2.TexcoordBuffer.AddElement (ToFloat2 (UV2));
				OriginalUVTextureBuffer2.TexcoordBuffer.AddElement(ToFloat2(OriginalUV2));
			}
			OutMeshInfo.TexcoordBuffers.AddElement (TextureBuffer0);
			OutMeshInfo.TexcoordBuffers.AddElement (TextureBuffer2);
			OutMeshInfo.TexcoordBuffers.AddElement (OriginalUVTextureBuffer2);
			DawnProfiler.EndSample();

			DawnProfiler.BeginSample("ExportMesh.FillTriangles");
			uint StartIndex = 0;
			for(int ElementIndex = 0; ElementIndex < MeshElementNum;++ElementIndex)
			{
				int[] ElementTriangles = MeshData.GetTriangles(ElementIndex);

				for (int TriangleIndex = 0; TriangleIndex < ElementTriangles.Length / 3; ++TriangleIndex) {
					int V0 = ElementTriangles [TriangleIndex * 3 + 0];
					int V1 = ElementTriangles [TriangleIndex * 3 + 2];
					int V2 = ElementTriangles [TriangleIndex * 3 + 1];

					if (OutMeshInfo.HeaderInfo.NumTriangles < ushort.MaxValue) {
						OutMeshInfo.Index16BitBuffer.AddElement ((ushort)V0);
						OutMeshInfo.Index16BitBuffer.AddElement ((ushort)V1);
						OutMeshInfo.Index16BitBuffer.AddElement ((ushort)V2);
					} else {
						OutMeshInfo.Index32BitBuffer.AddElement ((uint)V0);
						OutMeshInfo.Index32BitBuffer.AddElement ((uint)V1);
						OutMeshInfo.Index32BitBuffer.AddElement ((uint)V2);
					}
				}
				FMeshElementInfo MeshElementInfo = new FMeshElementInfo ();
				MeshElementInfo.PrimitiveCount = (uint)(ElementTriangles.Length / 3);
				MeshElementInfo.StartIndex = StartIndex;
				MeshElementInfo.Flags = 0;
				MeshElementInfo.MaterialIndex = 0xffff;
				OutMeshInfo.ElementInfos.AddElement(ref MeshElementInfo);

				StartIndex += MeshElementInfo.PrimitiveCount;
			}
			DawnProfiler.EndSample();
			DawnProfiler.EndSample ();
			return true;
		}
		
		FGuid GetMeshGuid(StaticMeshInfo MeshData)
		{
			return GUIDUtility.GetMeshGuid(MeshData.Mesh,MeshData.LODIndex);
		}
    }
}
