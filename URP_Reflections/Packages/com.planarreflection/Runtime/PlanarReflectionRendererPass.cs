using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;

namespace H3D.URP
{
    internal class PlanarReflectionRenderPass : ScriptableRenderPass
    {
        private ProfilingSampler sampler;
        private PlanarReflectionRendererFeature feature;
        internal RenderTargetHandle m_ReflectionRT, m_ReflectionDepth, m_BlurTexture, m_BlurIntensity, m_PPRIntermediate;
        Plane[] planes = new Plane[6];
        List<ShaderTagId> m_ShaderTagIdList = new List<ShaderTagId>();
        static Material usualIntenityMaterial, filterMaterial, pprIntensityMaterial;
        internal Plane plane;
        Vector4 textureSize;

        public PlanarReflectionRenderPass(PlanarReflectionRendererFeature feature)
        {
            this.feature = feature;
            sampler = new ProfilingSampler("PlanarReflection");
            m_ShaderTagIdList.Add(new ShaderTagId("BackLighting"));
            m_ShaderTagIdList.Add(new ShaderTagId("UniversalForward"));
            m_ShaderTagIdList.Add(new ShaderTagId("SRPDefaultUnlit"));

            m_ReflectionRT.Init("_PlanarReflectionTexture");
            m_BlurIntensity.Init("_PlanarReflectionIntensity");
            m_ReflectionDepth.Init("_ReflectionDepth");
            m_BlurTexture.Init("_ReflectionTemp");
            m_PPRIntermediate.Init("_IntermediateTexture");
        }
        
        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            if (usualIntenityMaterial == null)
            {
                usualIntenityMaterial = CoreUtils.CreateEngineMaterial(feature.usualReflectionIntensityShader);
            }
            if (filterMaterial == null)
            {
                filterMaterial = CoreUtils.CreateEngineMaterial(feature.PlanarReflectionFilterShader);
            }
            if (pprIntensityMaterial == null)
            {
                pprIntensityMaterial = CoreUtils.CreateEngineMaterial(feature.pprReflectionIntensityShader);
            }
            var reflectionPlane = ReflectionPlane.Instance;
            cameraTextureDescriptor.depthBufferBits = 0;
            if (reflectionPlane.m_TextureSize == ReflectionPlane.ReflectTexSize._Half)
            {
                cameraTextureDescriptor.height >>= 1;
                cameraTextureDescriptor.width >>= 1;
            }
            else if (reflectionPlane.m_TextureSize == ReflectionPlane.ReflectTexSize._Quarter)
            {
                cameraTextureDescriptor.height >>= 2;
                cameraTextureDescriptor.width >>= 2;
            }

            textureSize = new Vector4(cameraTextureDescriptor.width, cameraTextureDescriptor.height,
                1.0f / cameraTextureDescriptor.width, 1.0f / cameraTextureDescriptor.height);

            cmd.GetTemporaryRT(m_ReflectionRT.id, cameraTextureDescriptor);
            if (reflectionPlane.m_Technique == ReflectionPlane.PlanarReflectionTechnique.Usual)
            {
                cmd.GetTemporaryRT(m_ReflectionDepth.id, cameraTextureDescriptor.width, cameraTextureDescriptor.height, 16, FilterMode.Bilinear, RenderTextureFormat.Depth);
            }
            else
            {
                cameraTextureDescriptor.enableRandomWrite = true;
                var lastFormat = cameraTextureDescriptor.graphicsFormat;
                cameraTextureDescriptor.graphicsFormat = GraphicsFormat.R32_UInt;
                cmd.GetTemporaryRT(m_PPRIntermediate.id, cameraTextureDescriptor);
                cameraTextureDescriptor.graphicsFormat = lastFormat;
                cameraTextureDescriptor.enableRandomWrite = false;
            }

            if (reflectionPlane.m_BlurPower > 0)
            {
                cmd.GetTemporaryRT(m_BlurTexture.id, cameraTextureDescriptor);
                if (reflectionPlane.m_AdaptiveBlur)
                {
                    reflectionPlane.m_GradientIntensity = true;
                }
            }
            if (reflectionPlane.m_GradientIntensity)
            {
                cameraTextureDescriptor.graphicsFormat = GraphicsFormat.R8_UNorm;
                cmd.GetTemporaryRT(m_BlurIntensity.id, cameraTextureDescriptor);
            }

