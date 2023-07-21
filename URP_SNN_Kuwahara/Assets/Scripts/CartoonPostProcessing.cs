namespace UnityEngine.Rendering.Universal
{
    public enum Mode
    {
        SNN,
        Kuwahara
    }
    public class CartoonPostProcessingRenderFeature : ScriptableRendererFeature
    {
        public Mode mode;

        public Shader shader;

        CartoonPostProcessingPass _pass;
        Material material;

        public override void Create()
        {
            _pass = new CartoonPostProcessingPass(this);
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
            
            renderer.EnqueuePass(_pass);
        }

        internal class CartoonPostProcessingPass : ScriptableRenderPass
        {
            private CartoonPostProcessingRenderFeature feature;
            private RTHandle Temp;

            public CartoonPostProcessingPass(CartoonPostProcessingRenderFeature feature)
            {
                this.feature = feature;
                this.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            }

            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                cameraTextureDescriptor.msaaSamples = 1;
                cameraTextureDescriptor.depthBufferBits = 0;
                RenderingUtils.ReAllocateIfNeeded(ref Temp, cameraTextureDescriptor, name: "CartoonTemp");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get("CartoonPost");

                var material = feature.material;

                var source = renderingData.cameraData.renderer.cameraColorTargetHandle;
                CoreUtils.SetRenderTarget(cmd, Temp);
                MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                mpb.SetTexture(Shader.PropertyToID("_BlitTexture"), source);
                CoreUtils.DrawFullScreen(cmd, material, mpb,  feature.mode == Mode.Kuwahara ? 1 : 0);
                
                Blitter.BlitCameraTexture(cmd, Temp, source);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }
    }
}

