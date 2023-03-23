using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace GPUBaking
{
	public class LightmapTextureProcessor : AssetPostprocessor
	{
		void OnPreprocessTexture()
		{
			TextureImporter Importer = assetImporter as TextureImporter;

			string AssetPath = Importer.assetPath;
			int Index = AssetPath.LastIndexOf ("/");

			if (Index < 0)
				return;

			if (AssetPath.EndsWith (".tga"))
				return;
	
			string AssetName = AssetPath.Substring (Index+1);

			if (AssetName.Contains ("DawnBakedLightmap")) {
				Importer.textureType = TextureImporterType.Lightmap;
				Importer.sRGBTexture = PlayerSettings.colorSpace == ColorSpace.Gamma;
				Importer.mipmapEnabled = false;
				Importer.filterMode = Dawn4Unity.GetDebugSetting().bDebugLightmapTexel ? FilterMode.Point : FilterMode.Bilinear;
				Importer.textureCompression = GetCompressionMode(Dawn4Unity.GetLightingSetting());
				Importer.wrapMode = TextureWrapMode.Clamp;
			}
			else if (AssetName.Contains("DawnBakedDireciton"))
			{
#if UNITY_2019_1_OR_NEWER
				Importer.textureType = TextureImporterType.DirectionalLightmap;
#else
				Importer.textureType = TextureImporterType.Default;
#endif
				Importer.sRGBTexture = false;
				Importer.textureCompression = GetCompressionMode(Dawn4Unity.GetLightingSetting());
				Importer.wrapMode = TextureWrapMode.Clamp;
			}
			else if (AssetName.Contains ("DawnBakedShadowMask")) {
				Importer.textureType = TextureImporterType.Default;
				Importer.sRGBTexture = PlayerSettings.colorSpace == ColorSpace.Gamma;
				Importer.mipmapEnabled = false;
				Importer.filterMode = Dawn4Unity.GetDebugSetting().bDebugLightmapTexel ? FilterMode.Point : FilterMode.Bilinear;
				Importer.textureCompression = TextureImporterCompression.CompressedHQ;
				Importer.wrapMode = TextureWrapMode.Clamp;
			}
			else if (AssetName.Contains("DawnReflectionProbe"))
			{
				Importer.textureType = TextureImporterType.Default;
				Importer.textureShape = TextureImporterShape.TextureCube;
				Importer.sRGBTexture = PlayerSettings.colorSpace == ColorSpace.Gamma;
				Importer.isReadable = AssetName.Contains("TempDawnReflectionProbe");
			}
		}

		static TextureImporterCompression GetCompressionMode(DawnSettings Settings)
        {
			if(Settings!=null)
            {
				TextureImporterCompression Result = TextureImporterCompression.Uncompressed;
				switch (Settings.AtlasSettings.CompressMode)
                {
					case EDawnLightmapCompressionMode.Uncompressed:
						Result = TextureImporterCompression.Uncompressed;
						break;
					case EDawnLightmapCompressionMode.CompressedHQ:
						Result = TextureImporterCompression.CompressedHQ;
						break;
					case EDawnLightmapCompressionMode.CompressedLQ:
						Result = TextureImporterCompression.CompressedLQ;
						break;
					case EDawnLightmapCompressionMode.Compressed:
						Result = TextureImporterCompression.Compressed;
						break;
					default:
						break;
				}
				return Result;
			}
			return LightmapEditorSettings.textureCompression? TextureImporterCompression.CompressedHQ: TextureImporterCompression.Uncompressed;
		}
	}

}