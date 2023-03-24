using UnityEngine;
using UnityEngine.Diagnostics;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEngine.Rendering.Universal
{
    public class SnowPass : ScriptableRenderPass
    {
        private static readonly int m_SnowMasksTexID = Shader.PropertyToID("_SnowMasksTex");
        private static readonly int m_SnowParms = Shader.PropertyToID("_SnowParams");

        private static readonly ProfilingSampler m_Profiling = new ProfilingSampler("RainAndSnow");
        
        private Material m_SnowMaterial;
        private ComputeShader m_SnowPositionsComputeShader;
        private ComputeBuffer m_SnowIndirectDrawBuffer;
        private int m_SnowIndirectPositionsBuffer = Shader.PropertyToID("_SnowPositions");
        // 周围 8x8范围内下雪, 每个 tile 表示1x1范围内的雪
        private readonly int m_SnowTileCount = 64;

        /// <summary>
        /// Create the CopyColorPass
        /// </summary>
        public SnowPass(RenderPassEvent evt, ComputeShader snowPositions, Material material)
        {
            profilingSampler = new ProfilingSampler(nameof(SnowPass));
            renderPassEvent = evt;
            useNativeRenderPass = false;
            m_SnowMaterial = material;
            m_SnowPositionsComputeShader = snowPositions;
            m_SnowIndirectDrawBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
            uint[] args = new uint[5] { 6, 0, 0, 0, 0 };
            m_SnowIndirectDrawBuffer.SetData(args);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            CoreUtils.SetKeyword(cmd, ShaderKeywordStrings._SNOW, true);
            Snow snow = VolumeManager.instance.stack.GetComponent<Snow>();
            cmd.SetGlobalVector(m_SnowParms, new Vector4(snow.SnowIntensity.value, 0, 1.0f / (snow.SnowMaskTextureScale.value * 10.0f), snow.EnableMask.value ? 1.0f : 0.0f));
            cmd.SetGlobalTexture(m_SnowMasksTexID, snow.SnowMaskTexture.value);
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            Snow snow = VolumeManager.instance.stack.GetComponent<Snow>();

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_Profiling))
            {
                var format = SystemInfo.GetCompatibleFormat(GraphicsFormat.R32G32B32A32_SFloat, FormatUsage.Blend);
                var partilcesPerGroup = ((int)(snow.SnowIntensity.value * snow.SnowDensity.value * 100)) / 8 * 8;
                if (partilcesPerGroup > 0)
                {
                    RenderTextureDescriptor rtd = new RenderTextureDescriptor(m_SnowTileCount, partilcesPerGroup, format, 0);
                    rtd.enableRandomWrite = true;
                    cmd.GetTemporaryRT(m_SnowIndirectPositionsBuffer, rtd);

                    m_SnowPositionsComputeShader.SetBuffer(0, "_IndirectParams", m_SnowIndirectDrawBuffer);
                    cmd.DispatchCompute(m_SnowPositionsComputeShader, 0, 1, 1, 1);
                    
                    m_SnowPositionsComputeShader.SetBuffer(1, "_IndirectParams", m_SnowIndirectDrawBuffer);
                    var heightRange = snow.HeightRange.value;
                    var loopTimeRange = (heightRange.y - heightRange.x) / snow.FallDownSpeed.value;
                    var frustumPlanes = renderingData.cameraData.frustum.planes;
                    Vector4[] array = new Vector4[frustumPlanes.Length];
                    for (int i = 0; i < array.Length; ++i)
                    {
                        array[i] = frustumPlanes[i].normal;
                        array[i].w = frustumPlanes[i].distance;
                    }
                    m_SnowPositionsComputeShader.SetVectorArray("_FrustumPlanes", array);
                    m_SnowPositionsComputeShader.SetVector("_DistributionParams", new Vector4(heightRange.y, heightRange.y - heightRange.x, 1.0f / loopTimeRange, snow.JitterRadius.value));
                    cmd.DispatchCompute(m_SnowPositionsComputeShader, 1, m_SnowTileCount / 8, partilcesPerGroup / 8, 1);
                    
                    m_SnowMaterial.SetTexture("_SnowTexture", snow.SnowTexture.value);
                    cmd.DrawMeshInstancedIndirect(RenderingUtils.fullscreenMesh, 0, m_SnowMaterial, 2, m_SnowIndirectDrawBuffer);
                }
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        /// <inheritdoc/>
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            CoreUtils.SetKeyword(cmd, ShaderKeywordStrings._SNOW, false);
        }
    }
}
