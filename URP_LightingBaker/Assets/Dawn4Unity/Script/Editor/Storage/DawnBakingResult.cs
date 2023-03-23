using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NSwarm;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;
using System.Runtime.InteropServices;

namespace GPUBaking.Editor
{
    public class DawnSceneBakeResult
    {
        private Scene UnityScene;

        private DawnBakeResultAsset BakeResultAsset;
         
        public DawnSceneBakeResult(Scene InScene)
        {
            BakeResultAsset = ScriptableObject.CreateInstance<DawnBakeResultAsset>();
            UnityScene = InScene;
        }

        public void ClearAllBakedResult()
        {
            BakeResultAsset = ScriptableObject.CreateInstance<DawnBakeResultAsset>();
        }
        
        public void AddBakeLightmap(Texture2D Lightmap)
        {
            BakeResultAsset.BakedLightmaps.Add(Lightmap);
        }

        public void AddBakeShadowMask(Texture2D ShadowMask)
        {
            BakeResultAsset.BakedShadowMasks.Add(ShadowMask);
        }

        public void AddBakeDirectionalLightmap(Texture2D DirectionalLightmap)
        {
            BakeResultAsset.BakedDirectionalLightmaps.Add(DirectionalLightmap);
        }

        public void AddBakedLight(DawnBaseLight BakedLight)
        {
            DawnBakeResultAsset.DawnBakedLightInfo BakedLightInfo = new DawnBakeResultAsset.DawnBakedLightInfo();

            BakedLightInfo.LightID = GUID.CreateGUID(BakedLight);
            if(BakedLight.UnityLight != null)
            {
                BakedLightInfo.LightBakedData = new LightBakingInfo(BakedLight.UnityLight);
            }
            else
            {
                BakedLightInfo.LightBakedData = new LightBakingInfo(BakedLight.LightIndex, BakedLight.ChannelIndex, (EDawnLightingMask)BakedLight.LightingMask, (EDawnLightingMode)BakedLight.BakedMode);
            }    
            
            BakeResultAsset.BakedLightInfos.Add(BakedLightInfo);
        }
        
        public void AddBakedMeshInfo(MeshRenderer Renderer,int LightmapIndex,Vector4 LightmapScaleOffset)
        {
            // Record baked mesh result
            DawnBakeResultAsset.DawnMeshInstanceInfo BakedMeshInstanceInfo = new DawnBakeResultAsset.DawnMeshInstanceInfo();
            BakedMeshInstanceInfo.LightmapIndex = LightmapIndex;
            BakedMeshInstanceInfo.LightmapOffset = LightmapScaleOffset;

            // Use world space mesh vertices to calculate mesh guid
            BakedMeshInstanceInfo.MeshInstanceID = GUID.CreateGUID(Renderer);

            BakeResultAsset.MeshInfos.Add(BakedMeshInstanceInfo);
        }

        public void AddBakedLandscapeInfo(Terrain Landscape, int LightmapIndex, Vector4 LightmapScaleOffset)
        {
            // Record baked mesh result
            DawnBakeResultAsset.DawnLandscapeInfo BakedLandscapeInfo = new DawnBakeResultAsset.DawnLandscapeInfo();
            BakedLandscapeInfo.LandscapeIndex = LightmapIndex;
            BakedLandscapeInfo.LandscapeOffset = LightmapScaleOffset;

            // Use world space mesh vertices to calculate mesh guid
            BakedLandscapeInfo.LandscapeID = GUID.CreateGUID(Landscape);
            BakeResultAsset.Landscapes.Add(BakedLandscapeInfo);
        }

        public void AddBakedLightProbeInfo(List<SphericalHarmonicsL2> ProbeCeffs, List<Vector3> ProbePositions, List<DawnBakeResultAsset.DawnProbeOcclusion> ProbeOcclusions)
        {
            if (ProbeCeffs.Count != ProbePositions.Count)
            {
                DawnDebug.LogFormat("Add Baked LightProbe info failed, ProbeCeffs count is {0}, ProbePosition count is {1}",
                    ProbeCeffs.Count, ProbePositions.Count);
                return;
            }

            for (int i = 0; i < ProbePositions.Count; ++i)
            {
                BakeResultAsset.BakedLightProbePositions.Add(ProbePositions[i]);
                BakeResultAsset.BakedLightProbeCeffs.Add(new DawnBakeResultAsset.DawnProbeCeff(ProbeCeffs[i]));
                BakeResultAsset.BakedLightProbeOcclusions.Add(ProbeOcclusions[i]);
            }
        }

