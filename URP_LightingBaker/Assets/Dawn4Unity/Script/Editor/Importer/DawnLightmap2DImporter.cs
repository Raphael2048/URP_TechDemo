using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;


using NSwarm;
using AgentInterface;

namespace GPUBaking.Editor
{
	public partial class DawnImporter
    {
		public bool ImportLightmap2D(DawnBakingContext Context,NSwarm.FGuid Guid, DawnLightmap2DTask LightmapTask)
        {
			var ChannelName = ImportExportUtil.CreateChannelName (Guid, ImportExportUtil.LM_SCENE_VERSION, ImportExportUtil.LM_LM_EXTENSION);

			FUnityLightmap2DOutput LightmapOutput = new FUnityLightmap2DOutput();

			DawnProfilerSample SampleHandle = null;
			DawnProfiler.BeginSampleAnyThread ("ImportLightmap2D.SerializeObject",out SampleHandle);
			if (!ImportExportUtil.SerializeObject (LightingSystem.SwarmInterface, ChannelName, ref LightmapOutput, false, false)) {

				DawnProfiler.EndSampleAnyThread (SampleHandle);
				Context.LogErrorFormat ("ImportLightmap2D {0} For {1} Failure!!!",Guid.D,LightmapTask.Name);
				return false;
			}

			DawnProfiler.EndSampleAnyThread (SampleHandle);

			DawnDebug.Print ("ImportLightmap2D({0}) For {1}", Guid.D,LightmapTask.Name);

			Debug.Assert(LightmapTask.Allocation!=null);

			int Width = LightmapTask.Allocation.Width;
			int Height = LightmapTask.Allocation.Height;

			

			float4 Sample = new float4();

			DawnProfiler.BeginSampleAnyThread ("ImportLightmap2D.SetPixels",out SampleHandle);			

	        // UV bounds
			int4 UVBoundsPixel = LightmapTask.Allocation.UVBoundsPixel;
			
			// uv size in pixel include padding 
			int UVWidthPixel = LightmapTask.Allocation.UVWidthPixel + 2 * LightmapTask.Allocation.Padding;
			int UVHeightPixel = LightmapTask.Allocation.UVHeightPixel + 2 * LightmapTask.Allocation.Padding;
			
			Debug.Assert (UVWidthPixel  == LightmapOutput.HeaderInfo.Size.x && UVHeightPixel ==  LightmapOutput.HeaderInfo.Size.y);
			{
				Color[] Texels = new Color[UVWidthPixel * UVHeightPixel];
				// only saves the texel which is in the UV bounds
				for (uint Y = 0; Y < UVHeightPixel; ++Y) {
					for (uint X = 0; X < UVWidthPixel; ++X) {
						Sample = LightmapOutput.GetData (X, Y );

						Texels [Y * UVWidthPixel + X] = EncodeLightmap (Context,ref Sample);
					}
				}

				DawnProfiler.EndSampleAnyThread (SampleHandle);

				LightmapTask.Allocation.Texels = new List<Color> (Texels);
			}
			
			
			if (LightingSystem.BakingContext.Settings.LightmapSettings.DirectionalMode == EDawnDirectionalMode.Directional && LightmapOutput.DirLightmapData.NumElements > 0)
			{
				DawnProfiler.BeginSampleAnyThread ("ImportLightmap2D.SetDirPixels",out SampleHandle);

				{
					Color[] Directions = new Color[UVWidthPixel * UVHeightPixel];
					// only saves the texel which is in the UV bounds
					for (uint Y = 0; Y < UVHeightPixel; ++Y)
					{
						for (uint X = 0; X < UVWidthPixel; ++X)
						{
							Sample = LightmapOutput.GetDirData (X,Y );
							Directions [Y * UVWidthPixel + X] = EncodeDirectionLightmap(Context,ref Sample);
						}
					}
					DawnProfiler.EndSampleAnyThread (SampleHandle);
					LightmapTask.Allocation.DirectionalTexels = new List<Color>(Directions);
				}
			}
			
            return true;
        }

		Color FillUnmappedTexel(DawnBakingContext Context,uint X,uint Y,ref FLightmap2DOutput LightmapData,int Width,int Height)
		{
			Color Result = Color.black;
			int Coverage = 0;

			FLightmap2DSample Sample = new FLightmap2DSample();
			for (uint SampleY = (uint)Mathf.Max(0,Y - 1); SampleY <= (uint)Mathf.Min((uint)Height - 1,Y + 1); ++SampleY) {
				for(uint SampleX = (uint)Mathf.Max(0,X - 1); SampleX <= (uint)Mathf.Min((uint)Width - 1,X + 1); ++SampleX) {
					Sample = LightmapData.GetData (SampleX, SampleY);
					if (Sample.bIsMapped != 0 && Mathf.Abs(SampleY - Y) + Mathf.Abs(SampleX - X) == 1) {
						Result += EncodeLightmap (Context,ref Sample.IncidentLighting);
						++Coverage;
					}
				}
			}
			Result = Result / Mathf.Max(1,Coverage);
			Result.a = 1.0f;
			return Result;
		}
    }
}
