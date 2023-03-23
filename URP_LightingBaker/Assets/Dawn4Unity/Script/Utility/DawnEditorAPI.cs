using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GPUBaking
{
    public partial class DawnEditorAPI
    {
        public delegate void StartBakingDelegate();

        public delegate void StopBakingDelegate();

        public delegate bool LightingCompleteDelegate(bool bSuccessed);

        public static StartBakingDelegate StartBaking;

        public static StopBakingDelegate StopBaking;

        public static StartBakingDelegate BakeReflectionProbe;

        public static LightingCompleteDelegate OnCompleteEvent;
    }

    public partial class DawnEditorAPI
    {
        public delegate void ClearAllBakedResultDelegate();

        public delegate bool ExportBakedResultDelegate();

        public delegate bool ImportBakedResultDelegate();

        public static ClearAllBakedResultDelegate ClearAllBakedResult;

        public static ExportBakedResultDelegate ExportBakedResult;

        public static ImportBakedResultDelegate ImportBakedResult;
    }

    public partial class DawnEditorAPI
    {
        public delegate bool ConvertLightingDataDelegate();

        public delegate void ClearLightingDataDelegate();

        public static ConvertLightingDataDelegate ConvertLightingData;

        public static ClearLightingDataDelegate ClearLightingData;
    }
}


