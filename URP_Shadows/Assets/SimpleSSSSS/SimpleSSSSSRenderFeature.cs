using UnityEngine.Rendering.Universal;

namespace UnityEngine.Rendering.Universal
{
    public class SimpleSSSSSRenderFeature : ScriptableRendererFeature
    {
        [Reload("SimpleSSSSS/SimpleSSSSS.shader"), HideInInspector]
        public Shader shader;

        SimpleSSSSSPass _bloomPass;
        Material material;

        public override void Create()
        {
#if UNITY_EDITOR
            ResourceReloader.TryReloadAllNullIn(this, "Assets/");
#endif
            _bloomPass = new SimpleSSSSSPass(this);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (shader == null)
            {
                Debug.LogWarningFormat("Missing Shader");
                return;
            }

            if (material == null)
            {
                material = new Material(shader);
            }

            var stack = VolumeManager.instance.stack;
            var ss = stack.GetComponent<SimpleSSSSSVolumeComponent>();
            if (ss.IsActive())
            {
                _bloomPass.Setup(ss);
                renderer.EnqueuePass(_bloomPass);
            }
        }

        internal class SimpleSSSSSPass : ScriptableRenderPass
        {
            private int blurId;
            private SimpleSSSSSRenderFeature feature;
            private SimpleSSSSSVolumeComponent _component;
            RenderTargetHandle m_TempH;
            RenderTargetHandle m_TempV;

            public SimpleSSSSSPass(SimpleSSSSSRenderFeature feature)
            {
                this.feature = feature;
                this.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
                m_TempH.Init("TEMP_H");
                m_TempV.Init("TEMP_V");
                blurId = Shader.PropertyToID("_Blur");
            }

            public void Setup(SimpleSSSSSVolumeComponent component)
            {
                this._component = component;
            }

            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                var OriginWidth = cameraTextureDescriptor.width;
                var OriginHeight = cameraTextureDescriptor.height;

                var width = OriginWidth >> 3;
                var height = OriginHeight >> 3;
                cmd.GetTemporaryRT(m_TempH.id, width, height, 0, FilterMode.Bilinear, cameraTextureDescriptor.graphicsFormat);
                cmd.GetTemporaryRT(m_TempV.id, width, height, 0, FilterMode.Bilinear, cameraTextureDescriptor.graphicsFormat);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var material = feature.material;
                var source = renderingData.cameraData.renderer.cameraColorTarget;
                var depth = renderingData.cameraData.renderer.cameraDepthTarget;
                {
                    
                    CommandBuffer cmd = CommandBufferPool.Get("SimpleSSSSS");
                    material.SetFloat("_Threshold", _component.Threshold.value);
                    material.SetFloat("_Amplify", _component.Intensity.value);
                    material.SetVector("_MaxColor", _component.MaxColor.value);

                    Blit(cmd, source, m_TempV.Identifier(), material, 0);
                    Blit(cmd, m_TempV.Identifier(), m_TempH.Identifier(), material, 1);
                    Blit(cmd, m_TempH.Identifier(), m_TempV.Identifier(), material, 2);

                    context.ExecuteCommandBuffer(cmd);
                    CommandBufferPool.Release(cmd);
                }

                {
                    CommandBuffer cmd = CommandBufferPool.Get("Draw");
                    cmd.SetGlobalTexture(blurId, m_TempV.Identifier());

                    if (depth == RenderTargetHandle.CameraTarget.Identifier())
                    {
                        cmd.SetRenderTarget(source);
                    }
                    else
                    {
                        cmd.SetRenderTarget(source, depth);
                    }
                    cmd.DrawProcedural(Matrix4x4.identity, material, 4, MeshTopology.Triangles, 3);

                    context.ExecuteCommandBuffer(cmd);
                    CommandBufferPool.Release(cmd);
                }
            }

            public override void FrameCleanup(CommandBuffer cmd)
            {
                cmd.ReleaseTemporaryRT(m_TempH.id);
                cmd.ReleaseTemporaryRT(m_TempV.id);
            }
        }
    }
}

