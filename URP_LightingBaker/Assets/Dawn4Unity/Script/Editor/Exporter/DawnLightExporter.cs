using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

using NSwarm;
using AgentInterface;

namespace GPUBaking.Editor
{
	public partial class DawnExporter
    {
		void GatherLights(DawnBakingContext Context)
        {
			DynamicGI.UpdateEnvironment ();

			// gather all unity lights, add dawn light components automatically
			var AllUnityLights = GameObject.FindObjectsOfType<Light>();
			foreach (var Light in AllUnityLights)
			{
				if (Light.GetComponent<DawnBaseLight>() != null)
				{
					continue;
				}
				else
				{
					switch (Light.type)
					{
						case LightType.Directional:
							Light.gameObject.AddComponent<DawnDirectionalLight>();
							break;
						case LightType.Point:
							Light.gameObject.AddComponent<DawnPointLight>();
							break;
						case LightType.Rectangle:
							Light.gameObject.AddComponent<DawnRectLight>();
							break;
						case LightType.Spot:
							Light.gameObject.AddComponent<DawnSpotLight>();
							break;
						default:
							DawnDebug.LogWarningFormat("Add Dawn Light Component For {0} Not Supported!!!", Light);
							break;
					}
					var baseLight = Light.GetComponent<DawnBaseLight>();
					if (baseLight != null)
					{
						baseLight.UnityLight = Light;
						baseLight.UpdateWithUnity = true;
					}

				}
			}

			// gather all dawn lights
			var AllDawnLights = GameObject.FindObjectsOfType<DawnBaseLight>();			
            foreach (var Light in AllDawnLights)
            {
                if (Light.enabled == false)
                {
                    continue;
                }
                if (Light.lightmapBakeType == LightmapBakeType.Baked || Light.lightmapBakeType == LightmapBakeType.Mixed)
                {
                    Context.AddLight(Light);
                }
            }

			// gather all lightmeshes
            var AllLightMeshes = GameObject.FindObjectsOfType<DawnLightMesh>();
			foreach (var LightMesh in AllLightMeshes)
			{
				if (LightMesh.enabled == false)
				{
					continue;
				}
				var Renderer = LightMesh.GetComponent<MeshRenderer>();
				if(Renderer!=null)
                {
					Renderer.shadowCastingMode = ShadowCastingMode.Off;
				}
				Context.AddLight (LightMesh);
			}
        }

		bool ExportLights(DawnBakingContext Context)
		{
			foreach (var Light in Context.Lights) {
				bool bValidLight = false;
				FLightFullInfo LightInfo = new FLightFullInfo ();
				switch (Light.type) {
				case LightType.Directional:
					{
						ExportDirectionalLight (Context,Light,ref LightInfo);
						bValidLight = true;
					}
					break;
				case LightType.Point:
					{
						ExportPointLight (Context,Light,ref LightInfo);
						bValidLight = true;
					}
					break;
				case LightType.Spot:
					{
						ExportSpotLight (Context,Light,ref LightInfo);
						bValidLight = true;
					}
					break;
				case LightType.Area:
					{
						ExportRectAreaLight (Context,Light,ref LightInfo);
						bValidLight = true;
					}
					break;
				default:
					DawnDebug.LogWarningFormat ("Export Light For {0} Not Supported!!!", Light);
					break;
				}
				if (bValidLight) {
					Context.SetLightGuid(Light,ref LightInfo.LightGuid);
					SceneInfo.Lights.AddElement (ref LightInfo.LightData);
					SceneInfo.LightGuids.AddElement (ref LightInfo.LightGuid);
					SceneInfo.LightFlags.AddElement (ref LightInfo.LightFlags);
					SceneInfo.LightPowers.AddElement (ref LightInfo.LightPower);
				}
			}

			var SkyLight = Object.FindObjectOfType<DawnSkyLight> ();
			if (SkyLight != null) {
				ExportSkyLight (Context, SkyLight,ref SceneInfo.SkyLight);
			} else {
				ExportSkyLight (Context, ref SceneInfo.SkyLight);
			}

			ExportLightMeshes (Context);

			SceneInfo.HeaderInfo.NumLights = (uint)SceneInfo.Lights.NumElements;
			return true;
		}

