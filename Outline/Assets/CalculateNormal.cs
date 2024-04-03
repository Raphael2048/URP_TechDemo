using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

public class CalculateNormal : ScriptableWizard
{
    public GameObject obj;
    public Mesh mesh1;
    // public string path;
    private List<Mesh> mesh = new List<Mesh>();
    void OnWizardUpdate()
    {
        bool isValid = (obj != null);
    }

    void CreateMesh()
    {
        string path = AssetDatabase.GetAssetPath(obj);
        if(obj!=null)
        {
            if(path != null && path != "")
            {
                UnityEngine.Object[] allChild = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
                for(int i = 0;i < allChild.Length;i++)
                {

                    if(allChild[i].GetType()==typeof(UnityEngine.Mesh))
                    {
                        mesh.Add((Mesh)allChild[i]);
                    }
                }
            }
            else
            {
                Transform[] allChild;
                allChild = obj.transform.GetComponentsInChildren<Transform>();
                for(int i = 0;i<allChild.Length;i++)
                {
                    if(allChild[i].GetComponent<SkinnedMeshRenderer>()!=null)
                    {
                        mesh.Add(allChild[i].GetComponent<SkinnedMeshRenderer>().sharedMesh);
                    }
                    else if(allChild[i].GetComponent<MeshFilter>() != null)
                    {
                        mesh.Add(allChild[i].GetComponent<MeshFilter>().sharedMesh);
                    }
                }
            }
        }
        else if(mesh1!=null)
        {
            mesh.Add(mesh1);
        }
    }
    void OnWizardCreate()
    {       
        if(obj != null || mesh1 != null)
        {
            CreateMesh();
            if(mesh.Count == 0)
            {
                Debug.Log("Don't have mesh");
                return;
            }
        
            for(int i = 0;i<mesh.Count;i++)
            {
                AngleNormal(mesh[i]);
            }
        }
    }
    

    void AngleNormal(Mesh mesh)
    {
        int triangleCount = mesh.triangles.Length;
        int vertexCount = mesh.vertices.Length;
        int[] triangles = mesh.triangles;
        Vector3[] vertices = mesh.vertices;
        Vector4[] tangents = mesh.tangents;
        Vector3[] normals = mesh.normals;

        var angleNormalHash = new Dictionary<Vector3, Vector3>();
        // var angleNormalHash = new Dictionary<long,Vector3>();
        Vector2[] uv3 = new Vector2[vertexCount];
        Vector2[] uv4 = new Vector2[vertexCount];

        for(int i = 0;i < triangleCount; i+=3)
        {
            long i0 = triangles[i];
            long i1 = triangles[i+1];
            long i2 = triangles[i+2];
            Vector3 v0 = vertices[i0];
            Vector3 v1 = vertices[i1];
            Vector3 v2 = vertices[i2];

            Vector3 edge2 = (v2-v1).normalized;
            Vector3 edge1 = (v1-v0).normalized;
            Vector3 fragNormal = Vector3.Cross(edge1,edge2).normalized;
        
            for(int j = 0;j<3;j++)
            {
                Vector3 pos = vertices[triangles[i+j]];
                
                Vector3 lastp = vertices[triangles[i + (j + 2) % 3]];
                Vector3 nextp = vertices[triangles[i + (j + 1) % 3]];

                //计算角度
                Vector3 prev_to = (lastp - pos).normalized;
                Vector3 next_to = (nextp - pos).normalized;
                Vector3 angleNormal = fragNormal * (float)Math.Acos(Vector3.Dot(prev_to,next_to));

                if(!angleNormalHash.ContainsKey(pos))
                {
                    angleNormalHash.Add(pos, angleNormal);
                }
                else
                {
                    angleNormalHash[pos] += angleNormal;
                }
            }
        }

        for(int i = 0; i<vertexCount; i++)
        {
            Vector3 pos = vertices[i];
            Vector3 normal = angleNormalHash[pos].normalized;
            
            Vector3 tangentV3 = new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
            Vector3 bitangentV3 = Vector3.Cross(normals[i],tangentV3) * tangents[i].w;
            bitangentV3 = bitangentV3.normalized;
            var TBN = new Matrix4x4(tangentV3,bitangentV3,normals[i],Vector4.zero);
            TBN = TBN.transpose;
            normal = TBN.MultiplyVector(normal).normalized;

            uv3[i] = new Vector2(normal.x, normal.y);
            uv4[i] = new Vector2(normal.z,0);
            
        }
        mesh.uv3 = uv3;
        mesh.uv4 = uv4;
        Debug.Log("CalculateAngleNormal:"+mesh.name);
    }



    [MenuItem("Tools/Calculate Normal")]
    static void Calculate_Normal()
    {
        ScriptableWizard.DisplayWizard<CalculateNormal>("模型平均法线写入UV3&UV4","Average Normal");
    }
}