        public void SetSkyReflectionCubemap(Cubemap InCubemap)
        {
            BakeResultAsset.SkyReflectionCubemap = InCubemap;
        }

        private bool TryDeleteFile(string FileToDelete)
        {
			if(!AssetDatabase.DeleteAsset(FileToDelete))
            {
                try
                {
                    File.Delete(FileToDelete);
                }
                catch
                {
                    DawnDebug.LogErrorFormat("Fail to delete {0}", FileToDelete);
                    return false;
                }
            }

            return true;
        }

        private bool ExportTextureToFile(Texture2D Texture, string ExportPath)
        {
			DawnProfiler.BeginSample ("ExportTextureToFile.CheckFile");
            if (File.Exists(ExportPath) && !TryDeleteFile(ExportPath))
            {
				DawnProfiler.EndSample ();
                return false;
            }
			DawnProfiler.EndSample ();

            byte[] TextureData = null;
            string TextureExtension = ExportPath.Split('.').Last();
            if (TextureExtension.Equals("exr"))
            {
                TextureData = Texture.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat);
            }
            else if (TextureExtension.Equals("jpg"))
            {
                TextureData = Texture.EncodeToJPG();
            }
            else if (TextureExtension.Equals("png"))
            {
                TextureData = Texture.EncodeToPNG();
            }
            else
            {
                DawnDebug.LogFormat("[WARN] Unsupported export texture format: {0}", TextureExtension.Length == 0 ? "Unknown" : TextureExtension);
                return false;
            }
            
            if (TextureData == null)
            {
                // Unknown reason, exit
                DawnDebug.LogFormat("[WARN] Can not export texture: {0} to Path: {1}", Texture, ExportPath);
                return false;
            }
			DawnProfiler.BeginSample ("ExportTextureToFile.WriteAllBytes");
            File.WriteAllBytes(ExportPath, TextureData);
            AssetDatabase.ImportAsset(ExportPath);
            DawnProfiler.EndSample ();
            return true;
        }

        private Texture ExportTextureToFile(Cubemap InCubemap, string ExportPath)
        {
            if(AssetDatabase.Contains(InCubemap))
            {
                var ExistPath = AssetDatabase.GetAssetPath(InCubemap);
                if(ExistPath!= ExportPath)
                {
                    if(File.Exists(ExportPath))
                    {
                        AssetDatabase.DeleteAsset(ExportPath);
                    }
                    AssetDatabase.MoveAsset(ExistPath, ExportPath);
                }
                return AssetDatabase.LoadAssetAtPath<Cubemap>(ExportPath);
            }
            var NewCubemap = TextureUtils.ExpendCubemap(InCubemap);
            var ImageData = NewCubemap.EncodeToEXR();
            File.WriteAllBytes(ExportPath, ImageData);
            AssetDatabase.ImportAsset(ExportPath);
            return NewCubemap;
        }

