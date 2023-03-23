using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace IrradianceVolume
{
    public class Triangle
    {
        public TriangleMesh Mesh;
        public int Index0, Index1, Index2;

        public Triangle(TriangleMesh mesh, int index0, int index1, int index2)
        {
            this.Mesh = mesh;
            this.Index0 = index0;
            this.Index1 = index1;
            this.Index2 = index2;
        }
        
        public Bounds WorldBounds()
        {
            Vector3 v0 = Mesh.Vertices[Index0];
            Vector3 v1 = Mesh.Vertices[Index1];
            Vector3 v2 = Mesh.Vertices[Index2];
            Bounds b = new Bounds(v0, Vector3.zero);
            b.Encapsulate(v1);
            b.Encapsulate(v2);
            return b;
        }

        private readonly float Epllison = 1e-5f;
        public bool IntersectRay(Ray ray, float tMax, out float t, out bool backface)
        {
            t = float.MaxValue;
            backface = false;
            Vector3 v0 = Mesh.Vertices[Index0];
            Vector3 v1 = Mesh.Vertices[Index1];
            Vector3 v2 = Mesh.Vertices[Index2];
            
            Vector3 e1 = v1 - v0;
            Vector3 e2 = v2 - v0;
            Vector3 q = Vector3.Cross(ray.direction, e2);
            float a = Vector3.Dot(e1, q);
            if (a > -Epllison && a < Epllison) return false;
            float f = 1.0f / a;
            Vector3 s = ray.origin - v0;
            float u = f * Vector3.Dot(s, q);
            if (u < 0.0f) return false;
            Vector3 r = Vector3.Cross(s, e1);
            float v = f * (Vector3.Dot(ray.direction, r));
            if (v < 0.0f || u + v > 1.0f) return false;
            t = f * Vector3.Dot(e2, r);
            if (t > tMax || t < 0.0f) return false;
            backface = a < 0.0f;
            return true;
        }
    }

    public class TriangleMesh
    {
        public Mesh OriginMesh;
        public Transform OriginTransform;
        public Vector3[] Vertices;

        public TriangleMesh(Mesh m, Transform transform)
        {
            this.OriginMesh = m;
            this.OriginTransform = transform;
            Vertices = m.vertices;
            for(int i = 0; i < Vertices.Length; ++i)
            {
                Vertices[i] = transform.TransformPoint(Vertices[i]);
            }
        }
    }

    public enum SplitAxis
    {
        None = -1,
        X = 0,
        Y = 1,
        Z = 2,
    };

    public enum EdgeType
    {
        Start,
        End
    }

    public class BoundsEdge : IComparable
    {
        public float Value;
        public EdgeType EdgeType;
        public int PrimId;

        public BoundsEdge(EdgeType t, int i, float value)
        {
            EdgeType = t;
            PrimId = i;
            Value = value;
        }

        public int CompareTo(object obj)
        {
            BoundsEdge other = obj as BoundsEdge;
            if (other == null) return 1;
            if (Value == other.Value) return (int)EdgeType - (int)other.EdgeType;
            return Value > other.Value ? 1 : -1;
        }
    }
    
    public static class BoundsExtent
    {
        public static SplitAxis MaxAxis(this Bounds bounds)
        {
            return bounds.size.x > bounds.size.y ? bounds.size.x > bounds.size.z ? SplitAxis.X : SplitAxis.Z :
                bounds.size.y > bounds.size.z ? SplitAxis.Y : SplitAxis.Z;
        }

        public static float SurfaceArea(this Bounds bounds)
        {
            return 8 * (bounds.extents.x * bounds.extents.y + bounds.extents.y * bounds.extents.z +
                        bounds.extents.z * bounds.extents.x);
        }
    }

    public class BVHNode
    {
        public bool IsLeaf;
        public float Split;
        public SplitAxis SplitAxis;
        public int[] Primitives;
        public BVHNode Left, Right;
        public Bounds bounds;
        public void InitLeaf(int[] Prims, Bounds b)
        {
            IsLeaf = true;
            Primitives = Prims;
            this.bounds = b;
        }

        public void InitInterior(BVHNode l, BVHNode r)
        {
            IsLeaf = false;
            this.bounds  = l.bounds;
            this.bounds.Encapsulate(r.bounds);
            this.Left = l;
            this.Right = r;
        }
    }
    
    public class BVH
    {
        public List<TriangleMesh> TriangleMeshes;
        public List<Triangle> Triangles;
        public List<Bounds> BoundsList;
        BVHNode Root;
        Bounds AllBounds;
        private static readonly float PlaceOffset = 0.05f;
        
        public BVH(Renderer[] renderers)
        {
            TriangleMeshes = new List<TriangleMesh>();
            Triangles = new List<Triangle>();
            BoundsList = new List<Bounds>();
            Stopwatch sw = new Stopwatch();
            sw.Start();
            foreach (var renderer in renderers)
            {
                if (renderer.gameObject.isStatic)
                {
                    MeshFilter tMeshFilter = renderer.GetComponent<MeshFilter>();
                    if(tMeshFilter != null)
                    {
                        var mesh = tMeshFilter.sharedMesh;
                        if (mesh)
                        {
                            Transform transform = renderer.transform;
                            TriangleMesh m = new TriangleMesh(mesh, transform);
                            {
                                TriangleMeshes.Add(m);
                            }

                            var MeshTriangles = mesh.triangles;
                            for (int i = 0; i < MeshTriangles.Length / 3; ++i)
                            {
                                var t = new Triangle(m, MeshTriangles[i * 3], MeshTriangles[i * 3 + 1],
                                    MeshTriangles[i * 3 + 2]);
                                var bounds = t.WorldBounds();
                                {
                                    Triangles.Add(t);
                                    BoundsList.Add(bounds);
                                }
                            }
                        }
                    }
                }
            }

            sw.Stop();
            Debug.Log("Calculate Using: " + sw.ElapsedMilliseconds + "MS");
            sw.Reset();
            sw.Start();
            if(BoundsList.Count > 0)
            {
                var PrimIds = new int[Triangles.Count];
                for (int i = 0; i < Triangles.Count; ++i)
                {
                    PrimIds[i] = i;
                }

                Root = BuildBVH(PrimIds, 0, PrimIds.Length);
            }
            else
            {
                UnityEngine.Debug.LogError("未发现静态物体，请检查场景是否静态物体是否设置正确");
            }
            sw.Stop();
            Debug.Log("Build BVH Cost: " + sw.ElapsedMilliseconds + "MS");
        }

        public BVHNode BuildBVH(int[] Prims, int begin, int end)
        {
            Bounds b = BoundsList[Prims[begin]];
            BVHNode node = new BVHNode();
            for(int i = begin + 1; i < end; ++i)
            {
                b.Encapsulate(BoundsList[Prims[i]]);
            }

            int Primitives = end - begin;
            if (Primitives == 1)
            {
                int[] P = new int[]
                {
                    Prims[begin]
                };
                node.InitLeaf(P, b);
            }
            else
            {
                Bounds centerBounds = new Bounds(BoundsList[Prims[begin]].center, Vector3.zero);
                for(int i = begin + 1; i < end; ++i)
                {
                    centerBounds.Encapsulate(BoundsList[Prims[i]].center);
                }
                int axis = (int)centerBounds.MaxAxis();
                float mid = centerBounds.center[axis];

                int first_not = begin;
                for (; first_not < end; ++first_not)
                {
                    if (BoundsList[Prims[first_not]].center[axis] >= mid)
                    {
                        break;
                    }
                }

                if (first_not == end)
                {
                    first_not = begin;
                }
                else
                {
                    for (int i = first_not + 1; i < end; ++i)
                    {
                        if (BoundsList[Prims[i]].center[axis] < mid)
                        {
                            int swap = Prims[first_not];
                            Prims[first_not] = Prims[i];
                            Prims[i] = swap;
                            first_not++;
                        }
                    }
                }

                if (first_not == begin || first_not == end)
                {
                    int[] dest = new int[end - begin];
                    Array.Copy(Prims, begin, dest, 0, end - begin);
                    node.InitLeaf(dest, b);
                }
                else
                {
                    node.InitInterior(
                        BuildBVH(Prims, begin, first_not),
                        BuildBVH(Prims, first_not, end)
                        );
                }
                
                // List<int> left = new List<int>();
                // List<int> right = new List<int>();
                // for (int i = 0; i < Prims.Length; ++i)
                // {
                //     if (BoundsList[Prims[i]].center[axis] < mid)
                //     {
                //         left.Add(Prims[i]);
                //     }
                //     else
                //     {
                //         right.Add(Prims[i]);                        
                //     }
                // }
                //
                // if (left.Count == 0)
                // {
                //     node.InitLeaf(right.ToArray(), b);
                // }
                // else if (right.Count == 0)
                // {
                //     node.InitLeaf(left.ToArray(), b);
                // }
                // else
                // {
                //     node.InitInterior(
                //         BuildBVH(left.ToArray()), BuildBVH(right.ToArray())
                //         );
                // }
            }

            return node;
        }

        public bool IntersectBounds(Bounds bounds)
        {
            bool intersect = false;
            Stack<BVHNode> stack = new Stack<BVHNode>();
            stack.Push(Root);
            while (stack.Count > 0)
            {
                BVHNode node = stack.Pop();
                if (node.bounds.Intersects(bounds))
                {
                    if (node.IsLeaf)
                    {
                        if (node.Primitives.Length > 0 && node.bounds.Intersects(bounds))
                        {
                            int[] prims = node.Primitives;
                            foreach (var prime in prims)
                            {
                                if (Triangles[prime].WorldBounds().Intersects(bounds))
                                {
                                    intersect = true;
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        stack.Push(node.Left);
                        stack.Push(node.Right);
                    }
                }
            }
            return intersect;
        }
        public Vector3 BestPlacePosition(Vector3 pos, float tMax)
        {
            Vector3[] array = new Vector3[]
            {
                Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back,
                new Vector3(0.577f, 0.577f, 0.577f), new Vector3(0.577f, 0.577f, -0.577f), new Vector3(0.577f, -0.577f, 0.577f), new Vector3(0.577f, -0.577f, -0.577f), 
                new Vector3(-0.577f, 0.577f, 0.577f), new Vector3(-0.577f, 0.577f, -0.577f), new Vector3(-0.577f, -0.577f, 0.577f), new Vector3(-0.577f, -0.577f, -0.577f), 
            };
            float t = float.MaxValue;
            Vector3 bestPosition = pos;
            foreach (var dir in array)
            {
                if (IntersectRay(new Ray(pos, dir), tMax, out float tt, out bool backface))
                {
                    if (tt < t && backface)
                    {
                        t = tt;
                        bestPosition = pos + dir * (t + PlaceOffset);
                    }
                }
            }
            return bestPosition;
        }
        public bool IntersectRay(Ray ray, float tMax, out float t, out bool backface)
        {
            bool intersect = false;
            backface = false;
            t = float.MaxValue;
            Stack<BVHNode> stack = new Stack<BVHNode>();
            stack.Push(Root);
            while (stack.Count > 0)
            {
                BVHNode node = stack.Pop();
                if (node.bounds.IntersectRay(ray, out float distance))
                {
                    if (distance < tMax)
                    {
                        if (node.IsLeaf)
                        {
                            int[] prims = node.Primitives;
                            foreach (var prime in prims)
                            {
                                if (Triangles[prime].IntersectRay(ray, tMax, out float tt, out bool b))
                                {
                                    if (tt < t)
                                    {
                                        t = tt;
                                        intersect = true;
                                        backface = b;
                                    }
                                }
                            }
                        }
                        else
                        {
                            stack.Push(node.Left);
                            stack.Push(node.Right);
                        }
                    }
                }
            }
            return intersect;
        }
        
        public void DrawGizmos(int debugId)
        {
            Stack<BVHNode> stack = new Stack<BVHNode>();
            stack.Push(Root);
            int count = 0;
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                if (debugId == 0)
                {
                    Gizmos.DrawWireCube(node.bounds.center, node.bounds.extents * 2);
                }
                else
                {
                    count++;
                    if (debugId == count)
                    {
                        if (node.IsLeaf)
                        {
                            Gizmos.DrawWireCube(node.bounds.center, node.bounds.extents * 2);
                            Gizmos.color = Color.red;
                            foreach (var prim in node.Primitives)
                            {
                                var tri = Triangles[prim];
                                Vector3 b0 = tri.Mesh.Vertices[tri.Index0];
                                Vector3 b1 = tri.Mesh.Vertices[tri.Index1];
                                Vector3 b2 = tri.Mesh.Vertices[tri.Index2];
                                Gizmos.DrawLine(b0, b1);
                                Gizmos.DrawLine(b1, b2);
                                Gizmos.DrawLine(b2, b0);
                            }
                            Gizmos.color = Color.white;
                        }
                        else
                        {
                            Gizmos.color = Color.green;
                            Gizmos.DrawWireCube(node.bounds.center, node.bounds.extents * 2);
                            Gizmos.color = Color.white;
                        }
                    }
                }
                if (!node.IsLeaf)
                {
                    stack.Push(node.Left);
                    stack.Push(node.Right);
                }
            }
        }
    }
}