using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Scripting;
using System;
using System.IO;
using GPUBaking;

namespace GPUBaking.Editor
{
	class DawnLightmapPreview : EditorWindow
	{
        [MenuItem("Dawn/Tools/ShowLightmapPreview")]
        public static void OpenLightmapPreview()
        {
            DawnLightmapPreview window = EditorWindow.GetWindow<DawnLightmapPreview>("LightmapPreview");
            window.Show();
        }
        Vector2 scrollPosition;
        void OnGUI()
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Width(position.width), GUILayout.Height(position.height - 50));
            LightmapPreview();
            GUILayout.EndScrollView();
            
        }

        bool[] FoldoutVisible;
        void OnSelectionChange()
        {
            FoldoutVisible = new bool[Selection.gameObjects.Length];
            if (FoldoutVisible.Length > 0)
            {
                FoldoutVisible[0] = true;
            }
        }


        void LightmapPreview()
        {
            var gameObjects = Selection.gameObjects;
            for (int index = 0; index < gameObjects.Length; index++)
            {
                var renderer = gameObjects[index].GetComponent<MeshRenderer>();
                if (renderer != null && renderer.lightmapIndex >= 0 && renderer.lightmapIndex < LightmapSettings.lightmaps.Length)
                {
                    var LightmapData = LightmapSettings.lightmaps[renderer.lightmapIndex];

                    if (LightmapData.lightmapColor == null)
                    {
                        return;
                    }

                    float offsetx = LightmapData.lightmapColor.width * renderer.lightmapScaleOffset.z;
                    float offsety = LightmapData.lightmapColor.height * renderer.lightmapScaleOffset.w;

                    float Width = LightmapData.lightmapColor.width * renderer.lightmapScaleOffset.x;
                    float height = LightmapData.lightmapColor.height * renderer.lightmapScaleOffset.y;

                    Rect OffsetAndSize = new Rect(offsetx, offsety, Width, height);
                    if (FoldoutVisible == null || index >= FoldoutVisible.Length)
                    {
                        return;
                    }
                    FoldoutVisible[index] = EditorGUILayout.Foldout(FoldoutVisible[index], renderer.name);
                    if (FoldoutVisible[index])
                    {
                        EditorGUI.indentLevel = EditorGUI.indentLevel + 1;
                        EditorGUILayout.LabelField("LightmapIndex", renderer.lightmapIndex.ToString());
                        EditorGUILayout.LabelField("lightmapResolution", OffsetAndSize.ToString());
                        EditorGUILayout.LabelField("AtlasResolution", LightmapData.lightmapColor.width + "x" + LightmapData.lightmapColor.height);

                        var DawnMesh = renderer.GetComponent<DawnMeshComponent>();
                        if (DawnMesh != null)
                        {
                            float SurfaceArea = DawnMesh.GetCachedSurfaceArea(DawnMesh.GetComponent<MeshFilter>());
                            var UVBounds = DawnMesh.GetUVBounds(DawnMesh.GetComponent<MeshFilter>());

                            var Setting = Dawn4Unity.GetLightingSetting();
                            int RequestLightmapSize = DawnExporter.GetRequestLightmapSize(Setting, DawnMesh, DawnMesh.GetComponent<MeshRenderer>(), DawnMesh.GetComponent<MeshFilter>(), DawnMesh.GetComponentInParent<DawnLightmapGroupSelector>());

                            EditorGUILayout.LabelField("UVBounds", string.Format("{0:N4},{1:N4},{2:N4},{3:N4}", UVBounds.x, UVBounds.y, UVBounds.z, UVBounds.w));
                            EditorGUILayout.LabelField("SurfaceArea", SurfaceArea.ToString());
                            EditorGUILayout.LabelField("RequestLightmapSize", RequestLightmapSize.ToString());
                        }


                        EditorGUI.indentLevel = EditorGUI.indentLevel - 1;
                        GUILayout.Space(10);
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(15);

                        GUILayout.BeginVertical();
                        Vector2 scaleSize = Resize(LightmapData);

                        if (LightmapData.shadowMask != null)
                        {
                            EditorGUILayout.LabelField("Shadow Mask:");
                            Rect rect = GUILayoutUtility.GetRect(scaleSize.x, scaleSize.y, EditorStyles.objectField);
                            EditorGUI.DrawPreviewTexture(rect, LightmapData.shadowMask);
                        }

                        GUILayout.EndVertical();

                        GUILayout.Space(10);

                        GUILayout.BeginVertical();

                        if (LightmapData.lightmapColor != null)
                        {
                            EditorGUILayout.LabelField("Lightmap Color:");
                            Rect rect = GUILayoutUtility.GetRect(scaleSize.x, scaleSize.y, EditorStyles.objectField);
                            EditorGUI.DrawPreviewTexture(rect, LightmapData.lightmapColor);
                        }
                        GUILayout.EndVertical();

                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                        GUILayout.Space(20);
                    }

                }

            }
        }

        public static Vector2 Resize(LightmapData lightmapData)
        {
            int Width = lightmapData.lightmapColor.width;
            int Height = lightmapData.lightmapColor.height;

            float TargetResolution = 400;
            float Scale = Mathf.Max(TargetResolution / Width, TargetResolution / Height);

            if (Scale < 1.0f)
            {
                Width = (int)(Width * Scale);
                Height = (int)(Height * Scale);

            }
            return new Vector2(Width, Height);
        }


        void OnInspectorUpdate()
        {
            Repaint();
        }
    }
}
