using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

using NSwarm;
using AgentInterface;

namespace GPUBaking.Editor
{
	struct MaterialTextures
	{
		public EBlendMode BlendMode;
		public float OpacityMaskClipValue;
		public float EmissiveExposure;
		public Texture2D DiffuseTexture;
		public Texture2D TransmissionTexture;
		public Texture2D EmissiveTexture;
		public Texture2D NormalTexture;
	}

	public partial class DawnExporter
    {
		bool ExportMaterials(DawnBakingContext Context)
		{
			bool bSuccessed = true;
			for(int MatIndex = 0; MatIndex < Context.Materials.Count && bSuccessed;MatIndex++)
			{
				
				var Mat = Context.Materials[MatIndex];
				FSHAHashInfo HashInfo = new FSHAHashInfo ();
				GetMaterialHash (Context,Mat,out HashInfo.Hash);
				FMaterialInfo MaterialInfo = new FMaterialInfo();

				if (!Context.IsEnableExportCache || !ImportExportUtil.IsMaterialCached (LightingSystem.SwarmInterface, ref HashInfo)) {
					bSuccessed = ExportMaterial (Context,HashInfo,MatIndex, Mat, ref MaterialInfo);
					if (bSuccessed && Context.IsEnableExportCache) {
						bSuccessed = ImportExportUtil.SerializeMaterial (LightingSystem.SwarmInterface, ref HashInfo, ref MaterialInfo, true);
					}
				}
				if(Context.IsEnableExportCache)
				{
					MaterialInfo.HeaderInfo.Hash = HashInfo;
					MaterialInfo.HeaderInfo.ShadingModelID = -1;
				}

				if(bSuccessed)
				{
					SceneInfo.Materials.AddElement (ref MaterialInfo);
				}
				else{
					Context.LogErrorFormat ("Export Material:{0} Failure!!!", Mat.name);
				}
			}
			return bSuccessed;
		}

		bool ExportMaterial(DawnBakingContext Context,FSHAHashInfo MatHash,int MatIndex,Material Mat,ref FMaterialInfo OutMaterialInfo)
        {
			MaterialTextures Textures = new MaterialTextures();
			if (!BuildMaterial (Context,Mat,ref Textures)) {
				return false;
			}
			DawnDebug.Print("Export Material({0}):{1}",MatIndex,Mat.name);

			OutMaterialInfo.HeaderInfo.Hash = MatHash;
			OutMaterialInfo.HeaderInfo.ShadingModelID = 0;
			OutMaterialInfo.HeaderInfo.Flags = 0;
			OutMaterialInfo.HeaderInfo.BlendMode = (uint)Textures.BlendMode;
			OutMaterialInfo.HeaderInfo.bTwoSided = Mat.doubleSidedGI ? 1u : 0;
			OutMaterialInfo.HeaderInfo.OpacityMaskClipValue = Textures.OpacityMaskClipValue;

			if (Mat.doubleSidedGI) {
				OutMaterialInfo.HeaderInfo.Flags |= (uint)EMaterialInfoFlags.MATERIAL_FLAGS_TWOSIDE;
			}

			float BoostValue = 1.0f;

			if (Textures.DiffuseTexture) {
				ExportTexture (Mat.name,Textures.DiffuseTexture,ref OutMaterialInfo.DiffuseTexture,BoostValue);
			}
			if (Textures.TransmissionTexture) {
				ExportTexture (Mat.name,Textures.TransmissionTexture,ref OutMaterialInfo.TransmissionTexture,BoostValue);
			}
			if (Textures.EmissiveTexture) {
				ExportTexture (Mat.name,Textures.EmissiveTexture,ref OutMaterialInfo.EmissiveTexture,BoostValue * Textures.EmissiveExposure);
			}
			if (Textures.NormalTexture)
			{
				bool bUseDX5NM = !UnityEngine.Rendering.GraphicsSettings.HasShaderDefine(UnityEngine.Rendering.BuiltinShaderDefine.UNITY_NO_DXT5nm);
				ExportTexture(Mat.name, Textures.NormalTexture, ref OutMaterialInfo.NormalTexture, BoostValue,true,bUseDX5NM);
			}
			return true;
        }

