using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace UnityEngine.Rendering.Universal
{
    [Serializable, VolumeComponentMenuForRenderPipeline("Weather/Snow", typeof(UniversalRenderPipeline))]
    public sealed class Snow : VolumeComponent, IPostProcessComponent
    {
        public ClampedFloatParameter SnowIntensity = new ClampedFloatParameter(0.0f, 0.0f, 1.0f);
        public Texture2DParameter SnowMaskTexture = new Texture2DParameter(null);
        public ClampedFloatParameter SnowMaskTextureScale = new ClampedFloatParameter(1.0f, 0.2f, 5.0f);
        public BoolParameter EnableMask = new BoolParameter(false);

        public Texture2DParameter SnowTexture = new Texture2DParameter(null);
        public ClampedFloatParameter SnowDensity = new ClampedFloatParameter(5.0f, 0.0f, 10.0f);
        public ClampedFloatParameter JitterRadius = new ClampedFloatParameter(0.1f, 0.0f, 0.3f);
        public ClampedFloatParameter FallDownSpeed = new ClampedFloatParameter(0.2f, 0.05f, 3.0f);
        public FloatRangeParameter HeightRange = new FloatRangeParameter(new Vector2(0, 20.0f), -10.0f, 30.0f);
        
        protected override void OnEnable()
        {
            base.OnEnable();
#if UNITY_EDITOR
            String packagePath = UniversalRenderPipelineAsset.packagePath;
            SnowMaskTexture.Override(AssetDatabase.LoadAssetAtPath<Texture2D>(packagePath + "/Textures/Weather/SnowMasks.tga"));
#endif
        }
        public bool IsActive() => SnowIntensity.value > 0.0f;

        public bool IsTileCompatible() => false;
    }

}