using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

using NSwarm;
using AgentInterface;

namespace GPUBaking.Editor
{
	public partial class DawnImporter
    {
		public bool ImportLightProbes(DawnBakingContext Context, NSwarm.FGuid Guid, DawnLightProbeTask LightProbeTask)
        {
			var ChannelName = ImportExportUtil.CreateChannelName (Guid, ImportExportUtil.LM_SCENE_VERSION, ImportExportUtil.LM_LIGHTPROBE_EXTENSION);

			FLightProbeOutput LightProbeOutput = new FLightProbeOutput();

			if (!ImportExportUtil.SerializeObject (LightingSystem.SwarmInterface, ChannelName, ref LightProbeOutput, false, false)) {

				Context.LogErrorFormat ("ImportLightProbes {0} Failure!!!",Guid.D);

				return false;
			}

			LightProbeTask.ProbeCeffs = new List<SphericalHarmonicsL2> ();
			LightProbeTask.ProbePositions = new List<Vector3> ();
			LightProbeTask.ProbeOcclusions = new List<DawnBakeResultAsset.DawnProbeOcclusion>();

			ImportLightProbes(Context,LightProbeTask, LightProbeOutput);

			return true;
        }

		public bool ImportSparseLightProbes(DawnBakingContext Context, NSwarm.FGuid Guid, DawnLightProbeTask LightProbeTask)
		{
			var ChannelName = ImportExportUtil.CreateChannelName(Guid, ImportExportUtil.LM_SCENE_VERSION, ImportExportUtil.LM_LIGHTPROBE_EXTENSION);

			FSparseLightProbeOutput SparseLightProbeOutput = new FSparseLightProbeOutput();

			if (!ImportExportUtil.SerializeObject(LightingSystem.SwarmInterface, ChannelName, ref SparseLightProbeOutput, false, false))
			{

				DawnDebug.LogErrorFormat("ImportSparseLightProbes {0} Failure!!!", Guid.D);

				return false;
			}

			LightProbeTask.ProbeCeffs = new List<SphericalHarmonicsL2>();
			LightProbeTask.ProbePositions = new List<Vector3>();
			LightProbeTask.ProbeOcclusions = new List<DawnBakeResultAsset.DawnProbeOcclusion>();

			for (int Index = 0;Index < SparseLightProbeOutput.LightProbesForLevel.NumElements;++Index)
            {
				ImportLightProbes(Context,LightProbeTask, SparseLightProbeOutput.LightProbesForLevel[Index]);
			}

			return true;
		}

		private bool ImportLightProbes(DawnBakingContext Context, DawnLightProbeTask LightProbeTask,FLightProbeOutput LightProbeOutput)
		{
			var LightIndices = new System.Collections.Generic.Dictionary<DawnBaseLight, int>();
			var LightChannels = new System.Collections.Generic.Dictionary<DawnBaseLight, int>();
			for (int LightIndex = 0; LightIndex < Context.Lights.Count; ++LightIndex)
			{
				var LightInfo = Context.Lights[LightIndex];
				LightIndices.Add(LightInfo, LightIndex);
			}
			for (int Index = 0; Index < LightProbeOutput.LightProbes.NumElements; ++Index)
			{
				var CellInfo = LightProbeOutput.LightProbes[Index];
				//DawnDebug.Print("Cell-{0}:{1},Origin:{2}", Index, ToVector4(ref CellInfo.PostionAndRadius), LightProbeTask.SamplePostions[Index]);
				//DawnDebug.LogFormat ("Cell-{0}:SampleValue:{1}",Index,(CellInfo.SampleValue));
				//DawnDebug.LogFormat ("Cell-{0}:SkyOcclusion:{1}",Index,ToVector3(ref CellInfo.SkyOcclusion));
				//DawnDebug.LogFormat ("Cell-{0}:MinDistanceToSurface:{1}",Index,(CellInfo.MinDistanceToSurface));
				//DawnDebug.LogFormat ("Cell-{0}:DirectionalLightShadowing:{1}",Index,(CellInfo.DirectionalLightShadowing));
				//DawnDebug.LogFormat ("Cell-{0}:Flags:{1}",Index,(CellInfo.Flags));
				//DawnDebug.LogFormat ("Cell-{0}:Padding0:{1}",Index,ToVector2(ref CellInfo.Padding0));

				LightProbeTask.ProbePositions.Add(ToVector4(ref CellInfo.PostionAndRadius));
				LightProbeTask.ProbeCeffs.Add(ToSHRGB3(ref CellInfo.SampleValue));

				DawnBakeResultAsset.DawnProbeOcclusion ProbeOcclusion = new DawnBakeResultAsset.DawnProbeOcclusion();
				ProbeOcclusion.Occlusion = new float[4];
				ProbeOcclusion.OcclusionMaskChannel = new int[4];
				ProbeOcclusion.ProbeOcclusionLightIndex = new int[4];

				for (int LightIndex = 0; LightIndex < Context.MixedLights.Count; ++LightIndex)
				{
					var LightInfo = Context.MixedLights[LightIndex];

					int ProbeOcclusionLightIndex = LightIndices[LightInfo];
					float Occlusion = 0.0f;

					switch(LightIndex)
                    {
						case 0:
							Occlusion = CellInfo.DirectionalLightShadowing;
							break;
						case 1:
							Occlusion = CellInfo.SkyOcclusion.x;
							break;
						case 2:
							Occlusion = CellInfo.SkyOcclusion.y;
							break;
						case 3:
							Occlusion = CellInfo.SkyOcclusion.z;
							break;
						default:
							break;
					}

					ProbeOcclusion.Occlusion[LightIndex] = Occlusion;
					ProbeOcclusion.OcclusionMaskChannel[LightIndex] = LightIndex;
					ProbeOcclusion.ProbeOcclusionLightIndex[LightIndex] = ProbeOcclusionLightIndex;
				}

				for (int LightIndex = Context.MixedLights.Count; LightIndex < 4; ++LightIndex)
                {
					ProbeOcclusion.Occlusion[LightIndex] = 0.0f;
					ProbeOcclusion.OcclusionMaskChannel[LightIndex] = -1;
					ProbeOcclusion.ProbeOcclusionLightIndex[LightIndex] = -1;
				}
				LightProbeTask.ProbeOcclusions.Add(ProbeOcclusion);
			}

			return true;
		}

		SphericalHarmonicsL2 ToSHRGB3(ref TSHRGB3 Input)
		{
			SphericalHarmonicsL2 Output = new SphericalHarmonicsL2 ();
			for (int i = 0; i < 9; ++i) {
				Output [0, i] = Input.R.V [i];
				Output [1, i] = Input.G.V [i];
				Output [2, i] = Input.B.V [i];
			}
			return Output;
		}
    }
}