        public bool Export(bool bExportHDRLightmap,List<LightmapData> LightmapDatas)
        {
            var BakePathSetting = DawnBakePathSetting.GetInstance(UnityScene);

            string ExportFolder = BakePathSetting.BakeResultFolderPath(true); 

			string BakeResultAssetPath = BakePathSetting.DawnBakeResultAssetPath();

            string LightmapTextureExtension = bExportHDRLightmap ? ".exr" : ".jpg";

            string ShadowMaskTextureExtension = ".png";

            string DirectionalLightmapTextureExtension = ".png";

            for (int LightmapIndex = 0;LightmapIndex < BakeResultAsset.BakedLightmaps.Count;++LightmapIndex)
            {
                int GlobalLightmapIndex = LightmapIndex + LightmapDatas.Count;

                {
                    var BakedLightmapInfo = BakeResultAsset.BakedLightmaps[LightmapIndex];

                    var TexturePath = BakePathSetting.DawnBakeLightmapPath(GlobalLightmapIndex, BakedLightmapInfo.name, LightmapTextureExtension);

                    if (!ExportTextureToFile(BakedLightmapInfo, TexturePath))
                    {
                        DawnDebug.LogFormat("[WARN] Export Bake lightmap to {0} failed, exit...", TexturePath);
                    }
                    BakeResultAsset.BakedLightmaps[LightmapIndex] = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
                }

                if(BakeResultAsset.BakedShadowMasks.Count == BakeResultAsset.BakedLightmaps.Count)
                {
                    var BakedShadowMaskInfo = BakeResultAsset.BakedShadowMasks[LightmapIndex];

                    var TexturePath = BakePathSetting.DawnBakeShadowMaskPath(GlobalLightmapIndex, BakedShadowMaskInfo.name, ShadowMaskTextureExtension);

                    if (!ExportTextureToFile(BakedShadowMaskInfo, TexturePath))
                    {
                        DawnDebug.LogFormat("[WARN] Export Bake shadowmask to {0} failed, exit...", TexturePath);
                    }

                    BakeResultAsset.BakedShadowMasks[LightmapIndex] = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
                }

                if (BakeResultAsset.BakedDirectionalLightmaps.Count == BakeResultAsset.BakedLightmaps.Count)
                {
                    var DirectionalLightmap2DInfo = BakeResultAsset.BakedDirectionalLightmaps[LightmapIndex];

                    var TexturePath = BakePathSetting.DawnBakeDirectionLMPath(GlobalLightmapIndex, DirectionalLightmap2DInfo.name, DirectionalLightmapTextureExtension);

                    if (!ExportTextureToFile(DirectionalLightmap2DInfo, TexturePath))
                    {
                        DawnDebug.LogFormat("[WARN] Export Bake shadowmask to {0} failed, exit...", TexturePath);
                    }

                    BakeResultAsset.BakedDirectionalLightmaps[LightmapIndex] = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
                }
            }

            if(BakeResultAsset.SkyReflectionCubemap !=null)
            {
                var ReflectionProbePath = DawnBakePathSetting.GetInstance().DawnReflectionProbePath(0, false);
                BakeResultAsset.SkyReflectionCubemap = ExportTextureToFile(BakeResultAsset.SkyReflectionCubemap as Cubemap, ReflectionProbePath);
            }

            BakeResultAsset.BakedAmbientProbe = new DawnBakeResultAsset.DawnProbeCeff(RenderSettings.ambientProbe,
               RenderSettings.ambientIntensity > 0.0f ? 1.0f / Mathf.GammaToLinearSpace(RenderSettings.ambientIntensity) : 0.0f);

            if (File.Exists(BakeResultAssetPath))
            {
                TryDeleteFile(BakeResultAssetPath);
            }       

            GetLightmapData(BakeResultAsset, LightmapDatas);

            BakeResultAsset.name = UnityScene.name;
            BakeResultAsset.lightmapsMode = LightmapSettings.lightmapsMode;
            LightmapSettings.lightmaps = LightmapDatas.ToArray();

            AssetDatabase.CreateAsset(BakeResultAsset, BakeResultAssetPath);
            AssetDatabase.SaveAssets();
            
            DawnDebug.LogFormat("Dawn export baking result to folder: {0}", ExportFolder);
            return true;
        }

        private bool ImportTexture(ref Texture2D OutTexture, string ImportTexturePath)
        {
            if (!File.Exists(ImportTexturePath))
            {
                return false;
            }

            // Load Image interface is only used to handle jpg/png texture
            // AssetDatabase.LoadAssetAtPath method load png picture seems causing error pixel value...
            string TextureExtension = ImportTexturePath.Split('.').Last();
            switch (TextureExtension)
            {
                case "jpg":
                case "png":
                {
                    return OutTexture.LoadImage(File.ReadAllBytes(ImportTexturePath));
                }

                case "exr":
                {
                    return OutTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(ImportTexturePath);
                }
            }
            
            return false;
        }
        