            if (reflectionPlane.m_Technique == ReflectionPlane.PlanarReflectionTechnique.Usual)
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
                ConfigureTarget(m_ReflectionRT.Identifier(), m_ReflectionDepth.Identifier());
            }
            else
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
                ConfigureTarget(m_ReflectionRT.Identifier());
            }
            UpdatePlane();
        }

        void MappingNewIndexMap(CullingResults newCullResults, CullingResults oldCullResults)
        {
            var oldIndexMap = oldCullResults.GetLightIndexMap(Allocator.Temp);
            var newIndexMap = newCullResults.GetLightIndexMap(Allocator.Temp);

            for (int i = 0; i < newCullResults.visibleLights.Length; ++i)
            {
                newIndexMap[i] = -1;
                for (int j = 0; j < oldCullResults.visibleLights.Length; ++j)
                {
                    if (newCullResults.visibleLights[i] == oldCullResults.visibleLights[j])
                    {
                        newIndexMap[i] = oldIndexMap[j];
                        break;
                    }
                }
            }
            newCullResults.SetLightIndexMap(newIndexMap);
            newIndexMap.Dispose();
            oldIndexMap.Dispose();
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.camera.cameraType == CameraType.Reflection)
                return;
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, sampler))
            {
                CameraData cameraData = renderingData.cameraData;
                var reflectionPlane = ReflectionPlane.Instance;
                Matrix4x4 reflection = CalculateReflectionMatrix(plane);
                Matrix4x4 mirrorWorldToCamera = cameraData.GetViewMatrix() * reflection;
                Matrix4x4 projection = cameraData.GetProjectionMatrix();
                cmd.SetGlobalVector("_ReflectionPlane", new Vector4(plane.normal.x, plane.normal.y, plane.normal.z, plane.distance));
                cmd.SetGlobalVector("_ReflectionPlaneParams", new Vector4(reflectionPlane.m_Technique == ReflectionPlane.PlanarReflectionTechnique.PixelProjected ? 1 : 0, 0, 0));

                if (reflectionPlane.m_Technique == ReflectionPlane.PlanarReflectionTechnique.Usual)
                {
                    var np = mirrorWorldToCamera.TransformPlane(plane);
                    projection = CalculateObliqueMatrix(projection, np);
                    cmd.SetViewProjectionMatrices(mirrorWorldToCamera, projection);
                    cmd.SetInvertCulling(true);
                    cmd.SetRenderTarget(m_ReflectionRT.Identifier(), m_ReflectionDepth.Identifier());
                    cmd.ClearRenderTarget(true, true, Color.black);
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();
                    CullingResults cullingResults = new CullingResults();
                    if (cameraData.camera.TryGetCullingParameters(out ScriptableCullingParameters p))
                    {
                        GeometryUtility.CalculateFrustumPlanes(projection * mirrorWorldToCamera, planes);
                        for(int i = 0; i < 6; i++)
                        {
                            p.SetCullingPlane(i, planes[i]);
                        }
                        cullingResults = context.Cull(ref p);
                        // 反射物体的光照，必须同时是被主相机能看到的
                        MappingNewIndexMap(cullingResults, renderingData.cullResults);
                    }

                    // Opaque
                    SortingCriteria opaqueSortingSettings = cameraData.defaultOpaqueSortFlags;
                    DrawingSettings opaqueSettings = CreateDrawingSettings(m_ShaderTagIdList, ref renderingData, opaqueSortingSettings);
                    FilteringSettings opaqueFilteringSettings = new FilteringSettings(RenderQueueRange.opaque, renderingLayerMask: reflectionPlane.m_RenderLayerMask);
                    context.DrawRenderers(cullingResults, ref opaqueSettings, ref opaqueFilteringSettings);

                    if (reflectionPlane.m_ReflectSky)
                    {
                        context.DrawSkybox(cameraData.camera);
                    }

                    // Transparent
                    SortingCriteria transparentSortingSettings = SortingCriteria.CommonTransparent;
                    DrawingSettings transparentSettings = CreateDrawingSettings(m_ShaderTagIdList, ref renderingData, transparentSortingSettings);
                    FilteringSettings transparentFilteringSettings = new FilteringSettings(RenderQueueRange.transparent, renderingLayerMask: reflectionPlane.m_RenderLayerMask);
                    context.DrawRenderers(cullingResults, ref transparentSettings, ref transparentFilteringSettings);

                    cmd.SetInvertCulling(false);
                    
                    // 根据到平面的距离，计算出一个强度值
                    if (reflectionPlane.m_GradientIntensity)
                    {
                        Vector4 mirrorParams = Vector4.zero;
                        var worldToClipMatrix = GL.GetGPUProjectionMatrix(projection, false) * mirrorWorldToCamera;
                        mirrorParams.x = reflectionPlane.m_GradientDistance;
                        usualIntenityMaterial.SetMatrix("_InvVPMatrix", worldToClipMatrix.inverse);
                        usualIntenityMaterial.SetVector("_BlurParams", mirrorParams);
                        cmd.Blit(m_ReflectionRT.Identifier(), m_BlurIntensity.Identifier(), usualIntenityMaterial, 0);
                    }
                }
                // FOR PPR
                else
                {
                    var PPRProjection = feature.pprProjectionComputeShader;
                    Camera camera = renderingData.cameraData.camera;
                    projection = GL.GetGPUProjectionMatrix(projection, false);
                    var WorldToClip = projection * camera.worldToCameraMatrix;
                    cmd.SetComputeMatrixParam(PPRProjection, "_WorldToClipMatrix", WorldToClip);
                    cmd.SetComputeMatrixParam(PPRProjection, "_ClipToWorldMatrix", WorldToClip.inverse);
                    cmd.SetComputeVectorParam(PPRProjection, "_TextureSize", textureSize);
                    cmd.SetComputeVectorParam(PPRProjection, "_WSCameraPos", camera.transform.position);
                    cmd.SetComputeFloatParam(PPRProjection, "_BlurMaxDistance", 1.0f / reflectionPlane.m_GradientDistance);
                    cmd.SetComputeTextureParam(PPRProjection, 0, "_IntermediateTexture", m_PPRIntermediate.Identifier());
                    cmd.DispatchCompute(PPRProjection, 0, ((int)textureSize.x + 7) / 8, ((int)textureSize.y + 7) / 8, 1);

                    cmd.SetComputeTextureParam(PPRProjection, 1, "_IntermediateTexture", m_PPRIntermediate.Identifier());
                    cmd.DispatchCompute(PPRProjection, 1, ((int)textureSize.x + 7) / 8, ((int)textureSize.y + 7) / 8, 1);

                    var PPRReflection = pprIntensityMaterial;
                    PPRReflection.SetVector("_TextureSize", textureSize);
                    
                    var worldToClipMatrix = projection * mirrorWorldToCamera;
                    PPRReflection.SetMatrix("_ClipToWorldMatrix", worldToClipMatrix.inverse);

                    if (reflectionPlane.m_GradientIntensity)
                    {
                        var rts = new RenderTargetIdentifier[] {m_ReflectionRT.Identifier(), m_BlurIntensity.Identifier()};
                        cmd.SetRenderTarget(rts, m_ReflectionRT.Identifier());
                        cmd.DrawProcedural(Matrix4x4.identity, PPRReflection, 1, MeshTopology.Triangles,3);
                    }
                    else
                    {
                        cmd.SetRenderTarget(m_ReflectionRT.Identifier());
                        cmd.DrawProcedural(Matrix4x4.identity, PPRReflection, 0, MeshTopology.Triangles,3);
                    }
                }
                
                // COMMON
                if (reflectionPlane.m_BlurPower > 0)
                {
                    if (reflectionPlane.m_AdaptiveBlur)
                    {
                        for (int i = 0; i < reflectionPlane.m_BlurPower; ++i)
                        {
                            cmd.Blit(m_ReflectionRT.Identifier(), m_BlurTexture.Identifier(), filterMaterial, 2);
                            cmd.Blit(m_BlurTexture.Identifier(), m_ReflectionRT.Identifier(), filterMaterial, 3);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < reflectionPlane.m_BlurPower; ++i)
                        {
                            cmd.Blit(m_ReflectionRT.Identifier(), m_BlurTexture.Identifier(), filterMaterial, 0);
                            cmd.Blit(m_BlurTexture.Identifier(), m_ReflectionRT.Identifier(), filterMaterial, 1);
                        }
                    }
                }
                cmd.SetViewProjectionMatrices(cameraData.GetViewMatrix(), cameraData.GetProjectionMatrix());
            }
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        /// Cleanup any allocated resources that were created during the execution of this render pass.
        public override void FrameCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(m_ReflectionRT.id);
            cmd.ReleaseTemporaryRT(m_ReflectionDepth.id);
            cmd.ReleaseTemporaryRT(m_BlurTexture.id);
            cmd.ReleaseTemporaryRT(m_PPRIntermediate.id);
        }

        private void UpdatePlane()
        {
            var mirrorReflection = ReflectionPlane.Instance;
            // find out the reflection plane: position and normal in world space
            Vector3 pos = mirrorReflection.GetPlanePosition();
            Vector3 normal = mirrorReflection.GetPlaneNormal();
            float m_ClipPlaneOffset = mirrorReflection.m_ClipPlaneOffset;

            // Render reflection
            // Reflect camera around reflection plane
            float d = -Vector3.Dot(normal, pos) - m_ClipPlaneOffset;
            plane = new Plane(normal, d);
        }

        // Extended sign: returns -1, 0 or 1 based on sign of a
        private static float sgn(float a)
        {
            if (a > 0.0f) return 1.0f;
            if (a < 0.0f) return -1.0f;
            return 0.0f;
        }

        // Adjusts the given projection matrix so that near plane is the given clipPlane
        // clipPlane is given in camera space. See article in Game Programming Gems 5 and
        // http://aras-p.info/texts/obliqueortho.html

        private static Matrix4x4 CalculateObliqueMatrix(Matrix4x4 projection, Plane plane)
        {
            Vector4 clipPlane = new Vector4(plane.normal.x, plane.normal.y, plane.normal.z, plane.distance);
            Vector4 q = projection.inverse * new Vector4(
                sgn(clipPlane.x),
                sgn(clipPlane.y),
                1.0f,
                1.0f
            );
            Vector4 c = clipPlane * (2.0F / (Vector4.Dot(clipPlane, q)));
            // third row = clip plane - fourth row
            projection[2] = c.x - projection[3];
            projection[6] = c.y - projection[7];
            projection[10] = c.z - projection[11];
            projection[14] = c.w - projection[15];
            return projection;
        }

        private static Matrix4x4 CalculateReflectionMatrix(Plane plane)
        {
            Matrix4x4 reflectionMat = Matrix4x4.identity;
            reflectionMat.m00 = (1F - 2F * plane.normal[0] * plane.normal[0]);
            reflectionMat.m01 = (-2F * plane.normal[0] * plane.normal[1]);
            reflectionMat.m02 = (-2F * plane.normal[0] * plane.normal[2]);
            reflectionMat.m03 = (-2F * plane.distance * plane.normal[0]);

            reflectionMat.m10 = (-2F * plane.normal[1] * plane.normal[0]);
            reflectionMat.m11 = (1F - 2F * plane.normal[1] * plane.normal[1]);
            reflectionMat.m12 = (-2F * plane.normal[1] * plane.normal[2]);
            reflectionMat.m13 = (-2F * plane.distance * plane.normal[1]);

            reflectionMat.m20 = (-2F * plane.normal[2] * plane.normal[0]);
            reflectionMat.m21 = (-2F * plane.normal[2] * plane.normal[1]);
            reflectionMat.m22 = (1F - 2F * plane.normal[2] * plane.normal[2]);
            reflectionMat.m23 = (-2F * plane.distance * plane.normal[2]);

            reflectionMat.m30 = 0F;
            reflectionMat.m31 = 0F;
            reflectionMat.m32 = 0F;
            reflectionMat.m33 = 1F;

            return reflectionMat;
        }
    }
}
