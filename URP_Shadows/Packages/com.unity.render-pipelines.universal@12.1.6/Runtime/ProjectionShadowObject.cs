using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Rendering;
using System;
using UnityEngine.Rendering.Universal;

namespace UnityEngine.Rendering.Universal
{
    [ExecuteInEditMode, DisallowMultipleComponent]
    public class ProjectionShadowObject : MonoBehaviour
    {
        private Bounds m_Bounds;
        private List<Renderer> renders = new List<Renderer>();

        private Matrix4x4 m_MainLightViewMatrix;
        private Matrix4x4 m_MainLightProjectMatrix;
        
        private Matrix4x4 m_VolumeDrawMatrix;
        private Matrix4x4 m_VolumeRenderLTWMatrix;
        private Vector4 m_ShadowmapST;
        
        public Matrix4x4 m_MainLightShadowTransformMatrix
        {
            get;
            private set;
        }

        public float m_MainLightShadowmapTexelSize
        {
            get;
            private set;
        }
        
        private Bounds m_VolumeRenderLocalBounds;

        public static ProjectionShadowObject Instance;
        
        static Mesh m_CubeMesh;

        public static Mesh cubeMesh
        {
            get
            {
                if (m_CubeMesh) return m_CubeMesh;
                Vector3[] vertices = {
                    new Vector3 (-0.5f, -0.5f, -0.5f),
                    new Vector3 (0.5f, -0.5f, -0.5f),
                    new Vector3 (0.5f, 0.5f, -0.5f),
                    new Vector3 (-0.5f, 0.5f, -0.5f),
                    new Vector3 (-0.5f, 0.5f, 0.5f),
                    new Vector3 (0.5f, 0.5f, 0.5f),
                    new Vector3 (0.5f, -0.5f, 0.5f),
                    new Vector3 (-0.5f, -0.5f, 0.5f),
                };
                int[] triangles = {
                    0, 2, 1, //face front
                    0, 3, 2,
                    2, 3, 4, //face top
                    2, 4, 5,
                    1, 2, 5, //face right
                    1, 5, 6,
                    0, 7, 4, //face left
                    0, 4, 3,
                    5, 4, 7, //face back
                    5, 7, 6,
                    0, 6, 7, //face bottom
                    0, 1, 6
                };

                m_CubeMesh = new Mesh();
                m_CubeMesh.vertices = vertices;
                m_CubeMesh.triangles = triangles;
                m_CubeMesh.Optimize();
                m_CubeMesh.RecalculateNormals();
                return m_CubeMesh;
            }
        }

        private void OnEnable()
        {
            UpdateRenders();
            RenderPipelineManager.beginFrameRendering += SetRenderState;
        }

        private void OnValidate()
        {
            UpdateRenders();
        }

        private void OnDisable()
        {
            ClearRenderState();
            RenderPipelineManager.beginFrameRendering -= SetRenderState;
        }

        
        private void Update()
        {
            Instance = this;
            UpdateRenders();
            RefreshVolume();
        }

        private bool CheckRenderer(Renderer renderer)
        {
            if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
            {
                return true;
            }
            return false;
        }

        private void RefreshVolume()
        {
            int startRenderIndex = -1;
            for (int i = 0; i < renders.Count; i++)
            {
                if (CheckRenderer(renders[i]))
                {
                    startRenderIndex = i;
                    m_Bounds = renders[i].bounds;
                    break;
                }
            }
            if (gameObject.activeInHierarchy == false || startRenderIndex < 0)
            {
                return;
            }
            for (int i = startRenderIndex + 1; i < renders.Count; i++)
            {
                if (CheckRenderer(renders[i]))
                {
                    m_Bounds.Encapsulate(renders[i].bounds);
                }
            }
        }

        private Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
        {
            var newOrigin = matrix.MultiplyPoint(bounds.center);
            Vector3 newExtents = Vector3.zero;
            Vector3 p = Vector3.zero;
            
            p = matrix.MultiplyVector(new Vector3(bounds.extents.x, 0, 0));
            newExtents += new Vector3(Mathf.Abs(p.x), Mathf.Abs(p.y), Mathf.Abs(p.z));
            
            p = matrix.MultiplyVector(new Vector3(0,bounds.extents.y,  0));
            newExtents += new Vector3(Mathf.Abs(p.x), Mathf.Abs(p.y), Mathf.Abs(p.z));
            
            p = matrix.MultiplyVector(new Vector3(0, 0, bounds.extents.z));
            newExtents += new Vector3(Mathf.Abs(p.x), Mathf.Abs(p.y), Mathf.Abs(p.z));
            
            return new Bounds(newOrigin, newExtents * 2);
        }

