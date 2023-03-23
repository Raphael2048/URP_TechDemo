using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GPUBaking.Editor
{
    public class DawnBakeResultAsset : ScriptableObject
    {
        [System.Serializable]
        public struct DawnProbeCeff
        {
            public DawnProbeCeff(SphericalHarmonicsL2 SH,float Weight = 1.0f)
            {
                SHValue = new float[27];
                for (int i = 0; i < 3; ++i)
                {
                    for (int j = 0; j < 9; ++j)
                    {
                        SHValue[i * 9 + j] = SH[i, j] * Weight;
                    }
                }
            }

            public SphericalHarmonicsL2 ToSH()
            {
                SphericalHarmonicsL2 Result = new SphericalHarmonicsL2();
                for (int i = 0; i < 3; ++i)
                {
                    for (int j = 0; j < 9; ++j)
                    {
                        Result[i, j] = SHValue[i * 9 + j];
                    }
                }

                return Result;
            }

            public float[] SHValue;
        }

        [System.Serializable]
        public struct DawnProbeOcclusion
        {
            [SerializeField] public int[] ProbeOcclusionLightIndex;
            [SerializeField] public float[] Occlusion;
            [SerializeField] public int[] OcclusionMaskChannel;
        }

        [System.Serializable]
        public struct DawnBakedLightInfo
        {
            [SerializeField] public GUID LightID;
			[SerializeField] public LightBakingInfo LightBakedData;
        }
        
        [System.Serializable]
        public struct DawnMeshInstanceInfo
        {
            [SerializeField] public GUID MeshInstanceID;

            [SerializeField] public Vector4 LightmapOffset;
            [SerializeField] public int LightmapIndex;
        }
        
        [System.Serializable]
        public struct DawnLandscapeInfo
        {
            [SerializeField] public GUID LandscapeID;

            [SerializeField] public Vector4 LandscapeOffset;
            [SerializeField] public int LandscapeIndex;
        }
        
        [SerializeField] public List<DawnMeshInstanceInfo> MeshInfos = new List<DawnMeshInstanceInfo>();

        [SerializeField] public List<DawnLandscapeInfo> Landscapes = new List<DawnLandscapeInfo>();
        
        [SerializeField] public List<Texture2D> BakedLightmaps = new List<Texture2D>();

        [SerializeField] public List<Texture2D> BakedShadowMasks = new List<Texture2D>();
        
        [SerializeField] public List<Texture2D> BakedDirectionalLightmaps = new List<Texture2D>();
        
        [SerializeField] public List<DawnProbeCeff> BakedLightProbeCeffs = new List<DawnProbeCeff>();
        
        [SerializeField] public List<Vector3> BakedLightProbePositions = new List<Vector3>();

        [SerializeField] public List<DawnProbeOcclusion> BakedLightProbeOcclusions = new List<DawnProbeOcclusion>();

        [SerializeField] public DawnProbeCeff BakedAmbientProbe = new DawnProbeCeff();

        [SerializeField] public Texture SkyReflectionCubemap = null;

        [SerializeField] public List<DawnBakedLightInfo> BakedLightInfos = new List<DawnBakedLightInfo>();

        [SerializeField] public LightmapsMode lightmapsMode = LightmapsMode.NonDirectional;

    }
}