		void ExportLightCommon(DawnBakingContext Context,DawnBaseLight DawnLight, ref FLightFullInfo OutLightInfo)
		{
			var LightColor = GetLightColor(Context, DawnLight);
			var IndirectColor = GetIndirectColor(Context, DawnLight);
	
			OutLightInfo.LightData.Attenuation = LightColor.GetIntensity();
			OutLightInfo.LightData.Color = ToFloat3(LightColor);
			OutLightInfo.LightData.IndirectColor = ToFloat3(IndirectColor);
			OutLightInfo.LightData.Position = ToFloat3(DawnLight.transform.position);
			OutLightInfo.LightData.Normal = ToFloat3(DawnLight.transform.forward);
			OutLightInfo.LightData.Tangent = ToFloat3(DawnLight.transform.up);

			OutLightInfo.LightFlags = (uint)GPUBakingConst.LIGHT_FLAG_NONE;

			DawnPointLight PointLight = DawnLight as DawnPointLight;
			if (DawnLight.type == LightType.Area) {
				OutLightInfo.LightFlags |= (uint)GPUBakingConst.LIGHT_FLAG_INVERSE_SQUARED;
			}
			if ((DawnLight.type == LightType.Point || DawnLight.type == LightType.Spot )&& PointLight!=null) 
			{
				if (PointLight.falloffMode == DawnPointLight.FalloffMode.InverseSquaredFalloff)
					OutLightInfo.LightFlags |= (uint)GPUBakingConst.LIGHT_FLAG_INVERSE_SQUARED;
				if (PointLight.falloffMode == DawnPointLight.FalloffMode.BakeryFalloff)
					OutLightInfo.LightFlags |= (uint)GPUBakingConst.LIGHT_FLAG_BAKERY_FALLOFF;
				if (PointLight.falloffMode == DawnPointLight.FalloffMode.UnityFalloff)
					OutLightInfo.LightFlags |= (uint)GPUBakingConst.LIGHT_FLAG_UNITY_FALLOFF;
			}
#if UNITY_2020_1_OR_NEWER
			else if ((DawnLight.type == LightType.Point || DawnLight.type == LightType.Spot) && PointLight == null)
			{
				OutLightInfo.LightFlags |= (uint)GPUBakingConst.LIGHT_FLAG_INVERSE_SQUARED;
			}
#endif
			if (DawnLight.shadows != LightShadows.None) {
				OutLightInfo.LightFlags |= (uint)GPUBakingConst.LIGHT_FLAG_CAST_DIRECT_SHADOW;
			}
			if (DawnLight.lightmapBakeType == LightmapBakeType.Baked || Context.Settings.BakingMode == EDawnBakingMode.Subtractive) {
				OutLightInfo.LightFlags |= (uint)GPUBakingConst.LIGHT_FLAG_DIRECT_LIGHTING;
			}else if (DawnLight.lightmapBakeType == LightmapBakeType.Mixed && Context.Settings.BakingMode == EDawnBakingMode.ShadowMask) {
				OutLightInfo.LightFlags |= (uint)GPUBakingConst.LIGHT_FLAG_DISTANCE_FIELD_SHADOW;
			}

			OutLightInfo.LightGuid.A = 0;
			OutLightInfo.LightGuid.B = 0;
			OutLightInfo.LightGuid.C = 0;
			OutLightInfo.LightGuid.D = (uint)DawnLight.GetInstanceID();
			OutLightInfo.LightPower = GetLightPower(ref IndirectColor);
		}

		void ExportDirectionalLight(DawnBakingContext Context,DawnBaseLight InLight, ref FLightFullInfo OutLightInfo)
		{
			DawnDebug.Print ("ExportDirectionalLight:{0}", InLight.name);

			DawnDirectionalLight DawnLight = InLight as DawnDirectionalLight;

			float SceneRadius = Context.SceneRadius;
			float LightSourceAngle = DawnLight != null ? DawnLight.LightSourceAngle : 1.0f;
			float ShadowSpread = DawnLight != null ? DawnLight.ShadowSpread : 0.01f;

			ExportLightCommon (Context,DawnLight,ref OutLightInfo);

			OutLightInfo.LightData.Type = (uint)FLightType.LIGHT_DIRECTIONAL;
			OutLightInfo.LightData.Normal = OutLightInfo.LightData.Normal * -1;
			OutLightInfo.LightData.Dimensions = new float4 (0,0,SceneRadius * 2 * Mathf.Tan(Mathf.Deg2Rad * LightSourceAngle),ShadowSpread);
		}