		bool BuildMaterial(DawnBakingContext Context,Material Mat,ref MaterialTextures Textures)
		{
			Textures.DiffuseTexture = null;

			var ShaderName = Mat.shader.name;

			Textures.BlendMode = CaculateBlendMode(Mat);		

			Color DiffuseColor = Color.white;

			if (Textures.BlendMode == EBlendMode.BLEND_MODE_MASKED) {
				if (Mat.HasProperty ("_Cutoff")) {
					Textures.OpacityMaskClipValue = Mat.GetFloat ("_Cutoff");
				} else if (Mat.HasProperty ("_AlphaTestRef")) {
					Textures.OpacityMaskClipValue = Mat.GetFloat ("_AlphaTestRef");
				}
			}

			if (Mat.HasProperty ("_Color")) {
				DiffuseColor = Mat.GetColor ("_Color");
			}

			bool bHasMainTex = Mat.HasProperty ("_MainTex");

			if (bHasMainTex) {
				var MainTex = Mat.GetTexture ("_MainTex");
				Textures.DiffuseTexture = MainTex as Texture2D;
			}

			bool bHasNormalTex = Mat.HasProperty("_BumpMap");
			if (bHasNormalTex)
			{
				var NormalTex = Mat.GetTexture("_BumpMap");
				Textures.NormalTexture = NormalTex as Texture2D;

				if(Textures.NormalTexture!=null)
                {
					Textures.NormalTexture = TextureUtils.ResizeTexture(Context,Textures.NormalTexture);
				}
			}

			int LightmapPass = Context.Settings.MiscSettings.bUseMetaPass ? Mat.FindPass("META") : -1;

			if (LightmapPass > 0 && Textures.BlendMode == EBlendMode.BLEND_MODE_OPAQUE) {
				int TextureSize = Context.Settings.MiscSettings.NormalTextureResolution;
				RenderToTexture (Mat,LightmapPass,TextureSize,TextureSize,false,out Textures.DiffuseTexture);
			} else {
				if (Textures.DiffuseTexture != null) {
					if (Textures.BlendMode == EBlendMode.BLEND_MODE_MASKED)
					{
						Textures.DiffuseTexture = TextureUtils.CopyTexture (Textures.DiffuseTexture,Textures.DiffuseTexture.width,Textures.DiffuseTexture.height);
					}
					else
					{
						Textures.DiffuseTexture = TextureUtils.ResizeTexture (Context, Textures.DiffuseTexture);
					}
				}

				if (Textures.DiffuseTexture == null) {
					Textures.DiffuseTexture = CreateColorTexture (DiffuseColor.ToString (), 1, 1, DiffuseColor);
				}
			}

			if (Textures.BlendMode == EBlendMode.BLEND_MODE_MASKED) {
				Textures.TransmissionTexture = CreateMaskTexture (Context, Textures.DiffuseTexture);
			}

			if (Textures.BlendMode == EBlendMode.BLEND_MODE_TRANSLUCENT) {
				Textures.TransmissionTexture = CreateTransparentTexture (Context, Textures.DiffuseTexture);
			}

			if (GWhiteTexture == null) {
				GWhiteTexture = CreateColorTexture ("White", 1, 1, Color.white);
			}

			if (Textures.BlendMode != EBlendMode.BLEND_MODE_OPAQUE && Textures.TransmissionTexture == null) {
				Textures.TransmissionTexture = GWhiteTexture;
			}

			if ((Mat.globalIlluminationFlags & MaterialGlobalIlluminationFlags.BakedEmissive) != 0) {

				Color EmissionColor = Color.black;
				bool bHasEmissionColor = Mat.HasProperty("_EmissionColor");
				if (bHasEmissionColor) {
					EmissionColor = Mat.GetColor ("_EmissionColor");					
				}

				if (LightmapPass > 0)
				{
					if (bHasEmissionColor)
					{
						Color HDRColor = ExtractHDRColor(EmissionColor);
						Textures.EmissiveExposure = HDRColor.a;
						HDRColor.a = 1.0f;
						Mat.SetColor("_EmissionColor", HDRColor);
					}
					int TextureSize = Context.Settings.MiscSettings.NormalTextureResolution;
					RenderToTexture(Mat, LightmapPass, TextureSize, TextureSize, true, out Textures.EmissiveTexture);
					if (bHasEmissionColor)
					{
						Mat.SetColor("_EmissionColor", EmissionColor);
					}
				}
                else
                {
					if(bHasEmissionColor)
                    {
						EmissionColor = ExtractHDRColor(EmissionColor);
						EmissionColor *= EmissionColor.a;
						EmissionColor.a = 1.0f;
					}
					if (Mat.HasProperty("_EmissionMap"))
					{
						Textures.EmissiveTexture = Mat.GetTexture("_EmissionMap") as Texture2D;
					}
					if (Textures.EmissiveTexture != null)
					{
						Textures.EmissiveTexture = TextureUtils.ResizeTexture(Context, Textures.EmissiveTexture, true);
					}
				}

				if (Textures.EmissiveTexture == null && EmissionColor.maxColorComponent > 0) {
					Textures.EmissiveTexture = CreateColorTexture (EmissionColor.ToString (), 1, 1, EmissionColor);
				}
			}

			return true;
		}

