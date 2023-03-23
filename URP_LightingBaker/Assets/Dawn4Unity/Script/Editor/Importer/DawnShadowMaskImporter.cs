using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using NSwarm;
using AgentInterface;

namespace GPUBaking.Editor
{
	public partial class DawnImporter
    {
		public bool ImportShadowMask(DawnBakingContext Context, NSwarm.FGuid Guid, DawnShadowMaskTask ShadowTask)
        {
			var ChannelName = ImportExportUtil.CreateChannelName (Guid, ImportExportUtil.LM_SCENE_VERSION, ImportExportUtil.LM_SHADOW_EXTENSION);

			FQuantizedSDFShadowOutput ShadowOutput = new FQuantizedSDFShadowOutput();

			DawnProfilerSample SampleHandle = null;
			DawnProfiler.BeginSampleAnyThread ("ImportShadowMask.SerializeObject",out SampleHandle);

			if (!ImportExportUtil.SerializeObject (LightingSystem.SwarmInterface, ChannelName, ref ShadowOutput, false, false)) {
				DawnProfiler.EndSampleAnyThread (SampleHandle);
				Context.LogErrorFormat ("ImportShadowMap {0} For {1} Failure!!!",Guid.D,ShadowTask.Name);

				return false;
			}
			DawnProfiler.EndSampleAnyThread (SampleHandle);
			DawnDebug.Print ("ImportShadowMap({0}) For {1}", Guid.D,ShadowTask.Name);

			Debug.Assert(ShadowTask.Allocation!=null);

			int Width = ShadowTask.Allocation.Width;
			int Height = ShadowTask.Allocation.Height;

			

			FQuantizedSDFShadowInfo Sample = new FQuantizedSDFShadowInfo();

			DawnProfiler.BeginSampleAnyThread ("ImportShadowMask.SetPixels",out SampleHandle);

			const bool bFillUnmappedTexel = false;

			// UV bounds
			int4 UVBoundsPixel = ShadowTask.Allocation.UVBoundsPixel;
			int UVWidthPixel = ShadowTask.Allocation.UVWidthPixel + 2 * ShadowTask.Allocation.Padding;
			int UVHeightPixel = ShadowTask.Allocation.UVHeightPixel + 2 * ShadowTask.Allocation.Padding;
			
			Debug.Assert (UVWidthPixel == ShadowOutput.HeaderInfo.Size.x && UVHeightPixel ==  ShadowOutput.HeaderInfo.Size.y);

			{
				Color[] Texels = new Color[UVWidthPixel * UVHeightPixel];
				// only saves the texel which is in the UV bounds
				for (uint Y = 0; Y < UVHeightPixel; ++Y) {
					for (uint X = 0; X < UVWidthPixel; ++X) {
						Color Texel = Color.black;
						for(int LightIndex = 0;LightIndex < ShadowOutput.ShadowMaps.NumElements && LightIndex < 4;++LightIndex)
						{
							var ShadowMap = ShadowOutput.ShadowMaps[LightIndex];
							Sample = ShadowMap.GetData (X,Y,ShadowOutput.HeaderInfo.Size);
							if (Sample.Coverage !=0 || !bFillUnmappedTexel) {
								Texel[LightIndex] = ((float)(Sample.Distance)) / 255.0f;
							} else{
								Texel[LightIndex] = FillUnmappedTexel (X,Y,ref ShadowMap,Width,Height);
							}
							Texel [LightIndex] = Context.bLinearColorSpace ? Texel[LightIndex] : Mathf.LinearToGammaSpace (Texel [LightIndex]);
						}
						Texels [Y * UVWidthPixel + X] = Texel;
					}
				}

				DawnProfiler.EndSampleAnyThread (SampleHandle);

				ShadowTask.Allocation.ShadowTexels = new List<Color> (Texels);
			}

            return true;
        }

		float FillUnmappedTexel(uint X,uint Y,ref FQuantizedSDFShadowMap ShadowMapData,int Width,int Height)
		{
			float Result = 0;
			int Coverage = 0;
			int2 Size = new int2 (Width, Height);

			FQuantizedSDFShadowInfo Sample = new FQuantizedSDFShadowInfo();
			for (uint SampleY = (uint)Mathf.Max(0,Y - 1); SampleY <= (uint)Mathf.Min((uint)Height - 1,Y + 1); ++SampleY) {
				for(uint SampleX = (uint)Mathf.Max(0,X - 1); SampleX <= (uint)Mathf.Min((uint)Width - 1,X + 1); ++SampleX) {
					Sample = ShadowMapData.GetData (SampleX, SampleY,Size);
					if (Sample.Coverage != 0 && Mathf.Abs(SampleY - Y) + Mathf.Abs(SampleX - X) == 1) {
						Result += ((float)(Sample.Distance)) / 255.0f;
						++Coverage;
					}
				}
			}
			Result = Result / Mathf.Max(1,Coverage);
			return Result;
		}
    }
}