		void ExportPointLight(DawnBakingContext Context, DawnBaseLight InLight, ref FLightFullInfo OutLightInfo)
		{
			DawnDebug.Print ("ExportPointLight:{0}", InLight.name);

			DawnPointLight DawnLight = InLight as DawnPointLight;

			ExportLightCommon (Context,DawnLight,ref OutLightInfo);

			float LightFalloffExponent = DawnLight != null ? DawnLight.lightFalloffExponent : 2.0f;
			float SourceRadius = DawnLight != null ? DawnLight.sourceRadius : 0.0f;
			float SourceLength = DawnLight != null ? DawnLight.sourceLength : 0.0f;

			OutLightInfo.LightData.Type = (uint)FLightType.LIGHT_POINT;
			OutLightInfo.LightData.Dimensions.z = Mathf.Max(0.0f, SourceRadius);
			OutLightInfo.LightData.Dimensions.w = SourceLength;
			OutLightInfo.LightData.Attenuation = InLight.range;
			OutLightInfo.LightData.dPdu.x = LightFalloffExponent;
			OutLightInfo.LightData.dPdu.y = 1.0f;
		}

		void ExportSpotLight(DawnBakingContext Context, DawnBaseLight InLight, ref FLightFullInfo OutLightInfo)
		{
			DawnDebug.Print ("ExportSpotLight:{0}", InLight.name);

			DawnSpotLight DawnLight = InLight as DawnSpotLight;

			ExportLightCommon (Context,DawnLight,ref OutLightInfo);

			float LightFalloffExponent = DawnLight != null ? DawnLight.lightFalloffExponent : 8.0f;
			float SourceRadius = DawnLight != null ? DawnLight.sourceRadius : 0.0f;
			float SourceLength = DawnLight != null ? DawnLight.sourceLength : 0.0f;
			float InnerConeAngle = DawnLight != null ? DawnLight.innerConeAngle : 0.0f;

			//float testCosOUterCone = Mathf.Cos(Mathf.Deg2Rad * DawnLight.UnityLight.spotAngle * 0.5f);
			float CosOuterCone = Mathf.Cos (Mathf.Deg2Rad * DawnLight.innerConeAngle * 0.5f);
			float InvCosConeDifference = 1.0f / (Mathf.Cos (Mathf.Deg2Rad * 0) - CosOuterCone);

			OutLightInfo.LightData.Type = (uint)FLightType.LIGHT_SPOT;
			OutLightInfo.LightData.Dimensions.x = CosOuterCone;
			OutLightInfo.LightData.Dimensions.y = InvCosConeDifference;
			OutLightInfo.LightData.Dimensions.z = Mathf.Max(0.01f, SourceRadius);
			OutLightInfo.LightData.Dimensions.w = SourceLength;
			OutLightInfo.LightData.Attenuation = InLight.range;
			OutLightInfo.LightData.dPdu.x = LightFalloffExponent;
		}

		void ExportRectAreaLight(DawnBakingContext Context, DawnBaseLight InLight, ref FLightFullInfo OutLightInfo)
		{
			DawnDebug.Print ("ExportRectAreaLight:{0}",InLight.name);

			DawnRectLight DawnLight = InLight as DawnRectLight;

			ExportLightCommon (Context,DawnLight,ref OutLightInfo);

			Vector3 Tangent = InLight.transform.up;
			Vector3 Direction = InLight.transform.forward;

			float BarnDoorAngle = DawnLight !=null ? DawnLight.barnDoorAngle : 88.0f;
			float BarnDoorLength = DawnLight !=null ? DawnLight.barnDoorLength : 20.0f;
			float SourceWidth = DawnLight.Width;
			float SourceHeight = DawnLight.Height;

			OutLightInfo.LightData.Type = (uint)FLightType.LIGHT_RECT;
			OutLightInfo.LightData.dPdu = ToFloat3(Vector3.Cross(Direction,Tangent));
			OutLightInfo.LightData.dPdv = ToFloat3(Tangent);
			OutLightInfo.LightData.RectLightBarnCosAngle = Mathf.Cos(BarnDoorAngle * Mathf.Deg2Rad);
			OutLightInfo.LightData.RectLightBarnLength = BarnDoorLength;
			
			OutLightInfo.LightData.Attenuation = DawnLight!=null && DawnLight.AttenuationRadius > 0 ? DawnLight.AttenuationRadius:InLight.range;
			OutLightInfo.LightData.Dimensions = new float4(SourceWidth, SourceHeight, SourceWidth * 0.5f,0.0f);
		}

