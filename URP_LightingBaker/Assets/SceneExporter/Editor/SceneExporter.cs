using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf;
using Google.Protobuf.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Windows;

namespace SceneExport
{
    public class SceneExporter : MonoBehaviour
    {

        public static Vector2 Convert(UnityEngine.Vector2 v)
        {
            Vector2 output = new Vector2
            {
                X = v.x,
                Y = v.y
            };
            return output;
        }
        
        public static Vector3 Convert(UnityEngine.Vector3 v)
        {
            Vector3 output = new Vector3
            {
                X = v.x,
                Y = v.y,
                Z = v.z
            };
            return output;
        }

        public static Mesh Convert(UnityEngine.Mesh m)
        {
            Mesh mesh = new Mesh();
            var vertices = m.vertices;
            var normals = m.normals;
            var tangents = m.tangents;
            var uv = m.uv;
            for(int i = 0; i < vertices.Length; ++i)
            {
                mesh.Vertices.Add(Convert(vertices[i]));
                mesh.Normals.Add(Convert(normals[i]));
                // mesh.Tangents.Add(Convert(tangents[i]));
                mesh.Uv.Add(Convert(uv[i]));
            }

            var triangles = m.triangles;
            for (int i = 0; i < triangles.Length; ++i)
            {
                mesh.Triangles.Add(triangles[i]);
            }
            return mesh;
        }

        public static Renderer Convert(UnityEngine.Renderer r)
        {
            Renderer renderer = new Renderer();
            var t = r.transform.localToWorldMatrix;
            renderer.Transform = new Transform();
            renderer.Transform.Column0 = Convert((UnityEngine.Vector3)t.GetColumn(0));
            renderer.Transform.Column1 = Convert((UnityEngine.Vector3)t.GetColumn(1));
            renderer.Transform.Column2 = Convert((UnityEngine.Vector3)t.GetColumn(2));
            renderer.Transform.Column3 = Convert((UnityEngine.Vector3)t.GetColumn(3));
            return renderer;
        }

        public static Material Convert(UnityEngine.Material m, ref int textureid)
        {
            Material material = new Material();
            material.BaseColor = new Vector3() {X = 1.0f, Y = 1.0f, Z = 1.0f};
            material.BaseColorTexId = -1;
            
            if (m.HasProperty ("_BaseColor")) {
                var color = m.GetColor ("_BaseColor");
                material.BaseColor = new Vector3()
                {
                    X = color.r, Y = color.g, Z = color.b
                };
            }

            if (m.HasProperty("_BaseMap"))
            {
                Texture2D basemap = m.GetTexture("_BaseMap") as Texture2D;
                if (basemap)
                {
                    var dictionary = Application.dataPath + "/../export/textures/";
                    if (!System.IO.Directory.Exists(dictionary))
                    {
                        System.IO.Directory.CreateDirectory(dictionary);
                    }

                    RenderTexture rt = RenderTexture.GetTemporary(basemap.width, basemap.height, 0, RenderTextureFormat.ARGB32);
                    Graphics.Blit(basemap, rt);
                    RenderTexture.active = rt;
                    Texture2D basemapCopy = new Texture2D(basemap.width, basemap.height, TextureFormat.RGB24, false);
                    basemapCopy.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                    basemapCopy.Apply();
                    var data = basemapCopy.EncodeToPNG();
                    FileStream fs = new FileStream(dictionary + textureid + ".png", FileMode.Create, FileAccess.Write);
                    material.BaseColorTexId = textureid;
                    textureid++;
                    fs.Write(data, 0, data.Length);
                    fs.Flush();
                    fs.Close();
                    fs.Dispose();
                }
            }
            return material;
        }
        
        [MenuItem("ExportScene/Export")]
        public static void Export()
        {
            var renderers = FindObjectsOfType<MeshRenderer>();
            if (renderers.Length == 0) return;
            var lodGroups = FindObjectsOfType<LODGroup>();
            HashSet<MeshRenderer> ExclusiveRenderers = new HashSet<MeshRenderer>();
            foreach (var lodGroup in lodGroups)
            {
                var lods = lodGroup.GetLODs();
                for (int i = 1; i < lods.Length; ++i)
                {
                    foreach (var renderer in lods[i].renderers)
                    {
                        if (renderer is MeshRenderer)
                        {
                            ExclusiveRenderers.Add((MeshRenderer)renderer);
                        }
                    }
                }
            }

            var set = new HashSet<MeshRenderer>(renderers);
            foreach (var r in ExclusiveRenderers)
            {
                set.Remove(r);
            }

            renderers = set.ToArray();

            var scene = new Scene();

            Dictionary<UnityEngine.Mesh, int> meshIDs = new Dictionary<UnityEngine.Mesh, int>();
            Dictionary<UnityEngine.Material, int> materialIDs = new Dictionary<UnityEngine.Material, int>();
            
            int meshID = 0;
            int materialID = 0;
            int textureID = 0;
            
            foreach (var renderer in renderers)
            {
                if (GameObjectUtility.GetStaticEditorFlags(renderer.gameObject).HasFlag(StaticEditorFlags.ContributeGI))
                {
                    var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    if (!meshIDs.ContainsKey(mesh))
                    {
                        meshIDs[mesh] = meshID;
                        meshID++;
                        scene.Meshes.Add(Convert(mesh));
                    }

                    var material = renderer.sharedMaterial;
                    if (!materialIDs.ContainsKey(material))
                    {
                        materialIDs[material] = materialID;
                        materialID++;
                        scene.Materials.Add(Convert(material, ref textureID));
                    }

                    var r = Convert(renderer);
                    r.MeshId = meshIDs[mesh];
                    r.MaterialId = materialIDs[material];
                    scene.Renderers.Add(r);
                }
            }
            var dictionary = Application.dataPath + "/../export/";
            if (!System.IO.Directory.Exists(dictionary))
            {
                System.IO.Directory.CreateDirectory(dictionary);
            }
            using (var output = System.IO.File.Create(dictionary + "scene.dat"))
            {
                scene.WriteTo(new CodedOutputStream(output));
            }

            Debug.Log("Process " + renderers.Length + " Renderers:");
        }
    }
}