		Mesh QuadMesh = null;

		void RenderToTexture(Material Mat,int PassIndex,int Width,int Height,bool bHasEmissive,out Texture2D OutTexture)
		{
			var rtAlbedo = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32, bHasEmissive ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
			var texAlbedo = new Texture2D(Width, Height, TextureFormat.RGBA32, false, true);

			var OldCamera = Camera.current;
			Camera.SetupCurrent(null);

			Graphics.SetRenderTarget(rtAlbedo);
			GL.Clear(true, true, new Color(0,0,0,0));

			var metaControl = new Vector4(1,0,0,0);
			var metaControlAlbedo = new Vector4(1,0,0,0);
			var metaControlEmission = new Vector4(0,1,0,0);
			Shader.SetGlobalVector("unity_MetaVertexControl", metaControl);
			Shader.SetGlobalVector("unity_MetaFragmentControl", bHasEmissive ? metaControlEmission : metaControlAlbedo);
			Shader.SetGlobalFloat("unity_OneOverOutputBoost", 1.0f);
			Shader.SetGlobalFloat("unity_MaxOutputValue", 10000000.0f);
			Shader.SetGlobalFloat("unity_UseLinearSpace", bHasEmissive && PlayerSettings.colorSpace == ColorSpace.Linear ? 1.0f : 0.0f);

			var metaST = new Vector4(1.0f, 1.0f, 0.0f, 0.0f);
			Shader.SetGlobalVector("unity_LightmapST", metaST);
			Shader.SetGlobalVector("unity_DynamicLightmapST", metaST);

			Mat.SetShaderPassEnabled("Meta", true);

			bool bSuccessed = Mat.SetPass (PassIndex);
			Debug.Assert (bSuccessed);

			if(QuadMesh == null)
            {
				var verteics = new Vector3[] { new Vector3(-1, -1, 0.0f), new Vector3(1, -1, 0.0f), new Vector3(-1, 1, 0.0f), new Vector3(1, 1, 0.0f) };
				var normals = new Vector3[] { -Vector3.forward, -Vector3.forward, -Vector3.forward, -Vector3.forward };
				var triangles = new int[] { 0, 3, 1, 3, 0, 2 };
				var uvs = new Vector2[] { new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.0f), new Vector2(0.0f, 1.0f), new Vector2(1.0f, 1.0f) };
				var mesh = new Mesh();
				mesh.vertices = verteics;
				mesh.normals = normals;
				mesh.triangles = triangles;
				mesh.uv = uvs;
				mesh.uv2 = uvs;
				mesh.UploadMeshData(false);
				QuadMesh = mesh;
			}

			Debug.Assert(Camera.current == null);

			GL.sRGBWrite = false;
			GL.PushMatrix();
			GL.LoadOrtho();
			Graphics.DrawMeshNow(QuadMesh, UnityEngine.Matrix4x4.identity);
			GL.PopMatrix();

			Graphics.SetRenderTarget(rtAlbedo);
			texAlbedo.ReadPixels(new Rect(0,0,rtAlbedo.width,rtAlbedo.height), 0, 0, false);
			texAlbedo.Apply();
			Graphics.SetRenderTarget(null);

			Camera.SetupCurrent(OldCamera);

			OutTexture = texAlbedo;
		}

		internal Color ExtractHDRColor(Color InputColor)
		{
			Color32 BaseColor = InputColor;
			float Intensity = 0.0f;

			var HDRThreadhold = InputColor.maxColorComponent;

			if (HDRThreadhold == 0.0f 
				|| (HDRThreadhold <= 1.0f && HDRThreadhold >= 1 / 255.0f))
			{
				InputColor.a = 1.0f;
				return InputColor;
			}
			else
			{
				Intensity = Mathf.Log(255.0f * HDRThreadhold / 0xbf) / Mathf.Log(2.0f);

				BaseColor.r = (byte)Mathf.Min(0xbf, Mathf.CeilToInt(0xbf * InputColor.r / HDRThreadhold));
				BaseColor.g = (byte)Mathf.Min(0xbf, Mathf.CeilToInt(0xbf * InputColor.g / HDRThreadhold));
				BaseColor.b = (byte)Mathf.Min(0xbf, Mathf.CeilToInt(0xbf * InputColor.b / HDRThreadhold));
			}

			Color OutputColor = BaseColor;
			OutputColor.a = Mathf.Pow(2.0f, Intensity);

			return OutputColor;
		}

