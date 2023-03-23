using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace H3D.URP
{
    internal class PlanarReflectionRendererFeature : ScriptableRendererFeature
    {


#if UNITY_EDITOR
        public static readonly string packagePath = "Packages/com.PlanarReflection";
#endif

        // For Usual
        [Reload("Shaders/UsualReflectionIntensity.shader"), HideInInspector]
        public Shader usualReflectionIntensityShader;
        
        // For PPR
        [Reload("Shaders/PPRProjection.compute"), HideInInspector]
        public ComputeShader pprProjectionComputeShader;
        [Reload("Shaders/PPRReflectionIntensity.shader"), HideInInspector]
        public Shader pprReflectionIntensityShader;

        // Common Filter
        [Reload("Shaders/PlanarReflectionFilter.shader"),HideInInspector]
        public Shader PlanarReflectionFilterShader;
        private PlanarReflectionRenderPass m_RenderPass;

        public override void Create()
        {
#if UNITY_EDITOR
            ResourceReloader.TryReloadAllNullIn(this, packagePath);
#endif
            m_RenderPass = new PlanarReflectionRenderPass(this);
        }

        // Here you can inject one or multiple render passes in the renderer.
        // This method is called when setting up the renderer once per-camera.
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if ((ReflectionPlane.Instance?.Configurable() ?? false) &&
                renderingData.cameraData.renderType != CameraRenderType.Overlay)
            {
                renderer.EnqueuePass(m_RenderPass);
            }
        }

    }
}
