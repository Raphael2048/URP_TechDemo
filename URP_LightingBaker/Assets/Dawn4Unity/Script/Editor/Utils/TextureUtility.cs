using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

using UnityEngine.Experimental.Rendering;

namespace GPUBaking.Editor
{
	internal class TextureUtils
	{
		public static Texture2D ResizeTexture(DawnBakingContext Context, Texture2D texture2D,bool bDumpDebugFile = false)
		{
			int Width = texture2D.width;
			int Height = texture2D.height;

			float TargetResolution = Context.Settings.MiscSettings.NormalTextureResolution;
			float Scale = Mathf.Max(TargetResolution / Width, TargetResolution / Height);

			if (Scale < 1.0f) {
				
				Width = (int)(Width * Scale);
				Height = (int)(Height * Scale);

				var RestoreRT = RenderTexture.active;

				RenderTexture RenderTarget = RenderTexture.GetTemporary (Width, Height);
				RenderTexture.active = RenderTarget;
				Graphics.Blit (texture2D, RenderTarget);

				var ResizedTexture = new Texture2D (Width, Height);
				ResizedTexture.ReadPixels (new Rect (0, 0, Width, Height), 0, 0);
				ResizedTexture.Apply ();

				RenderTexture.active = RestoreRT;

				return ResizedTexture;
			}

			return CopyTexture (texture2D,Width,Height);
		}

		public static Texture2D CopyTexture(Texture2D SourceTexture,int Width,int Height)
		{
			Texture2D NewTexture = new Texture2D(Width, Height, SourceTexture.format, SourceTexture.mipmapCount > 1);
			Graphics.CopyTexture(SourceTexture, NewTexture);
			return NewTexture;
		}

		public static Cubemap CopyCubemap(Cubemap SourceTexture)
		{
			Cubemap NewTexture = new Cubemap(SourceTexture.width, SourceTexture.format, SourceTexture.mipmapCount > 1);
			Graphics.CopyTexture(SourceTexture, NewTexture);
			return NewTexture;
		}

		public static Texture2D FlipTexture(Texture2D SourceTexture)
        {
			int width = SourceTexture.width;
			int height = SourceTexture.height;
			Texture2D NewTexture = new Texture2D(width, height, SourceTexture.format, SourceTexture.mipmapCount > 0);
			Color[] pixels = SourceTexture.GetPixels();
			Color[] pixelsFlipped = new Color[pixels.Length];

			for (int i = 0; i < height; i++)
			{
				System.Array.Copy(pixels, i * width, pixelsFlipped, (height - i - 1) * width, width);
			}

			NewTexture.SetPixels(pixelsFlipped);
			NewTexture.Apply();
			return NewTexture;
		}

		public static Texture2D ExpendCubemap(Cubemap InCubemap)
        {
			Texture2D NewCubemap = new Texture2D(InCubemap.width * 6, InCubemap.height, TextureFormat.RGBAFloat, false, true);
			for (int Face = 0; Face < 6; ++Face)
			{
				Graphics.CopyTexture(
					InCubemap, Face, 0, 0, 0, InCubemap.width, InCubemap.height,
					NewCubemap, 0, 0, Face * InCubemap.width, 0);
			}
			return FlipTexture(NewCubemap);
		}
	}
}