		EBlendMode CaculateBlendMode(Material Mat)
		{

			if (Mat.renderQueue >= 2450 && Mat.renderQueue < 3000) {
				return EBlendMode.BLEND_MODE_MASKED;
			}

			if (Mat.renderQueue >= 3000 && Mat.renderQueue < 4000) {
				return EBlendMode.BLEND_MODE_TRANSLUCENT;
			}

			return EBlendMode.BLEND_MODE_OPAQUE;
		}

		static Texture2D GWhiteTexture;

		Texture2D CreateColorTexture(string TextureName,int Width,int Height,Color DefaultValue)
		{
			Texture2D Texture = new Texture2D(Width,Height, TextureFormat.RGBAFloat, false);

			Texture.name = TextureName;
			
			for(int Y = 0;Y<Height;Y++)
			{
				for(int X = 0;X < Width;X++)
				{
					Texture.SetPixel(X,Y,DefaultValue);
				}
			}
			
			Texture.Apply();
			return Texture;
		}

		Texture2D CreateMaskTexture(DawnBakingContext Context,Texture2D SourceTexture)
		{
			int Width = SourceTexture.width;
			int Height = SourceTexture.height;

            Texture2D MaskTexture = new Texture2D (Width, Height);

			MaskTexture.name = SourceTexture.name+"(Mask)";

			for(int Y = 0;Y<Height;Y++)
			{
				for(int X = 0;X < Width;X++)
				{
					float Alpha = SourceTexture.GetPixel(X,Y).a;
					MaskTexture.SetPixel(X,Y,new Color(Alpha,Alpha,Alpha,Alpha));
				}
			}

			return MaskTexture;
		}


		Texture2D CreateTransparentTexture(DawnBakingContext Context,Texture2D SourceTexture)
		{
			int Width = SourceTexture.width;
			int Height = SourceTexture.height;
			
			// Transparent Texture needs to be ARGB32, RGBA32, RGB24, Alpha8 or one of float formats to use SetPixel method
			Texture2D Result = new Texture2D (Width, Height, TextureFormat.RGBA32, false);
            Result.name = SourceTexture.name+"(Transparent)";

            for (int Y = 0; Y < Height; Y++)
            {
                for (int X = 0; X < Width; X++)
                {
					Color Pixel = SourceTexture.GetPixel(X, Y);
                    float Alpha = Pixel.a;
                    Pixel *= Pixel.a;
                    Pixel.a = Alpha;
                    Result.SetPixel(X, Y, Pixel);
                }
            }
            
            Result.Apply();
            return Result;
		}

		bool ExportTexture(string DebugName,Texture2D SourceTexture,ref FTexture2DInfo TextureInfo,float Boost,bool bNormalMap = false,bool bUseDX5NM = false)
        {
			DawnDebug.Print("Export Texture:{0}({1}x{2}) For {3}",SourceTexture.name,SourceTexture.width,SourceTexture.height,DebugName);

			int Width = SourceTexture.width;
			int Height = SourceTexture.height;

			Texture2D Texture = SourceTexture;

			TextureInfo.HeaderInfo.SizeX = (uint)Width;
			TextureInfo.HeaderInfo.SizeY = (uint)Height;
			TextureInfo.HeaderInfo.Format = 2;

			TextureInfo.Colors.Resize (Width * Height);

			for(int Y = 0;Y<Height;Y++)
			{
				for(int X = 0;X < Width;X++)
				{
					Color Pixel = Texture.GetPixel(X, Y);
					if(bNormalMap)
                    {
						if(bUseDX5NM)
                        {
							Pixel = UnpackNormalmapRGorAG(Pixel);
						}
                        else
                        {
							Pixel = Pixel * 2 - Color.white;
							Pixel.a = 1.0f;

						}
					}
					TextureInfo.Colors [Y * Width + X] = ToFloat4 (Pixel * Boost);
				}
			}

			return true;
        }

		int GetMaterialHash (DawnBakingContext Context, Material Mat,out byte[] Hash)
		{
            GUIDUtility.GetMaterialHash(Mat, Context.Settings.MiscSettings.bUseMetaPass, out Hash);
			return Hash.Length;
		}

		internal static Color UnpackNormalmapRGorAG(Color PackedNormal)
		{
			// This do the trick
			PackedNormal.r *= PackedNormal.a;
			Color Normal = new Color(0.0f, 0.0f, 0.0f, 1.0f);
			Normal.r = PackedNormal.r * 2 - 1;
			Normal.g = PackedNormal.g * 2 - 1;
			Normal.b = Mathf.Sqrt(1 - Mathf.Clamp01(Normal.r * Normal.r + Normal.g * Normal.g));
			return Normal;
		}
	}
}
