using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

namespace UnityEngine.Rendering.Universal
{
    [Serializable, VolumeComponentMenuForRenderPipeline("Weather/Rain", typeof(UniversalRenderPipeline))]
    public sealed class Rain : VolumeComponent, IPostProcessComponent
    {
        public ClampedFloatParameter RainIntensity = new ClampedFloatParameter(0.0f, 0.0f, 1.0f);

        [Reload("Models/Weather/RainModel.mesh")]
        public Mesh rainDome;

        [Reload("Models/Weather/RainSplash.mesh")]
        public Mesh rainSplashMesh;
        
        public Texture2DParameter RainTexture = new Texture2DParameter(null);

        public ClampedFloatParameter RainTextureScale = new ClampedFloatParameter(7.0f, 0.1f, 10.0f);

        public ClampedFloatParameter RainSpeed = new ClampedFloatParameter(15.0f, 0.0f, 50.0f);
        
        public FloatRangeParameter RainRange = new FloatRangeParameter(new Vector2(0.0f, 39.0f), 0.0f, 200.0f);

        public BoolParameter EnableRipple = new BoolParameter(false);

        public Texture2DParameter RippleSheetTexture = new Texture2DParameter(null);

        public ClampedFloatParameter RippleIntensity = new ClampedFloatParameter(5.0f, 0.0f, 10.0f);

        public ClampedFloatParameter RippleSize = new ClampedFloatParameter(1.0f, 0.1f, 10.0f);

        public BoolParameter EnableSplash = new BoolParameter(false);
        
        public Texture2DParameter SplashSheetTexture = new Texture2DParameter(null);

        public ClampedFloatParameter SplashRange = new ClampedFloatParameter(1.0f, 0.1f, 5.0f);
        public ClampedFloatParameter SplashSize = new ClampedFloatParameter(0.5f, 0.1f, 1.0f);
        
        public BoolParameter EnableMask = new BoolParameter(false);
        
        protected override void OnEnable()
        {
            base.OnEnable();
#if UNITY_EDITOR
            ResourceReloader.TryReloadAllNullIn(this, UniversalRenderPipelineAsset.packagePath);
            String packagePath = UniversalRenderPipelineAsset.packagePath;
            RainTexture.Override(AssetDatabase.LoadAssetAtPath<Texture2D>(packagePath + "/Textures/Weather/RainTexture.tga"));
            RippleSheetTexture.Override(AssetDatabase.LoadAssetAtPath<Texture2D>(packagePath + "/Textures/Weather/RippleSheet.TGA"));
            SplashSheetTexture.Override(AssetDatabase.LoadAssetAtPath<Texture2D>(packagePath + "/Textures/Weather/RainSplash.TGA"));
#endif
        }
        public bool IsActive() => RainIntensity.value > 0.0f;

        public bool IsTileCompatible() => false;
    }

}