        public bool Import(List<LightmapData> LightmapDatas)
        {
			string BakeResultAssetPath = DawnBakePathSetting.GetInstance(UnityScene).DawnBakeResultAssetPath();

            if (!File.Exists(BakeResultAssetPath))
            {
                DawnDebug.Log(string.Format("Cannot find bake result file: {0}", BakeResultAssetPath));
                return false;
            }
            
            DawnBakeResultAsset ImportAsset = AssetDatabase.LoadMainAssetAtPath(BakeResultAssetPath) as DawnBakeResultAsset;
            if (null == ImportAsset)
            {
                DawnDebug.Log(string.Format("Cannot load bake result file: {0}", BakeResultAssetPath));
                return false;
            }

            int BaseLightmapIndex = LightmapDatas.Count;
            GetLightmapData(ImportAsset, LightmapDatas);

            LightmapSettings.lightmaps = LightmapDatas.ToArray();
            LightmapSettings.lightmapsMode = ImportAsset.lightmapsMode;

            DawnDebug.LogFormat("LightmapDatas:{0}", LightmapDatas.Count);

            var RootObjects = UnityScene.GetRootGameObjects();

            foreach(var RootObject in RootObjects)
            {

                var AllMeshRenderers = RootObject.GetComponentsInChildren<MeshRenderer>();

                foreach (var MeshRenderer in AllMeshRenderers)
                {
                    //TODO Use Right Lodindex!!!
                    var GameObjectID = GUID.CreateGUID(MeshRenderer);

                    DawnBakeResultAsset.DawnMeshInstanceInfo Result = ImportAsset.MeshInfos.Find(Info =>
                        Info.MeshInstanceID.Equals(GameObjectID));

                    if (!GameObjectID.Equals(Result.MeshInstanceID))
                    {
                        continue;
                    }

                    MeshRenderer.lightmapIndex = Result.LightmapIndex + BaseLightmapIndex;
                    MeshRenderer.lightmapScaleOffset = Result.LightmapOffset;

                    DawnDebug.LogFormat("lightmapIndex:{0}/{1} = {2}", MeshRenderer.gameObject.scene.name, MeshRenderer.name, MeshRenderer.lightmapIndex);
                }

                var AllLandscapes = RootObject.GetComponentsInChildren<Terrain>();

                foreach (var Landscape in AllLandscapes)
                {
                    var LandscapeID = GUID.CreateGUID(Landscape);

                    DawnBakeResultAsset.DawnLandscapeInfo Result = ImportAsset.Landscapes.Find(Info =>
                        Info.LandscapeID.Equals(LandscapeID));

                    if (!LandscapeID.Equals(Result.LandscapeID))
                    {
                        continue;
                    }

                    Landscape.lightmapIndex = Result.LandscapeIndex + BaseLightmapIndex;
                    Landscape.lightmapScaleOffset = Result.LandscapeOffset;
                }

                if (ImportAsset.BakedLightInfos.Count > 0)
                {
                    var AllLights = RootObject.GetComponentsInChildren<Light>();
                    foreach (var Light in AllLights)
                    {
                        var LightGUID = GUID.CreateGUID(Light);

                        var MatchLightInfo = ImportAsset.BakedLightInfos.Find(
                            LightInfo => LightInfo.LightID.Equals(LightGUID));

                        if (!LightGUID.Equals(MatchLightInfo.LightID))
                        {
                            continue;
                        }

                        MatchLightInfo.LightBakedData.ApplyBakedData(Light);
                    }
                }
            }

            RenderSettings.ambientProbe = ImportAsset.BakedAmbientProbe.ToSH();
            
            return true;
        }

        public int GetLightmapData(DawnBakeResultAsset ImportAsset, List<LightmapData> LightmapDatas)
        {
            for (int i = 0; i < ImportAsset.BakedLightmaps.Count; ++i)
            {
                LightmapData Data = new LightmapData();
                Data.lightmapColor = ImportAsset.BakedLightmaps[i];

                if (ImportAsset.BakedShadowMasks.Count > i)
                {
                    Data.shadowMask = ImportAsset.BakedShadowMasks[i];
                }

                if (ImportAsset.BakedDirectionalLightmaps.Count > i)
                {
                    Data.lightmapDir = ImportAsset.BakedDirectionalLightmaps[i];
                }

                LightmapDatas.Add(Data);
            }
            return ImportAsset.BakedLightmaps.Count;
        }
    }
}