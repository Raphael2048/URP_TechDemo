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
    [InitializeOnLoad]
    public class DawnStorage
    {
        internal static DawnBakingContext BakedContext;

        private static Scene ActivateScene;

        public static DawnSceneBakeResult GetSceneLightingData(Scene InScene)
        {
            return BakedContext.GetSceneLightingData(InScene).SceneBakeResult;
        }

        public static DawnSceneBakeResult GetSceneLightingData()
        {
            return GetSceneLightingData(EditorSceneManager.GetActiveScene());
        }


        static DawnStorage()
        {
            //EditorApplication.update += OnUpdate;
            EditorSceneManager.sceneOpened += OnSceneLoaded;
        }

		public static void ClearAllBakedResult()
		{
            if(BakedContext!=null)
            {
                BakedContext.InitSceneLightings();
            }
        }

        public static bool ImportBakeResult()
        {
            return ImportBakeResult(OpenSceneMode.Single);
        }

        public static bool ImportBakeResult(OpenSceneMode Mode)
        {
            if (BakedContext == null)
            {
                BakedContext = new DawnBakingContext();
            }

            BakedContext.InitSceneLightings();

            List<LightmapData> LightmapDatas = new List<LightmapData>();
            
            bool bSuccessed = true;           
            for (int SceneIndex = 0; bSuccessed && SceneIndex < EditorSceneManager.sceneCount;++SceneIndex)
            {
                var Scene = EditorSceneManager.GetSceneAt(SceneIndex);
                if (!Scene.isLoaded) {
                    continue;
                }
                var SceneLighting = GetSceneLightingData(Scene);
                bSuccessed = bSuccessed && SceneLighting.Import(LightmapDatas);
            }
            return bSuccessed;
        }

        public static bool ExportBakeResult()
        {
            bool bSuccessed = true;
            List<LightmapData> LightmapDatas = new List<LightmapData>();
            foreach (var SceneLighting in BakedContext.SceneLightingData)
            {
                if (!SceneLighting.scene.isLoaded)
                {
                    continue;
                }
                bSuccessed = SceneLighting.SceneBakeResult.Export(Dawn4Unity.GetLightingSetting().LightmapSettings.bUseHDRLightmap, LightmapDatas);
                if (!bSuccessed)
                {
                    break;
                }
            }
            return bSuccessed;
        }

        public static void SetSkyReflectionCubemap(Cubemap InCubemap)
        {
            foreach (var SceneLighting in BakedContext.SceneLightingData)
            {
                SceneLighting.SceneBakeResult.SetSkyReflectionCubemap(InCubemap);
            }
        }

        private static void OnUpdate()
        {
            if (SceneManager.GetActiveScene().IsValid() && 
                ActivateScene != SceneManager.GetActiveScene() && 
                !EditorApplication.isPlaying)
            {
                // Only handle non-play mode scene, enter play mode scene will be handled by DawnStorageBuildProcessor class
                ActivateScene = SceneManager.GetActiveScene();
                ImportBakeResult();
            }
        }

        private static void OnSceneLoaded(Scene scene, OpenSceneMode mode)
        {
            Debug.LogFormat("Load:{0}", scene.path);
            if (scene.name == "DawnTempScene")
            {
                return;
            }
            if (mode == OpenSceneMode.AdditiveWithoutLoading)
            {
                return;
            }
            if (mode == OpenSceneMode.Single)
            {
                ActivateScene = scene;
            }
            ImportBakeResult(mode);
        }

        [MenuItem("Dawn/Tools/Debug/ImportAndExport/ImportBakeResult")]
        static void ImportBakeToFolder()
        {
            ImportBakeResult();
        }

		[MenuItem("Dawn/Tools/Debug/ImportAndExport/ExportBakeResult")]
        static void ExportBakeToFolder()
        {
            ExportBakeResult();
        }
    }
}