        // 主光的相关矩阵信息会被保存
        public void UpdateMainLightVolume(Vector3 lightDirection, float shadowmapSize, bool enableModulatedShadow)
        {
            Vector3 lightDir = lightDirection;
            Vector3 rightDir = Vector3.Cross(Vector3.up, lightDir).normalized;
            Vector3 upDir = Vector3.Cross(lightDir, rightDir).normalized;
            var wToLMatrix = new Matrix4x4(
                new Vector4(rightDir.x, upDir.x, lightDir.x, 0),
                new Vector4(rightDir.y, upDir.y, lightDir.y, 0),
                new Vector4(rightDir.z, upDir.z, lightDir.z, 0),
                new Vector4(0, 0, 0, 1)
            );
            var ligthSpaceBounds = TransformBounds(m_Bounds, wToLMatrix);
            Vector3 m_VolumeSize = ligthSpaceBounds.extents * 2;
            Vector3 m_VolumeCenter = ligthSpaceBounds.center;
            m_VolumeSize.x = m_VolumeSize.y = Mathf.Max(m_VolumeSize.x, m_VolumeSize.y);
            var xsize = m_VolumeSize.x * 0.5f;
            var ysize = m_VolumeSize.y * 0.5f;
            m_MainLightViewMatrix = new Matrix4x4(
                new Vector4(wToLMatrix[0, 0], wToLMatrix[1, 0], -wToLMatrix[2, 0], 0),
                new Vector4(wToLMatrix[0, 1], wToLMatrix[1, 1], -wToLMatrix[2, 1], 0),
                new Vector4(wToLMatrix[0, 2], wToLMatrix[1, 2], -wToLMatrix[2, 2], 0),
                new Vector4(-m_VolumeCenter.x, -m_VolumeCenter.y, m_VolumeCenter.z - m_VolumeSize.z * 0.5f, 1));
            m_MainLightProjectMatrix = Matrix4x4.Ortho(-xsize, xsize, -ysize, ysize, 0, m_VolumeSize.z);
            
            m_MainLightShadowTransformMatrix = ShadowUtils.GetShadowTransform(m_MainLightProjectMatrix, m_MainLightViewMatrix);
            
            float frustumSize = 2.0f / m_MainLightProjectMatrix.m00;
            m_MainLightShadowmapTexelSize = frustumSize / shadowmapSize;
            
            if (enableModulatedShadow)
            {
                float length = m_Bounds.size.y / Mathf.Max(Vector3.Dot(lightDir, Vector3.down), 0.025f) * 2.0f + m_VolumeSize.x;
                length = Mathf.Max(length, m_VolumeSize.z);
                Matrix4x4 m_LTWMatrix = wToLMatrix.inverse;
                m_VolumeRenderLTWMatrix = Matrix4x4.TRS(m_LTWMatrix.MultiplyPoint(m_VolumeCenter + Vector3.forward * (length - m_VolumeSize.z) * 0.5f), m_LTWMatrix.rotation, Vector3.one);
                m_VolumeDrawMatrix = Matrix4x4.TRS(m_VolumeRenderLTWMatrix.GetColumn(3), m_VolumeRenderLTWMatrix.rotation,
                    new Vector3(m_VolumeSize.x, m_VolumeSize.y, length));
                m_VolumeRenderLocalBounds = new Bounds(Vector3.zero, m_VolumeDrawMatrix.lossyScale);
            }
        }

        // 辅助光的相关矩阵信息不会保存
        public void ExtractAdditionalLightTransform(Vector3 lightDirection, float shadowmapSize, out Matrix4x4 view,
            out Matrix4x4 projection, out Matrix4x4 shadowTransform, out float shadowmapBiasBase)
        {
            Vector3 lightDir = lightDirection;
            Vector3 rightDir = Vector3.Cross(Vector3.up, lightDir).normalized;
            Vector3 upDir = Vector3.Cross(lightDir, rightDir).normalized;
            var wToLMatrix = new Matrix4x4(
                new Vector4(rightDir.x, upDir.x, lightDir.x, 0),
                new Vector4(rightDir.y, upDir.y, lightDir.y, 0),
                new Vector4(rightDir.z, upDir.z, lightDir.z, 0),
                new Vector4(0, 0, 0, 1)
            );

            var ligthSpaceBounds = TransformBounds(m_Bounds, wToLMatrix);
            Vector3 m_VolumeCenter = ligthSpaceBounds.center;
            Vector3 m_VolumeSize = ligthSpaceBounds.extents * 2;
            m_VolumeSize.x = m_VolumeSize.y = Mathf.Max(m_VolumeSize.x, m_VolumeSize.y);
            
            var xsize = m_VolumeSize.x * 0.5f;
            var ysize = m_VolumeSize.y * 0.5f;
            view = new Matrix4x4(
                new Vector4(wToLMatrix[0, 0], wToLMatrix[1, 0], -wToLMatrix[2, 0], 0),
                new Vector4(wToLMatrix[0, 1], wToLMatrix[1, 1], -wToLMatrix[2, 1], 0),
                new Vector4(wToLMatrix[0, 2], wToLMatrix[1, 2], -wToLMatrix[2, 2], 0),
                new Vector4(-m_VolumeCenter.x, -m_VolumeCenter.y, m_VolumeCenter.z - m_VolumeSize.z * 0.5f, 1));
            projection = Matrix4x4.Ortho(-xsize, xsize, -ysize, ysize, 0, m_VolumeSize.z );
            
            shadowTransform = ShadowUtils.GetShadowTransform(projection, view);
            float frustumSize = 2.0f / projection.m00;
            shadowmapBiasBase = frustumSize / shadowmapSize;
        }

