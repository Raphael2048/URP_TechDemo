using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using System;
using NSwarm;
using AgentInterface;

namespace GPUBaking.Editor
{
	public class DawnLightProbeTask : DawnTask
	{
		public static FGuid PrecomputedVolumeLightingGuid = new FGuid(0xce97c5c3, 0xab614fd3, 0xb2da55c0, 0xe6c33fb4);

		public List<SphericalHarmonicsL2> ProbeCeffs;

		public List<Vector3> ProbePositions;

		public List<Vector4> SamplePostions;

		public List<DawnBakeResultAsset.DawnProbeOcclusion> ProbeOcclusions;

		public DawnLightProbeTask()
		{
			SamplePostions = new List<Vector4>();
		}

		public DawnLightProbeTask(List<Vector4> SamplePostions)
		{
			this.SamplePostions = SamplePostions;
		}

		public override bool ApplyResult(DawnBakingContext Context)
		{
			Context.ProbeCeffs.AddRange (ProbeCeffs);
			Context.ProbePositions.AddRange (ProbePositions);

			return true;
		}

		public override bool ExportResult(DawnBakingContext Context)
		{
			DawnStorage.GetSceneLightingData().AddBakedLightProbeInfo(ProbeCeffs, ProbePositions, ProbeOcclusions);
			return true;
		}

		public override int Cost
		{
			get { return SamplePostions.Count * 64;}
		}

		public override ETaskType Type
		{
			get {return ETaskType.LIGHTPROBE;}
		}
	}

	public class DawnAutoGenerationLightProbeTask : DawnLightProbeTask
	{
		public DawnAutoGenerationLightProbeTask()
        {
			TaskGuid = DawnLightProbeTask.PrecomputedVolumeLightingGuid;
		}

		public override bool ApplyResult(DawnBakingContext Context)
		{
			DawnDebug.LogFormat("LightProbe Auto Generation With {0}", ProbePositions.Count);

			bool bDebugProbes = true;

			if(ProbePositions.Count > 0 && Dawn4Unity.GetLightingSetting().LightProbeSettings.AutoGenerationSetting.GenerateUnityLightProbeGroup)
            {
				GameObject go = GameObject.Find("DawnAutoLightProbeGroup");
				if(go == null)
                {
					go = new GameObject("DawnAutoLightProbeGroup");
					go.AddComponent<LightProbeGroup>();
                }
				LightProbeGroup lpg = go.GetComponent<LightProbeGroup>();
				lpg.probePositions = ProbePositions.ToArray();
            }
			
			return bDebugProbes ? true : base.ApplyResult(Context);
		}

		public override int Cost
		{
			get { return 10000; }
		}

		public override ETaskType Type
		{
			get { return ETaskType.AUTO_GENERATION_LIGHTPROBE; }
		}
	}
}