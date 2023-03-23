using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GPUBaking
{
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
   	public class DawnLightmapBakingParameters : MonoBehaviour
    {
        public ELightmapDenoiserMode DenoiserMode = ELightmapDenoiserMode.Optix;
		[Range(16,1000000)]
        public int SamplesPerPixel = 4096;
		[Range(1,20)]
        public int MaxBounces = 4;
		[Range(1,20)]
        public int MaxSkyBounces = 1;
		[Range(0.01f,4.0f)]
        public float PenumbraShadowFraction = 1;
        [Range(1, 8)]
        public int SuperSampleFactor = 1;

        public float OffsetScale = 1.0f;

		public bool SeamFixed = true;
    }
}
