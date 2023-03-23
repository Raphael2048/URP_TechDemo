using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEngine.Profiling;
namespace IrradianceVolume
{
    public class IrradianceVolumeRenderFeature : ScriptableRendererFeature
    {

        [Reload("Shaders/IrradianceVolumeInterpolation.compute"), HideInInspector]
        public ComputeShader cs;

        [Reload("Shaders/IrradianceVolumeDebug.shader"), HideInInspector]
        public Shader debugShader;

        public Material debugMaterial;
        
        private IrradianceVolumeRenderPass m_RenderPass;
        private IrradianceVolumeDebugRenderPass m_DebugPass;

        public override void Create()
        {
#if UNITY_EDITOR
            ResourceReloader.TryReloadAllNullIn(this, "Packages/com.unity.render-pipelines.universal");
#endif
            m_RenderPass = new IrradianceVolumeRenderPass(this);
            m_DebugPass = new IrradianceVolumeDebugRenderPass(this);
        }

        // Here you can inject one or multiple render passes in the renderer.
        // This method is called when setting up the renderer once per-camera.
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (debugMaterial == null)
            {
                debugMaterial = new Material(debugShader);
                debugMaterial.enableInstancing = true;
            }

            var ir = IrradianceVolumeManager.Instance;
            if (ir != null)
            {
                if ((ir.Octree != null) && renderingData.cameraData.renderType != CameraRenderType.Overlay)
                {
                    renderer.EnqueuePass(m_RenderPass);
#if UNITY_EDITOR
                    if(ir.Octree.Tree !=null && ir.DrawSamplePos)
                        renderer.EnqueuePass(m_DebugPass);
#endif
                }
            }
        }
    }

    public class IrradianceVolumeRenderPass : ScriptableRenderPass
    {
        private Vector4[] Params = new Vector4[10];

        private RenderTexture m_RT1, m_RT2;
        private IrradianceVolumeRenderFeature _feature;

        private RenderTextureDescriptor CreateRTDescritor(Vector3Int size)
        {
            RenderTextureDescriptor descriptor = new RenderTextureDescriptor();
            descriptor.width = size.x;
            descriptor.height = size.y;
            descriptor.volumeDepth = size.z;
            descriptor.dimension = TextureDimension.Tex3D;
            descriptor.colorFormat = RenderTextureFormat.ARGB32;
            descriptor.enableRandomWrite = true;
            descriptor.msaaSamples = 1;
            return descriptor;
        }

        public IrradianceVolumeRenderPass(IrradianceVolumeRenderFeature feature)
        {
            this.renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
            this._feature = feature;
        }
        
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            CoreUtils.SetKeyword(cmd, "_IRRADIANCE_VOLUME", true);
            var octree = IrradianceVolumeManager.Instance.Octree;
            var cs = _feature.cs;

            cmd.SetGlobalTexture("_IrradianceVolumeSHTexture1", octree.SHTexture1);
            cmd.SetGlobalTexture("_IrradianceVolumeSHTexture2", octree.SHTexture2);
            
            cmd.SetGlobalTexture("_IrradianceVolumeMappingTexture", octree.MappingTexture);
            octree.GetMappingTextureCoordParams(out var mul, out var add);
            Params[0] = (Vector3)mul;
            Params[0].w = octree.GetMappingTextureCoordToSHMainBlockTextureCoord();
            Params[1] = (Vector3)add;
            Params[2] = new Vector4(octree.MappingTextureSize.x, octree.MappingTextureSize.y, octree.MappingTextureSize.z);
            Params[3] = new Vector3(1.0f / octree.TextureSize.x, 1.0f / octree.TextureSize.y, 1.0f / octree.TextureSize.z);
            cmd.SetGlobalVectorArray("_IrradianceVolumeParams", Params);

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            CoreUtils.SetKeyword(cmd, "_IRRADIANCE_VOLUME", false);
        }
    }

    public class IrradianceVolumeDebugRenderPass : ScriptableRenderPass
    {
        GraphicsBuffer _indirectBuffer;
        private ComputeBuffer PositionsBuffer;
        private IrradianceVolumeRenderFeature _feature;
        
        public IrradianceVolumeDebugRenderPass(IrradianceVolumeRenderFeature feature)
        {
            _feature = feature;
            PositionsBuffer = new ComputeBuffer(1000000, sizeof(float) * 3, ComputeBufferType.Default);
            this.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            IrradianceVolumeManager manager = IrradianceVolumeManager.Instance;
            var positions = manager.Octree.GetAllDebugPositions(2);
            PositionsBuffer.SetData(positions);
            cmd.SetGlobalBuffer("_PositionsBuffer", PositionsBuffer);
            cmd.DrawMeshInstancedProcedural(RenderingUtils.sphereMesh, 0, _feature.debugMaterial, 0, positions.Length);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
