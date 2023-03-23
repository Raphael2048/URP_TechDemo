using UnityEngine.Rendering.Universal;

namespace UnityEngine.Rendering.Universal
{
    public class LayeredBloomRenderFeature : ScriptableRendererFeature
    {
        [Range(0, 10)]
        public float Threshold = 0.5f;

        [Range(0, 8)]
        public float Intensity = 0;

        [Range(0, 3)]
        public float Layer0 = 0.02f;

        [Range(0, 3)]
        public float Layer1 = 0.04f;

        [Range(0, 3)]
        public float Layer2 = 0.06f;

        [Range(0, 3)]
        public float Layer3 = 0.08f;

        public Shader shader;

        LayeredBloomPass _bloomPass;
        Material material;

        public override void Create()
        {
            _bloomPass = new LayeredBloomPass(this);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var src = renderer.cameraColorTarget;

            if (shader == null)
            {
                Debug.LogWarningFormat("Missing Shader");
                return;
            }

            if (material == null)
            {
                material = new Material(shader);
            }

            _bloomPass.Setup(src);
            renderer.EnqueuePass(_bloomPass);
        }

        internal class LayeredBloomPass : ScriptableRenderPass
        {
            private int[] blurIds = new int[4];
            private LayeredBloomRenderFeature feature;
            RenderTargetHandle[] m_TempH = new RenderTargetHandle[4];
            RenderTargetHandle[] m_TempV = new RenderTargetHandle[4];
            RenderTargetIdentifier source;

            public LayeredBloomPass(LayeredBloomRenderFeature feature)
            {
                this.feature = feature;
                this.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                for (int i = 0; i < 4; i++)
                {
                    m_TempH[i].Init("TEMP_H" + i);
                    m_TempV[i].Init("TEMP_V" + i);
                    blurIds[i] = Shader.PropertyToID("_Blur" + i);
                }
            }

            public void Setup(RenderTargetIdentifier source)
            {
                this.source = source;
            }

            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                var OriginWidth = cameraTextureDescriptor.width;
                var OriginHeight = cameraTextureDescriptor.height;
                for (int i = 0; i < 4; ++i)
                {
                    var width = OriginWidth >> (i * 2 + 1);
                    var height = OriginHeight >> (i * 2 + 1);
                    cmd.GetTemporaryRT(m_TempH[i].id, width, height, 0, FilterMode.Bilinear, cameraTextureDescriptor.graphicsFormat);
                    cmd.GetTemporaryRT(m_TempV[i].id, width, height, 0, FilterMode.Bilinear, cameraTextureDescriptor.graphicsFormat);
                }
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get("LayeredBloom");

                var material = feature.material;
                
                material.SetFloat("_Threshold", feature.Threshold);
                material.SetFloat("_Amplify", feature.Intensity);
                material.SetVector("_LayerIntensity", new Vector4(feature.Layer0, feature.Layer1, feature.Layer2, feature.Layer3));

                Blit(cmd, source, m_TempV[0].Identifier(), material, 0);
                Blit(cmd, m_TempV[0].Identifier(), m_TempH[0].Identifier(), material, 1);
                Blit(cmd, m_TempH[0].Identifier(), m_TempV[0].Identifier(), material, 2);

                for (int i = 1; i < 4; ++i)
                {
                    Blit(cmd, m_TempV[i - 1].Identifier(), m_TempV[i].Identifier(), material, 3);
                    Blit(cmd, m_TempV[i].Identifier(), m_TempH[i].Identifier(), material, 1);
                    Blit(cmd, m_TempH[i].Identifier(), m_TempV[i].Identifier(), material, 2);
                }

                for (int i = 0; i < 4; i++)
                {
                    cmd.SetGlobalTexture(blurIds[i], m_TempV[i].Identifier());
                }
                cmd.Blit(null, source, material, 4);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void FrameCleanup(CommandBuffer cmd)
            {
                for (int i = 0; i < 4; ++i)
                {
                    cmd.ReleaseTemporaryRT(m_TempH[i].id);
                    cmd.ReleaseTemporaryRT(m_TempV[i].id);
                }
            }
        }
    }
}