		void ExportSkyLight(DawnBakingContext Context,DawnSkyLight InLight, ref FSkyLightInfo OutLightInfo)
		{
			DawnDebug.Print ("ExportSkyLight:{0}",InLight.name);

			OutLightInfo.SkyData.Color = ToFloat4(InLight.color) * InLight.intensity;
			OutLightInfo.SkyData.Color.w = InLight.indirectMultiplier * Context.Settings.MiscSettings.IndirectIntensity;

			if (InLight.skyTexture != null) {
				Debug.AssertFormat (InLight.skyTexture.width == InLight.skyTexture.height,"skyTexture:{0}x{1}",InLight.skyTexture.width , InLight.skyTexture.height);
				var cubemap = TextureUtils.CopyCubemap(InLight.skyTexture);
				ExportSkyCubeMap (cubemap, ref OutLightInfo);				
				OutLightInfo.SkyData.EnvColor = ToFloat4(Color.black);
			} else {
				OutLightInfo.SkyData.EnvColor = ToFloat4(InLight.color) * InLight.intensity;
			}
		}

		void ExportSkyLight(DawnBakingContext Context, ref FSkyLightInfo OutLightInfo)
		{
			//var SkySH = RenderSettings.ambientProbe;
			DawnDebug.Print("ExportSkyLight " + RenderSettings.ambientMode);

			// 1. get the Evironment light mode
			// Skybox
			if (RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Skybox && RenderSettings.skybox != null)
			{
				OutLightInfo.SkyData.Color = ToFloat4(RenderSettings.ambientSkyColor) * (RenderSettings.ambientIntensity);
				OutLightInfo.SkyData.Color.w = Context.Settings.MiscSettings.IndirectIntensity;
				OutLightInfo.SkyData.EnvColor = ToFloat4(Color.black);
			}
			// Gradient
			else if (RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Trilight)
			{
				OutLightInfo.SkyData.Color = ToFloat4(Color.white);
				OutLightInfo.SkyData.Color.w = Context.Settings.MiscSettings.IndirectIntensity;
				OutLightInfo.SkyData.EnvColor = ToFloat4(Color.black);
				ExportSkyCubeMap(RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor, RenderSettings.ambientGroundColor, ref OutLightInfo);
			}
			// Color
			else if (RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Flat || RenderSettings.skybox == null)
			{
				OutLightInfo.SkyData.EnvColor = ToFloat4(RenderSettings.ambientLight.linear * Context.Settings.MiscSettings.IndirectIntensity);
				OutLightInfo.SkyData.EnvColor *= (1 / Mathf.PI);
				OutLightInfo.SkyData.Color = ToFloat4(Color.black);
				OutLightInfo.SkyData.Color.w = Context.Settings.MiscSettings.IndirectIntensity;
			}

			if (RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Skybox && RenderSettings.skybox !=null )
			{
				// 2. Generate the skylight cubemap
				Cubemap cubemap = new Cubemap(RenderSettings.defaultReflectionResolution, TextureFormat.RGBAFloat, false);

				// Create a temporary camera to render a skybox cubemap
				var lastActiveCamera = SceneView.lastActiveSceneView.camera;
				GameObject go = new GameObject("DawnCubemapCamera");
				var renderCamera = go.AddComponent<Camera>();
				var reflectionProbe = go.AddComponent<ReflectionProbe>();
				go.transform.position = lastActiveCamera.gameObject.transform.position;

				// ignore ohter objects
				reflectionProbe.cullingMask = 0;
				renderCamera.cullingMask = 0;

				var TexturePath = DawnBakePathSetting.GetInstance().DawnReflectionProbePath(0,true);
				if (UnityEditor.Lightmapping.BakeReflectionProbe(reflectionProbe, TexturePath))
                {
					cubemap = AssetDatabase.LoadAssetAtPath<Cubemap>(TexturePath);
				}
                else
                {
					renderCamera.RenderToCubemap(cubemap);
				}

				MonoBehaviour.DestroyImmediate(go);

				ExportSkyCubeMap(cubemap, ref OutLightInfo);

				if(RenderSettings.defaultReflectionMode == DefaultReflectionMode.Skybox)
                {
					Context.SkyReflectionCubemap = cubemap;
				}
                else
                {
					AssetDatabase.DeleteAsset(TexturePath);
				}
			}
		}

