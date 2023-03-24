using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEngine.Rendering.Universal
{
    public class RainPass : ScriptableRenderPass
    {
        private Material m_RainMaterial;

        private static readonly int m_RainTexID = Shader.PropertyToID("_RainTex");
        private static readonly int m_RainParams = Shader.PropertyToID("_RainParams");
        private static readonly int m_RainLayerParams = Shader.PropertyToID("_RainLayerParams");
        private static readonly int m_RainRippleSheet = Shader.PropertyToID("_RainRippleSheet");
        private static readonly int m_RainSplashSheet = Shader.PropertyToID("_RainSplashSheet");
        private static readonly int m_RainSplashParams = Shader.PropertyToID("_RainSplashParams");

        private static readonly ProfilingSampler m_Profiling = new ProfilingSampler("RainAndSnow");

        /// <summary>
        /// Create the CopyColorPass
        /// </summary>
        public RainPass(RenderPassEvent evt, Material rainMaterial)
        {
            base.profilingSampler = new ProfilingSampler(nameof(RainPass));

            m_RainMaterial = rainMaterial;
            renderPassEvent = evt;
            base.useNativeRenderPass = false;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            CoreUtils.SetKeyword(cmd, ShaderKeywordStrings._RAIN, true);
            Rain rain = VolumeManager.instance.stack.GetComponent<Rain>();
            var useMask = rain.EnableMask.value;
            cmd.SetGlobalVector(m_RainParams, new Vector4(rain.RainIntensity.value,  rain.EnableRipple.value ? rain.RippleIntensity.value : 0, 1.0f / rain.RippleSize.value, useMask ? 1.0f : 0.0f));
        }

        private static readonly float splashModelHalfBoundSize = 20.0f;

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (m_RainMaterial == null)
            {
                Debug.LogErrorFormat("Missing {0}. {1} render pass will not execute. Check for missing reference in the renderer resources.", m_RainMaterial, GetType().Name);
                return;
            }

            Rain rain = VolumeManager.instance.stack.GetComponent<Rain>();

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_Profiling))
            {
                var useMask = rain.EnableMask.value;
                var LayerParams = new Vector4(rain.RainTextureScale.value, rain.RainSpeed.value * rain.RainTextureScale.value,
                    (rain.RainRange.value.y - rain.RainRange.value.x) * 0.5f, rain.RainRange.value.x);
                cmd.SetGlobalTexture(m_RainTexID, rain.RainTexture.value);
                cmd.SetGlobalTexture(m_RainRippleSheet, rain.RippleSheetTexture.value);
                cmd.SetGlobalVector(m_RainLayerParams, LayerParams);
                var cameraTransfrom = renderingData.cameraData.camera.transform;
                CoreUtils.SetKeyword(m_RainMaterial, "_USE_MASK", useMask);
                cmd.DrawMesh(rain.rainDome, Matrix4x4.Translate(cameraTransfrom.position), m_RainMaterial, 0, 0);

                if (rain.EnableSplash.value && rain.EnableMask.value)
                {
                    cmd.SetGlobalTexture(m_RainSplashSheet, rain.SplashSheetTexture.value);
                    cmd.SetGlobalVector(m_RainSplashParams, new Vector4(rain.SplashSize.value, rain.SplashRange.value, splashModelHalfBoundSize * rain.SplashRange.value, 0));
                    cmd.DrawMesh(rain.rainSplashMesh, Matrix4x4.TRS(new Vector3(cameraTransfrom.position.x, 0, cameraTransfrom.position.z), Quaternion.identity, Vector3.one * rain.SplashRange.value), m_RainMaterial, 0, 1);
                }
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        /// <inheritdoc/>
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            CoreUtils.SetKeyword(cmd, ShaderKeywordStrings._RAIN, false);
        }
    }
}
