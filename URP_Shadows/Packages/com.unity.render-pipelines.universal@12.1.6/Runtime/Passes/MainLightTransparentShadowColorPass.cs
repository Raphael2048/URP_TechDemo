using System;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal.Internal
{
    /// <summary>
    /// Renders a shadow map for the main Light.
    /// </summary>
    public class MainLightTransparentShadowColorPass : ScriptableRenderPass
    {
        int m_ShadowmapWidth;
        RenderTargetHandle m_MainLightColorShadowmap, m_MainLightShadowmap;
        const string m_ProfilerTag = "Render MainLight Transparent Shadowmap";
        ProfilingSampler m_ProfilingSampler = new ProfilingSampler(m_ProfilerTag);

        public MainLightTransparentShadowColorPass(RenderPassEvent evt)
        {
            renderPassEvent = evt;
            m_MainLightColorShadowmap.Init("_MainLightColorShadowmapTexture");
            m_MainLightShadowmap.Init("_MainLightShadowmapTexture");
        }

        public bool Setup(ref RenderingData renderingData)
        {
            m_ShadowmapWidth = renderingData.shadowData.mainLightShadowmapWidth;
            if (renderingData.lightData.mainLightCastTransparentShadow) return true;
            return false;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            cmd.GetTemporaryRT(m_MainLightColorShadowmap.id, m_ShadowmapWidth, m_ShadowmapWidth, 0, FilterMode.Bilinear, GraphicsFormat.R16_UNorm);
            // Shadowmap Already Allocated
            ConfigureTarget(m_MainLightColorShadowmap.Identifier(), m_MainLightShadowmap.Identifier());
            ConfigureClear(ClearFlag.Color, Color.white);
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            int shadowLightIndex = renderingData.lightData.mainLightIndex;
            if (shadowLightIndex == -1)
                return;
            var pso = ProjectionShadowObject.Instance;
            if (pso == null) return;
            
            CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                pso.DrawTransparentShadowMap(cmd);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        /// <inheritdoc/>
        public override void FrameCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
                throw new ArgumentNullException("cmd");
            
            cmd.ReleaseTemporaryRT(m_MainLightColorShadowmap.id);
        }
    };
}