		void ExportSkyCubeMap(Color SkyColor,Color EquatorColor,Color GroundColor, ref FSkyLightInfo OutLightInfo)
        {
			int CubeMapSize = RenderSettings.defaultReflectionResolution;
			OutLightInfo.SkyData.MipDimensions = new int4(1, CubeMapSize, 0, 0);
			List<Color> faceSeq = new List<Color> { EquatorColor, EquatorColor, SkyColor , GroundColor, EquatorColor, EquatorColor };
			
			foreach (var face in faceSeq)
			{
				var pixel = ExtractHDRColor(face);
				for (int h = 0; h < CubeMapSize; h++)
                {
					for (int w = 0; w < CubeMapSize; w++)
					{
						float InvPI = 1 / Mathf.PI;
						OutLightInfo.EnvCube.AddElement(ToFloat4(pixel) * InvPI);
					}
				}
			}
		}

		void ExportSkyCubeMap(Cubemap cubemap, ref FSkyLightInfo OutLightInfo)
		{
			bool bConvertLinear = PlayerSettings.colorSpace == ColorSpace.Gamma;
			int CubeMapSize = cubemap.width;
			int NumMips = Mathf.CeilToInt(Mathf.Log(CubeMapSize)/Mathf.Log(2)) + 1;

			DawnDebug.Print("SkyLight NumMips:{0},CubeMapSize:{1}",NumMips,CubeMapSize);

			OutLightInfo.SkyData.MipDimensions = new int4 (NumMips, CubeMapSize, 0, 0);
			//OutLightInfo.EnvCube.Resize (CubeMapSize * 6);

			List<CubemapFace> faceSeq = new List<CubemapFace>{CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY, CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ };
			foreach (CubemapFace face in faceSeq)
			{
				for(int h = 0; h < cubemap.height; h++)
					for(int w = 0; w < cubemap.width; w++)
					{
						Color pixel = cubemap.GetPixel(face, w, h);
						float InvPI = 1 / Mathf.PI;
						OutLightInfo.EnvCube.AddElement(ToFloat4(bConvertLinear ? pixel.linear : pixel) * InvPI);
					}			
			}
		}

		void ExportLightMeshes(DawnBakingContext Context)
		{
			foreach (var LightMesh in Context.LightMeshes)
			{
				FLightFullInfo LightInfo = new FLightFullInfo();
				FLightMeshInfo LightMeshInfo = new FLightMeshInfo();
				if (ExportLightMesh (Context,LightMesh,ref LightMeshInfo,ref LightInfo)) {
					//use to find mesh light in baker
					LightInfo.LightData.Dimensions.x = SceneInfo.LightMeshes.NumElements;
					SceneInfo.Lights.AddElement (LightInfo.LightData);
					SceneInfo.LightGuids.AddElement (LightInfo.LightGuid);
					SceneInfo.LightFlags.AddElement (LightInfo.LightFlags);
					SceneInfo.LightPowers.AddElement (LightInfo.LightPower);
					SceneInfo.LightMeshes.AddElement (LightMeshInfo);
				}
			}
		}

		bool ExportLightMesh(DawnBakingContext Context,DawnLightMesh LightMesh,ref FLightMeshInfo OutLightMeshInfo,ref FLightFullInfo OutLightInfo)
		{
			var Filter = LightMesh.GetComponent<MeshFilter> ();
			var Mesh = Filter.sharedMesh;
			if (Mesh == null) {
				return false;
			}

			float SurfaceArea = ExportLightMeshSurfels (Context,LightMesh,Mesh,ref OutLightMeshInfo);

			Color LightColor = LightMesh.EmissiveColor;
			Color IndirectColor = LightMesh.EmissiveColor;
			float Brightness = LightMesh.EmissiveIntensity;
			float IndirectLightingIntensity = LightMesh.IndirectIntensity;

			OutLightInfo.LightData.Type = (uint)FLightType.LIGHT_MESH;
			OutLightInfo.LightData.Dimensions.z = SurfaceArea;
			OutLightInfo.LightData.Attenuation = LightMesh.LightSourceRadius;
			OutLightInfo.LightData.Color = ToFloat3(LightColor) * Brightness;
			OutLightInfo.LightData.IndirectColor = ToFloat3(IndirectColor) * IndirectLightingIntensity * Brightness;
			OutLightInfo.LightData.Position = ToFloat3(LightMesh.transform.position);
			OutLightInfo.LightData.Normal = ToFloat3(LightMesh.transform.forward);
			OutLightInfo.LightData.Tangent = ToFloat3(LightMesh.transform.up);

			OutLightInfo.LightFlags = (uint)GPUBakingConst.LIGHT_FLAG_NONE;

			if (LightMesh.bUseInverseSquaredFalloff) {
				OutLightInfo.LightFlags |= (uint)GPUBakingConst.LIGHT_FLAG_INVERSE_SQUARED;
			}

			OutLightInfo.LightFlags |= (uint)GPUBakingConst.LIGHT_FLAG_CAST_DIRECT_SHADOW;
			OutLightInfo.LightFlags |= (uint)GPUBakingConst.LIGHT_FLAG_DIRECT_LIGHTING;

			OutLightInfo.LightGuid.A = 0;
			OutLightInfo.LightGuid.B = 0;
			OutLightInfo.LightGuid.C = 0;
			OutLightInfo.LightGuid.D = (uint)LightMesh.GetInstanceID();
			OutLightInfo.LightPower = SurfaceArea * Brightness;
			OutLightInfo.LightData.dPdu.x = LightMesh.LightFalloffExponent;
			return true;
		}