        private void UpdateRenders()
        {
            var allRenders = GetComponentsInChildren<Renderer>();
            renders.Clear();
            foreach (var item in allRenders)
            {
                if (item == null || item is ParticleSystemRenderer)
                {
                    continue;
                }
                renders.Add(item);
            }
        }

        public void DrawMainLightShadowmap(CommandBuffer cmd, Material shadowMaterial)
        {
            cmd.SetViewProjectionMatrices(m_MainLightViewMatrix, m_MainLightProjectMatrix);
            DrawShadowMap(cmd, shadowMaterial);
        }
        public void DrawShadowMap(CommandBuffer cmd, Material shadowMaterial)
        {
            foreach (var render in renders)
            {
                if (CheckRenderer(render))
                {
                    for (int i = 0; i < render.sharedMaterials.Length; ++i)
                    {
                        if (render.sharedMaterials[i] != null)
                        {
                            // Full Opaque
                            if (render.sharedMaterials[i].renderQueue <= 2000)
                            {
                                cmd.DrawRenderer(render, shadowMaterial, i, 0);
                            }
                            // AlphaTest
                            else if (render.sharedMaterials[i].renderQueue <= 2500)
                            {
                                var id = render.sharedMaterials[i].FindPass("ShadowCaster");
                                if (id > -1)
                                {
                                    cmd.DrawRenderer(render, render.sharedMaterials[i], i, id);
                                }
                            }
                        }
                    }
                }
            }
        }

        public void DrawTransparentShadowMap(CommandBuffer cmd)
        {
            cmd.SetViewProjectionMatrices(m_MainLightViewMatrix, m_MainLightProjectMatrix);
            foreach (var render in renders)
            {
                if (CheckRenderer(render))
                {
                    for (int i = 0; i < render.sharedMaterials.Length; ++i)
                    {
                        if (render.sharedMaterials[i] != null)
                        {
                            if (render.sharedMaterials[i].renderQueue > 2500)
                            {
                                var id = render.sharedMaterials[i].FindPass("TransparentShadow");
                                if (id > -1)
                                {
                                    cmd.DrawRenderer(render, render.sharedMaterials[i], i, id);
                                }
                            }
                        }
                    }
                }
            }
        }

        public void SetRenderState(ScriptableRenderContext context, Camera[] camera)
        {
            if (Instance == this)
            {
                foreach (var render in renders)
                {
                    if (CheckRenderer(render))
                    {
                        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                        mpb.SetVector("_H3D_ProjectionShadowParams", new Vector4(1, 0, 0, 0));
                        render.SetPropertyBlock(mpb);
                    }
                }
            }
        }

        public void ClearRenderState()
        {
            foreach (var render in renders)
            {
                if (CheckRenderer(render))
                {
                    MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                    mpb.SetVector("_H3D_ProjectionShadowParams", Vector4.zero);
                    render.SetPropertyBlock(mpb);
                }
            }
        }
        
        public void DrawModulatedShadow(CommandBuffer cmd, ref RenderingData renderingData, Material shadowMaterial, bool modulatedSelfShadow)
        {
            Vector3 camPos = renderingData.cameraData.camera.transform.position;
            Vector4 point = renderingData.cameraData.GetProjectionMatrix().inverse.MultiplyPoint(new Vector4(-1, -1, -1, 1));
            float sqrNearPlaneDistance = point.x * point.x + point.y * point.y + point.z * point.z;
            camPos = m_VolumeRenderLTWMatrix.inverse.MultiplyPoint(camPos);
            bool isInside = m_VolumeRenderLocalBounds.SqrDistance(camPos) < sqrNearPlaneDistance;
            if (!modulatedSelfShadow)
            {
                //目前使用的是stencil标记不需要阴影区域，暂时用不到
            }
            cmd.DrawMesh(cubeMesh, m_VolumeDrawMatrix, shadowMaterial, 0, isInside ? 4 : 3);
        }
        
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireCube(m_Bounds.center, m_Bounds.size);

            Gizmos.matrix = m_MainLightViewMatrix.inverse;
            Gizmos.color = new Color(1f, 1f, 1f, 0.5f);
            Gizmos.DrawSphere(Vector3.zero, 0.05f);
            Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
            Gizmos.DrawCube(new Vector3(0, 0, 1.0f / m_MainLightProjectMatrix.m22), new Vector3(2.0f / m_MainLightProjectMatrix.m00, 2.0f / m_MainLightProjectMatrix.m11, -2.0f / m_MainLightProjectMatrix.m22));

            Gizmos.matrix = m_VolumeDrawMatrix;
            Gizmos.color = Color.black;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }
#endif
    }
}