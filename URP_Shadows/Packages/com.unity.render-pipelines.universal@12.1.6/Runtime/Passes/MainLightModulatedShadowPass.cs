using System;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal.Internal
{
    /// <summary>
    /// Renders a shadow map for the main Light.
    /// </summary>
    public class MainLightModulatedShadowPass : ScriptableRenderPass
    {
        Material projectionMaterial;
        const string m_ProfilerTag = "Render MainLight Modulated Shadow";
        ProfilingSampler m_ProfilingSampler = new ProfilingSampler(m_ProfilerTag);
        public MainLightModulatedShadowPass(RenderPassEvent evt, Material material)
        {
            renderPassEvent = evt;
            projectionMaterial = material;
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var lightData = renderingData.lightData;
            int shadowLightIndex = lightData.mainLightIndex;
            if (shadowLightIndex == -1)
                return;

            VisibleLight shadowLight = lightData.visibleLights[shadowLightIndex];
            CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                Vector4 p = Vector4.zero;
                p.x = lightData.mainLightUsePCSSModulatedShadow ? 1.0f : 0.0f;
                p.y = lightData.mainLightModulatedShaodwFilterWidth;
                projectionMaterial.SetVector("_H3D_ModulatedShadowColor", lightData.mainLightModulatedShadowColor);
                projectionMaterial.SetVector("_H3D_ModulatedShadowParams", p);
                var pso = ProjectionShadowObject.Instance;
                if (pso == null) return;
                pso.DrawModulatedShadow(cmd, ref renderingData, projectionMaterial, true);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    };
}