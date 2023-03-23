using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

public class SunShaftRenderFeature : ScriptableRendererFeature
{
    private Material material;
    private LightingRenderPass renderPass;
    private RenderTargetIdentifier target;
    public Shader shader;
    public Color TintColor = Color.white;
    [Range(0, 10)]
    public float BloomThreshold = 0.0f;
    [Range(0, 5)]
    public float BloomScale = 0.2f;
    [Range(0.1f, 100)]
    public float BloomMaxBrightness = 100.0f;
    public float BlurRadius = 1.0f;
    [Range(3, 16)]
    public int BlurSamples = 8;

    public float MaskDepth = 100;
    [Range(0, 1)]
    public float ScreenFade = 0.1f;
    

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (material == null)
        {
            material = new Material(shader);
        }

        if (material == null) return;
        if (BloomScale == 0) return;
        target = renderer.cameraColorTarget;
        renderer.EnqueuePass(renderPass);
    }
    public override void Create()
    {
        renderPass = new LightingRenderPass(this);
    }
    
    public class LightingRenderPass : ScriptableRenderPass
    {
        private SunShaftRenderFeature renderFeature;
        const string m_ProfilerTag = "SunShaft";
        private RenderTargetHandle _temp1, _temp2;
        private Vector4[] Params;
        public LightingRenderPass(SunShaftRenderFeature renderFeature)
        {
            this.renderFeature = renderFeature;
            this.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            _temp1.Init("_SunShaftTemp1");
            _temp2.Init("_SunShaftTemp2");
            Params = new Vector4[3];
        }


        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
            var camera = renderingData.cameraData.camera;
            var sun = RenderSettings.sun;
            var vp = camera.WorldToViewportPoint(camera.transform.position - sun.transform.forward * 10);
            if (Vector3.Dot(sun.transform.forward, camera.transform.forward) > 0) return;
            
            var width = camera.pixelWidth;
            var height = camera.pixelHeight;
            var desc = new RenderTextureDescriptor(width / 2, height / 2, renderingData.cameraData.cameraTargetDescriptor.colorFormat, 0);
            cmd.GetTemporaryRT(_temp1.id, desc);
            cmd.GetTemporaryRT(_temp2.id, desc);
            
            var source = renderFeature.target;
            Params[0] = new Vector4(renderFeature.BloomThreshold, renderFeature.BloomScale,
                renderFeature.BloomMaxBrightness, Mathf.Max (renderFeature.MaskDepth, 0));
            Params[1] = new Vector4(vp.x, vp.y, renderFeature.BlurRadius, renderFeature.BlurSamples);
            Params[2] = renderFeature.TintColor;
            Params[2].w = renderFeature.ScreenFade;
            renderFeature.material.SetVectorArray("Params", Params);
            
            renderFeature.material.SetVector("_BloomParameter", new Vector4(renderFeature.BloomThreshold, renderFeature.BloomScale, renderFeature.BloomMaxBrightness));
            renderFeature.material.SetVector("_BlurParameter", new Vector4(vp.x, vp.y, renderFeature.BlurRadius, renderFeature.BlurSamples));
            renderFeature.material.SetVector("_Color", renderFeature.TintColor);
            renderFeature.material.SetVector("_FadeParameter", new Vector4(Mathf.Max (renderFeature.MaskDepth, 0), renderFeature.ScreenFade));
            cmd.Blit(source, _temp1.Identifier(), renderFeature.material, 0);
            cmd.Blit(_temp1.Identifier(), _temp2.Identifier(), renderFeature.material, 1);
            cmd.Blit(_temp2.Identifier(), _temp1.Identifier(), renderFeature.material, 2);
            cmd.Blit(_temp1.Identifier(), _temp2.Identifier(), renderFeature.material, 3);
            cmd.Blit(_temp2.Identifier(), source, renderFeature.material, 4);
            
            cmd.ReleaseTemporaryRT(_temp1.id);
            cmd.ReleaseTemporaryRT(_temp2.id);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
