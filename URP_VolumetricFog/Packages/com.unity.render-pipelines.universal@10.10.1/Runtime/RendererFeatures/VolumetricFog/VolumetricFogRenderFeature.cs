using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SocialPlatforms;

public class VolumetricFogRenderFeature : ScriptableRendererFeature
{
    private Material m_BlitMatrial;
    private LightingRenderPass m_RenderPass;
    [Reload("Shaders/VolumetricFog.compute"), HideInInspector]
    public ComputeShader m_Compute;
    [Reload("Shaders/VolumetricFogApply.shader"), HideInInspector]
    public Shader m_ApplyShader;

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!((VolumeManager.instance.stack.GetComponent<VolumetricFog>().IsActive()) || (LocalVolumetricFog.Instance?.IsActive(renderingData.cameraData.camera) ?? false))) return;
        if (m_Compute == null || m_ApplyShader == null) return;
        if (m_BlitMatrial == null)
        {
            m_BlitMatrial = new Material(m_ApplyShader);
        }
        if (renderingData.cameraData.renderType == CameraRenderType.Overlay) return;
        renderer.EnqueuePass(m_RenderPass);
    }
    public override void Create()
    {
#if UNITY_EDITOR
        ResourceReloader.TryReloadAllNullIn(this, "Packages/com.unity.render-pipelines.universal");
#endif
        m_RenderPass = new LightingRenderPass(this);
    }

    struct FogParams
    {
        public Color ForwardScatteringColor;
        public Color BackwardScatteringColor;
        public Color AmbientLight;
        public float OutsideIntensity;
        public float HeightFallOff;
        public float HorizonHeight;
        public float MaxFogDistance;
        public float InnerIntensity;
        public float Distance;
        public bool IgnoreSkybox;
        public Texture3D Noise;
        public float NoiseTiling;
        public Vector3 NoiseSpeed;
        public Texture3D Detail;
        public float DetailTiling;
        public Vector3 DetailSpeed;
        public float DetailIntensity;
        public bool EnableVolumetricLight;
        public bool EnableLocalVolumetricFog;
        public Vector3 LocalVolumetricFogStartLocation;
        public Vector3 LocalVolumetricFogInvSize;
        public float LocalVolumetricFogInvEdgeFade;
        public int VolumetricFogQuality;

        public static FogParams GetFogParams(VolumetricFog fog)
        {
            FogParams p;
            p.ForwardScatteringColor = fog.ForwardScatteringColor.value;
            p.BackwardScatteringColor = fog.BackwardScatteringColor.value;
            p.AmbientLight = fog.AmbientLight.value;
            p.OutsideIntensity = fog.OutsideIntensity.value;
            p.HeightFallOff = fog.HeightFallOff.value;
            p.HorizonHeight = fog.HorizonHeight.value;
            p.MaxFogDistance = fog.MaxFogDistance.value;
            p.InnerIntensity = fog.InnerIntensity.value;
            p.Distance = fog.Distance.value;
            p.IgnoreSkybox = fog.IgnoreSkybox.value;
            p.Noise = fog.Noise.value;
            p.NoiseTiling = fog.NoiseTiling.value;
            p.NoiseSpeed = fog.NoiseSpeed.value;
            p.Detail = fog.Detail.value;
            p.DetailTiling = fog.DetailTiling.value;
            p.DetailSpeed = fog.DetailSpeed.value;
            p.DetailIntensity = fog.DetailIntensity.value;
            p.EnableVolumetricLight = fog.EnableVolumetricLight.value;
            p.EnableLocalVolumetricFog = false;
            p.LocalVolumetricFogStartLocation = Vector3.zero;
            p.LocalVolumetricFogInvSize = Vector3.one;
            p.LocalVolumetricFogInvEdgeFade = 0;
            p.VolumetricFogQuality = fog.VolumetricFogQuality.value;
            return p;
        }
        
        public static FogParams GetFogParams(LocalVolumetricFog fog)
        {
            FogParams p;
            p.ForwardScatteringColor = fog.ForwardScatteringColor;
            p.BackwardScatteringColor = fog.BackwardScatteringColor;
            p.AmbientLight = fog.AmbientLight;
            p.OutsideIntensity = 0;
            p.HeightFallOff = 0;
            p.HorizonHeight = 0;
            p.MaxFogDistance = 99999;
            p.InnerIntensity = fog.InnerIntensity;
            p.Distance = fog.Distance;
            p.IgnoreSkybox = false;
            p.Noise = fog.Noise;
            p.NoiseTiling = fog.NoiseTiling;
            p.NoiseSpeed = fog.NoiseSpeed;
            p.Detail = fog.Detail;
            p.DetailTiling = fog.DetailTiling;
            p.DetailSpeed = fog.DetailSpeed;
            p.DetailIntensity = fog.DetailIntensity;
            p.EnableVolumetricLight = fog.EnableVolumetricLight;
            p.EnableLocalVolumetricFog = true;
            p.LocalVolumetricFogStartLocation = fog.transform.position - fog.Size / 2;
            p.LocalVolumetricFogInvSize = new Vector3(1.0f / fog.Size.x, 1.0f / fog.Size.y, 1.0f / fog.Size.z);
            p.LocalVolumetricFogInvEdgeFade = Mathf.Min(1.0f / fog.EdgeFade, 2000.0f);
            p.VolumetricFogQuality = fog.Quality;
            return p;
        }
    }

    public class LightingRenderPass : ScriptableRenderPass
    {
        
        const string m_ProfilerTag = "VolumetricLighting";
        VolumetricFogRenderFeature m_RenderFeature;
        private Vector3Int m_VolumeResolution = new Vector3Int(128, 72, 64);

        private RenderTexture[] m_HistoryTextures = new RenderTexture[2];
        private RenderTexture m_ScatteringIntegrated;
        private bool m_ResetHistory = true;
        private int m_FrameCount = 0;
        private Matrix4x4 PrevWorldToClipMatrix;
        private Vector4[] Params = new Vector4[11];
        public LightingRenderPass(VolumetricFogRenderFeature renderFeature)
        {
            this.m_RenderFeature = renderFeature;
            this.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
        }
        private RenderTextureDescriptor CreateRTDescritor(Vector3Int size, bool useDummy)
        {
            RenderTextureDescriptor descriptor = new RenderTextureDescriptor();
            descriptor.width = useDummy ? 1 : size.x;
            descriptor.height = useDummy ? 1 : size.y;
            descriptor.volumeDepth = useDummy ? 1 : size.z;
            descriptor.dimension = TextureDimension.Tex3D;
            descriptor.colorFormat = RenderTextureFormat.ARGBHalf;
            descriptor.enableRandomWrite = true;
            descriptor.msaaSamples = 1;
            return descriptor;
        }

        static float Halton(int Index, int Base)
        {
            float Result = 0.0f;
            float InvBase = 1.0f / Base;
            float Fraction = InvBase;
            while (Index > 0)
            {
                Result += (Index % Base) * Fraction;
                Index /= Base;
                Fraction *= InvBase;
            }
            return Result;
        }

        static Vector3 VolumetricRandom(int Frame)
        {
            int num = Frame & 1023;
            return new Vector3(Halton(num, 2), Halton(num, 3), Halton(num, 5));
        }
        
        Vector4 GetVolumetricFogGridZParams(float near, float far, int GridSize, int DistributionScale)
        {
            float N = near + 0.1f;
            float F = far;
            float S = DistributionScale;
            float O = (F - N * Mathf.Pow(2.0f, GridSize / S)) / (F - N);
            float B = (1 - O) / N;
            return new Vector4(B, O, S / GridSize, far);
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
        static readonly int DistributionScale = 32;

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {

            //Debug.Log("LightingRenderPass");
            CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
            m_FrameCount++;
            var camera = renderingData.cameraData.camera;
            var cs = m_RenderFeature.m_Compute;

            FogParams p;
            {
                var globalFog = VolumeManager.instance.stack.GetComponent<VolumetricFog>();
                var isGlobalFog = globalFog.IsActive();
                if (isGlobalFog)
                {
                    p = FogParams.GetFogParams(globalFog);
                }
                else
                {
                    p = FogParams.GetFogParams(LocalVolumetricFog.Instance);
                }
            }

            if (p.VolumetricFogQuality == 2)
            {
                m_VolumeResolution = new Vector3Int(128, 72, 128);
            }
            else if (p.VolumetricFogQuality == 3)
            {
                m_VolumeResolution = new Vector3Int(256, 144, 128);
            }
            else
            {
                m_VolumeResolution = new Vector3Int(128, 72, 64);
            }
            
            var useDummyTexture = p.InnerIntensity <= 0;

            var descriptor = CreateRTDescritor(m_VolumeResolution, useDummyTexture);
            var ThisWorldToClipMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false) * camera.worldToCameraMatrix;
            cmd.SetComputeMatrixParam(cs, "_PrevWorldToClipMatrix", PrevWorldToClipMatrix);

            PrevWorldToClipMatrix = ThisWorldToClipMatrix;
            // Scattering
            var historyRead = m_HistoryTextures[m_FrameCount % 2];
            if (FetchOrCreate(ref historyRead, descriptor))
            {
                m_HistoryTextures[m_FrameCount % 2] = historyRead;
                m_ResetHistory = true;
            }

            var historyWrite = m_HistoryTextures[(m_FrameCount + 1) % 2];
            if (FetchOrCreate(ref historyWrite, descriptor))
            {
                m_HistoryTextures[m_FrameCount % 2] = historyWrite;
            }

            FetchOrCreate(ref m_ScatteringIntegrated, descriptor);
            var GridZParmas = GetVolumetricFogGridZParams(camera.nearClipPlane, p.Distance,
                descriptor.volumeDepth, DistributionScale);
            Params[0] = new Vector4(1.0f / m_VolumeResolution.x, 1.0f / m_VolumeResolution.y,
                1.0f / m_VolumeResolution.z);
            Params[0].w = m_ResetHistory ? 1 : 0;
            Params[1] = GridZParmas;
            Params[2] = VolumetricRandom(m_FrameCount);
            
            Params[2].w = p.InnerIntensity;
            Params[3] = p.ForwardScatteringColor;
            Params[3].w = p.OutsideIntensity;
            Params[4] = p.BackwardScatteringColor;
            Params[4].w = p.HeightFallOff;
            Params[5] = p.AmbientLight;
            int ScatteringDispathIndex = 0;
            //TODO:URP
            ForwardRenderer renderer = renderingData.cameraData.renderer as ForwardRenderer;
            if (p.EnableVolumetricLight && renderer != null)
                ScatteringDispathIndex = 3;
            if (p.Noise != null)
            {
                ScatteringDispathIndex++;
                if (p.Detail != null)
                {
                    ScatteringDispathIndex++;
                    cs.SetTexture(ScatteringDispathIndex, "_DetailTexture", p.Detail);
                    Params[5].w = p.DetailIntensity;
                    Params[6] = p.DetailSpeed;
                    Params[6].w = p.DetailTiling;
                }

                cs.SetTexture(ScatteringDispathIndex, "_NoiseTexture", p.Noise);
                Params[7] = p.NoiseSpeed;
                Params[7].w = p.NoiseTiling;
            }

            Params[8].x = p.MaxFogDistance;
            Params[8].y = p.HorizonHeight;
            bool fastAttenuation =
                GraphicsSettings.HasShaderDefine(Graphics.activeTier, BuiltinShaderDefine.SHADER_API_MOBILE) ||
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.Switch;
            Params[8].z = fastAttenuation ? -0.36f : 1.0f;
            Params[9] = p.LocalVolumetricFogStartLocation;
            Params[9].w = p.EnableLocalVolumetricFog ? 1.0f : 0.0f;
            Params[10] = p.LocalVolumetricFogInvSize;
            Params[10].w = p.LocalVolumetricFogInvEdgeFade;

            cmd.SetGlobalVectorArray("_VolumetricFogParams", Params);

            if (!useDummyTexture)
            {
                cs.SetVectorArray("_VolumetricFogParams", Params);
                cs.SetTexture(ScatteringDispathIndex, "_ScatteringLight", historyWrite);
                cs.SetTexture(ScatteringDispathIndex, "_ScatteringLightHistory", historyRead);
                cmd.DispatchCompute(cs, ScatteringDispathIndex, m_VolumeResolution.x / 8, m_VolumeResolution.y / 8, m_VolumeResolution.z);

                // Integration
                cs.SetTexture(6, "_ScatteringLightHistory", historyWrite);
                cs.SetTexture(6, "_ScatteringLightIntegrated", m_ScatteringIntegrated);
                cmd.DispatchCompute(cs, 6, m_VolumeResolution.x / 8, m_VolumeResolution.y / 8, 1);
                m_ResetHistory = false;
            }

            // Apply
            cmd.SetGlobalTexture("_ScatteringLightResult", m_ScatteringIntegrated);
            

            if (p.IgnoreSkybox)
            {
                m_RenderFeature.m_BlitMatrial.EnableKeyword("_IgnoreSkybox");
            }
            else
            {
                m_RenderFeature.m_BlitMatrial.DisableKeyword("_IgnoreSkybox");
            }
            
            cmd.SetRenderTarget(renderingData.cameraData.renderer.cameraColorTarget);
            cmd.DrawProcedural(Matrix4x4.identity, m_RenderFeature.m_BlitMatrial, 0, MeshTopology.Triangles, 3);
            CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.VolumetricFog, true);
           
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            
            CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.VolumetricFog, false);
        }
    }

}