		float ExportLightMeshSurfels(DawnBakingContext Context,DawnLightMesh LightMesh,Mesh InMesh,ref FLightMeshInfo OutLightMeshInfo)
		{
			var objectToWorld =  LightMesh.transform.localToWorldMatrix;

			var vertices = (Vector3[])InMesh.vertices.Clone ();
			var triangles = InMesh.triangles;

			// transform the vertices to world space,
			// do it in place since they are a copy
			for (int i = 0; i < vertices.Length; i++)
				vertices[i] = objectToWorld.MultiplyPoint(vertices[i]);

			OutLightMeshInfo.Surfels.Resize (triangles.Length / 3);
			// calculate the area
			float SurfaceArea = 0;
			for (int i = 0; i < triangles.Length / 3; i++)
			{
				FLightMeshSurfelInfo SurfelInfo = new FLightMeshSurfelInfo ();
				Vector3 a = vertices[triangles[3 * i]];
				Vector3 b = vertices[triangles[3 * i + 1]];
				Vector3 c = vertices[triangles[3 * i + 2]];
				Vector3 n = Vector3.Cross (b - a, c - a);

				SurfelInfo.Vertex0 = ToFloat4 (a, 1.0f);
				SurfelInfo.Vertex1 = ToFloat4 (b, 1.0f);
				SurfelInfo.Vertex2 = ToFloat4 (c, 1.0f);
				SurfelInfo.NormalAndArea = ToFloat4 (n.normalized,n.magnitude * 0.5f);

				OutLightMeshInfo.Surfels[i] = SurfelInfo;
				SurfaceArea += SurfelInfo.NormalAndArea.w;
			}
			
			return SurfaceArea;
		}
		HDRColor GetLightColor(DawnBakingContext Context, DawnBaseLight DawnLight)
		{
			Color lightColor =  DawnLight.color;
			float lightIntensity = DawnLight.intensity;
			var directColor = HDRColor.CreateHDRColor(lightColor, lightIntensity);
			return directColor;
		}

		HDRColor GetIndirectColor(DawnBakingContext Context,DawnBaseLight DawnLight)
		{
			Color lightColor = DawnLight.color;
			float lightIntensity = DawnLight.intensity;
			float indirectMultiplier = DawnLight.indirectMultiplier;

			var indirectColor = HDRColor.CreateHDRColor(lightColor, lightIntensity * indirectMultiplier * Context.Settings.MiscSettings.IndirectIntensity);
			return indirectColor;
		}

		float GetLightPower(ref HDRColor LightHdrColor)
		{
			return LightHdrColor.GetPower();
		}
    }

	public struct HDRColor
	{
		public HDRColor(UnityEngine.Color InBaseColor, float InIntensity)
        {
			this.BaseColor = InBaseColor;
			this.Intensity = InIntensity;
		}
		public Color GetBaseColor()
        {
			return BaseColor;

		}
		public float GetIntensity()
		{
			return Intensity;
		}

		public float GetPower()
        {
			return BaseColor.grayscale * Intensity;
        }

		public HDRColor ToLinear()
		{
			return new HDRColor(BaseColor.linear, Mathf.GammaToLinearSpace(Intensity));
		}

		public static HDRColor CreateHDRColor(UnityEngine.Color InColor, float Intensity,bool bBakeryMode = false)
		{
			if(bBakeryMode)
            {
				return GraphicsSettings.lightsUseLinearIntensity ? new HDRColor(InColor, Intensity) : new HDRColor(InColor.linear, Intensity);
			}
			return GraphicsSettings.lightsUseLinearIntensity ? new HDRColor(InColor.linear, Intensity) : new HDRColor(InColor, Intensity).ToLinear();
		}

		Color BaseColor;
		float Intensity;
	}
}
