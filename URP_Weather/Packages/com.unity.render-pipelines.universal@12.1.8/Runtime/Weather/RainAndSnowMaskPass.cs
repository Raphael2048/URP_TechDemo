using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEngine.Rendering.Universal
{
    public class RainAndSnowMaskPass : ScriptableRenderPass
    {
        
        private static readonly int m_RainSnowHeightMap = Shader.PropertyToID("_RainSnowHeightMap");
        private static readonly int m_RainSnowHeightMapDepth = Shader.PropertyToID("_RainSnowHeightMapDepth");
        private static readonly int m_RainSnowHeightMapTemp = Shader.PropertyToID("_RainSnowHeightMapTemp");
        private static readonly int m_RainSnowHeightMapMatrix = Shader.PropertyToID("_RainSnowHeightMapMatrix");
        private static readonly int m_RainSnowHeightMapInvMatrix = Shader.PropertyToID("_RainSnowHeightMapInvMatrix");
        private static readonly ProfilingSampler m_Profiling = new ProfilingSampler("RainAndSnowMask");
        private RenderTexture m_HeightMap;
        private Material m_Material;
        private ShaderTagId m_ShaderTagId = new ShaderTagId("ShadowCaster");
        private FilteringSettings m_FilteringSettings;
        
        private static readonly float m_HalfSize = 100f;
        private static readonly float m_HeightBegin = 1.0f;
        private static readonly float m_HeightEnd = 20.0f;
        // Go Away Distance to trigger refresh
        private static readonly float m_RefreshDistance = 20.0f;
        private static readonly int m_ShadowMapResolution = 512;
        private static readonly bool USE_CACHE = true;
        private Vector2 m_LastPos = new Vector2(float.MaxValue, float.MaxValue);
        public RainAndSnowMaskPass(RenderPassEvent evt, Material material)
        {
            base.profilingSampler = new ProfilingSampler(nameof(RainAndSnowMaskPass));
            renderPassEvent = evt;
            base.useNativeRenderPass = false;
            m_FilteringSettings = new FilteringSettings(RenderQueueRange.all);
            m_Material = material;
        }

        public bool Setup(ref CameraData cameraData)
        {
            if (!USE_CACHE)
            {
                return true;
            }
            else
            {
                var position = cameraData.camera.transform.position;
                if (Vector2.Distance(m_LastPos, new Vector2(position.x, position.z)) > m_RefreshDistance)
                {
                    m_LastPos = new Vector2(position.x, position.z);
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
        
        bool FetchOrCreate(ref RenderTexture rt, RenderTextureDescriptor descriptor)
        {
            if (rt == null || rt.width != descriptor.width || rt.height != descriptor.height ||
                rt.volumeDepth != descriptor.volumeDepth)
            {
                if (rt) RenderTexture.ReleaseTemporary(rt);
                rt = RenderTexture.GetTemporary(descriptor);
                if (!rt.IsCreated()) rt.Create();
                return true;
            }
            return false;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var format = GraphicsFormatUtility.GetDepthStencilFormat(32, 0);
            RenderTextureDescriptor rtd = new RenderTextureDescriptor(m_ShadowMapResolution, m_ShadowMapResolution, GraphicsFormat.None, format);
            rtd.shadowSamplingMode = ShadowSamplingMode.CompareDepths;
            cmd.GetTemporaryRT(m_RainSnowHeightMapDepth, rtd);

            rtd.depthStencilFormat = GraphicsFormat.None;
            rtd.graphicsFormat = GraphicsFormat.R16G16_UNorm;
            cmd.GetTemporaryRT(m_RainSnowHeightMapTemp, rtd);
            FetchOrCreate(ref m_HeightMap, rtd);
            // ConfigureTarget( new RenderTargetIdentifier(m_RainSnowHeightMapDepth), m_RainSnowHeightMapDepth.depthStencilFormat);
            ConfigureTarget(m_RainSnowHeightMapDepth, m_RainSnowHeightMapDepth);
            ConfigureClear(ClearFlag.All, Color.black);
        }

        Vector4 GetShadowmapBias(Matrix4x4 projMatrix, int shadowmapSize)
        {
            float frustumSize = 2.0f / projMatrix.m00;
            float texelSize = frustumSize / shadowmapSize;
            float depthBias = -texelSize;
            float normalBias = - texelSize;
            const float kernelRadius = 5.0f;
            depthBias *= kernelRadius;
            normalBias *= kernelRadius;
            return new Vector4(depthBias, normalBias, 0.0f, 0.0f);
        }

        
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cameraPosition = renderingData.cameraData.camera.transform.position;
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_Profiling))
            {
                Camera cullingCamera = RainAndSnowMaskCamera.GetInstanceCamera();
                cullingCamera.orthographic = true;
                cullingCamera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                cullingCamera.transform.position = new Vector3(cameraPosition.x, m_HeightEnd, cameraPosition.z);
                cullingCamera.orthographicSize = m_HalfSize;
                cullingCamera.nearClipPlane = 0;
                cullingCamera.farClipPlane = m_HeightEnd - m_HeightBegin;
                cullingCamera.aspect = 1.0f;
                cullingCamera.TryGetCullingParameters(out ScriptableCullingParameters parameters);
                var cullResults = context.Cull(ref parameters);
                
                var projMatrix = Matrix4x4.Ortho(-m_HalfSize, m_HalfSize, -m_HalfSize, m_HalfSize, 0, m_HeightEnd - m_HeightBegin);
                var lookAtBeginPos = new Vector3(cameraPosition.x, m_HeightEnd, cameraPosition.z);
                var viewMatrix = Matrix4x4.LookAt(lookAtBeginPos, lookAtBeginPos + Vector3.down, Vector3.forward);
                viewMatrix.SetColumn(2, -viewMatrix.GetColumn(2));
                viewMatrix = viewMatrix.inverse;
                var transformMatrix = ShadowUtils.GetShadowTransform(projMatrix, viewMatrix);
                cmd.SetGlobalMatrix(m_RainSnowHeightMapMatrix, transformMatrix);
                cmd.SetGlobalMatrix(m_RainSnowHeightMapInvMatrix, transformMatrix.inverse);
                cmd.SetViewProjectionMatrices(viewMatrix, projMatrix);
                cmd.SetGlobalVector("_LightDirection", Vector3.up);
                cmd.SetGlobalVector("_ShadowBias", GetShadowmapBias(projMatrix, m_ShadowMapResolution));
                SortingSettings sortingSettings = new SortingSettings(renderingData.cameraData.camera) { criteria = renderingData.cameraData.defaultOpaqueSortFlags };
                DrawingSettings drawingSettings = new DrawingSettings(m_ShaderTagId, sortingSettings)
                {
                    perObjectData = 0,
                    mainLightIndex = 1,
                    enableDynamicBatching = renderingData.supportsDynamicBatching,
                    // Disable instancing for preview cameras. This is consistent with the built-in forward renderer. Also fixes case 1127324.
                    enableInstancing = renderingData.cameraData.camera.cameraType == CameraType.Preview ? false : true,
                };
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                context.DrawRenderers(cullResults, ref drawingSettings, ref m_FilteringSettings);
                
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                 
                cmd.SetRenderTarget(m_RainSnowHeightMapTemp);
                cmd.DrawProcedural(Matrix4x4.identity, m_Material, 3, MeshTopology.Triangles, 3);
                cmd.SetRenderTarget(m_HeightMap);
                cmd.DrawProcedural(Matrix4x4.identity, m_Material, 4, MeshTopology.Triangles, 3);
                
                cmd.SetGlobalTexture(m_RainSnowHeightMap, m_HeightMap);

            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        /// <inheritdoc/>
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(m_RainSnowHeightMapDepth);
            cmd.ReleaseTemporaryRT(m_RainSnowHeightMapTemp);
        }

        public void Dispose()
        {
            RenderTexture.ReleaseTemporary(m_HeightMap);
        }
    }
}
